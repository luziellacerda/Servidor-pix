#!/usr/bin/env python3
"""Reproduce the pinned Sega multitap port aliasing, then test an isolated patch.

No ROM/BIOS/Android or live services are used. Requires cc and the pinned core
source with libraries/clowncommon. The original source directory is read-only.
The patch covers the duplicate port indices, not mixed physical port layouts,
J-Cart, three-button selection or a complete Android/core implementation.
"""
import argparse
import difflib
import hashlib
import json
import shutil
import subprocess
import tempfile
from pathlib import Path

EXPECTED = {
    "source/controller-manager.c": "6a4470172ba544ae03576c6de14b56bc606ad0eec9b057b488602091aa335fe1",
    "source/controller-multitap-sega.c": "d409cfbd456d2a1b4856d6de3db5da87ffcf42df79ba6d69de675b5b6f7902eb",
    "source/controller-multitap-ea.c": "1b139e1351129302de0b5e6ff388df68e9c74a2b477ceec07820ae03dbebce36",
    "source/controller.c": "fb6a1cfabf94d7c6779b8a96c55ee4ec9570844e397e09732dc4c9aad5e62422",
    "libraries/clowncommon/clowncommon.h": "cfc50a2bc54f74b0d43c00329131569b5c788914071c008f9039732224dfc869",
}

HELPER = '''typedef struct StationSegaPortCallback
{
    Controller_Callback callback;
    const void *user_data;
    cc_u8f base;
} StationSegaPortCallback;

static cc_bool StationSegaPortInput(void *user_data, cc_u8f index, Controller_Button button)
{
    const StationSegaPortCallback *bound = (const StationSegaPortCallback*)user_data;
    return bound->callback((void*)bound->user_data, bound->base + index, button);
}

'''

HARNESS = r'''
#include "source/controller-manager.h"
#include <stdio.h>
#include <string.h>
static unsigned seen, desired_pad, desired_button;
static cc_bool buttons(void *u, cc_u8f pad, Controller_Button button)
{
    (void)u;
    seen |= 1U << pad;
    return pad == desired_pad && button == desired_button;
}
int main(void)
{
    static const Controller_Button order[3][4] = {
        {CONTROLLER_BUTTON_RIGHT, CONTROLLER_BUTTON_LEFT, CONTROLLER_BUTTON_DOWN, CONTROLLER_BUTTON_UP},
        {CONTROLLER_BUTTON_START, CONTROLLER_BUTTON_A, CONTROLLER_BUTTON_C, CONTROLLER_BUTTON_B},
        {CONTROLLER_BUTTON_MODE, CONTROLLER_BUTTON_X, CONTROLLER_BUTTON_Y, CONTROLLER_BUTTON_Z},
    };
    unsigned port, pad, group, button, checks=0, failures=0, standard=0, ea=0;
    ControllerManager manager;
    memset(&manager,0,sizeof(manager));
    ControllerManager_Initialise(&manager);
    manager.configuration.protocol=CONTROLLER_MANAGER_PROTOCOL_SEGA_TAP;
    for(port=0;port<2;port++) for(pad=0;pad<4;pad++)
        for(group=0;group<3;group++) for(button=0;button<4;button++)
    {
        unsigned read;
        desired_pad=port*4+pad; desired_button=order[group][button]; seen=0;
        manager.state.sega_multitaps[port].th_bit=0;
        manager.state.sega_multitaps[port].tl_bit=0;
        manager.state.sega_multitaps[port].pulses=7+pad*3+group;
        read=ControllerManager_Read(&manager,port,0,buttons,NULL)&15;
        checks++;
        if(read!=(15U^(1U<<(3-button))) || seen!=(1U<<desired_pad)) failures++;
    }
    /* Actual manager dispatch of standard pads and EA selection must retain ownership. */
    for(port=0;port<2;port++)
    {
        unsigned read;
        ControllerManager_Initialise(&manager);
        manager.configuration.protocol=CONTROLLER_MANAGER_PROTOCOL_STANDARD;
        desired_pad=port;desired_button=CONTROLLER_BUTTON_A;seen=0;
        read=ControllerManager_Read(&manager,port,0,buttons,NULL);
        if((read&16)!=0 || seen!=(1U<<port)) return 2;
        standard++;
    }
    for(pad=0;pad<4;pad++)
    {
        unsigned read;
        ControllerManager_Initialise(&manager);
        manager.configuration.protocol=CONTROLLER_MANAGER_PROTOCOL_EA_4_WAY_PLAY;
        manager.state.ea_multitap.selected_controller=pad;
        desired_pad=pad;desired_button=CONTROLLER_BUTTON_A;seen=0;
        read=ControllerManager_Read(&manager,0,0,buttons,NULL);
        if((read&16)!=0 || seen!=(1U<<pad)) return 3;
        ea++;
    }
    printf("{\"segaButtonCases\":%u,\"misroutedSegaCases\":%u,\"standardOwnershipCases\":%u,\"eaOwnershipCases\":%u}\n", checks, failures, standard, ea);
    return 0;
}
'''


def main():
    args = argparse.ArgumentParser(description=__doc__)
    args.add_argument("--core-source", required=True, type=Path)
    args.add_argument("--output", required=True, type=Path)
    ns = args.parse_args()
    for path, expected in EXPECTED.items():
        actual = hashlib.sha256((ns.core_source / path).read_bytes()).hexdigest()
        if actual != expected:
            raise SystemExit("Pinned source differs: " + path)
    with tempfile.TemporaryDirectory(prefix="station-mega-mapping-") as tmp:
        stage = Path(tmp)
        (stage / "source").mkdir()
        for path in ns.core_source.glob("source/controller*"):
            shutil.copyfile(path, stage / "source" / path.name)
        (stage / "libraries/clowncommon").mkdir(parents=True)
        shutil.copyfile(ns.core_source / "libraries/clowncommon/clowncommon.h", stage / "libraries/clowncommon/clowncommon.h")
        (stage / "harness.c").write_text(HARNESS)
        manager_path = stage / "source/controller-manager.c"
        original = manager_path.read_text()
        command = ["cc", "-std=c99", "-DCC_USE_C99_INTEGERS", "-Wall", "-Wextra", "-Werror", "-I.", "harness.c"]
        command += ["source/" + name for name in ["controller-manager.c", "controller-multitap-sega.c", "controller-multitap-ea.c", "controller.c"]]
        results = {}
        for state in ["baseline", "candidate"]:
            if state == "candidate":
                target = "\t\t\treturn ControllerMultitapSega_Read(&manager->state.sega_multitaps[port_index], callback, user_data);"
                if original.count(target) != 1:
                    raise SystemExit("Unexpected upstream dispatch")
                replacement = "\t\t{\n\t\t\tconst StationSegaPortCallback bound = {callback, user_data, port_index * 4};\n\t\t\treturn ControllerMultitapSega_Read(&manager->state.sega_multitaps[port_index], StationSegaPortInput, &bound);\n\t\t}"
                patched = original.replace("cc_u8f ControllerManager_Read(", HELPER + "cc_u8f ControllerManager_Read(", 1).replace(target, replacement)
                manager_path.write_text(patched)
            subprocess.run(command + ["-o", "probe"], cwd=stage, check=True, capture_output=True)
            results[state] = json.loads(subprocess.check_output([str(stage / "probe")], cwd=stage, text=True))
        assert results["baseline"]["misroutedSegaCases"] == 48
        assert results["candidate"]["misroutedSegaCases"] == 0
        assert all(x["standardOwnershipCases"] == 2 and x["eaOwnershipCases"] == 4 for x in results.values())
        ns.output.mkdir(parents=True, exist_ok=True)
        patch = "".join(difflib.unified_diff(original.splitlines(True), patched.splitlines(True), fromfile="a/source/controller-manager.c", tofile="b/source/controller-manager.c"))
        (ns.output / "clown-sega-port-offset.patch").write_text(patch)
        report = {"schemaVersion": 1, "sourceCommit": "88ef45a6585556e2247dd26b6c937d6b9fb4a12d", "sourceSha256": EXPECTED,
                  "results": results, "productionModified": False, "androidBinaryBuilt": False,
                  "scope": "Independent physical port callback indices only; other layouts and input remapping remain required.",
                  "limits": ["No ROM/BIOS/gameplay/Android/TCP or WAN was tested.", "This defect does not explain the SNES Battletoads latency.", "Integrate complete layout handling before creating a new engine/core hash; never replace the active binary with this partial patch."]}
        (ns.output / "mega-port-mapping-proof.json").write_text(json.dumps(report, ensure_ascii=False, indent=2) + "\n")
        print(json.dumps(results))


if __name__ == "__main__":
    main()
