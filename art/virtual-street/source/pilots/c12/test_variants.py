"""Real Blender matrix: all ten C12 studies, corruption cases, export/reimport costs."""
import json
from pathlib import Path
import sys
import tempfile
import unittest

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from recipes import QUALITIES, STYLES
from scene import box, build_scene
from validation import StudyError, bounds, create_proxies, export_objects, export_study_glb, validate_scene


class VariantTests(unittest.TestCase):
    def test_invalid_variants_fail_before_scene_reset(self):
        for style, quality in ((True, "standard"), (1.0, "standard"), (-1, "economy"),
                               (5, "standard"), ("1", "standard"), (1, "high"), (1, None)):
            with self.subTest(style=style, quality=quality):
                marker = bpy.context.scene.name
                with self.assertRaisesRegex(ValueError, "variant"):
                    build_scene(style, quality)
                self.assertEqual(bpy.context.scene.name, marker)

    def test_all_ten_preserve_program_egress_and_reduce_actual_economy_cost(self):
        records = {}
        with tempfile.TemporaryDirectory(prefix="c12-variants-") as tmp:
            for style in range(5):
                for quality in QUALITIES:
                    with self.subTest(style=style, quality=quality):
                        scene = build_scene(style, quality)
                        create_proxies()
                        metrics = validate_scene()
                        self.assertEqual(metrics["clearSweptWidthMetres"], 1.2)
                        self.assertEqual(len(metrics["zones"]), 7)
                        # Four deterministic corruption checks on EVERY actual variant.
                        table = bpy.data.objects["Table-top"]
                        table["exportable"] = False
                        try:
                            with self.assertRaisesRegex(StudyError, "program"):
                                validate_scene()
                        finally:
                            table["exportable"] = True
                        mat = table.data.materials[0]
                        owner = mat["study_owner"]
                        mat["study_owner"] = "unknown"
                        try:
                            with self.assertRaisesRegex(StudyError, "ownership"):
                                validate_scene()
                        finally:
                            mat["study_owner"] = owner
                        scene["study_quality"] = "standard" if quality == "economy" else "economy"
                        try:
                            with self.assertRaisesRegex(StudyError, "variant"):
                                validate_scene()
                        finally:
                            scene["study_quality"] = quality
                        obstacle = box("Variant-exit-obstruction", (0, .5, .6), (1.5, .5, 1.2), mat, bevel=0)
                        proxy = box("Variant-exit-proxy", (0, .5, .6), (1.5, .5, 1.2), None, "CollisionGuides", bevel=0)
                        proxy["proxy_for"] = obstacle.name
                        proxy.hide_render = True
                        try:
                            with self.assertRaisesRegex(StudyError, "egress"):
                                validate_scene()
                        finally:
                            bpy.data.objects.remove(obstacle, do_unlink=True)
                            bpy.data.objects.remove(proxy, do_unlink=True)
                        before = {o.name: bounds(o) for o in export_objects()}
                        path = Path(tmp) / (str(style) + "-" + quality + ".glb")
                        glb = export_study_glb(path)
                        self.assertEqual(glb["triangles"], metrics["trianglesEvaluated"])
                        self.assertEqual(glb["meshes"], metrics["meshObjects"])
                        # A clean real GLB reimport, not a metadata-only bounds assertion.
                        bpy.ops.wm.read_factory_settings(use_empty=True)
                        bpy.ops.import_scene.gltf(filepath=str(path))
                        self.assertEqual(len(bpy.data.objects), len(before))
                        for obj in bpy.data.objects:
                            self.assertIn(obj.name, before)
                            for actual, expected in zip(bounds(obj), before[obj.name]):
                                for a, e in zip(actual, expected):
                                    self.assertAlmostEqual(a, e, places=4)
                        records[(style, quality)] = {"metrics": metrics, "glb": glb}
        self.assertEqual(len(records), 10)
        self.assertEqual(len({v["metrics"]["geometryMaterialSha256"] for v in records.values()}), 10)
        for style in range(5):
            economy, standard = records[(style, "economy")], records[(style, "standard")]
            for invariant in ("interiorLxPxHMetres", "modelBoundsBlenderXYZ", "programInventory", "zones", "meshObjects"):
                self.assertEqual(economy["metrics"][invariant], standard["metrics"][invariant])
            self.assertLess(economy["glb"]["triangles"], standard["glb"]["triangles"] * .8)
            self.assertLess(economy["glb"]["bytes"], standard["glb"]["bytes"] * .85)
        print("C12_VARIANT_COSTS " + json.dumps([
            {"style": style, "name": STYLES[style], "quality": quality,
             "triangles": record["glb"]["triangles"], "glbBytes": record["glb"]["bytes"],
             "meshes": record["glb"]["meshes"], "runtimeDrawCalls": "not-measured"}
            for (style, quality), record in records.items()]), flush=True)


if __name__ == "__main__":
    result = unittest.TextTestRunner(verbosity=2).run(unittest.defaultTestLoader.loadTestsFromTestCase(VariantTests))
    if not result.wasSuccessful():
        raise SystemExit(1)
