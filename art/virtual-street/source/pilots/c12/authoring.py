"""Offline C12 authoring, imported only after the launcher freezes local sources/inputs."""
import hashlib
import json
from pathlib import Path
import time

import bpy
import io_scene_gltf2

from registry import load_registry
from scene import build_scene
from validation import create_proxies, export_study_glb, geometry_fingerprint, validate_scene


def run(args, out, frozen_root, snapshot):
    if bpy.app.version != (4, 0, 2):
        raise ValueError("Study recipe tested with Blender 4.0.2 only; no automatic engine upgrade")
    row = next(row for row in load_registry(frozen_root) if row["id"] == "C12")
    if [float(row[key]) for key in ("largeur_m", "profondeur_m", "hauteur_m")] != [6, 10, 3.4]:
        raise ValueError("Canonical C12 dimensions changed")
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
    glb = export_study_glb(out / "c12-classic-metric-study.glb")
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
    report = {"kind": "c12-original-metric-study", "status": "incomplete-unapproved", "publicationAllowed": False,
              "rightsReview": "pending-not-accepted", "humanArtReview": "pending", "runtimeNavigation": "not-tested",
              "authoringProvenance": "Original local Blender/Python geometry and constant PBR authored in this source; no external art assets/fonts/images/services",
              "blender": bpy.app.version_string, "gltfExporterVersion": list(io_scene_gltf2.bl_info["version"]),
              "runtimeTargetUnchanged": "three/161", "seed": 12, "lightmaps": "not-authored; future TEXCOORD_1 -> uv1, channel 1",
              "studyTheme": "Classic", "canonicalRegistryReviewThemeUnchanged": row["theme_revue_v3"],
              "themeReason": "Exploratory Classic among five allowed styles, noncanonical; canonical Modern review unchanged",
              "qualityProfiles": "single-unqualified-study-no-economy-standard-claim", "canonicalImagesDelivered": 0,
              "gate": "Four real pilots must receive written human acceptance before the remaining 32; eight-large requirement unchanged",
              "renderSettings": {"engine": "Cycles", "device": "CPU", "samples": args.samples, "denoise": False,
                                 "threads": 6, "maxBounces": 5, "lighting": "offline-area-lights-not-baked-or-exported"},
              "metrics": metrics, "glb": glb, "renders": renders,
              "buildAndExportSeconds": round(authoring_seconds, 3), "totalSeconds": round(time.monotonic()-started, 3),
              "blendBytes": (out / "c12-classic-metric-study.blend").stat().st_size}
    snapshot.finalize(out, report)
    print("C12_STUDY_OUTPUT " + str(out), flush=True)
    print(json.dumps(report, indent=2), flush=True)
