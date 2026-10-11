"""Isolated candidate/rollback fixtures; no Linux deployment or real approval.

Runs the exact patched operator functions against temporary files on Windows.
Only the Linux credential, group, flock and fsync primitives are substituted.
Their OS security semantics and interprocess concurrency are not tested here.
"""
import ast
import contextlib
import copy
import hashlib
import io
import json
import os
from pathlib import Path
import shutil
import tempfile
from types import SimpleNamespace
import unittest

from test_explicit_engine_modes import ROOT, M, item, engine, mode
from audit_test_fixtures import PATCHED_OPERATOR_PATH


class PortableOs:
    O_DIRECTORY=1 << 30
    def __init__(self,home):
        self.home=home;self.euid=0;self.environ={"PKEXEC_UID":"1000"}
        self.sync=home/"synthetic-directory-sync";self.sync.write_bytes(b"fixture")
    def __getattr__(self,name):return getattr(os,name)
    def geteuid(self):return self.euid
    def fchmod(self,*args):pass
    def fchown(self,*args):pass
    def fsync(self,*args):pass
    def open(self,path,flags):
        return os.open(self.sync,os.O_RDONLY) if flags==self.O_DIRECTORY else os.open(path,flags)


def operator(home):
    source=PATCHED_OPERATOR_PATH.read_text(encoding="utf-8")
    allowed={"read","digest","binary","write","restore_bytes","verify_binaries","reviewed_modes","stage","apply"}
    tree=ast.parse(source)
    tree.body=[n for n in tree.body if isinstance(n,ast.FunctionDef) and n.name in allowed]
    if {n.name for n in tree.body}!=allowed:raise AssertionError("Missing exact live operator function")
    calls=[]
    scope=dict(Path=Path,hashlib=hashlib,json=json,os=PortableOs(home),shutil=shutil,
        tempfile=tempfile,MAX_BINARY=256*1024*1024,prepare=M.prepare,
        validate_manifest=M.validate_manifest,validate_modes=M.validate_modes,
        pwd=SimpleNamespace(getpwnam=lambda name:SimpleNamespace(pw_gid=1000)),
        fcntl=SimpleNamespace(LOCK_EX=2,flock=lambda fd,lock:calls.append((fd,lock))))
    exec(compile(tree,"exact-patched-operator-fixture","exec"),scope)
    scope["_lock_calls"]=calls
    return scope


def put(path,value,pretty=True):
    # Formatting makes byte-exact rollback distinct from a semantic JSON rewrite.
    path.write_text(json.dumps(value,ensure_ascii=False,indent=2 if pretty else None)+"\n",encoding="utf-8")


class OperatorTransactionFixtures(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix="station-operator-isolated-")
        self.home=Path(self.temp.name).resolve();self.op=operator(self.home)
        self.paths={k:self.home/("index.json" if k=="catalog" else k+".json") for k in ("catalog","profiles","current","modes")}
        self.e=engine();self.e["coreSha256"]=hashlib.sha256(b"synthetic core bytes").hexdigest()
        self.e["runtimeSha256"]=hashlib.sha256(b"synthetic runtime bytes").hexdigest()
        self.incoming=dict(schemaVersion=1,engines=[self.e])
        self.legacy=engine("synthetic_legacy_engine",None)
        self.current=dict(schemaVersion=1,engines=[self.legacy])
        self.catalog=dict(items=[item()])
        self.profiles=M.prepare(self.catalog["items"],[],self.current,dict(schemaVersion=1,modes=[]))
        self.oldmode=mode("synthetic_legacy_engine");self.oldmode.pop("engineIds")
        self.oldmode["profileId"]="synthetic-legacy-reviewed-mode"
        self.oldmodes=dict(schemaVersion=1,modes=[self.oldmode])
        self.merged=dict(schemaVersion=1,modes=[self.oldmode,mode()])
        for k,v in (("catalog",self.catalog),("profiles",self.profiles),("current",self.current),("modes",self.oldmodes)):put(self.paths[k],v)
        self.infile=self.home/"incoming.json";put(self.infile,self.incoming)
        self.mergedfile=self.home/"merged.json";put(self.mergedfile,self.merged)
        self.artifacts=self.home/"source-artifacts";self.artifacts.mkdir()
        (self.artifacts/self.e["library"]).write_bytes(b"synthetic core bytes")
        (self.artifacts/self.e["runtimeLibrary"]).write_bytes(b"synthetic runtime bytes")
        self.candidate=self.home/"candidate"
        self.args=SimpleNamespace(**self.paths,incoming=self.infile,artifacts=self.artifacts,
            output=self.candidate,mode_additions=self.mergedfile)
        self.configfile=self.home/"config.json"
        put(self.configfile,dict(outputDirectory=str(self.home),autoOnlineProfiles=dict(
            registry=str(self.paths["profiles"]),engineManifest=str(self.paths["current"]),preparedModes=str(self.paths["modes"]))))
        self.apply_args=SimpleNamespace(config=self.configfile,candidate=self.candidate)
        self.original={k:p.read_bytes() for k,p in self.paths.items()}

    def tearDown(self):self.temp.cleanup()
    def stage(self):
        with contextlib.redirect_stdout(io.StringIO()):self.op["stage"](self.args)
    def apply(self):
        with contextlib.redirect_stdout(io.StringIO()):self.op["apply"](self.apply_args)
    def assert_unchanged(self):
        self.assertEqual({k:p.read_bytes() for k,p in self.paths.items()},self.original)
    def capture_writes(self,fail_name=None):
        real=self.op["write"];writes=[];failed=False
        def captured(path,value,gid=None):
            nonlocal failed
            writes.append(path.name)
            if fail_name==path.name and not failed:
                failed=True;raise OSError("Synthetic injected publication write failure")
            return real(path,value,gid)
        self.op["write"]=captured
        return writes

    def test_stage_seals_modes_with_unchanged_baseline(self):
        self.stage();receipt=json.loads((self.candidate/"receipt.json").read_bytes())
        self.assertEqual(set(receipt["files"]),{"incoming.json","engines.json","profiles.json","modes.json"})
        self.assertEqual(receipt["baseline"]["modes"],hashlib.sha256(self.original["modes"]).hexdigest())
        self.assertEqual(receipt["preparedModesAdded"],1);self.assert_unchanged()

    def test_apply_publishes_three_registries_profiles_last(self):
        self.stage();writes=self.capture_writes();self.apply()
        self.assertEqual(writes[:3],["modes.json","current.json","profiles.json"])
        self.assertEqual(json.loads(self.paths["modes"].read_bytes()),self.merged)
        current=json.loads(self.paths["current"].read_bytes())
        self.assertEqual(current["engines"][0],self.legacy)
        self.assertEqual(current["engines"][1],self.e)
        profiles=json.loads(self.paths["profiles"].read_bytes())
        self.assertEqual(profiles[:len(self.profiles)],self.profiles)
        self.assertTrue(any(p["engineId"]==self.e["engineId"] for p in profiles))
        self.assertEqual(len(self.op["_lock_calls"]),1)

    def test_profiles_write_failure_rolls_back_all_three_byte_exact(self):
        self.stage();self.capture_writes("profiles.json")
        with self.assertRaises(OSError):self.apply()
        self.assert_unchanged()

    def test_engine_write_failure_rolls_back_all_three_byte_exact(self):
        self.stage();self.capture_writes("current.json")
        with self.assertRaises(OSError):self.apply()
        self.assert_unchanged()

    def test_modes_write_failure_rolls_back_all_three_byte_exact(self):
        self.stage();self.capture_writes("modes.json")
        with self.assertRaises(OSError):self.apply()
        self.assert_unchanged()

    def test_rollback_attempts_every_registry_after_one_restore_failure(self):
        self.stage();self.capture_writes("profiles.json")
        real=self.op["restore_bytes"];restored=[]
        def failing_restore(path,source,gid):
            restored.append(path.name)
            if path.name=="modes.json":raise OSError("Synthetic restore failure")
            return real(path,source,gid)
        self.op["restore_bytes"]=failing_restore
        with self.assertRaisesRegex(ValueError,"operator recovery"):self.apply()
        self.assertEqual(restored,["modes.json","current.json","profiles.json"])
        self.assertEqual(self.paths["current"].read_bytes(),self.original["current"])
        self.assertEqual(self.paths["profiles"].read_bytes(),self.original["profiles"])

    def test_changed_catalog_baseline_fails_before_any_publish(self):
        self.stage();self.paths["catalog"].write_bytes(self.original["catalog"]+b"\n")
        writes=self.capture_writes()
        with self.assertRaisesRegex(ValueError,"baseline changed"):self.apply()
        self.assertEqual(writes,[])
        for k in ("modes","current","profiles"):self.assertEqual(self.paths[k].read_bytes(),self.original[k])

    def test_changed_modes_baseline_fails_before_any_publish(self):
        self.stage();self.paths["modes"].write_bytes(self.original["modes"]+b"\n")
        writes=self.capture_writes()
        with self.assertRaisesRegex(ValueError,"baseline changed"):self.apply()
        self.assertEqual(writes,[])

    def test_changed_candidate_mode_seal_fails(self):
        self.stage();p=self.candidate/"modes.json";p.write_bytes(p.read_bytes()+b"\n")
        with self.assertRaisesRegex(ValueError,"seal changed"):self.apply()
        self.assert_unchanged()

    def test_resealed_scope_outside_incoming_fails_revalidation(self):
        self.stage();changed=copy.deepcopy(self.merged)
        changed["modes"][-1]["engineIds"]=["synthetic_outside_delivery"]
        p=self.candidate/"modes.json";put(p,changed)
        receipt=json.loads((self.candidate/"receipt.json").read_bytes());receipt["files"]["modes.json"]=self.op["digest"](p)
        put(self.candidate/"receipt.json",receipt)
        with self.assertRaisesRegex(ValueError,"only incoming"):self.apply()
        self.assert_unchanged()

    def test_stage_scope_outside_incoming_is_rejected(self):
        changed=copy.deepcopy(self.merged);changed["modes"][-1]["engineIds"]=["synthetic_outside_delivery"]
        put(self.mergedfile,changed)
        with self.assertRaisesRegex(ValueError,"only incoming"):self.stage()
        self.assertFalse(self.candidate.exists());self.assert_unchanged()

    def test_stage_old_mode_prefix_change_is_rejected(self):
        changed=copy.deepcopy(self.merged);changed["modes"][0]["modeTitle"]="Changed baseline mode"
        put(self.mergedfile,changed)
        with self.assertRaisesRegex(ValueError,"remain exact"):self.stage()
        self.assertFalse(self.candidate.exists());self.assert_unchanged()

    def test_old_three_file_candidate_is_still_accepted_without_mode_write(self):
        self.args.mode_additions=None;self.stage()
        receipt=json.loads((self.candidate/"receipt.json").read_bytes());receipt["files"].pop("modes.json")
        (self.candidate/"modes.json").unlink();put(self.candidate/"receipt.json",receipt)
        writes=self.capture_writes();self.apply()
        self.assertNotIn("modes.json",writes);self.assertEqual(self.paths["modes"].read_bytes(),self.original["modes"])

    def test_new_candidate_without_changed_modes_preserves_exact_mode_bytes(self):
        self.args.mode_additions=None;self.stage();writes=self.capture_writes();self.apply()
        self.assertNotIn("modes.json",writes);self.assertEqual(self.paths["modes"].read_bytes(),self.original["modes"])

    def test_stage_verifies_actual_artifact_bytes(self):
        (self.artifacts/self.e["library"]).write_bytes(b"Different actual binary")
        with self.assertRaisesRegex(ValueError,"actual APK binary"):self.stage()
        self.assertFalse(self.candidate.exists());self.assert_unchanged()

    def test_apply_reverifies_sealed_candidate_actual_artifacts(self):
        self.stage();(self.candidate/"artifacts"/self.e["runtimeLibrary"]).write_bytes(b"tampered runtime")
        with self.assertRaisesRegex(ValueError,"actual APK binary"):self.apply()
        self.assert_unchanged()

    def test_non_administrator_is_rejected(self):
        self.stage();self.op["os"].euid=1000
        with self.assertRaisesRegex(ValueError,"administrative authentication"):self.apply()
        self.assert_unchanged()

    def test_missing_operator_identity_is_rejected(self):
        self.stage();self.op["os"].environ={}
        with self.assertRaisesRegex(ValueError,"administrative authentication"):self.apply()
        self.assert_unchanged()

    def test_immutable_engine_identity_is_not_replaced(self):
        self.incoming["engines"][0]["engineId"]=self.legacy["engineId"];put(self.infile,self.incoming)
        self.args.mode_additions=None
        with self.assertRaisesRegex(ValueError,"immutable"):self.stage()
        self.assert_unchanged()

    def test_repeated_apply_requires_reprepare_current_baseline(self):
        self.stage();self.apply()
        with self.assertRaisesRegex(ValueError,"baseline changed"):self.apply()


if __name__=="__main__":
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(OperatorTransactionFixtures)
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    receipt=dict(schemaVersion=1,testMethods=result.testsRun,failures=len(result.failures),errors=len(result.errors),
        passed=result.wasSuccessful(),scope="Isolated exact operator functions with synthetic files; Linux credentials/group/flock/fsync substituted; no OS security/concurrency or production/gameplay validation",
        operatorSourceSha256=hashlib.sha256(PATCHED_OPERATOR_PATH.read_bytes()).hexdigest(),
        regressionSourceSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    (ROOT/"operator-transaction-regression-receipt.json").write_text(json.dumps(receipt,indent=2)+"\n",encoding="utf-8")
    raise SystemExit(0 if result.wasSuccessful() else 1)
