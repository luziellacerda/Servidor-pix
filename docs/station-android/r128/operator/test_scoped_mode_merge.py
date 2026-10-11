"""Synthetic scoped-mode merge fixtures; no actual game approvals or publication."""
import copy
import hashlib
import json
from pathlib import Path
import unittest

from test_explicit_engine_modes import M, ROOT
from build_incoming_manifest import incoming
from merge_scoped_modes import merge
from audit_test_fixtures import final_runtime

RUNTIME=final_runtime()
MANIFEST,POLICIES=incoming(RUNTIME)
ENGINES={e["platform"]:e for e in MANIFEST["engines"]}


def item(system="n64",content="a"*64):
    extension={"n64":"z64","gamecube":"iso","wii":"wbfs","psx":"chd"}[system]
    return dict(itemId="synthetic_"+system,platform=system,contentSha256=content,catalogVisible=True,
        artifact=dict(launchPath="synthetic."+extension),metadata=dict(players="2"))


def profile(system="n64"):
    p=POLICIES[system];i=item(system)
    return dict(itemId=i["itemId"],platform=system,contentSha256=i["contentSha256"],
        engineId="synthetic_prior_exact_owner",coreSha256=p["coreSha256"],runtimeSha256="b"*64,
        profileId="synthetic_existing_game_mode",profileSha256=p["profileSha256"],approved=True,
        maximumPlayers=2,allowedPlayerCounts=[2],controllerProfile=p["controllerProfile"],
        mode="two-player-mode",modeTitle="Synthetic fixture only",instructions=["Synthetic preparation fixture."],
        sources=["https://example.invalid/synthetic-mode-evidence"])


def decision(system="psx"):
    p=profile(system)
    for k in ("engineId","coreSha256","runtimeSha256","profileSha256"):p.pop(k)
    p.update(biosPolicy="HLE-per-game-approved",reviewEvidence=["https://example.invalid/synthetic-hle-review"])
    return p


def run(rows=None,profiles=None,modes=None,decisions=None,runtime=None):
    return merge(M,dict(items=rows or [item()]),profiles or [],modes or dict(schemaVersion=1,modes=[]),
        runtime or RUNTIME,decisions)


class MergeFixtures(unittest.TestCase):
    def test_exact_previous_native_profiles_are_cloned(self):
        for system in ("n64","gamecube","wii"):
            with self.subTest(system=system):
                source=profile(system);out,r=run(rows=[item(system)],profiles=[source])
                self.assertEqual(r["addedScopedModes"],1)
                self.assertEqual(out["modes"][0]["engineIds"],[ENGINES[system]["engineId"]])
                self.assertEqual(out["modes"][0]["mode"],source["mode"])
                self.assertEqual(out["modes"][0]["allowedPlayerCounts"],source["allowedPlayerCounts"])

    def test_old_prepared_modes_prefix_is_untouched(self):
        old=decision("n64");old.pop("biosPolicy");old.pop("reviewEvidence")
        baseline=dict(schemaVersion=1,modes=[old]);freeze=copy.deepcopy(baseline)
        out,r=run(profiles=[profile()],modes=baseline)
        self.assertEqual(out["modes"][:1],freeze["modes"])
        self.assertEqual(baseline,freeze)
        self.assertNotEqual(out["modes"][1]["profileId"],old["profileId"])

    def test_psx_prior_retroarch_or_native_approval_is_never_auto_migrated(self):
        out,r=run(rows=[item("psx")],profiles=[profile("psx")])
        self.assertEqual(out["modes"],[])
        self.assertTrue(any(x["reason"]=="explicit-reviewed-mode-pending" for x in r["pendingOrSkipped"]))

    def test_explicit_per_edition_hle_review_creates_scoped_psx(self):
        out,r=run(rows=[item("psx")],decisions=dict(schemaVersion=1,modes=[decision()]))
        self.assertEqual(out["modes"][0]["engineIds"],[ENGINES["psx"]["engineId"]])
        self.assertEqual(r["previewProfilesAdded"],1)
        self.assertNotIn("biosPolicy",out["modes"][0])

    def test_psx_review_requires_declared_hle_policy_and_evidence(self):
        for mutation in ({"biosPolicy":"unreviewed"},{"reviewEvidence":[]},{"allowedPlayerCounts":[2,3]},{"maximumPlayers":4}):
            with self.subTest(mutation=mutation):
                d=decision();d.update(mutation)
                with self.assertRaises(ValueError):run(rows=[item("psx")],decisions=dict(schemaVersion=1,modes=[d]))

    def test_old_wrong_core_controller_or_config_is_not_cloned(self):
        for key,value in (("coreSha256","0"*64),("profileSha256","0"*64),("controllerProfile","retroarch-other")):
            with self.subTest(key=key):
                p=profile();p[key]=value
                out,r=run(profiles=[p]);self.assertEqual(out["modes"],[])

    def test_changed_content_is_not_cloned(self):
        out,r=run(rows=[item(content="d"*64)],profiles=[profile()])
        self.assertEqual(out["modes"],[])
        self.assertTrue(any(x["reason"]=="catalog-binding-mismatch" for x in r["pendingOrSkipped"]))

    def test_refused_existing_profile_is_not_promoted(self):
        p=profile();p["approved"]=False
        out,r=run(profiles=[p]);self.assertEqual(out["modes"],[])

    def test_unknown_next_import_remains_without_binding(self):
        new=item();new["itemId"]="synthetic_new_import"
        out,r=run(rows=[item(),new],profiles=[profile()])
        self.assertEqual(len(out["modes"]),1)
        self.assertTrue(any(x.get("itemId")==new["itemId"] and x["reason"]=="explicit-reviewed-mode-pending" for x in r["pendingOrSkipped"]))

    def test_cue_without_complete_identity_is_not_enrolled(self):
        i=item("psx");i["artifact"]["launchPath"]="synthetic.cue"
        out,r=run(rows=[i],decisions=dict(schemaVersion=1,modes=[decision()]))
        self.assertEqual(out["modes"],[])
        self.assertTrue(any(x["reason"]=="cue-set-identity-pending" for x in r["pendingOrSkipped"]))

    def test_mode_merge_is_idempotent(self):
        first,r=run(profiles=[profile()]);second,r2=run(profiles=[profile()],modes=first)
        self.assertEqual(first,second)
        self.assertEqual(r2["addedScopedModes"],0)

    def test_dynamic_new_runtime_id_is_derived_not_fixed(self):
        current=copy.deepcopy(RUNTIME)
        for e in current["engines"]:
            e["runtimeSha256"]="d"*64;e["engineId"]=e["engineId"].rsplit("-",1)[0]+"-"+"d"*12
        out,r=run(profiles=[profile()],runtime=current)
        self.assertTrue(out["modes"][0]["engineIds"][0].endswith("d"*12))
        self.assertNotEqual(out["modes"][0]["profileId"],run(profiles=[profile()])[0]["modes"][0]["profileId"])

    def test_conflicting_scoped_identity_fails_closed(self):
        first,r=run(profiles=[profile()]);first["modes"][0]["modeTitle"]="Changed fixture"
        with self.assertRaises(ValueError):run(profiles=[profile()],modes=first)

    def test_non_target_system_is_refused(self):
        d=decision();d["platform"]="ps3"
        with self.assertRaises(ValueError):run(decisions=dict(schemaVersion=1,modes=[d]))

    def test_long_profile_binding_id_stays_bounded(self):
        p=profile();p["profileId"]="a"*128
        # Profile registry allows128; prepared mode registry allows64.
        out,r=run(profiles=[p]);self.assertLessEqual(len(out["modes"][0]["profileId"]),64)

    def test_review_with_stale_final_runtime_is_refused(self):
        d=decision();d["engineId"]="stale"
        with self.assertRaises(ValueError):run(rows=[item("psx")],decisions=dict(schemaVersion=1,modes=[d]))


if __name__=="__main__":
    suite=unittest.defaultTestLoader.loadTestsFromTestCase(MergeFixtures)
    result=unittest.TextTestRunner(verbosity=2).run(suite)
    receipt=dict(schemaVersion=1,testMethods=result.testsRun,failures=len(result.failures),errors=len(result.errors),passed=result.wasSuccessful(),
        scope="Synthetic administrative mode merge only; no real HLE approval, deployment, API or Android gameplay",
        regressionSourceSha256=hashlib.sha256(Path(__file__).read_bytes()).hexdigest(),
        mergeSourceSha256=hashlib.sha256((ROOT/"merge_scoped_modes.py").read_bytes()).hexdigest())
    (ROOT/"mode-merge-regression-receipt.json").write_text(json.dumps(receipt,indent=2)+"\n",encoding="utf-8")
    raise SystemExit(0 if result.wasSuccessful() else 1)
