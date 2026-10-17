#!/usr/bin/env python3
"""Outbound bandwidth sharing. This owns one qdisc and one nftables table only."""
import argparse
import ipaddress
import json
import pathlib
import re
import subprocess
import tempfile

MAJOR = "53"
TABLE = "station_qos"
DEFAULT_CONFIG = "/etc/station-network-qos/config.json"


def run(args, *, data=None):
    result = subprocess.run(args, input=data, text=True, capture_output=True, timeout=15)
    if result.returncode:
        # Never include request headers, addresses of clients or unrelated rulesets.
        raise RuntimeError("command_failed:" + pathlib.Path(args[0]).name + ":" + result.stderr[:300])
    return result.stdout


def validate(config):
    if not re.fullmatch(r"[A-Za-z0-9_-]{1,15}", config["interface"]):
        raise ValueError("invalid_interface")
    link, wan = float(config["linkMbps"]), float(config["wanMbps"])
    if not (1 <= wan <= link <= 10000):
        raise ValueError("invalid_rates")
    for key, family in [("lan4", 4), ("lan6", 6)]:
        for value in config.get(key, []):
            if ipaddress.ip_network(value).version != family:
                raise ValueError("invalid_network_family")
    if len(config.get("lan4", [])) > 8 or len(config.get("lan6", [])) > 8:
        raise ValueError("too_many_networks")
    if not 0 <= int(config.get("nginxUid", 33)) <= 65535:
        raise ValueError("invalid_nginx_uid")
    return config


def classes(config):
    """Share the link with a positive LAN guarantee; WAN can borrow up to its ceiling."""
    config = validate(config)
    link, wan = float(config["linkMbps"]), float(config["wanMbps"])
    lan = max(link - wan, link * .1)
    wan_rate = link - lan
    rows = [("1", "root", link, link, 0), ("10", "1", wan_rate, wan, 0),
            ("20", "1", lan, link, 1)]
    for prefix, rate, ceil in [("10", wan_rate, wan), ("20", lan, link)]:
        for suffix, share, priority in [("1", .2, 0), ("2", .2, 1), ("3", .6, 2)]:
            rows.append((prefix + suffix, prefix, rate * share, ceil, priority))
    return rows


def tc_batch(config, *, create=True):
    dev = config["interface"]
    lines = []
    if create:
        lines.append(f"qdisc replace dev {dev} root handle {MAJOR}: htb default 102 r2q 100")
    for minor, parent, rate, ceil, priority in classes(config):
        parent_id = f"{MAJOR}:" if parent == "root" else f"{MAJOR}:{parent}"
        # Explicit quantum avoids long bursts for sparse game traffic.
        lines.append(f"class {'add' if create else 'change'} dev {dev} parent {parent_id} "
                     f"classid {MAJOR}:{minor} htb rate {rate:g}mbit ceil {ceil:g}mbit "
                     f"prio {priority} burst 16k cburst 16k quantum 1514")
        if create and len(minor) == 3:
            lines.append(f"qdisc add dev {dev} parent {MAJOR}:{minor} handle {minor}: "
                         "fq_codel limit 4096 flows 1024 quantum 1514 target 5ms "
                         "interval 100ms memory_limit 8Mb ecn")
    return "\n".join(lines) + "\n"


def nft_text(config):
    validate(config)
    lines = [f"table inet {TABLE} {{",
             " set games4 { type ipv4_addr . inet_service; flags timeout; timeout 60s; size 4096; }",
             " set games6 { type ipv6_addr . inet_service; flags timeout; timeout 60s; size 4096; }"]
    for lane, prefix in [("wan", "10"), ("lan", "20")]:
        game, other, bulk = [f"0x0053{int(prefix + n, 16):04x}" for n in ["1", "2", "3"]]
        lines += [f" chain {lane} {{",
                  f"  tcp sport 443 ip daddr . tcp dport @games4 counter meta priority set {game} return",
                  f"  tcp sport 443 ip6 daddr . tcp dport @games6 counter meta priority set {game} return",
                  # Existing tunnels multiplex more than games; label them honestly.
                  f"  meta l4proto {{ tcp, udp }} th dport 7844 counter meta priority set {game} return",
                  f"  meta length <= 96 tcp flags & (syn | fin | rst | psh) == 0 tcp flags & ack != 0 "
                  f"counter meta priority set {game} return",
                  f"  tcp sport 443 counter meta priority set {bulk} return",
                  f"  counter meta priority set {other}", " }"]
    lines += [" chain classify { type filter hook postrouting priority 300; policy accept;",
              f'  oifname != "{config["interface"]}" return']
    for net in config.get("lan4", []):
        lines.append(f"  ip daddr {net} goto lan")
    for net in config.get("lan6", []):
        lines.append(f"  ip6 daddr {net} goto lan")
    lines += ["  goto wan", " }", "}"]
    return "\n".join(lines) + "\n"


def roots(dev):
    return [x for x in json.loads(run(["tc", "-j", "qdisc", "show", "dev", dev])) if x.get("root")]


def owned(root):
    return root.get("handle") == MAJOR + ":" and root.get("kind") == "htb"


def require_safe_root(dev, baseline_handles=("0:",)):
    current = roots(dev)
    if len(current) != 1:
        raise RuntimeError("unexpected_qdisc_root")
    root = current[0]
    if not owned(root) and not (root.get("kind") == "fq_codel" and root.get("handle") in baseline_handles):
        raise RuntimeError("refusing_to_replace_foreign_qdisc")
    return root


def apply(config):
    root = require_safe_root(config["interface"], config.get("baselineHandles", ["0:"]))
    if not owned(root):
        # nft checks run before touching the host queue.
        text = nft_text(config)
        run(["nft", "--check", "-f", "-"], data=text)
        with tempfile.NamedTemporaryFile(mode="w", suffix=".tc") as batch:
            batch.write(tc_batch(config)); batch.flush()
            run(["tc", "-batch", batch.name])
        run(["nft", "-f", "-"], data=text)
    else:
        # Changing rates preserves connections, game sets and queued packets.
        with tempfile.NamedTemporaryFile(mode="w", suffix=".tc") as batch:
            batch.write(tc_batch(config, create=False)); batch.flush()
            run(["tc", "-batch", batch.name])
    return status(config)


def parse_class_status(text):
    """This iproute2 prints HTB classes as text even when -j is requested."""
    actual = []
    for match in re.finditer(r"^class htb ([0-9a-f:]+) ([^\n]+)\n(.*?)(?=^class |\Z)", text, re.M | re.S):
        handle, header, statistics = match.groups()
        row = {"handle": handle}
        for name in ["rate", "ceil"]:
            value = re.search(r"\b" + name + r" ([0-9.]+)([KMGT]?bit)\b", header)
            if not value: raise RuntimeError("unrecognized_htb_rate")
            factor = {"bit": 1, "Kbit": 1000, "Mbit": 1000000, "Gbit": 1000000000, "Tbit": 1000000000000}[value[2]]
            row[name + "Mbps"] = float(value[1]) * factor / 1000000
        sent = re.search(r"Sent (\d+) bytes (\d+) pkt \(dropped (\d+), overlimits (\d+)", statistics)
        if sent:
            row.update(dict(zip(["bytes", "packets", "drops", "overlimits"], map(int, sent.groups()))))
        for name in ["lended", "borrowed"]:
            value = re.search(name + r": (\d+)", statistics)
            if value: row[name] = int(value[1])
        actual.append(row)
    return actual


def status(config):
    dev = config["interface"]
    current = roots(dev)
    if len(current) != 1 or not owned(current[0]):
        raise RuntimeError("qos_not_active")
    actual = parse_class_status(run(["tc", "-s", "class", "show", "dev", dev]))
    expected = {f"{MAJOR}:{x[0]}" for x in classes(config)}
    if {x.get("handle") for x in actual} != expected:
        raise RuntimeError("qos_classes_incomplete")
    for minor, parent, rate, ceil, priority in classes(config):
        row = next(x for x in actual if x["handle"] == f"{MAJOR}:{minor}")
        if abs(row["rateMbps"] - rate) > max(.005 * rate, .01) or abs(row["ceilMbps"] - ceil) > max(.005 * ceil, .01):
            raise RuntimeError("qos_rate_mismatch")
    # Do not return dynamic client tuples.
    run(["nft", "list", "chain", "inet", TABLE, "classify"])
    budgets = {minor: rate for minor, _, rate, _, _ in classes(config)}
    return {"active": True, "interface": dev, "linkMbps": config["linkMbps"],
            "wanMbps": config["wanMbps"], "gameReservedPercent": 20,
            "otherReservedPercent": 20, "downloadReservedPercent": 60,
            "wanGuaranteedMbps": budgets["10"], "lanGuaranteedMbps": budgets["20"],
            "configuredPlanDownloadMbps": config.get("operatorPlanDownloadMbps"),
            "configuredPlanUploadMbps": config.get("operatorPlanUploadMbps", config.get("operatorPlanUploadMbpsApproximate")),
            "direction": "outbound", "ingressLimitAppliedByThisPolicy": False,
            "idleCapacityBorrowing": True,
            "classes": actual}


def stop(config):
    current = roots(config["interface"])
    if len(current) == 1 and owned(current[0]):
        # Removing our root reattaches the kernel default fq_codel, handle 0:.
        run(["tc", "qdisc", "del", "dev", config["interface"], "root"])
    tables = json.loads(run(["nft", "-j", "list", "tables"]))
    if any(x.get("table", {}).get("name") == TABLE and x.get("table", {}).get("family") == "inet"
           for x in tables.get("nftables", [])):
        run(["nft", "delete", "table", "inet", TABLE])


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("command", choices=["apply", "status", "stop", "check"])
    parser.add_argument("--config", default=DEFAULT_CONFIG)
    args = parser.parse_args()
    config = validate(json.loads(pathlib.Path(args.config).read_text()))
    if args.command == "stop":
        stop(config); result = {"stopped": True}
    elif args.command == "apply":
        result = apply(config)
    elif args.command == "check":
        try: result = status(config)
        except RuntimeError:
            # Restore only a missing owned policy with the known default queue.
            require_safe_root(config["interface"], config.get("baselineHandles", ["0:"]))
            stop(config); result = apply(config)
    else: result = status(config)
    print(json.dumps(result, sort_keys=True))


if __name__ == "__main__":
    main()
