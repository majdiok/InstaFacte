"""Deterministic stdlib provenance regressions; only disposable synthetic inputs."""
import hashlib
import importlib.util
import json
from pathlib import Path
import sys
import tempfile
import unittest

sys.path.insert(0, str(Path(__file__).resolve().parent))
from provenance import SourceDriftError, SourceSnapshot


class ProvenanceTests(unittest.TestCase):
    def setUp(self):
        self.temporary = tempfile.TemporaryDirectory(prefix="c12-provenance-")
        self.addCleanup(self.temporary.cleanup)
        self.root = Path(self.temporary.name)
        self.source = self.root / "source"
        self.source.mkdir()
        (self.root / "release-spec").mkdir()
        self.head = "fixture-head-before-authoring"
        self.files = {"scene.py": b"VALUE = 'executed-before-edit'\n",
                      "shared_export.py": b"EXPORT_CAMERAS = False\n",
                      "registry.py": b"WIDTH = 6\n", "canonical36.csv": b"synthetic,fixture\n",
                      "v3-matrix-canonical.md": b"Synthetic test input, not the canonical matrix\n"}
        for name, data in self.files.items():
            (self.source / name).write_bytes(data)
        (self.root / "release-spec/approved-inputs.lock.json").write_text('{"synthetic": true}')
        self.snapshot = SourceSnapshot.capture(self.root, head_reader=lambda _: self.head)
        self.out = self.root / "output"
        self.out.mkdir()
        self.frozen = self.snapshot.write_snapshot(self.out / "source-snapshot")

    def assert_finalize_refused(self):
        with self.assertRaisesRegex(SourceDriftError, "source-drift"):
            self.snapshot.finalize(self.out, {"status": "incomplete-unapproved"})
        self.assertFalse((self.out / "study-evidence.json").exists())

    def test_frozen_execution_and_late_source_edit_cannot_finalize(self):
        # Explicit sequential boundary, no background race/sleep or canonical edit.
        spec = importlib.util.spec_from_file_location("c12_frozen_fixture", self.frozen / "source/scene.py")
        module = importlib.util.module_from_spec(spec)
        spec.loader.exec_module(module)
        (self.source / "scene.py").write_bytes(b"VALUE = 'changed-after-authoring'\n")
        self.assertEqual(module.VALUE, "executed-before-edit")
        self.assertEqual((self.frozen / "source/scene.py").read_bytes(), self.files["scene.py"])
        self.assert_finalize_refused()

    def test_capture_checks_exact_bootstrap_bytes(self):
        with self.assertRaisesRegex(SourceDriftError, "bootstrap changed"):
            SourceSnapshot.capture(self.root, executed_sources={"source/scene.py": b"older loaded code"},
                                   head_reader=lambda _: self.head)

    def test_reject_dependency_edit_including_export_helper(self):
        for name in ("shared_export.py", "registry.py", "canonical36.csv", "v3-matrix-canonical.md"):
            with self.subTest(name=name):
                path = self.source / name
                path.write_bytes(self.files[name] + b"changed")
                self.assert_finalize_refused()
                path.write_bytes(self.files[name])

    def test_reject_added_module(self):
        (self.source / "new_export_helper.py").write_text("EXPORT_LIGHTS = True\n")
        self.assert_finalize_refused()

    def test_reject_removed_module(self):
        (self.source / "scene.py").unlink()
        self.assert_finalize_refused()

    def test_reject_head_drift(self):
        self.head = "fixture-head-changed-during-render"
        self.assert_finalize_refused()

    def test_reject_snapshot_edit(self):
        path = self.frozen / "source/scene.py"
        path.chmod(0o644)
        path.write_text("VALUE = 'modified-snapshot'\n")
        self.assert_finalize_refused()

    def test_success_records_captured_hashes_and_never_overwrites(self):
        self.snapshot.finalize(self.out, {"status": "incomplete-unapproved"})
        report = json.loads((self.out / "study-evidence.json").read_text())
        self.assertEqual(report["checkoutBaseCommit"], self.head)
        self.assertEqual(report["sourceDriftCheck"], "passed-before-finalization")
        self.assertEqual(report["sourceFilesSha256"]["source/shared_export.py"],
                         hashlib.sha256(self.files["shared_export.py"]).hexdigest())
        self.assertIn("release-spec/approved-inputs.lock.json", report["sourceFilesSha256"])
        with self.assertRaises(FileExistsError):
            self.snapshot.finalize(self.out, {})
        with self.assertRaises(FileExistsError):
            self.snapshot.write_snapshot(self.frozen)


if __name__ == "__main__":
    unittest.main()
