#!/usr/bin/env python3
"""Guard against stale publication and unwanted collateral metadata changes."""
import copy
import importlib.util
import json
from pathlib import Path
import unittest

SOURCE = Path(__file__).resolve().parents[2] / "docs/station-android/scripts/aplicar-sinopses-revisadas.py"
spec = importlib.util.spec_from_file_location("station_synopsis_update", SOURCE)
module = importlib.util.module_from_spec(spec)
spec.loader.exec_module(module)


class SynopsisPublication(unittest.TestCase):
    def fixture(self):
        rows = [dict(itemId="target", platform="snes", name="Actual game", revision=4,
                     coverId="existing-cover", coverPath="/immutable/cover.jpg",
                     filePath="/immutable/game.zip", artifact=dict(sha256="a" * 64),
                     metadata=dict(description="Old text", publisher="Keep publisher")),
                dict(itemId="unrelated", platform="snes", name="Other game", revision=3,
                     coverId="other-cover", metadata=dict(description="Unrelated text"))]
        document = dict(revision=18, items=rows, futureField={"preserved": True})
        raw = module.encode(document)
        overrides = module.encode({"snes:game.zip": {"launchPath": "game.sfc"},
                                   "snes:other.zip": {"name": "Keep override"}})
        change = dict(precondition=dict(itemId="target", platform="snes", itemRevision=4,
                                       artifactSha256="a" * 64, previousDescription="Old text",
                                       previousDescriptionSha256=module.sha(b"Old text")),
                      proposedDescription="Reviewed description",
                      proposedDescriptionSha256=module.sha(b"Reviewed description"))
        group = dict(sourcePath="/source/game.zip", overrideKey="snes:game.zip", itemIds=["target"])
        plan = dict(expectedIndexSha256=module.sha(raw), expectedIndexRevision=18,
                    expectedOverridesSha256=module.sha(overrides), proposals=[change], sourceGroups=[group])
        state = dict(sources={"/source/game.zip": {"ids": ["target"]}})
        return raw, overrides, state, plan

    def test_preserves_paths_ids_other_metadata_and_existing_overrides(self):
        raw, overrides, state, plan = self.fixture()
        after, new_overrides, ids = module.prepare(raw, overrides, state, plan)
        original = json.loads(raw)
        self.assertEqual(after["revision"], 19)
        self.assertEqual(ids, ["target"])
        changed = copy.deepcopy(after)
        changed["revision"] = 18
        changed["items"][0]["metadata"]["description"] = "Old text"
        self.assertEqual(changed, original)
        self.assertEqual(new_overrides["snes:game.zip"]["launchPath"], "game.sfc")
        self.assertEqual(new_overrides["snes:other.zip"], {"name": "Keep override"})
        self.assertEqual(json.loads(raw), original)

    def test_rejects_index_changed_after_review(self):
        raw, overrides, state, plan = self.fixture()
        changed = json.loads(raw)
        changed["items"][1]["metadata"]["description"] = "New unrelated valid prose"
        with self.assertRaisesRegex(ValueError, "Index changed"):
            module.prepare(module.encode(changed), overrides, state, plan)

    def test_rejects_override_changed_after_review(self):
        raw, _, state, plan = self.fixture()
        with self.assertRaisesRegex(ValueError, "overrides changed"):
            module.prepare(raw, b"{}", state, plan)

    def test_every_literal_precondition_is_checked(self):
        raw, overrides, state, plan = self.fixture()
        for field in ("platform", "itemRevision", "artifactSha256", "previousDescription",
                      "previousDescriptionSha256"):
            with self.subTest(field=field):
                candidate = copy.deepcopy(plan)
                candidate["proposals"][0]["precondition"][field] = "wrong"
                with self.assertRaisesRegex(ValueError, "precondition changed"):
                    module.prepare(raw, overrides, state, candidate)

    def test_count_is_utf16_units_not_python_characters(self):
        raw, overrides, state, plan = self.fixture()
        text = "\U0001F600" * 1001
        plan["proposals"][0].update(proposedDescription=text, proposedDescriptionSha256=module.sha(text.encode()))
        with self.assertRaisesRegex(ValueError, "existing contract"):
            module.prepare(raw, overrides, state, plan)

    def test_importer_json_budget_prevents_silent_shortening(self):
        raw, overrides, state, plan = self.fixture()
        text = "\u4e00" * 1500
        plan["proposals"][0].update(proposedDescription=text, proposedDescriptionSha256=module.sha(text.encode()))
        with self.assertRaisesRegex(ValueError, "shortened by the importer"):
            module.prepare(raw, overrides, state, plan)

    def test_changed_source_binding_fails(self):
        raw, overrides, state, plan = self.fixture()
        state["sources"]["/source/game.zip"]["ids"] = ["target", "unrelated"]
        with self.assertRaisesRegex(ValueError, "binding changed"):
            module.prepare(raw, overrides, state, plan)

    def test_shared_source_must_preserve_nonproposed_description(self):
        raw, overrides, state, plan = self.fixture()
        state["sources"]["/source/game.zip"]["ids"].append("unrelated")
        plan["sourceGroups"][0]["itemIds"].append("unrelated")
        with self.assertRaisesRegex(ValueError, "unproposed item"):
            module.prepare(raw, overrides, state, plan)

    def test_every_proposal_requires_persistent_binding(self):
        raw, overrides, state, plan = self.fixture()
        plan["sourceGroups"] = []
        with self.assertRaisesRegex(ValueError, "persistent source binding"):
            module.prepare(raw, overrides, state, plan)


if __name__ == "__main__":
    unittest.main()
