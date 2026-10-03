#!/usr/bin/env python3
"""Regression checks for XML/revista confusion and exact-folder cover identity."""

from collections import defaultdict
import copy
import csv
import hashlib
import importlib.util
import json
from pathlib import Path
import subprocess
import sys
import tempfile
import unittest
from xml.etree import ElementTree

from PIL import Image


ROOT = Path(__file__).resolve().parents[2]
SCRIPTS = ROOT / "docs/station-android/scripts"
sys.path.insert(0, str(SCRIPTS))
from station_revista import select_revista_cover


def load(name):
    spec = importlib.util.spec_from_file_location(name.replace("-", "_"), SCRIPTS / name)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


class RevistaTest(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="station-revista-")
        self.addCleanup(self.temporary.cleanup)
        self.volume = Path(self.temporary.name)
        self.root = self.volume / "snes"
        self.magazine_root = self.root / "media/revista"
        self.magazine_root.mkdir(parents=True)
        (self.volume / "megadrive").mkdir()
        (self.volume / "megadrive/gamelist.xml").write_text("<gameList/>")
        self.rom = self.root / "Fixture.sfc"
        self.rom.write_bytes(b"fixture ROM")
        self.image = self.picture(self.root / "media/images/Fixture.png", "red")
        self.cover = self.picture(self.magazine_root / "Fixture.jpg", "blue")
        self.game = ElementTree.fromstring("<game><path>./Fixture.sfc</path>"
            "<name>Fixture</name><image>./media/images/Fixture.png</image></game>")
        (self.root / "gamelist.xml").write_text("<gameList>" +
            ElementTree.tostring(self.game, encoding="unicode") + "</gameList>")

    def picture(self, path, color):
        path.parent.mkdir(parents=True, exist_ok=True)
        Image.new("RGB", (3, 4), color).save(path)
        return path

    def magazine(self):
        result = defaultdict(list)
        for path in self.magazine_root.rglob("*"):
            if path.is_file():
                result[path.stem].append(path)
        return result

    def expected(self, rom=None):
        rom = rom or self.rom
        _, rule, digest = select_revista_cover(self.root, rom, self.magazine())
        return dict(revistaSelectionStatus="ok", revistaSelectionRule=rule,
                    revistaSelectedSha256=digest)

    def test_preparer_chooses_revista_when_xml_image_is_valid_but_different(self):
        module = load("preparar-catalogo-volume.py")
        selected, source = module.select_cover(self.root, self.game, self.rom,
                                               self.magazine(), self.expected())
        self.assertEqual((selected, source), (self.cover, "revista"))
        self.assertNotEqual(selected.read_bytes(), self.image.read_bytes())

    def test_root_rom_ignores_variant_in_another_collection(self):
        self.picture(self.magazine_root / "PT-BR/Fixture.png", "green")
        selected, rule, _ = select_revista_cover(self.root, self.rom, self.magazine())
        self.assertEqual((selected, rule), (self.cover, "rom_folder"))

    def test_translated_rom_prefers_its_exact_collection_over_root(self):
        rom = self.root / "PT-BR/Fixture.sfc"
        rom.parent.mkdir()
        rom.write_bytes(self.rom.read_bytes())
        translated = self.picture(self.magazine_root / "PT-BR/Fixture.png", "green")
        selected, rule, _ = select_revista_cover(self.root, rom, self.magazine())
        self.assertEqual((selected, rule), (translated, "rom_folder"))

    def test_different_contents_in_selected_folder_are_rejected(self):
        self.picture(self.magazine_root / "Fixture.png", "green")
        with self.assertRaisesRegex(ValueError, "different cover contents"):
            select_revista_cover(self.root, self.rom, self.magazine())

    def test_different_contents_in_unrelated_folders_are_not_arbitrarily_chosen(self):
        self.cover.unlink()
        self.picture(self.magazine_root / "A/Fixture.png", "green")
        self.picture(self.magazine_root / "B/Fixture.png", "blue")
        with self.assertRaisesRegex(ValueError, "different cover contents"):
            select_revista_cover(self.root, self.rom, self.magazine())

    def test_identical_copies_in_unrelated_folders_are_safe(self):
        self.cover.unlink()
        a = self.picture(self.magazine_root / "A/Fixture.png", "blue")
        b = self.magazine_root / "B/Fixture.png"
        b.parent.mkdir()
        b.write_bytes(a.read_bytes())
        selected, rule, _ = select_revista_cover(self.root, self.rom, self.magazine())
        self.assertEqual((selected, rule), (a, "identical_copies"))

    def test_missing_revista_does_not_fall_back_to_xml(self):
        self.cover.unlink()
        with self.assertRaisesRegex(ValueError, "missing"):
            load("preparar-catalogo-volume.py").select_cover(
                self.root, self.game, self.rom, self.magazine(), {})

    def test_invalid_cover_in_correct_folder_does_not_use_unrelated_variant(self):
        self.cover.write_bytes(b"broken")
        self.picture(self.magazine_root / "Other/Fixture.png", "green")
        with self.assertRaisesRegex(ValueError, "invalid"):
            select_revista_cover(self.root, self.rom, self.magazine())

    def test_image_outside_revista_via_symlink_is_rejected(self):
        self.cover.unlink()
        (self.magazine_root / "Fixture.png").symlink_to(self.image)
        with self.assertRaisesRegex(ValueError, "leaves magazine"):
            select_revista_cover(self.root, self.rom, self.magazine())

    def test_snapshot_detects_cover_change(self):
        expected = self.expected()
        self.picture(self.cover, "green")
        with self.assertRaisesRegex(ValueError, "changed since disk inventory"):
            select_revista_cover(self.root, self.rom, self.magazine(), expected)

    def correction_fixture(self):
        digest = hashlib.sha256(self.rom.read_bytes()).hexdigest()
        item = dict(itemId="published_01", coverId="cover_published_01", name="Fixture",
                    platform="snes", revision=3, filePath=str(self.rom),
                    coverPath=str(self.image), artifact={"sha256": digest, "format": "raw"})
        hidden = dict(item, itemId="published_alias_01", coverId="cover_alias_01",
                      catalogVisible=False)
        source = dict(itemId="published_01", sourcePlatform="snes", xmlEntry=1,
                      sourcePath=str(self.rom), sourceGameSha256=digest, collection="geral")
        sources = dict(items=[source, dict(source, itemId=hidden["itemId"])])
        disk = {("snes", "1"): dict(self.expected(), name="Fixture", gamePresent="yes",
                                   gameSha256=digest)}
        return dict(revision=3, items=[item, hidden]), sources, disk

    def test_correction_preserves_ids_artifacts_and_hidden_aliases(self):
        index, sources, disk = self.correction_fixture()
        before = copy.deepcopy(index)
        result, audit = load("corrigir-capas-revista.py").correct(index, sources, disk, self.volume, 4)
        self.assertEqual(index, before)
        self.assertEqual(result["revision"], 4)
        for old, new in zip(before["items"], result["items"]):
            self.assertEqual({k:v for k,v in old.items() if k not in {"revision", "coverPath"}},
                             {k:v for k,v in new.items() if k not in {"revision", "coverPath"}})
            self.assertEqual(new["coverPath"], str(self.cover))
            self.assertEqual(new["revision"], 4)
        self.assertTrue(result["items"][1]["catalogVisible"] is False)
        self.assertTrue(all(r["coverChanged"] for r in audit))

    def test_correction_requires_higher_item_revision(self):
        index, sources, disk = self.correction_fixture()
        index["items"][0]["revision"] = 5
        with self.assertRaisesRegex(ValueError, "revision must increase"):
            load("corrigir-capas-revista.py").correct(index, sources, disk, self.volume, 4)

    def test_correction_rejects_missing_source_identity(self):
        index, sources, disk = self.correction_fixture()
        sources["items"].pop()
        with self.assertRaisesRegex(ValueError, "source map differs"):
            load("corrigir-capas-revista.py").correct(index, sources, disk, self.volume, 4)

    def test_return_keeps_cover_bytes_ids_roms_and_increases_cache_revision(self):
        index, _, _ = self.correction_fixture()
        previous = copy.deepcopy(index)
        restored = load("implantar-capas-revista-20261003.py").return_index(index)
        self.assertEqual(index, previous)
        self.assertEqual(restored["revision"], 5)
        self.assertTrue(all(row["revision"] == 5 for row in restored["items"]))
        for old, new in zip(previous["items"], restored["items"]):
            self.assertEqual({k:v for k,v in old.items() if k != "revision"},
                             {k:v for k,v in new.items() if k != "revision"})

    def test_strict_crosscheck_rejects_xml_match_and_accepts_selected_revista(self):
        index, sources, _ = self.correction_fixture()
        disk_script = load("gerar-catalogo-midia.py")
        rows = disk_script.collect(self.volume, "snes", {})
        disk = self.volume / "disk.tsv"
        with disk.open("w", newline="") as file:
            writer = csv.DictWriter(file, disk_script.FIELDS, delimiter="\t")
            writer.writeheader(); writer.writerows(rows)
        index_path, map_path = self.volume / "index.json", self.volume / "map.json"
        map_path.write_text(json.dumps(sources))
        index_path.write_text(json.dumps(index))
        output = self.volume / "cross.tsv"
        command = [sys.executable, str(SCRIPTS / "cruzar-indice-catalogo.py"), "--index",
                   str(index_path), "--source-map", str(map_path), "--disk-tsv", str(disk),
                   "--require-revista", "--output", str(output)]
        rejected = subprocess.run(command, capture_output=True, text=True)
        self.assertNotEqual(rejected.returncode, 0)
        self.assertFalse(output.exists())
        for row in index["items"]:
            row["coverPath"] = str(self.cover)
        index_path.write_text(json.dumps(index))
        accepted = subprocess.run(command, capture_output=True, text=True)
        self.assertEqual(accepted.returncode, 0, accepted.stderr)
        self.assertEqual(json.loads(accepted.stdout)["sourceRevistaMatchCounts"], {"yes": 2})


if __name__ == "__main__":
    unittest.main()
