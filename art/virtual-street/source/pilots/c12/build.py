"""Build only this offline C12 study into a fresh ignored build/c12-* directory."""
import argparse
import hashlib
import json
from pathlib import Path
import re
import subprocess
import sys
import time

import bpy

HERE = Path(__file__).resolve().parent
sys.path.insert(0, str(HERE))
sys.path.insert(0, str(HERE.parents[1]))
from registry import load_registry
from scene import build_scene
from validation import create_proxies, export_objects, geometry_fingerprint, validate_glb, validate_scene


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--run-id", required=True)
    parser.add_argument("--samples", type=int, default=24, choices=range(4, 65))
    parser.add_argument("--no-render", action="store_true", help="Geometry/export iteration only; not two-image readiness")
    args = parser.parse_args(sys.argv[sys.argv.index("--")+1:] if "--" in sys.argv else [])
    if bpy.app.version != (4, 0, 2):
        raise ValueError("Study recipe tested with Blender 4.0.2 only; no automatic engine upgrade")
    if not re.fullmatch(r"c12-[a-z0-9][a-z0-9-]{0,55}", args.run_id):
        raise ValueError("Run ID must be c12- followed by safe lowercase characters")
    art_root = HERE.parents[2]
    row = next(row for row in load_registry(art_root) if row["id"] == "C12")
    if [float(row[key]) for key in ("largeur_m", "profondeur_m", "hauteur_m")] != [6, 10, 3.4]:
        raise ValueError("Canonical C12 dimensions changed")
    build = art_root / "build"
    if build.is_symlink():
        raise ValueError("Build directory cannot be a symlink")
    build.mkdir(exist_ok=True)
    out = build / args.run_id
    out.mkdir()  # Fail rather than overwrite any previous study or follow a symlink.
    started = time.monotonic()
    scene = build_scene()
    create_proxies()
    metrics = validate_scene()
    scene.render.engine = "CYCLES"
    scene.cycles.device = "CPU"
    scene.cycles.samples = args.samples
    scene.cycles.use_denoising = False  # Ubuntu 4.0.2 build has no OpenImageDenoiser.
    scene.cycles.seed = 12
    scene.cycles.max_bounces = 5
    scene.cycles.transparent_max_bounces = 6
    scene.render.threads_mode = "FIXED"
    scene.render.threads = 6
    scene.render.resolution_x, scene.render.resolution_y = 1600, 1000
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = "PNG"
    scene.render.image_settings.color_mode = "RGB"
    scene.render.image_settings.color_depth = "8"
    scene.render.film_transparent = False
    scene.view_settings.view_transform = "AgX"
    scene.camera = bpy.data.objects["Study-interior"]
    bpy.ops.wm.save_as_mainfile(filepath=str(out / "c12-classic-metric-study.blend"))
    bpy.ops.object.select_all(action="DESELECT")
    for obj in export_objects():
        obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(out / "c12-classic-metric-study.glb"), export_format="GLB",
                              use_selection=True, export_apply=True, export_yup=True,
                              export_cameras=False, export_lights=False, export_extras=False)
    glb = validate_glb(out / "c12-classic-metric-study.glb")
    authoring_seconds = time.monotonic() - started
    renders = []
    for view in (() if args.no_render else ("facade", "interior")):
        begin = time.monotonic()
        scene.camera = bpy.data.objects["Study-" + view]
        scene.render.filepath = str(out / ("c12-classic-" + view + "-offline-study.png"))
        bpy.ops.render.render(write_still=True)
        if geometry_fingerprint()[0] != metrics["geometryMaterialSha256"]:
            raise ValueError("Geometry/materials changed between export and renders")
        image = Path(scene.render.filepath)
        renders.append({"view": view, "path": image.name, "bytes": image.stat().st_size,
                        "sha256": hashlib.sha256(image.read_bytes()).hexdigest(),
                        "seconds": round(time.monotonic()-begin, 3), "geometryMaterialSha256": metrics["geometryMaterialSha256"],
                        "cameraBlenderXYZ": list(scene.camera.location), "lensMm": scene.camera.data.lens,
                        "cameraMatrixWorld": [list(row) for row in scene.camera.matrix_world],
                        "width": 1600, "height": 1000, "kind": "offline-metric-study-not-canonical"})
    import io_scene_gltf2
    sources = {p.name: hashlib.sha256(p.read_bytes()).hexdigest() for p in sorted(HERE.glob("*.py"))}
    commit = subprocess.run(["git", "rev-parse", "HEAD"], cwd=art_root, text=True, capture_output=True, check=True).stdout.strip()
    report = {"kind": "c12-original-metric-study", "status": "incomplete-unapproved", "publicationAllowed": False,
              "rightsReview": "pending-not-accepted", "humanArtReview": "pending", "runtimeNavigation": "not-tested",
              "authoringProvenance": "Original local Blender/Python geometry and constant PBR authored in this source; no external art assets/fonts/images/services",
              "sourceFilesSha256": sources, "checkoutBaseCommit": commit, "sourceMayBeUncommitted": True,
              "blender": bpy.app.version_string, "gltfExporterVersion": list(io_scene_gltf2.bl_info["version"]),
              "runtimeTargetUnchanged": "three/161", "seed": 12, "lightmaps": "not-authored; future TEXCOORD_1 -> uv1, channel 1",
              "studyTheme": "Classic", "canonicalRegistryReviewThemeUnchanged": row["theme_revue_v3"],
              "themeReason": "Explicit Classic-first metric study request; not a change to canonical Modern or final five-style coverage",
              "qualityProfiles": "single-unqualified-study-no-economy-standard-claim", "canonicalImagesDelivered": 0,
              "gate": "Four real pilots must receive written human acceptance before the remaining 32; eight-large requirement unchanged",
              "renderSettings": {"engine": "Cycles", "device": "CPU", "samples": args.samples, "denoise": False,
                                 "threads": 6, "maxBounces": 5, "lighting": "offline-area-lights-not-baked-or-exported"},
              "metrics": metrics, "glb": glb, "renders": renders,
              "buildAndExportSeconds": round(authoring_seconds, 3), "totalSeconds": round(time.monotonic()-started, 3),
              "blendBytes": (out / "c12-classic-metric-study.blend").stat().st_size}
    (out / "study-evidence.json").write_text(json.dumps(report, indent=2) + "\n")
    print("C12_STUDY_OUTPUT " + str(out), flush=True)
    print(json.dumps(report, indent=2), flush=True)


if __name__ == "__main__":
    main()
