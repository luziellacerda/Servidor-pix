import ipaddress
import tempfile
import pathlib
import unittest
from unittest.mock import patch

import policy
from priority import Registry, peer, tcp_peers


class PolicySafetyTests(unittest.TestCase):
    def test_real_htb_text_and_statistics_formats(self):
        raw = ("class htb 53:101 parent 53:10 leaf 101: prio 0 rate 6600Kbit ceil 33Mbit burst 16Kb cburst 16Kb \n"
               " Sent 2300 bytes 12 pkt (dropped 1, overlimits 8 requeues 0) \n"
               " lended: 6 borrowed: 5 giants: 0\n"
               "class htb 53:1 root rate 95Mbit ceil 95Mbit burst 16Kb cburst 16Kb \n"
               " Sent 0 bytes 0 pkt (dropped 0, overlimits 0 requeues 0) \n")
        rows = policy.parse_class_status(raw)
        self.assertEqual(len(rows), 2)
        self.assertEqual(rows[0]["rateMbps"], 6.6)
        self.assertEqual(rows[0]["ceilMbps"], 33)
        self.assertEqual(rows[0]["borrowed"], 5)
        self.assertEqual(rows[0]["drops"], 1)
        self.assertEqual(rows[1]["rateMbps"], 95)

    def test_reject_foreign_queue_before_mutation(self):
        for existing in [{"kind": "cake", "handle": "9:"}, {"kind": "htb", "handle": "7:"}]:
            with patch("policy.roots", return_value=[existing]), patch("policy.run") as command:
                with self.assertRaises(RuntimeError):
                    policy.require_safe_root("eth0")
                command.assert_not_called()

    def test_peer_validation_cannot_inject_nft_commands(self):
        for address, port in [("127.0.0.1; delete table inet filter", 1), ("127.0.0.1", 0),
                              ("::1", 65536), ("::1", "1;flush ruleset")]:
            with self.assertRaises(ValueError): peer(address, port)

    def test_mark_requires_an_existing_nginx_connection(self):
        registry = Registry(33)
        with patch("priority.tcp_peers", return_value={}), patch("priority.run") as command:
            self.assertFalse(registry.add(peer("192.0.2.1", 50000)))
            command.assert_not_called()

    def test_client_tuple_reuse_is_removed_by_inode(self):
        registry = Registry(33)
        value = peer("192.0.2.1", 50000)
        registry.entries[value] = "1"
        with patch("priority.tcp_peers", return_value={value: "2"}), patch("priority.run") as command:
            registry.refresh()
            self.assertFalse(registry.entries)
            text = command.call_args.kwargs["data"]
            self.assertIn("flush set inet station_qos games4", text)
            self.assertNotIn("add element", text)

    def test_mark_survives_daemon_restart_with_same_socket(self):
        with tempfile.TemporaryDirectory() as tmp:
            state = pathlib.Path(tmp) / "active.json"
            value = peer("192.0.2.1", 50000)
            registry = Registry(33, state=state)
            with patch("priority.tcp_peers", return_value={value: "101"}), patch("priority.run"):
                self.assertTrue(registry.add(value))
            restored = Registry(33, state=state)
            self.assertEqual(restored.entries, {value: "101"})
            with patch("priority.tcp_peers", return_value={value: "101"}), patch("priority.run"):
                restored.refresh()
            self.assertEqual(restored.entries, {value: "101"})

    def test_invalid_link_or_wan_rate_refused(self):
        for link, wan in [(90, 100), (100, -1), (10001, 100), (100, float('nan')), (float('inf'), 100)]:
            with self.assertRaises(ValueError):
                policy.validate({"interface": "eth0", "linkMbps": link, "wanMbps": wan})

    def test_class_budget_fits_link_including_equal_ceiling(self):
        for link, wan in [(95, 33), (40, 10), (950, 90), (100, 100), (100, 95), (1, 1)]:
            rows = policy.classes({"interface": "eth0", "linkMbps": link, "wanMbps": wan})
            root = next(x for x in rows if x[0] == "1")
            self.assertAlmostEqual(sum(x[2] for x in rows if x[1] == "1"), root[2])
            for parent in ["10", "20"]:
                children = [x for x in rows if x[1] == parent]
                rate = next(x[2] for x in rows if x[0] == parent)
                self.assertAlmostEqual(sum(x[2] for x in children), rate)
                self.assertTrue(all(x[3] <= root[3] for x in children))
                self.assertTrue(all(0 < x[2] <= x[3] for x in children))

    def test_full_upload_ceiling_keeps_lan_and_idle_borrowing(self):
        rows = {x[0]: x for x in policy.classes({"interface": "eth0", "linkMbps": 100, "wanMbps": 100})}
        self.assertEqual(rows["10"][2:4], (90, 100))
        self.assertEqual(rows["20"][2:4], (10, 100))
        self.assertEqual(rows["101"][2:4], (18, 100))
        self.assertEqual(rows["103"][2:4], (54, 100))


if __name__ == "__main__":
    unittest.main()
