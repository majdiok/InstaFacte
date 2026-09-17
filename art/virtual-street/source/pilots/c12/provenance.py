"""Freeze local authoring code/inputs before imports; reject drift at finalization."""
import hashlib
import json
from pathlib import Path
import subprocess
from types import MappingProxyType


class SourceDriftError(ValueError):
    pass


def checkout_head(root):
    return subprocess.run(["git", "rev-parse", "HEAD"], cwd=root, text=True,
                          capture_output=True, check=True).stdout.strip()


def input_files(root):
    # Include future shared export helpers too, not a fixed four-script inventory.
    paths = list((root / "source").rglob("*.py"))
    paths += [root / name for name in ("source/canonical36.csv", "source/v3-matrix-canonical.md",
                                       "release-spec/approved-inputs.lock.json")]
    return {p.relative_to(root).as_posix(): p for p in sorted(paths)}


def read_inputs(root):
    result = {}
    for name, path in input_files(root).items():
        if path.is_symlink() or not path.is_file() or path.stat().st_size > 10_000_000:
            raise SourceDriftError("source-drift: invalid input " + name)
        result[name] = path.read_bytes()
    return result


class SourceSnapshot:
    def __init__(self, root, head, contents, head_reader):
        self.root = Path(root)
        self.head = head
        self.contents = MappingProxyType(contents)
        self.head_reader = head_reader
        self.snapshot_root = None

    @classmethod
    def capture(cls, root, *, head=None, executed_sources=None, head_reader=checkout_head):
        root = Path(root)
        head = head if head is not None else head_reader(root)
        contents = read_inputs(root)
        for name, executed in (executed_sources or {}).items():
            if contents.get(name) != executed:
                raise SourceDriftError("source-drift: bootstrap changed before capture: " + name)
        snapshot = cls(root, head, contents, head_reader)
        snapshot.assert_unchanged()
        return snapshot

    def write_snapshot(self, destination):
        destination = Path(destination)
        destination.mkdir()  # New directory only; never reuse pycache/older sources.
        for name, data in self.contents.items():
            path = destination / name
            path.parent.mkdir(parents=True, exist_ok=True)
            with path.open("xb") as stream:
                stream.write(data)
            path.chmod(0o444)
        self.snapshot_root = destination
        self.assert_unchanged()
        return destination

    def assert_unchanged(self):
        try:
            if self.head_reader(self.root) != self.head:
                raise SourceDriftError("source-drift: checkout HEAD changed")
            if read_inputs(self.root) != self.contents:
                raise SourceDriftError("source-drift: original input contents or membership changed")
            if self.snapshot_root is not None and read_inputs(self.snapshot_root) != self.contents:
                raise SourceDriftError("source-drift: frozen snapshot changed")
        except OSError as error:
            raise SourceDriftError("source-drift: input disappeared or became unreadable") from error

    def evidence(self):
        return {"sourceFilesSha256": {name: hashlib.sha256(data).hexdigest()
                                      for name, data in self.contents.items()},
                "checkoutBaseCommit": self.head, "sourceMayBeUncommitted": True,
                "sourceExecution": "frozen-snapshot-before-authoring-imports",
                "sourceSnapshot": "source-snapshot", "sourceDriftCheck": "passed-before-finalization"}

    def finalize(self, output, report):
        # Serialize immutable captured values, NEVER recalculate hashes from live files.
        payload = json.dumps({**report, **self.evidence()}, indent=2) + "\n"
        self.assert_unchanged()
        with (Path(output) / "study-evidence.json").open("x") as stream:
            stream.write(payload)
