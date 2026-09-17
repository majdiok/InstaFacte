"""Run with Blender --background --python-exit-code 1 --python this_file.py."""
from pathlib import Path
import sys
import tempfile
import unittest

import bpy

sys.path.insert(0, str(Path(__file__).resolve().parent))
from scene import build_scene
from validation import StudyError, bounds, create_proxies, export_objects, export_study_glb, validate_glb, validate_scene


class SceneTests(unittest.TestCase):
    @classmethod
    def setUpClass(cls):
        build_scene()
        create_proxies()

    def test_01_actual_dimensions_path_and_owned_materials(self):
        metrics = validate_scene()
        self.assertEqual(len(metrics["zones"]), 7)
        self.assertGreater(metrics["obstacleProxies"], 100)
        self.assertGreater(metrics["reachableGridCells"], 1000)
        for actual, expected in zip(metrics["interiorLxPxHMetres"], (6, 10, 3.4)):
            self.assertAlmostEqual(actual, expected, places=4)

    def test_02_reject_centimetre_units(self):
        bpy.context.scene.unit_settings.scale_length = .01
        try:
            with self.assertRaisesRegex(StudyError, "units"):
                validate_scene()
        finally:
            bpy.context.scene.unit_settings.scale_length = 1

    def test_03_reject_real_wall_displacement(self):
        wall = bpy.data.objects["Wall-right"]
        wall.location.x += .2
        try:
            with self.assertRaisesRegex(StudyError, "bounds"):
                validate_scene()
        finally:
            wall.location.x -= .2

    def test_04_reject_foreign_material(self):
        material = bpy.data.objects["Table-top"].data.materials[0]
        owner = material["study_owner"]
        material["study_owner"] = "unknown"
        try:
            with self.assertRaisesRegex(StudyError, "ownership"):
                validate_scene()
        finally:
            material["study_owner"] = owner

    def test_05_reject_missing_zone(self):
        objects = [o for o in export_objects() if o.get("zone") == "window-left"]
        for obj in objects:
            del obj["zone"]
        try:
            with self.assertRaisesRegex(StudyError, "zones: missing"):
                validate_scene()
        finally:
            for obj in objects:
                obj["zone"] = "window-left"

    def test_06_reject_stale_obstacle_proxy(self):
        proxy = bpy.data.objects["Collision-Table-top"]
        proxy.location.x += .2
        try:
            with self.assertRaisesRegex(StudyError, "proxies"):
                validate_scene()
        finally:
            proxy.location.x -= .2

    def test_07_reject_physically_blocked_exit_even_with_matching_proxy(self):
        # A real new box, correctly owned and correctly proxied, must fail egress.
        from scene import box
        obj = box("Test-exit-obstruction", (0, .5, .6), (1.5, .5, 1.2),
                  bpy.data.objects["Table-top"].data.materials[0], bevel=0)
        proxy = box("Test-exit-proxy", (0, .5, .6), (1.5, .5, 1.2), None, "CollisionGuides", bevel=0)
        proxy["proxy_for"] = obj.name
        proxy.hide_render = True
        try:
            with self.assertRaisesRegex(StudyError, "egress"):
                validate_scene()
        finally:
            bpy.data.objects.remove(obj, do_unlink=True)
            bpy.data.objects.remove(proxy, do_unlink=True)

    def test_08_glb_export_and_reimport_match_real_bounds(self):
        with tempfile.TemporaryDirectory(prefix="c12-test-") as tmp:
            path = Path(tmp) / "study.glb"
            before = {o.name: bounds(o) for o in export_objects()}
            result = export_study_glb(path)
            self.assertGreater(result["bytes"], 10000)
            # Reimport into a clean scene, so names are not collision-renamed.
            # Blender maps glTF +Y-up back to source Z-up; compare evaluated bounds.
            bpy.ops.wm.read_factory_settings(use_empty=True)
            bpy.ops.import_scene.gltf(filepath=str(path))
            imported = set(bpy.data.objects)
            try:
                self.assertEqual(len(imported), len(before))
                for obj in imported:
                    source_name = obj.name
                    self.assertIn(source_name, before)
                    for actual, expected in zip(bounds(obj), before[source_name]):
                        for a, e in zip(actual, expected):
                            self.assertAlmostEqual(a, e, places=4)
            finally:
                for obj in imported:
                    bpy.data.objects.remove(obj, do_unlink=True)
            path.write_bytes(b"")
            with self.assertRaisesRegex(StudyError, "export"):
                validate_glb(path)


if __name__ == "__main__":
    suite = unittest.defaultTestLoader.loadTestsFromTestCase(SceneTests)
    result = unittest.TextTestRunner(verbosity=2).run(suite)
    if not result.wasSuccessful():
        raise SystemExit(1)
