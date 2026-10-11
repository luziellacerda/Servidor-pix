"""Read pinned historical code and the caller's final receipt for isolated tests.

No redundant .base source or old APK manifest is distributed. Git reads the
existing repository's object database; this module never fetches or mutates it.
"""
import json
import os
from pathlib import Path
import subprocess
from types import ModuleType

ROOT=Path(__file__).resolve().parent
SERVER_COMMIT="141b488d6ad7fd3d2c8e74e76c1a004f1cb257fc"
SERVER_PROFILE_PATH="docs/station-android/scripts/station_online_profiles.py"

PATCHED_PROFILE_PATH=Path(os.environ.get("STATION_PROFILES_MODULE",str(ROOT/"station_online_profiles.py"))).resolve()
PATCHED_OPERATOR_PATH=Path(os.environ.get("STATION_OPERATOR_SCRIPT",str(ROOT/"cadastrar-motor-online-station.py"))).resolve()


def baseline_source():
    supplied=os.environ.get("STATION_SERVER_REPO")
    repo=Path(supplied).resolve() if supplied else Path(subprocess.check_output(
        ["git","-C",str(ROOT),"rev-parse","--show-toplevel"],text=True).strip())
    return subprocess.check_output(["git","-c","safe.directory="+repo.as_posix(),
        "-C",str(repo),"show",SERVER_COMMIT+":"+SERVER_PROFILE_PATH])


def baseline_module():
    source=baseline_source();module=ModuleType("pinned_historical_station_profiles")
    exec(compile(source,SERVER_COMMIT+":"+SERVER_PROFILE_PATH,"exec"),module.__dict__)
    return module,source


def final_runtime():
    supplied=os.environ.get("STATION_FINAL_RUNTIME_RECEIPT")
    path=Path(supplied).resolve() if supplied else ROOT/"runtime-final.json"
    if not path.is_file():raise ValueError("Set STATION_FINAL_RUNTIME_RECEIPT to the actual final delivered runtime receipt")
    return json.loads(path.read_bytes())
