"""B-PROC site logistics props at real-world size (PropBible; refs in Tools/tripo/refs/).

blender --background --factory-startup --python Tools/blender/site_props.py -- [--render]
Writes Assets/_Game/Art/Models/B-PROC/SM_OfficeTrailer.fbx, SM_RollOffDumpster30.fbx, SM_PortableToilet.fbx
and Captures/Props/site_props/*.png. Typical product sizes are UNVERIFIED (SME check) - see prop_specs.json.
Blender units = metres, Z up; FBX exported Y-up for Unity.
"""
import json, math, sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
FT, IN = 0.3048, 0.0254
RENDER = "--render" in sys.argv


def mat(name, rgb, metal=0.0, rough=0.6):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.use_nodes = True
    bsdf = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    bsdf.inputs["Base Color"].default_value = (*rgb, 1)
    bsdf.inputs["Metallic"].default_value = metal
    bsdf.inputs["Roughness"].default_value = rough
    return m


def box(col, name, center, size, m, bevel=0.0):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    o = bpy.context.object; o.name = name; o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        b = o.modifiers.new("b", "BEVEL"); b.width = bevel; b.segments = 1
        bpy.ops.object.modifier_apply(modifier="b")
    o.data.materials.append(m)
    for c in list(o.users_collection): c.objects.unlink(o)
    col.objects.link(o)
    return o


def cyl(col, name, center, r, depth, m, rot=(0, 0, 0), verts=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=verts, radius=r, depth=depth, location=center, rotation=rot)
    o = bpy.context.object; o.name = name
    o.data.materials.append(m)
    for c in list(o.users_collection): c.objects.unlink(o)
    col.objects.link(o)
    return o


def new_col(name):
    col = bpy.data.collections.new(name); bpy.context.scene.collection.children.link(col); return col


# ---------------- 10 x 40 ft mobile office trailer ----------------
def office_trailer():
    col = new_col("SM_OfficeTrailer")
    L, W = 40 * FT, 10 * FT
    floor = 0.75                      # floor height on jacks (authored)
    H = 2.55                          # box height (authored; ~8.4 ft)
    white = mat("M_TrailerSiding", (0.86, 0.86, 0.83), 0.2, 0.5)
    rib = mat("M_TrailerRib", (0.78, 0.78, 0.75), 0.2, 0.5)
    skirt = mat("M_VinylSkirt", (0.05, 0.05, 0.05), 0, 0.8)
    glass = mat("M_WindowGlass", (0.1, 0.13, 0.15), 0.5, 0.1)
    galv = mat("M_Galvanized", (0.6, 0.62, 0.63), 0.9, 0.4)
    steel = mat("M_BlackSteel", (0.06, 0.06, 0.06), 0.6, 0.6)
    box(col, "Body", (0, 0, floor + H / 2), (L, W, H), white, 0.02)
    for x in [i * 0.6 - L / 2 + 0.3 for i in range(int(L / 0.6))]:  # ribbed siding both long sides
        for y in (W / 2 + 0.006, -W / 2 - 0.006):
            box(col, "Rib", (x, y, floor + H / 2), (0.03, 0.012, H - 0.1), rib)
    box(col, "Skirting", (0, 0, floor / 2 + 0.05), (L - 0.3, W - 0.1, floor - 0.1), skirt)
    for i, x in enumerate((-4.5, -2.6, 1.8, 3.9)):                   # sliding windows (front)
        box(col, "Window", (x, -W / 2 - 0.02, floor + 1.55), (1.1, 0.03, 0.9), glass)
        box(col, "WindowFrame", (x, -W / 2 - 0.015, floor + 1.55), (1.2, 0.02, 1.0), galv)
    box(col, "Door", (-0.4, -W / 2 - 0.02, floor + 1.02), (0.92, 0.04, 2.03), white)
    box(col, "DoorKnob", (-0.05, -W / 2 - 0.06, floor + 1.0), (0.05, 0.05, 0.05), galv)
    box(col, "WallAC", (5.3, -W / 2 - 0.3, floor + 1.7), (0.7, 0.55, 0.45), rib, 0.01)
    # Galvanized stair + landing with handrails (door at x=-0.4)
    box(col, "Landing", (-0.4, -W / 2 - 0.65, floor - 0.04), (1.5, 1.2, 0.06), galv)
    steps = 4
    for s in range(steps):
        z = floor - 0.04 - (s + 1) * floor / (steps + 1)
        box(col, "Tread", (-0.4, -W / 2 - 1.25 - s * 0.28, z), (1.0, 0.28, 0.04), galv)
    for x in (-0.4 - 0.72, -0.4 + 0.72):
        box(col, "LandingPost", (x, -W / 2 - 1.2, floor + 0.45), (0.04, 0.04, 0.95), galv)
        box(col, "Handrail", (x, -W / 2 - 0.65, floor + 0.93), (0.04, 1.2, 0.04), galv)
    for x in (-L / 2 + 0.6, -L / 2 + 3, L / 2 - 3, L / 2 - 0.6):   # stabilizer jacks
        for y in (-W / 2 + 0.3, W / 2 - 0.3):
            box(col, "Jack", (x, y, floor / 2), (0.08, 0.08, floor), steel)
    box(col, "HitchA", (L / 2 + 0.9, 0, 0.45), (1.8, 0.1, 0.1), steel)
    for a in (0.35, -0.35):
        o = box(col, "HitchArm", (L / 2 + 0.55, a / 2, 0.45), (1.3, 0.08, 0.08), steel)
        o.rotation_euler = (0, 0, -a * 0.6)
    return col


# ---------------- 30-yard roll-off dumpster ----------------
def rolloff():
    col = new_col("SM_RollOffDumpster30")
    L, W, H = 22 * FT, 7.5 * FT, 6 * FT
    green = mat("M_DumpsterGreen", (0.07, 0.2, 0.12), 0.4, 0.65)
    rust = mat("M_Rust", (0.2, 0.09, 0.04), 0.2, 0.9)
    t = 0.05
    base = 0.2                                   # rails + rollers clearance
    box(col, "Floor", (0, 0, base + t / 2), (L, W, t), green)
    for y in (W / 2 - t / 2, -W / 2 + t / 2):
        box(col, "SideWall", (0.4, y, base + H / 2), (L - 0.8, t, H), green)
    box(col, "RearDoor", (L / 2 - t / 2, 0, base + H / 2), (t, W, H), green)
    box(col, "FrontWall", (-L / 2 + 0.4 + t / 2, 0, base + H / 2), (t, W, H), green)
    o = box(col, "NoseSkid", (-L / 2 + 0.2, 0, base + 0.35), (0.5, W - 0.3, t), green)
    o.rotation_euler = (0, math.radians(35), 0)               # sloped nose where the hook pulls the box
    for x in [(-L / 2 + 1.2) + i * 0.75 for i in range(8)]:  # vertical ribs
        for y in (W / 2 + 0.04, -W / 2 - 0.04):
            box(col, "Rib", (x, y, base + H / 2), (0.1, 0.08, H), green)
    for y in (W / 2 + 0.02, -W / 2 - 0.02):
        box(col, "TopRail", (0.3, y, base + H - 0.06), (L - 0.6, 0.12, 0.12), green)
    for y in (0.55, -0.55):
        box(col, "Rail", (0, y, base / 2), (L, 0.12, base), green)
    for x in (L / 2 - 0.3, -L / 2 + 0.6):
        cyl(col, "Roller", (x, 0, 0.1), 0.1, W - 0.4, green, rot=(math.pi / 2, 0, 0))
    box(col, "HookBar", (-L / 2 - 0.25, 0, base + 0.9), (0.1, 0.9, 0.1), green)
    for i, (x, z) in enumerate(((-2, 0.9), (0.5, 1.4), (2.3, 0.6))):      # rust patches (wear)
        box(col, "RustPatch", (x, -W / 2 - 0.085, base + z), (0.22 + 0.08 * i, 0.005, 0.14), rust)
    return col


# ---------------- standard portable toilet ----------------
def toilet():
    col = new_col("SM_PortableToilet")
    W, D, H = 43 * IN, 47 * IN, 89 * IN
    blue = mat("M_ToiletBlue", (0.03, 0.18, 0.6), 0.0, 0.45)
    roof = mat("M_ToiletRoof", (0.9, 0.9, 0.88), 0.0, 0.3)
    dark = mat("M_ToiletBase", (0.08, 0.08, 0.08), 0.0, 0.8)
    box(col, "Base", (0, 0, 0.06), (W, D, 0.12), dark, 0.01)
    box(col, "Shell", (0, 0, 0.12 + (H - 0.3) / 2), (W - 0.04, D - 0.04, H - 0.3), blue, 0.03)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=(0, 0, H - 0.2))
    dome = bpy.context.object; dome.name = "RoofDome"; dome.scale = (W / 2, D / 2, 0.2)
    bpy.ops.object.transform_apply(scale=True); dome.data.materials.append(roof)
    for c in list(dome.users_collection): c.objects.unlink(dome)
    col.objects.link(dome)
    cyl(col, "VentPipe", (-W / 2 + 0.15, D / 2 - 0.15, H + 0.05), 0.05, 0.35, dark)
    box(col, "Door", (0, -D / 2 + 0.005, 0.12 + 0.98), (0.68, 0.03, 1.92), blue, 0.02)
    box(col, "Latch", (0.26, -D / 2 - 0.02, 1.05), (0.1, 0.03, 0.06), dark)
    for z in (0.35, 1.05, 1.75):
        box(col, "Hinge", (-0.34, -D / 2 - 0.01, z), (0.04, 0.02, 0.1), roof)
    for x in (-0.15, 0.1):
        box(col, "VentSlot", (x, -D / 2 - 0.002, H - 0.55), (0.18, 0.01, 0.05), dark)
    return col


def world_uvs(col, tile=1.2):
    """Cube-project UVs at world scale (one texture repeat per `tile` metres) so Higgsfield
    albedo textures keep real-world size on every part instead of stretching per face."""
    for o in col.objects:
        bpy.ops.object.select_all(action="DESELECT")
        o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
        bpy.ops.uv.cube_project(cube_size=tile, scale_to_bounds=False, correct_aspect=True)
        bpy.ops.object.mode_set(mode="OBJECT")


def export(col):
    world_uvs(col)
    bpy.ops.object.select_all(action="DESELECT")
    for o in col.objects: o.select_set(True)
    bpy.context.view_layer.objects.active = col.objects[0]
    bpy.ops.object.join()
    o = bpy.context.view_layer.objects.active; o.name = col.name
    out = ROOT / "Assets/_Game/Art/Models/B-PROC" / f"{col.name}.fbx"
    bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True, axis_forward="-Z", axis_up="Y",
                             apply_scale_options="FBX_SCALE_UNITS", mesh_smooth_type="FACE")
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    size = [round(max(p[i] for p in pts) - min(p[i] for p in pts), 3) for i in range(3)]
    tris = sum(len(p.vertices) - 2 for p in o.data.polygons)
    return {"name": col.name, "size_xyz_m": size, "tris": tris}


bpy.ops.wm.read_factory_settings(use_empty=True)
report = []
for build in (office_trailer, rolloff, toilet):
    col = build()
    report.append(export(col))
    col.hide_render = True
print(json.dumps(report, indent=1))

if RENDER:
    out = ROOT / "Captures/Props/site_props"; out.mkdir(parents=True, exist_ok=True)
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = 1280, 720
    world = bpy.data.worlds.new("W"); scene.world = world; world.use_nodes = True
    bg = next(n for n in world.node_tree.nodes if n.type == "BACKGROUND")
    bg.inputs[0].default_value = (0.55, 0.6, 0.65, 1); bg.inputs[1].default_value = 0.8
    sun = bpy.data.objects.new("Sun", bpy.data.lights.new("Sun", "SUN")); scene.collection.objects.link(sun)
    sun.rotation_euler = (math.radians(50), 0, math.radians(35)); sun.data.energy = 3
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); scene.collection.objects.link(cam); scene.camera = cam
    for col in bpy.data.collections:
        for c in bpy.data.collections: c.hide_render = c != col
        o = col.objects[0]
        pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
        ctr = sum(pts, Vector()) / 8; r = max((p - ctr).length for p in pts)
        cam.location = ctr + Vector((0.9, -1.6, 0.55)).normalized() * r * 3.2
        cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
        scene.render.filepath = str(out / f"{col.name}.png")
        bpy.ops.render.render(write_still=True)
