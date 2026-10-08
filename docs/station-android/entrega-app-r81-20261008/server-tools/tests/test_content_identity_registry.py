"""Synthetic, offline tests. Pass --work-dir to keep every artifact outside Git."""
import argparse
import contextlib
import copy
import hashlib
import importlib.util
import io
import json
from pathlib import Path
import tempfile
import unittest
from unittest import mock
import warnings
import zipfile

MODULE = Path(__file__).resolve().parents[1] / "prepare_content_identity_registry.py"
spec = importlib.util.spec_from_file_location("content_registry", MODULE)
tool = importlib.util.module_from_spec(spec)
spec.loader.exec_module(tool)
WORK = None


class RegistryTests(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory(prefix="identity-test-", dir=WORK)
        self.root = Path(self.temp.name)
        self.payload = b"synthetic-content-only\x00" * 64

    def tearDown(self):
        self.temp.cleanup()

    def fixture(self, zipped=False, entries=None):
        source = self.root / ("private-artifact.zip" if zipped else "private-artifact.sfc")
        if zipped:
            entries = entries or [("folder/game.sfc", self.payload)]
            with warnings.catch_warnings():
                warnings.simplefilter("ignore", UserWarning)
                with zipfile.ZipFile(source, "w", compression=zipfile.ZIP_DEFLATED) as archive:
                    for name, content in entries:
                        member = zipfile.ZipInfo(name)
                        # Preserve malicious bytes: Windows otherwise replaces backslashes
                        # when constructing a ZipInfo and would weaken this fixture.
                        member.filename = member.orig_filename = name
                        member.compress_type = zipfile.ZIP_DEFLATED
                        archive.writestr(member, content)
            expanded = sum(len(content) for name, content in entries if not name.endswith("/"))
            count = sum(not name.endswith("/") for name, _ in entries)
        else:
            source.write_bytes(self.payload)
            expanded, count = len(self.payload), 1
        row = dict(itemId="synthetic-item", platform="snes", revision=4,
                   artifact=dict(format="zip" if zipped else "raw", sizeBytes=source.stat().st_size,
                                 sha256=hashlib.sha256(source.read_bytes()).hexdigest(),
                                 launchPath="folder/game.sfc" if zipped else "game.sfc",
                                 expandedSizeBytes=expanded, fileCount=count))
        return source, row

    def prepare(self, source, row, field="items"):
        return tool.prepare({field: [row]}, {row["itemId"]: str(source)})

    def rejected(self, source, row):
        with self.assertRaises((tool.ValidationError, zipfile.BadZipFile)):
            self.prepare(source, row)

    def test_raw_exact_contract_and_no_private_paths(self):
        source, row = self.fixture()
        result, encoded = self.prepare(source, row)
        self.assertEqual(set(result), {"schemaVersion", "entries"})
        entry = result["entries"][0]
        self.assertEqual(set(entry), tool.ENTRY_KEYS)
        self.assertEqual(entry["contentSha256"], row["artifact"]["sha256"])
        self.assertNotIn(str(self.root), encoded.decode())
        self.assertNotIn("approved", encoded.decode())

    def test_zip_stream_exact_launch_and_container_separate(self):
        source, row = self.fixture(True, [("folder/", b""), ("readme.txt", b"extra"), ("folder/game.sfc", self.payload)])
        result, _ = self.prepare(source, row)
        entry = result["entries"][0]
        self.assertEqual(entry["contentSha256"], hashlib.sha256(self.payload).hexdigest())
        self.assertNotEqual(entry["contentSha256"], entry["artifactSha256"])
        self.assertFalse((self.root / "folder").exists())

    def test_records_nested_and_flat_inventory(self):
        source, row = self.fixture(True)
        expected, _ = self.prepare(source, row, "records")
        flat = {"itemId": row["itemId"], "platform": row["platform"]}
        flat.update({"artifact" + k[0].upper() + k[1:]: v for k, v in row["artifact"].items()})
        actual, _ = self.prepare(source, flat, "records")
        self.assertEqual(expected, actual)

    def test_artifact_sha_size_and_raw_descriptor_mismatch(self):
        for key, value in (("sha256", "0" * 64), ("sizeBytes", 1), ("fileCount", 2), ("expandedSizeBytes", 1)):
            with self.subTest(key=key):
                source, row = self.fixture()
                row["artifact"][key] = value
                self.rejected(source, row)

    def test_exact_launch_case_and_missing_member(self):
        for launch in ("folder/Game.sfc", "game.sfc", "absent.sfc"):
            with self.subTest(launch=launch):
                source, row = self.fixture(True)
                row["artifact"]["launchPath"] = launch
                self.rejected(source, row)

    def test_zip_paths_and_duplicates(self):
        for name in ("../other", "/other", "C:/other", "a\\other", "a//other", "./other", "folder/game.sfc", "other\x00hidden"):
            with self.subTest(name=name):
                source, row = self.fixture(True, [("folder/game.sfc", self.payload), (name, b"bad")])
                self.rejected(source, row)

    def test_zip_symbolic_link_and_unsupported_compression(self):
        for symbolic in (True, False):
            source, row = self.fixture(True)
            with zipfile.ZipFile(source, "w") as archive:
                member = zipfile.ZipInfo("folder/game.sfc")
                if symbolic:
                    member.create_system = 3
                    member.external_attr = 0o120777 << 16
                else:
                    member.compress_type = zipfile.ZIP_BZIP2
                archive.writestr(member, self.payload)
            row["artifact"]["sizeBytes"] = source.stat().st_size
            row["artifact"]["sha256"] = hashlib.sha256(source.read_bytes()).hexdigest()
            self.rejected(source, row)

    def test_expansion_and_count_limits(self):
        for key, value in (("expandedSizeBytes", 1), ("expandedSizeBytes", tool.MAX_BYTES + 1),
                           ("expandedSizeBytes", True), ("fileCount", 2), ("fileCount", tool.MAX_FILES + 1),
                           ("fileCount", 0)):
            with self.subTest(key=key, value=value):
                source, row = self.fixture(True)
                row["artifact"][key] = value
                self.rejected(source, row)

    def test_bad_format_and_hash(self):
        for key, value in (("format", "rar"), ("sha256", "F" * 64), ("sha256", None), ("launchPath", "../bad")):
            source, row = self.fixture()
            row["artifact"][key] = value
            self.rejected(source, row)

    def test_catalog_and_mapping_rejections(self):
        source, row = self.fixture()
        mapping = {row["itemId"]: str(source)}
        for catalog in ({"items": [row, row]}, {"items": [row], "records": [row]}, {"items": [row] * 4097}):
            with self.assertRaises(tool.ValidationError):
                tool.prepare(catalog, mapping)
        for bad in ({"unknown": str(source)}, {row["itemId"]: "relative.sfc"}, {row["itemId"]: 42}):
            with self.assertRaises(tool.ValidationError):
                tool.prepare({"items": [row]}, bad)
        with self.assertRaises(tool.ValidationError):
            json.loads('{"same":1,"same":2}', object_pairs_hook=tool.unique_object)

    def test_cli_success_and_failure_preserve_output_and_hide_paths(self):
        source, row = self.fixture(True)
        catalog, mapping, output = [self.root / n for n in ("catalog.json", "mapping.json", "registry.json")]
        catalog.write_text(json.dumps({"items": [row]}), encoding="utf-8")
        mapping.write_text(json.dumps({row["itemId"]: str(source)}), encoding="utf-8")
        args = ["--catalog", str(catalog), "--mapping", str(mapping), "--output", str(output)]
        stdout, stderr = io.StringIO(), io.StringIO()
        with contextlib.redirect_stdout(stdout), contextlib.redirect_stderr(stderr):
            self.assertEqual(tool.main(args), 0)
            good = output.read_bytes()
            mapping.write_text(json.dumps({row["itemId"]: str(self.root / "private-missing.zip")}), encoding="utf-8")
            self.assertEqual(tool.main(args), 1)
        self.assertEqual(output.read_bytes(), good)
        self.assertNotIn(str(self.root), stdout.getvalue() + stderr.getvalue())

    def test_output_cannot_overwrite_inputs_or_artifact(self):
        source, row = self.fixture()
        catalog, mapping = self.root / "catalog.json", self.root / "mapping.json"
        catalog.write_text(json.dumps({"items": [row]}), encoding="utf-8")
        mapping.write_text(json.dumps({row["itemId"]: str(source)}), encoding="utf-8")
        for target in (source, catalog, mapping):
            before = target.read_bytes()
            with contextlib.redirect_stderr(io.StringIO()):
                status = tool.main(["--catalog", str(catalog), "--mapping", str(mapping), "--output", str(target)])
            self.assertEqual(status, 1)
            self.assertEqual(target.read_bytes(), before)

    def test_existing_merge_preserves_qualified_unmapped_entry(self):
        legacy_source, legacy = self.fixture()
        legacy["itemId"] = "legacy-item"
        existing, _ = self.prepare(legacy_source, legacy)
        source, current = self.fixture(True)
        result, _ = tool.prepare({"items": [legacy, current]}, {current["itemId"]: str(source)}, existing)
        self.assertEqual(len(result["entries"]), 2)
        self.assertEqual(result["entries"][0], existing["entries"][0])
        self.assertEqual(result["entries"][1]["contentSha256"], hashlib.sha256(self.payload).hexdigest())
        self.assertEqual(len(existing["entries"]), 1)

    def test_existing_stale_rejects_but_verified_mapping_replaces(self):
        source, old_row = self.fixture()
        existing, _ = self.prepare(source, old_row)
        self.payload = b"changed-synthetic-edition"
        source, current = self.fixture()
        with self.assertRaises(tool.ValidationError):
            tool.prepare({"items": [current]}, {}, existing)
        result, _ = tool.prepare({"items": [current]}, {current["itemId"]: str(source)}, existing)
        self.assertEqual(len(result["entries"]), 1)
        self.assertEqual(result["entries"][0]["artifactSha256"], current["artifact"]["sha256"])
        self.assertNotEqual(result["entries"][0]["contentSha256"], existing["entries"][0]["contentSha256"])
        source.write_bytes(b"wrong-current-artifact")
        with self.assertRaises(tool.ValidationError):
            tool.prepare({"items": [current]}, {current["itemId"]: str(source)}, existing)

    def test_existing_descriptor_counts_and_absent_id_rejected(self):
        source, row = self.fixture(True)
        existing, _ = self.prepare(source, row)
        for field, value in (("fileCount", 2), ("expandedSizeBytes", 1), ("launchPath", "different.sfc"),
                             ("platform", "megadrive"), ("artifactSha256", "0" * 64)):
            with self.subTest(field=field):
                changed = copy.deepcopy(existing)
                changed["entries"][0][field] = value
                with self.assertRaises(tool.ValidationError):
                    tool.prepare({"items": [row]}, {}, changed)
        with self.assertRaises(tool.ValidationError):
            tool.prepare({"items": []}, {}, existing)

    def test_existing_schema_fields_hashes_types_and_duplicates(self):
        source, row = self.fixture()
        existing, _ = self.prepare(source, row)
        invalid_documents = [None, [], {"schemaVersion": True, "entries": []},
                             {"schemaVersion": 2, "entries": []}, {"schemaVersion": 1, "entries": {}},
                             dict(existing, extra=True), dict(existing, entries=existing["entries"] * 2),
                             dict(existing, entries=existing["entries"] * 4097)]
        for field, value in (("contentSha256", "F" * 64), ("artifactSha256", "bad"),
                             ("fileCount", True), ("fileCount", 100001), ("expandedSizeBytes", 0),
                             ("expandedSizeBytes", tool.MAX_BYTES + 1), ("platform", ""),
                             ("launchPath", "../bad"), ("itemId", "bad\n")):
            changed = copy.deepcopy(existing)
            changed["entries"][0][field] = value
            invalid_documents.append(changed)
        for remove in (True, False):
            changed = copy.deepcopy(existing)
            if remove:
                del changed["entries"][0]["contentSha256"]
            else:
                changed["entries"][0]["approved"] = True
            invalid_documents.append(changed)
        for number, document in enumerate(invalid_documents):
            with self.subTest(case=number), self.assertRaises(tool.ValidationError):
                tool.prepare({"items": [row]}, {row["itemId"]: str(source)}, document)
        duplicate = self.root / "duplicate-existing.json"
        duplicate.write_text('{"schemaVersion":1,"schemaVersion":1,"entries":[]}', encoding="utf-8")
        with self.assertRaises(tool.ValidationError):
            tool.load_json(duplicate, tool.MAX_REGISTRY_BYTES)

    def test_existing_raw_content_must_equal_current_raw_digest(self):
        source, row = self.fixture()
        existing, _ = self.prepare(source, row)
        existing["entries"][0]["contentSha256"] = "0" * 64
        with self.assertRaises(tool.ValidationError):
            tool.prepare({"items": [row]}, {}, existing)
        result, _ = tool.prepare({"items": [row]}, {row["itemId"]: str(source)}, existing)
        self.assertEqual(result["entries"][0]["contentSha256"], row["artifact"]["sha256"])

    def test_existing_cli_merge_and_refuse_existing_output(self):
        source, row = self.fixture()
        document, encoded = self.prepare(source, row)
        catalog, mapping, existing, output = [self.root / name for name in
                                             ("catalog.json", "mapping.json", "existing.json", "new.json")]
        catalog.write_text(json.dumps({"items": [row]}), encoding="utf-8")
        mapping.write_text("{}", encoding="utf-8")
        existing.write_bytes(encoded)
        base = ["--catalog", str(catalog), "--mapping", str(mapping), "--existing", str(existing)]
        with contextlib.redirect_stdout(io.StringIO()), contextlib.redirect_stderr(io.StringIO()):
            self.assertEqual(tool.main(base + ["--output", str(existing)]), 1)
            self.assertEqual(existing.read_bytes(), encoded)
            self.assertEqual(tool.main(base + ["--output", str(output)]), 0)
            self.assertEqual(json.loads(output.read_bytes()), document)
            self.assertEqual(tool.main(base + ["--output", str(output)]), 1)
            self.assertEqual(output.read_bytes(), encoded)
        self.assertEqual(existing.read_bytes(), encoded)

    def test_new_output_atomic_publication_cannot_overwrite_racing_file(self):
        output = self.root / "racing-registry.json"
        original_link = tool.os.link
        def racing_link(source, destination):
            Path(destination).write_bytes(b"another-operator-output")
            return original_link(source, destination)
        with mock.patch.object(tool.os, "link", side_effect=racing_link), self.assertRaises(FileExistsError):
            tool.write_registry(output, b"candidate-output")
        self.assertEqual(output.read_bytes(), b"another-operator-output")
        self.assertEqual(list(self.root.glob(".content-identity-*.tmp")), [])


if __name__ == "__main__":
    parser = argparse.ArgumentParser()
    parser.add_argument("--work-dir", type=Path, required=True)
    args, remaining = parser.parse_known_args()
    args.work_dir.mkdir(parents=True, exist_ok=True)
    WORK = str(args.work_dir)
    unittest.main(argv=[__file__] + remaining)
