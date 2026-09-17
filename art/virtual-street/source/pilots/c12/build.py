"""Launch this offline study from frozen sources into a fresh ignored build/c12-* directory."""
import argparse
from pathlib import Path
import subprocess
import sys


HERE = Path(__file__).resolve().parent


def main(entry_source):
    art_root = HERE.parents[2]
    # Capture HEAD before executing any project dependency or authoring import.
    head = subprocess.run(["git", "rev-parse", "HEAD"], cwd=art_root, text=True,
                          capture_output=True, check=True).stdout.strip()
    provenance_path = HERE / "provenance.py"
    provenance_source = provenance_path.read_bytes()
    namespace = {"__file__": str(provenance_path), "__name__": "c12_frozen_provenance"}
    exec(compile(provenance_source, str(provenance_path), "exec"), namespace)
    relative = HERE.relative_to(art_root).as_posix()
    snapshot = namespace["SourceSnapshot"].capture(
        art_root, head=head, executed_sources={relative + "/build.py": entry_source,
                                              relative + "/provenance.py": provenance_source})
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--samples", type=int, default=24, choices=range(4, 65))
    parser.add_argument("--style", type=int, default=1, choices=range(5), help="0 Classic, 1 Modern, 2 Vintage, 3 Minimal, 4 Artisan")
    parser.add_argument("--quality", default="standard", choices=("economy", "standard"))
    parser.add_argument("--no-render", action="store_true", help="Geometry/export iteration only; not two-image readiness")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    import re
    if not re.fullmatch(r"c12-[a-z0-9][a-z0-9-]{0,55}", args.run_id):
        raise ValueError("Run ID must be c12- followed by safe lowercase characters")
    build = art_root / "build"
    if build.is_symlink():
        raise ValueError("Build directory cannot be a symlink")
    build.mkdir(exist_ok=True)
    out = build / args.run_id
    out.mkdir()
    frozen_root = snapshot.write_snapshot(out / "source-snapshot")
    # Fresh paths, no pycache and no previously loaded project modules. Only stdlib
    # and the installed Blender/toolchain remain outside the local source snapshot.
    for module in tuple(sys.modules.values()):
        path = getattr(module, "__file__", None)
        if path and module.__name__ != "__main__" and Path(path).resolve().is_relative_to(art_root / "source"):
            raise ValueError("Use a fresh Blender process; project module already imported: " + module.__name__)
    sys.dont_write_bytecode = True
    sys.path[:0] = [str(frozen_root / relative), str(frozen_root / "source")]
    import authoring
    snapshot.assert_unchanged()
    authoring.run(args, out, frozen_root, snapshot)


if __name__ == "__main__":
    # Re-execute the launcher from the very bytes recorded in the snapshot too.
    # An edit before/during capture cannot label old loaded build code with a new hash.
    entry_source = Path(__file__).read_bytes()
    namespace = {"__file__": str(Path(__file__).resolve()), "__name__": "c12_frozen_launcher"}
    exec(compile(entry_source, __file__, "exec"), namespace)
    namespace["main"](entry_source)
