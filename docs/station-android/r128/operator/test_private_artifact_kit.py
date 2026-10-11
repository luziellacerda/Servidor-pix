"""Synthetic ZIP byte/privacy fixtures; does not test APK cryptographic signing."""
import contextlib
import copy
import hashlib
import io
import json
from pathlib import Path
import sys
import tempfile
import unittest
from unittest.mock import patch
import warnings
import zipfile

from test_explicit_engine_modes import ROOT
from test_scoped_mode_merge import RUNTIME
import extract_private_server_artifacts as KIT


class ArtifactKitFixtures(unittest.TestCase):
    def setUp(self):
        self.temp=tempfile.TemporaryDirectory(prefix="station-private-kit-isolated-")
        self.home=Path(self.temp.name);self.apk=self.home/"synthetic.zip"
        self.runtime=copy.deepcopy(RUNTIME);self.data={}
        for e in self.runtime["engines"]:
            for key,sha in (("library","coreSha256"),("runtimeLibrary","runtimeSha256")):
                name=e[key];self.data.setdefault(name,("Synthetic bytes: "+name).encode())
                e[sha]=hashlib.sha256(self.data[name]).hexdigest()
            e["engineId"]=e["engineId"].rsplit("-",1)[0]+"-"+e["runtimeSha256"][:12]
            self.data["libsynthetic_dependency.so"]=b"Synthetic dependency bytes"
            e["dependencySha256"]={"libsynthetic_dependency.so":hashlib.sha256(self.data["libsynthetic_dependency.so"]).hexdigest()}
        self.runtimefile=self.home/"runtime.json";self.packagefile=self.home/"package.json"
        self.out=self.home/"current-kit";self.package={}
        self.write_apk();self.write_receipts()

    def tearDown(self):self.temp.cleanup()
    def write_apk(self,duplicate=False):
        with zipfile.ZipFile(self.apk,"w") as z:
            for name,data in self.data.items():z.writestr("lib/arm64-v8a/"+name,data)
            z.writestr("assets/games/synthetic-private-rom.bin",b"Never copy synthetic game bytes")
            if duplicate:
                with warnings.catch_warnings():
                    warnings.simplefilter("ignore");name=next(iter(self.data));z.writestr("lib/arm64-v8a/"+name,self.data[name])
    def write_receipts(self):
        self.runtimefile.write_text(json.dumps(self.runtime)+"\n",encoding="utf-8")
        self.package.update(revision="SYNTHETIC-TEST",apkBytes=self.apk.stat().st_size,
            apkSha256=hashlib.sha256(self.apk.read_bytes()).hexdigest(),
            signatureSchemesVerified=["v2","v3"],zipAlignmentVerifiedBytes=16384,
            identityReceiptSha256=hashlib.sha256(self.runtimefile.read_bytes()).hexdigest(),
            engineIds=[e["engineId"] for e in self.runtime["engines"]])
        self.packagefile.write_text(json.dumps(self.package)+"\n",encoding="utf-8")
    def run_kit(self):
        argv=["extract_private_server_artifacts.py","--apk",str(self.apk),"--package-receipt",str(self.packagefile),
            "--runtime-receipt",str(self.runtimefile),"--output",str(self.out)]
        with patch.object(sys,"argv",argv),contextlib.redirect_stdout(io.StringIO()):KIT.main()

    def test_only_six_libraries_are_copied_no_dependencies_or_games(self):
        self.run_kit();names={p.name for p in self.out.iterdir()}
        self.assertEqual(len(names),7);self.assertNotIn("libsynthetic_dependency.so",names)
        self.assertNotIn("synthetic-private-rom.bin",names)
        receipt=json.loads((self.out/"artifact-kit-receipt.json").read_bytes())
        self.assertEqual(receipt["selectedCoreRuntimeLibraryCount"],6);self.assertFalse(receipt["uploaded"])
        self.assertEqual(len(receipt["libraryChecks"]),7)

    def test_wrong_full_apk_hash_is_rejected(self):
        self.package["apkSha256"]="0"*64;self.packagefile.write_text(json.dumps(self.package),encoding="utf-8")
        with self.assertRaisesRegex(ValueError,"complete APK differs"):self.run_kit()
        self.assertFalse(self.out.exists())

    def test_runtime_receipt_byte_seal_is_rejected(self):
        self.runtimefile.write_bytes(self.runtimefile.read_bytes()+b"\n")
        with self.assertRaisesRegex(ValueError,"identity seal"):self.run_kit()
        self.assertFalse(self.out.exists())

    def test_actual_library_mismatch_leaves_existing_kit_unchanged(self):
        self.run_kit();before={p.name:p.read_bytes() for p in self.out.iterdir()}
        self.data[next(iter(self.data))]=b"Wrong selected library bytes";self.write_apk();self.write_receipts()
        with self.assertRaisesRegex(ValueError,"library differs"):self.run_kit()
        self.assertEqual({p.name:p.read_bytes() for p in self.out.iterdir()},before)

    def test_actual_dependency_mismatch_leaves_existing_kit_unchanged(self):
        self.run_kit();before={p.name:p.read_bytes() for p in self.out.iterdir()}
        self.data["libsynthetic_dependency.so"]=b"Wrong dependency bytes";self.write_apk();self.write_receipts()
        with self.assertRaisesRegex(ValueError,"library differs"):self.run_kit()
        self.assertEqual({p.name:p.read_bytes() for p in self.out.iterdir()},before)

    def test_duplicate_apk_entry_is_rejected(self):
        self.write_apk(duplicate=True);self.write_receipts()
        with self.assertRaisesRegex(ValueError,"Duplicate APK"):self.run_kit()
        self.assertEqual(list(self.out.iterdir()),[])

    def test_unexpected_private_file_is_not_deleted(self):
        self.out.mkdir();p=self.out/"synthetic-license-secret.json";p.write_bytes(b"do not touch")
        with self.assertRaisesRegex(ValueError,"unexpected files"):self.run_kit()
        self.assertEqual(p.read_bytes(),b"do not touch")

    def test_selected_dependency_hash_conflict_is_rejected(self):
        self.runtime["engines"][0]["dependencySha256"][self.runtime["engines"][0]["library"]]="0"*64
        self.write_receipts()
        with self.assertRaisesRegex(ValueError,"identity conflicts"):self.run_kit()
        self.assertFalse(self.out.exists())


if __name__=="__main__":
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(ArtifactKitFixtures)
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    receipt=dict(schemaVersion=1,testMethods=result.testsRun,failures=len(result.failures),errors=len(result.errors),passed=result.wasSuccessful(),
        scope="Synthetic ZIP/full byte hash/selected library privacy; signing assertions are fixture input, not APK cryptographic validation; no server publication",
        helperSha256=hashlib.sha256((ROOT/"extract_private_server_artifacts.py").read_bytes()).hexdigest(),
        regressionSourceSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    (ROOT/"private-kit-regression-receipt.json").write_text(json.dumps(receipt,indent=2)+"\n",encoding="utf-8")
    raise SystemExit(0 if result.wasSuccessful() else 1)
