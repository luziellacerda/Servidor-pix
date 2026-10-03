#!/usr/bin/env python3
"""Verify ID preservation and fail-closed reconciliation using private fixtures."""
import copy
import hashlib
import json
from pathlib import Path
import subprocess
import tempfile
import unittest

from PIL import Image

ROOT = Path(__file__).resolve().parents[2]
SCRIPT = ROOT / "docs/station-android/scripts/conciliar-indices-station.py"


class ReconciliationTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="station-reconcile-")
        self.addCleanup(self.temporary.cleanup)
        self.folder = Path(self.temporary.name)
        self.game = self.folder / "game.bin"
        self.game.write_bytes(b"fixture ROM")
        self.cover = self.folder / "cover.png"
        Image.new("RGB", (2, 2), "red").save(self.cover)
        self.hash = hashlib.sha256(self.game.read_bytes()).hexdigest()
        self.artifact = dict(fileName="game.bin", format="raw", sizeBytes=11,
            sha256=self.hash, launchPath="game.bin", expandedSizeBytes=11, fileCount=1)
        self.item = dict(itemId="candidate_01", name="Fixture", platform="megadrivebr",
            revision=2, coverId="cover_fixture_01", filePath=str(self.game),
            coverPath=str(self.cover), artifact=self.artifact)
        self.base_item = {key: value for key, value in self.item.items() if key != "artifact"}
        self.base_item.update(itemId="published_01", platform="megadrive", revision=1)
        self.base = dict(revision=1, items=[self.base_item])
        self.candidate = dict(revision=2, items=[self.item])
        self.mapping = dict(items=[dict(itemId="candidate_01", sourcePlatform="megadrive",
            sourcePath=str(self.game), sourceGameSha256=self.hash)])

    def reconcile(self, revision=3, repair=False):
        for name, data in (("base", self.base), ("candidate", self.candidate),
                           ("mapping", self.mapping)):
            (self.folder / (name + ".json")).write_text(json.dumps(data))
        output = self.folder / "merged.json"
        command = ["python3", str(SCRIPT), "--base-index",
            str(self.folder / "base.json"), "--candidate-index",
            str(self.folder / "candidate.json"), "--source-map",
            str(self.folder / "mapping.json"), "--revision", str(revision),
            "--output", str(output)]
        if repair:
            command.append("--repair-platforms-from-source-path")
        run = subprocess.run(command, capture_output=True, text=True)
        return run, output

    def test_exact_source_preserves_id_when_classified_as_br(self):
        run, output = self.reconcile()
        self.assertEqual(run.returncode, 0, run.stderr)
        merged = json.loads(output.read_text())
        self.assertEqual(merged["items"][0]["itemId"], "published_01")
        self.assertEqual(merged["items"][0]["platform"], "megadrivebr")
        self.assertEqual(merged["items"][0]["revision"], 3)
        self.assertEqual(json.loads(run.stdout)["platformChangesWithPreservedId"],
                         {"megadrive -> megadrivebr": 1})
        self.assertEqual(output.stat().st_mode & 0o777, 0o600)

    def test_same_console_hash_preserves_id_after_source_move(self):
        moved = self.folder / "old-copy.bin"
        moved.write_bytes(self.game.read_bytes())
        self.base_item["filePath"] = str(moved)
        run, output = self.reconcile()
        self.assertEqual(run.returncode, 0, run.stderr)
        self.assertEqual(json.loads(output.read_text())["items"][0]["itemId"], "published_01")

    def test_equal_bytes_do_not_match_different_console(self):
        self.base_item["platform"] = "snes"
        run, output = self.reconcile()
        self.assertNotEqual(run.returncode, 0)
        self.assertFalse(output.exists())

    def test_explicit_repair_corrects_console_only_with_exact_source_path(self):
        self.base_item["platform"] = "gamegear"
        run, output = self.reconcile(repair=True)
        self.assertEqual(run.returncode, 0, run.stderr)
        row = json.loads(output.read_text())["items"][0]
        self.assertEqual(row["itemId"], "published_01")
        self.assertEqual(row["platform"], "megadrivebr")
        self.assertEqual(row["coverId"], self.base_item["coverId"])

    def test_explicit_repair_still_rejects_cross_console_hash_fallback(self):
        moved = self.folder / "moved.bin"
        moved.write_bytes(self.game.read_bytes())
        self.base_item.update(platform="snes", filePath=str(moved))
        run, output = self.reconcile(repair=True)
        self.assertNotEqual(run.returncode, 0)
        self.assertFalse(output.exists())

    def test_duplicate_ids_resolve_but_only_correct_canonical_game_is_visible(self):
        alias = dict(self.base_item, itemId="published_alias", platform="gb",
                     coverId="cover_alias_01")
        self.base_item["platform"] = "megadrivebr"
        self.base["items"] = [alias, self.base_item]
        run, output = self.reconcile(repair=True)
        self.assertEqual(run.returncode, 0, run.stderr)
        rows = {r["itemId"]:r for r in json.loads(output.read_text())["items"]}
        self.assertEqual(set(rows), {"published_alias", "published_01"})
        self.assertFalse(rows["published_alias"]["catalogVisible"])
        self.assertNotIn("catalogVisible", rows["published_01"])
        self.assertEqual(rows["published_alias"]["artifact"], rows["published_01"]["artifact"])
        report = json.loads(run.stdout)
        self.assertEqual(report["catalogItems"], 1)
        self.assertEqual(report["hiddenCompatibilityItems"], 1)
        self.assertEqual(report["preservedPublishedMappedIds"], 2)
        self.assertEqual(report["newTargetItems"], 0)

    def test_ambiguous_same_console_hash_is_rejected(self):
        second = copy.deepcopy(self.item)
        second.update(itemId="candidate_02", platform="megadrive")
        self.candidate["items"].append(second)
        other = self.folder / "second-source.bin"
        other.write_bytes(self.game.read_bytes())
        self.mapping["items"].append(dict(itemId="candidate_02", sourcePlatform="megadrive",
            sourcePath=str(other), sourceGameSha256=self.hash))
        moved = self.folder / "old-copy.bin"
        moved.write_bytes(self.game.read_bytes())
        self.base_item["filePath"] = str(moved)
        run, output = self.reconcile()
        self.assertNotEqual(run.returncode, 0)
        self.assertFalse(output.exists())

    def test_revision_cannot_regress_below_published_item(self):
        self.base_item["revision"] = 10
        run, output = self.reconcile(3)
        self.assertNotEqual(run.returncode, 0)
        self.assertFalse(output.exists())

    def test_other_platform_with_stale_artifact_is_not_silently_inherited(self):
        self.base_item.update(platform="gb", artifact=dict(self.artifact, sha256="0" * 64))
        run, output = self.reconcile()
        self.assertNotEqual(run.returncode, 0)
        self.assertFalse(output.exists())

    def test_staged_candidate_matches_by_verified_artifact_hash(self):
        staged = self.folder / "staged.bin"
        staged.write_bytes(self.game.read_bytes())
        self.base_item["filePath"] = str(staged)
        self.mapping["items"][0]["sourceGameSha256"] = "f" * 64
        run, output = self.reconcile()
        self.assertEqual(run.returncode, 0, run.stderr)
        self.assertEqual(json.loads(output.read_text())["items"][0]["itemId"], "published_01")

    def test_candidate_stale_artifact_is_rejected(self):
        self.item["artifact"]["sha256"] = "0" * 64
        run, output = self.reconcile()
        self.assertNotEqual(run.returncode, 0)
        self.assertFalse(output.exists())

    def test_unavailable_candidate_cover_is_rejected(self):
        self.cover.unlink()
        run, output = self.reconcile()
        self.assertNotEqual(run.returncode, 0)
        self.assertFalse(output.exists())

    def test_other_platform_preserves_id_revision_and_valid_artifact(self):
        other = dict(self.base_item, platform="gb", itemId="published_gb_01",
                     coverId="cover_gb_01", artifact=dict(self.artifact), revision=1)
        self.base["items"] = [other]
        run, output = self.reconcile()
        self.assertEqual(run.returncode, 0, run.stderr)
        inherited = next(row for row in json.loads(output.read_text())["items"]
                         if row["itemId"] == "published_gb_01")
        self.assertEqual(inherited["revision"], 1)
        self.assertEqual(inherited["artifact"], self.artifact)


if __name__ == "__main__":
    unittest.main()
