"""Isolated administrative preparation fixtures; no gameplay or publication."""
import copy
import hashlib
import importlib.util
import json
from pathlib import Path
import tempfile
import unittest
from audit_test_fixtures import baseline_module,final_runtime,PATCHED_PROFILE_PATH

ROOT=Path(__file__).resolve().parent


def load(path,name):
    spec=importlib.util.spec_from_file_location(name,path)
    module=importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


M=load(PATCHED_PROFILE_PATH,"scoped_profiles")
BASE,BASE_SOURCE=baseline_module()
DELIVERY=load(ROOT/"build_incoming_manifest.py","incoming_manifest")


def item(name="synthetic_item_a", system="psx", content="a"*64):
    return dict(itemId=name,contentSha256=content,platform=system,catalogVisible=True,
        metadata=dict(players="2"),artifact=dict(launchPath="fixture.chd" if system=="psx" else "fixture.z64"))


def engine(identity="synthetic_psx_engine_new", scoped=True, system="psx"):
    name="standard-2p-v1" if system=="psx" else "direct-four-ports-v1"
    maximum=2 if system=="psx" else 4
    config=dict(schemaVersion=1,controllerProfile=name,devices=[1]*maximum,coreOptions="synthetic-options=true\n")
    value=dict(engineId=identity,platform=system,coreSha256="b"*64,runtimeSha256="c"*64,
        library="libfixture_core.so",runtimeLibrary="libfixture_runtime.so",launchReady=True,
        recoveryProtocol="station-stream.v3",maximumPlayers=maximum,
        extensions=["chd"] if system=="psx" else ["z64"],options=config["coreOptions"],
        controllerProfiles=[dict(controllerProfile=name,maximumPlayers=maximum,configuration=config)])
    if scoped is not None:value["requireExplicitEngineModes"]=scoped
    return value


def mode(identity="synthetic_psx_engine_new", name="synthetic_item_a", content="a"*64,
         controller="standard-2p-v1", maximum=2, approved=True):
    return dict(itemId=name,contentSha256=content,platform="psx",profileId="synthetic-reviewed-mode",
        maximumPlayers=maximum,allowedPlayerCounts=list(range(2,maximum+1)),approved=approved,
        controllerProfile=controller,mode="two-human-inputs",modeTitle="Synthetic fixture only",
        instructions=["Synthetic isolated fixture; not a game approval."],sources=["https://example.invalid/isolated-fixture"],
        engineIds=[identity])


def prepare(engines,rows=None,modes=None,existing=None,module=M):
    return module.prepare(rows or [item()],existing or [],dict(schemaVersion=1,engines=engines),
        dict(schemaVersion=1,modes=modes or []))


class ExplicitModeFixtures(unittest.TestCase):
    def test_unknown_psx_does_not_receive_generic_approval(self):
        self.assertEqual(prepare([engine()]),[])

    def test_old_generic_mode_does_not_approve_new_runtime(self):
        old=mode();old.pop("engineIds")
        self.assertEqual(prepare([engine()],modes=[old]),[])

    def test_exact_explicit_scope_creates_one_binding(self):
        result=prepare([engine()],modes=[mode()])
        self.assertEqual(len(result),1)
        self.assertEqual(result[0]["engineId"],"synthetic_psx_engine_new")
        self.assertEqual(result[0]["allowedPlayerCounts"],[2])
        self.assertTrue(result[0]["approved"])

    def test_refused_mode_remains_refused(self):
        result=prepare([engine()],modes=[mode(approved=False)])
        self.assertFalse(result[0]["approved"])

    def test_other_runtime_scope_does_not_match(self):
        self.assertEqual(prepare([engine()],modes=[mode(identity="synthetic_other_runtime")]),[])

    def test_unscoped_old_engine_ignores_new_scoped_mode(self):
        result=prepare([engine(),engine("synthetic_old",False)],modes=[mode()])
        self.assertEqual(len(result),1)
        self.assertEqual(result[0]["engineId"],"synthetic_psx_engine_new")

    def test_changed_content_cannot_reuse_old_approval(self):
        self.assertEqual(prepare([engine()],rows=[item(content="d"*64)],modes=[mode()]),[])

    def test_changed_platform_cannot_reuse_old_approval(self):
        changed=mode();changed["platform"]="n64"
        self.assertEqual(prepare([engine()],modes=[changed]),[])

    def test_unsupported_launch_extension_does_not_match(self):
        changed=item();changed["artifact"]["launchPath"]="unsupported.iso"
        self.assertEqual(prepare([engine()],rows=[changed],modes=[mode()]),[])

    def test_next_import_unknown_item_remains_pending(self):
        rows=[item(),item("synthetic_future_import",content="d"*64)]
        result=prepare([engine()],rows=rows,modes=[mode()])
        self.assertEqual([x["itemId"] for x in result],["synthetic_item_a"])

    def test_fields_do_not_leak_into_public_wire_profile(self):
        result=prepare([engine()],modes=[mode()])[0]
        self.assertNotIn("engineIds",result)
        self.assertNotIn("requireExplicitEngineModes",result)

    def test_existing_profiles_prefix_is_deeply_preserved(self):
        old=prepare([engine("synthetic_legacy",None)],module=BASE)
        frozen=copy.deepcopy(old)
        result=prepare([engine()],modes=[mode()],existing=old)
        self.assertEqual(result[:len(frozen)],frozen)
        self.assertEqual(old,frozen)
        self.assertEqual(len(result),len(old)+1)

    def test_repeat_preparation_is_idempotent(self):
        first=prepare([engine()],modes=[mode()])
        self.assertEqual(prepare([engine()],modes=[mode()],existing=first),first)

    def test_legacy_defaults_are_byte_equivalent(self):
        args=dict(engines=[engine("synthetic_legacy",None)])
        self.assertEqual(json.dumps(prepare(**args),sort_keys=True),json.dumps(prepare(**args,module=BASE),sort_keys=True))

    def test_legacy_unscoped_prepared_mode_is_unchanged(self):
        e=engine("synthetic_legacy",None);old=mode();old.pop("engineIds")
        self.assertEqual(prepare([e],modes=[old]),prepare([e],modes=[old],module=BASE))

    def test_explicit_false_flag_preserves_default_behavior(self):
        e=engine("synthetic_legacy",False)
        self.assertEqual(prepare([e]),prepare([e],module=BASE))

    def test_non_boolean_gate_is_rejected(self):
        for value in ("true",1,0,None,[],{}):
            with self.subTest(value=value):
                e=engine();e["requireExplicitEngineModes"]=value
                with self.assertRaises(ValueError):prepare([e])

    def test_bad_scopes_are_rejected(self):
        for value in ([],"engine",["same","same"],["bad\nengine"],["x"*121],["e"+str(i) for i in range(65)]):
            with self.subTest(value=value):
                m=mode();m["engineIds"]=value
                with self.assertRaises(ValueError):prepare([engine()],modes=[m])

    def test_invalid_platform_capacity_is_rejected(self):
        with self.assertRaises(ValueError):prepare([engine()],modes=[mode(maximum=3)])

    def test_n64_documented_two_four_counts_preserved(self):
        e=engine("synthetic_n64",True,"n64")
        m=mode(identity=e["engineId"],controller="direct-four-ports-v1",maximum=4)
        m["platform"]="n64";m["allowedPlayerCounts"]=[2,4]
        result=prepare([e],rows=[item(system="n64")],modes=[m])
        self.assertEqual(result[0]["allowedPlayerCounts"],[2,4])

    def test_duplicate_mode_identity_is_rejected(self):
        other=mode();other["engineIds"]=["synthetic_other_runtime"]
        with self.assertRaises(ValueError):prepare([engine()],modes=[mode(),other])

    def test_profile_sha_is_canonical_without_final_newline(self):
        e=engine();result=prepare([e],modes=[mode()])[0]
        self.assertEqual(result["profileSha256"],hashlib.sha256(DELIVERY.canonical(e["controllerProfiles"][0]["configuration"])).hexdigest())

    def test_actual_delivery_manifest_profiles_pass_server_validator(self):
        incoming,_=DELIVERY.incoming(final_runtime())
        self.assertEqual(M.validate_manifest(incoming),incoming)
        for e in incoming["engines"]:
            expected=DELIVERY.POLICY[e["platform"]][2]
            self.assertEqual(hashlib.sha256(DELIVERY.canonical(e["controllerProfiles"][0]["configuration"])).hexdigest(),expected)

    def test_generated_fields_are_administrative_only(self):
        incoming,_=DELIVERY.incoming(final_runtime())
        self.assertTrue(all(e["requireExplicitEngineModes"] for e in incoming["engines"]))
        self.assertEqual({e["platform"] for e in incoming["engines"]},{"n64","gamecube","wii","psx"})


if __name__=="__main__":
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(ExplicitModeFixtures)
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    receipt=dict(schemaVersion=1,testMethods=result.testsRun,failures=len(result.failures),errors=len(result.errors),
        passed=result.wasSuccessful(),scope="Isolated synthetic administrative preparation; no Linux publication, API deployment, device operation or physical gameplay",
        operatorBaselineSha256=hashlib.sha256(BASE_SOURCE).hexdigest(),
        operatorPatchedSha256=hashlib.sha256(PATCHED_PROFILE_PATH.read_bytes()).hexdigest(),
        regressionSourceSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest())
    (ROOT/"operator-regression-receipt.json").write_text(json.dumps(receipt,indent=2)+"\n",encoding="utf-8")
    raise SystemExit(0 if result.wasSuccessful() else 1)
