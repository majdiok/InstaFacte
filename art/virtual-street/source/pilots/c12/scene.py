"""Original C12 metric study, not an approved catalogue asset. Blender 4.0.2."""
import math

import bpy
from mathutils import Vector

OWNER = "c12-original-metric-study"
COLLECTIONS = ("Architecture", "Props", "BrandingAnchors", "CollisionGuides", "CameraGuides", "BakeSources")
ZONES = {
    "window-left": (-2.95, 0.2, -0.95, 1.65),
    "window-right": (0.95, 0.2, 2.95, 1.65),
    "display-table": (-0.65, 3.2, 0.65, 5.6),
    "rack-left": (-2.95, 2.8, -2.12, 6.3),
    "rack-right": (2.12, 2.8, 2.95, 6.3),
    "fitting": (-2.95, 7.7, -0.65, 9.95),
    "checkout-decorative": (1.3, 8.0, 2.9, 8.7),
}
# Axis-aligned walking routes: 1.2 m-wide swept rectangles, not navigation code.
ROUTES = {
    "entry": [(0, -0.6), (0, 2.5)],
    "table-loop": [(0, 2.5), (1.4, 2.5), (1.4, 6.6), (-1.4, 6.6), (-1.4, 2.5), (0, 2.5)],
    "fitting": [(-1.4, 6.6), (-1.5, 6.6), (-1.5, 8.8)],
    "checkout": [(1.4, 6.6), (0, 6.6), (0, 8.9)],
}


def assign(obj, name, material, collection="Props", zone=None):
    obj.name = name
    for old in list(obj.users_collection):
        old.objects.unlink(obj)
    bpy.data.collections[collection].objects.link(obj)
    obj["study_owner"] = OWNER
    obj["exportable"] = collection in ("Architecture", "Props")
    if zone:
        obj["zone"] = zone
    if material:
        obj.data.materials.append(material)
    return obj


def box(name, loc, size, mat, collection="Props", zone=None, bevel=0.012):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    obj = assign(bpy.context.object, name, mat, collection, zone)
    obj.dimensions = size
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if bevel:
        modifier = obj.modifiers.new("Small original edge bevel", "BEVEL")
        modifier.width = min(bevel, min(size) / 4)
        modifier.segments = 2
    return obj


def rod(name, a, b, radius, mat, zone=None):
    direction = Vector(b) - Vector(a)
    bpy.ops.mesh.primitive_cylinder_add(vertices=12, radius=radius, depth=direction.length,
                                      location=(Vector(a) + Vector(b)) / 2)
    obj = assign(bpy.context.object, name, mat, zone=zone)
    obj.rotation_euler = direction.to_track_quat("Z", "Y").to_euler()
    return obj


def material(name, color, roughness=0.65, metallic=0, alpha=1):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    mat["study_owner"] = OWNER
    mat["provenance"] = "original-constant-pbr-no-textures"
    mat.diffuse_color = (*color, alpha)
    node = mat.node_tree.nodes.get("Principled BSDF")
    node.inputs["Base Color"].default_value = (*color, alpha)
    node.inputs["Roughness"].default_value = roughness
    node.inputs["Metallic"].default_value = metallic
    node.inputs["Alpha"].default_value = alpha
    if alpha < 1:
        mat.blend_method = "BLEND"
        mat.use_screen_refraction = False
    return mat


def camera(name, location, target, lens):
    data = bpy.data.cameras.new(name)
    obj = bpy.data.objects.new(name, data)
    bpy.data.collections["CameraGuides"].objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()
    data.lens = lens
    data.clip_start, data.clip_end = 0.05, 100
    return obj


def area(name, location, target, power, size):
    data = bpy.data.lights.new(name, "AREA")
    data.energy, data.shape, data.size = power, "DISK", size
    data.color = (1.0, 0.9, 0.77)
    obj = bpy.data.objects.new(name, data)
    bpy.data.collections["BakeSources"].objects.link(obj)
    obj.location = location
    obj.rotation_euler = (Vector(target) - obj.location).to_track_quat("-Z", "Y").to_euler()


def garment(name, x, y, mat, metal, zone):
    # Original extruded, deliberately simple clothing silhouette; no logo/texture.
    outline = [(-.22, 1.52), (-.36, 1.4), (-.30, 1.19), (-.20, 1.25),
               (-.19, .88), (.19, .88), (.20, 1.25), (.30, 1.19), (.36, 1.4), (.22, 1.52)]
    vertices = [(x + dx, y + dy, z) for dy in (-.028, .028) for dx, z in outline]
    n = len(outline)
    faces = [tuple(reversed(range(n))), tuple(range(n, 2*n))]
    faces += [(i, (i+1) % n, (i+1) % n+n, i+n) for i in range(n)]
    mesh = bpy.data.meshes.new(name)
    mesh.from_pydata(vertices, [], faces)
    mesh.update()
    obj = bpy.data.objects.new(name, mesh)
    bpy.context.collection.objects.link(obj)
    assign(obj, name, mat, zone=zone)
    for suffix, a, b in (("left", (x-.2, y, 1.52), (x, y, 1.64)),
                         ("right", (x, y, 1.64), (x+.2, y, 1.52)),
                         ("base", (x-.2, y, 1.52), (x+.2, y, 1.52)),
                         ("hook", (x, y, 1.64), (x, y, 1.76))):
        rod(name + "-hanger-" + suffix, a, b, .007, metal, zone)


def build_scene():
    bpy.ops.wm.read_factory_settings(use_empty=True)
    scene = bpy.context.scene
    scene.unit_settings.system = "METRIC"
    scene.unit_settings.scale_length = 1
    scene.unit_settings.length_unit = "METERS"
    scene["study_status"] = "metric-blockout-incomplete-unapproved"
    scene["publication_allowed"] = False
    for name in COLLECTIONS:
        collection = bpy.data.collections.new(name)
        scene.collection.children.link(collection)
    plaster = material("C12-Plaster-Temporary", (.68, .62, .51))
    floor = material("C12-Stone-Temporary", (.48, .43, .35), .78)
    wood = material("C12-WarmWood-Temporary", (.26, .12, .055), .5)
    metal = material("C12-Bronze-Temporary", (.20, .12, .06), .3, .65)
    fabric = [material("C12-Cloth-" + str(i), color) for i, color in enumerate(
        ((.14, .21, .22), (.48, .26, .12), (.64, .58, .46), (.21, .24, .30)))]
    glass = material("C12-Glass-Alpha-Study", (.74, .83, .84), .18, .15, .12)
    mirror = material("C12-Mirror-Simulated", (.33, .42, .43), .24, .75)
    ivory = material("C12-AbstractForm-Temporary", (.65, .59, .47), .55)
    architecture = "Architecture"
    box("Floor", (0, 5, -.10), (6.4, 10.4, .2), floor, architecture)
    box("Wall-left", (-3.1, 5, 1.7), (.2, 10.4, 3.4), plaster, architecture)
    box("Wall-right", (3.1, 5, 1.7), (.2, 10.4, 3.4), plaster, architecture)
    box("Wall-back", (0, 10.1, 1.7), (6.4, .2, 3.4), plaster, architecture)
    box("Ceiling", (0, 5, 3.5), (6.4, 10.4, .2), plaster, architecture)
    box("Facade-header", (0, -.1, 3.025), (6.4, .22, .75), wood, architecture)
    box("Facade-cornice", (0, -.18, 3.35), (6.5, .34, .12), plaster, architecture)
    # Blank sign only. No third-party/bundled font, invented brand or tenant identity.
    box("Sign-blank", (0, -.23, 3.03), (2.25, .04, .37), plaster, architecture)
    for x in (-3, -.85, .85, 3):
        box("Facade-mullion", (x, -.08, 1.325), (.1, .16, 2.65), metal, architecture)
    for x in (-1.925, 1.925):
        box("Window-pane", (x, -.07, 1.34), (2.05, .035, 2.57), glass, architecture)
        box("Window-sill", (x, -.09, .06), (2.05, .22, .12), wood, architecture)
    # The door is physically open, folded against the right display, not invisible.
    box("Door-open-leaf", (.91, .72, 1.3), (.065, 1.4, 2.6), glass, architecture)
    for y in (.02, 1.42):
        box("Door-open-frame", (.91, y, 1.3), (.075, .06, 2.6), metal, architecture)
    for z in (.04, 2.57):
        box("Door-open-rail", (.91, .72, z), (.075, 1.45, .065), metal, architecture)
    rod("Door-handle", (.85, 1.2, .95), (.85, 1.2, 1.25), .014, metal)
    for side in (-1, 1):
        zone = "window-left" if side < 0 else "window-right"
        x = side * 1.95
        box("Window-display-plinth", (x, .95, .1), (1.55, .95, .2), wood, zone=zone)
        # Abstract display dress form: a faceted truncated cone on a stand, no head/limbs.
        rod("Abstract-form-stand", (x, .95, .2), (x, .95, 1.1), .025, metal, zone)
        bpy.ops.mesh.primitive_cone_add(vertices=8, radius1=.28, radius2=.20, depth=.7,
                                       location=(x, .95, 1.3))
        obj = assign(bpy.context.object, "Abstract-headless-display-form", ivory, zone=zone)
        obj.scale.y = .6
        bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
        rack_zone = "rack-left" if side < 0 else "rack-right"
        rx = side * 2.56
        for y in (2.95, 6.15):
            rod("Rack-upright", (rx, y, .05), (rx, y, 1.8), .022, metal, rack_zone)
            box("Rack-foot", (rx, y, .04), (.75, .18, .08), wood, zone=rack_zone)
        rod("Rack-rail", (rx, 2.95, 1.78), (rx, 6.15, 1.78), .022, metal, rack_zone)
        for i in range(9):
            garment("Garment-" + str(side) + "-" + str(i), rx, 3.15+i*.33,
                    fabric[i % len(fabric)], metal, rack_zone)
        box("Wall-base-trim", (side*2.985, 5, .12), (.03, 9.8, .24), wood, architecture)
    box("Table-top", (0, 4.4, .65), (1.3, 2.4, .10), wood, zone="display-table")
    for x in (-.45, .45):
        for y in (3.45, 5.35):
            box("Table-leg", (x, y, .30), (.08, .08, .6), metal, zone="display-table")
    for y in (3.65, 4.35, 5.05):
        for i in range(3):
            box("Folded-cloth", (0, y, .73+i*.055), (.65, .43, .05), fabric[i], zone="display-table")
    box("Fitting-partition", (-.65, 8.85, 1.35), (.10, 2.3, 2.7), plaster, zone="fitting")
    box("Fitting-header", (-1.8, 7.75, 2.6), (2.3, .10, .2), wood, zone="fitting")
    rod("Curtain-rail", (-2.95, 7.76, 2.5), (-.72, 7.76, 2.5), .025, metal, "fitting")
    for i in range(7):
        box("Gathered-curtain-fold", (-2.88+i*.075, 7.76 + (i % 2)*.065, 1.3),
            (.09, .09, 2.35), fabric[0], zone="fitting", bevel=.035)
    box("Mirror-frame", (-1.8, 9.93, 1.35), (1.0, .09, 2.05), wood, zone="fitting")
    box("Mirror-simulated-face", (-1.8, 9.87, 1.35), (.88, .025, 1.93), mirror, zone="fitting")
    box("Checkout-decorative-counter", (2.1, 8.35, .50), (1.6, .7, 1), wood, zone="checkout-decorative")
    box("Checkout-slab", (2.1, 8.35, 1.035), (1.6, .7, .07), plaster, zone="checkout-decorative")
    box("Back-display-shelf", (2.1, 9.7, 1.6), (1.5, .35, .06), wood)
    for i in range(4):
        box("Back-folded-cloth", (1.65+i*.3, 9.7, 1.68), (.24, .26, .10), fabric[i])
    for y in (2.5, 5.5, 8.5):
        box("Ceiling-light-housing", (0, y, 3.34), (1.6, .15, .08), metal, architecture)
        area("Offline-ceiling-light", (0, y, 3.22), (0, y, 0), 220, 2.0)
    area("Offline-daylight", (0, -3, 5), (0, 4, 1), 1100, 7)
    area("Offline-facade-fill", (-5, -6, 4), (0, 0, 1), 700, 5)
    world = bpy.data.worlds.new("Original constant studio world")
    world.use_nodes = True
    world.node_tree.nodes["Background"].inputs[0].default_value = (.55, .61, .68, 1)
    world.node_tree.nodes["Background"].inputs[1].default_value = .35
    scene.world = world
    camera("Study-facade", (-6, -10, 1.65), (0, 1.2, 1.65), 40)
    camera("Study-interior", (0, 1.95, 1.65), (0, 7, 1.45), 19)
    for name, xy in (("Spawn", (0, 1.95)), ("Exit", (0, -.6))):
        obj = bpy.data.objects.new(name, None)
        bpy.data.collections["CameraGuides"].objects.link(obj)
        obj.location = (*xy, 1.65)
    # Explicit closed-space guide, never exported or rendered.
    guide = box("Interior-metric-envelope", (0, 5, 1.7), (6, 10, 3.4), None,
                "CollisionGuides", bevel=0)
    guide.hide_render = True
    guide.display_type = "WIRE"
    bpy.context.view_layer.update()
    return scene
