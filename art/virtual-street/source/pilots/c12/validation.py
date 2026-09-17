"""Geometric study checks on evaluated Blender meshes, not source-code matching."""
import hashlib
import json
import math
import struct

import bpy
from mathutils import Vector

from scene import OWNER, ROUTES, ZONES, box


class StudyError(ValueError):
    pass


def require(condition, message):
    if not condition:
        raise StudyError(message)


def export_objects():
    return sorted((o for o in bpy.context.scene.objects if o.get("exportable")), key=lambda o: o.name)


def export_study_glb(path):
    """Use exactly the same uncompressed export in the build and reimport test."""
    bpy.ops.object.select_all(action="DESELECT")
    for obj in export_objects():
        obj.select_set(True)
    bpy.ops.export_scene.gltf(filepath=str(path), export_format="GLB",
                              use_selection=True, export_apply=True, export_yup=True,
                              export_cameras=False, export_lights=False, export_extras=False)
    return validate_glb(path)


def bounds(obj):
    evaluated = obj.evaluated_get(bpy.context.evaluated_depsgraph_get())
    corners = [evaluated.matrix_world @ Vector(v) for v in evaluated.bound_box]
    return [[min(v[i] for v in corners) for i in range(3)],
            [max(v[i] for v in corners) for i in range(3)]]


def scene_bounds(objects):
    boxes = [bounds(o) for o in objects]
    return [[min(b[0][i] for b in boxes) for i in range(3)],
            [max(b[1][i] for b in boxes) for i in range(3)]]


def overlap(a, b):
    return all(a[0][i] < b[1][i] - 1e-5 and a[1][i] > b[0][i] + 1e-5 for i in range(3))


def obstacle_objects():
    # Anything intersecting the pedestrian body, including low tables/stands.
    return [o for o in export_objects() if bounds(o)[0][2] < 1.95 and bounds(o)[1][2] > .025]


def create_proxies():
    for obj in obstacle_objects():
        low, high = bounds(obj)
        proxy = box("Collision-" + obj.name, [(a+b)/2 for a, b in zip(low, high)],
                    [b-a for a, b in zip(low, high)], None, "CollisionGuides", bevel=0)
        proxy["proxy_for"] = obj.name
        proxy.hide_render = True
        proxy.display_type = "WIRE"
    bpy.context.view_layer.update()


def geometry_fingerprint():
    digest = hashlib.sha256()
    triangles = 0
    depsgraph = bpy.context.evaluated_depsgraph_get()
    for obj in export_objects():
        evaluated = obj.evaluated_get(depsgraph)
        mesh = evaluated.to_mesh()
        mesh.calc_loop_triangles()
        triangles += len(mesh.loop_triangles)
        digest.update(obj.name.encode())
        for vertex in mesh.vertices:
            digest.update(struct.pack("<3f", *(evaluated.matrix_world @ vertex.co)))
        for face in mesh.loop_triangles:
            digest.update(struct.pack("<3I", *face.vertices))
        for mat in mesh.materials:
            digest.update(mat.name.encode())
            pbr = mat.node_tree.nodes.get("Principled BSDF")
            for name in ("Base Color", "Roughness", "Metallic", "Alpha"):
                value = pbr.inputs[name].default_value
                digest.update(str(list(value) if hasattr(value, "__len__") else value).encode())
        evaluated.to_mesh_clear()
    return digest.hexdigest(), triangles


def validate_scene():
    scene = bpy.context.scene
    bpy.context.view_layer.update()
    require(scene.unit_settings.system == "METRIC" and scene.unit_settings.scale_length == 1,
            "units: expected one Blender unit per metre")
    require(scene.get("publication_allowed") is False and
            scene.get("study_status") == "metric-blockout-incomplete-unapproved", "status: study only")
    objects = export_objects()
    require(len(objects) > 30 and all(o.type == "MESH" for o in objects), "geometry: nonempty mesh model required")
    b = {name: bounds(bpy.data.objects[name]) for name in ("Wall-left", "Wall-right", "Wall-back", "Floor", "Ceiling")}
    interior = [b["Wall-right"][0][0] - b["Wall-left"][1][0], b["Wall-back"][0][1],
                b["Ceiling"][0][2] - b["Floor"][1][2]]
    require(all(abs(a-e) < 1e-4 for a, e in zip(interior, (6, 10, 3.4))), "bounds: interior must stay 6x10x3.4m")
    envelope = bounds(bpy.data.objects["Interior-metric-envelope"])
    require(all(abs(v-e) < 1e-4 for side, expected in zip(envelope, ((-3, 0, 0), (3, 10, 3.4)))
                for v, e in zip(side, expected)), "bounds: guide differs from actual room")
    for obj in objects:
        require(obj.get("study_owner") == OWNER, "ownership: foreign object")
        low, high = bounds(obj)
        require(all(math.isfinite(v) for v in low+high) and all(a < b for a, b in zip(low, high)), "geometry: degenerate bounds")
        require(obj.data.materials and all(m and m.get("study_owner") == OWNER for m in obj.data.materials), "ownership: foreign/missing material")
        for mat in obj.data.materials:
            require({n.type for n in mat.node_tree.nodes} == {"BSDF_PRINCIPLED", "OUTPUT_MATERIAL"}, "materials: constant original PBR only")
    require(not bpy.data.images and not bpy.data.libraries, "dependencies: external data forbidden")
    for zone, (x0, y0, x1, y1) in ZONES.items():
        members = [o for o in objects if o.get("zone") == zone]
        require(members, "zones: missing " + zone)
        for obj in members:
            low, high = bounds(obj)
            require(low[0] >= x0-.061 and low[1] >= y0-.061 and high[0] <= x1+.061 and high[1] <= y1+.061,
                    "zones: object outside " + zone + ": " + obj.name)
    obstacles = obstacle_objects()
    proxies = [o for o in scene.objects if o.get("proxy_for")]
    require(len(proxies) == len(obstacles), "proxies: missing or extra collider")
    for obj in obstacles:
        matches = [p for p in proxies if p["proxy_for"] == obj.name]
        require(len(matches) == 1, "proxies: expected exactly one collider per mesh")
        require(max(abs(a-b) for aa, bb in zip(bounds(obj), bounds(matches[0])) for a, b in zip(aa, bb)) < 1e-4,
                "proxies: evaluated geometry differs")
        require(not matches[0].get("exportable") and matches[0].hide_render, "proxies: guides leaked")
    # Check entire 1.2m-wide rectangular sweeps, including turns. Conservative AABBs.
    segments = 0
    for name, points in ROUTES.items():
        for a, b in zip(points, points[1:]):
            require(a[0] == b[0] or a[1] == b[1], "egress: only rectilinear segments supported")
            sweep = [[min(a[0], b[0])-.6, min(a[1], b[1])-.6, .025],
                     [max(a[0], b[0])+.6, max(a[1], b[1])+.6, 1.95]]
            for obstacle in obstacles:
                require(not overlap(sweep, bounds(obstacle)), "egress: " + name + " blocked by " + obstacle.name)
            segments += 1
    # Independent connectivity check on a 10cm grid inflated by capsule radius .30m.
    boxes = [bounds(o) for o in obstacles]
    def free(x, y):
        return not any(low[0]-.3 < x < high[0]+.3 and low[1]-.3 < y < high[1]+.3 for low, high in boxes)
    start = (0, -6)
    seen, queue = {start}, [start]
    for x, y in queue:
        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            point = (x+dx, y+dy)
            if point not in seen and -26 <= point[0] <= 26 and -6 <= point[1] <= 96 and free(point[0]/10, point[1]/10):
                seen.add(point)
                queue.append(point)
    for target in ((0, 20), (-16, 88), (0, 89), (-14, 44), (14, 44)):
        require(target in seen, "egress: disconnected discovery/spawn point")
    fingerprint, triangles = geometry_fingerprint()
    require(triangles > 100, "geometry: empty triangle export")
    return {"interiorLxPxHMetres": interior, "modelBoundsBlenderXYZ": scene_bounds(objects),
            "meshObjects": len(objects), "trianglesEvaluated": triangles,
            "materials": len({m.name for o in objects for m in o.data.materials}),
            "obstacleProxies": len(proxies), "zones": list(ZONES), "testedRouteSegments": segments,
            "clearSweptWidthMetres": 1.2, "capsuleRadiusMetres": .3, "gridStepMetres": .1,
            "reachableGridCells": len(seen), "geometryMaterialSha256": fingerprint,
            "qualification": "offline-geometric-study-only-not-browser-navigation"}


def validate_glb(path):
    data = path.read_bytes()
    require(28 < len(data) < 20_000_000, "export: empty or excessive GLB")
    magic, version, length = struct.unpack_from("<4sII", data)
    require(magic == b"glTF" and version == 2 and length == len(data), "export: invalid GLB header")
    size, kind = struct.unpack_from("<II", data, 12)
    require(kind == 0x4E4F534A and 20+size < len(data), "export: missing JSON/binary chunks")
    document = json.loads(data[20:20+size])
    def closed(value):
        if isinstance(value, dict):
            require("uri" not in value, "export: free URI forbidden")
            for item in value.values():
                closed(item)
        elif isinstance(value, list):
            for item in value:
                closed(item)
    closed(document)
    require(document.get("meshes") and document.get("accessors") and document.get("buffers"), "export: nonempty mesh required")
    require(not document.get("extensionsRequired"), "export: no required extension for this study")
    require(not any(n.get("name", "").startswith(("Collision-", "Study-", "Offline-", "Interior-metric-")) for n in document.get("nodes", [])), "export: guide leaked")
    return {"bytes": len(data), "sha256": hashlib.sha256(data).hexdigest(),
            "meshes": len(document["meshes"]), "extensionsUsed": document.get("extensionsUsed", [])}
