"""First-person rugged tablet held in two gloved hands (work gloves, hi-vis sleeves). B-PROC, web budget ~6k tris.
blender -b --factory-startup --python fp_tablet_hands.py -- out.fbx preview.png
Convention: tablet face points -Y (toward a viewer standing at -Y), X = right, Z = up. Metres.
Objects: Tablet, Screen (the display quad the UI is mapped onto), Glove_L, Glove_R."""
import math
import sys
import bpy
import bmesh
from mathutils import Vector, Matrix

out, preview = sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
W, H, T = 0.215, 0.265, 0.022        # rugged 10" tablet held portrait (screen 0.175 x 0.218 m = UI 574x736 aspect)


def mat(name, rgb, rough=0.6, metal=0.0):
    m = bpy.data.materials.new(name); m.use_nodes = True
    b = next(n for n in m.node_tree.nodes if n.type == "BSDF_PRINCIPLED")
    b.inputs["Base Color"].default_value = (*rgb, 1); b.inputs["Roughness"].default_value = rough; b.inputs["Metallic"].default_value = metal
    return m


M_BODY = mat("M_FP_TabletBody", (0.09, 0.095, 0.1), 0.7)
M_BUMP = mat("M_FP_TabletBumper", (0.95, 0.68, 0.06), 0.55)
M_SCREEN = mat("M_FP_Screen", (0.01, 0.012, 0.014), 0.15)
M_GLOVE = mat("M_FP_GloveLeather", (0.62, 0.45, 0.26), 0.8)
M_PALM = mat("M_FP_GlovePalm", (0.18, 0.18, 0.19), 0.85)
M_CUFF = mat("M_FP_GloveCuff", (0.85, 0.58, 0.12), 0.9)
M_SLEEVE = mat("M_FP_SleeveHiVis", (0.72, 0.9, 0.08), 0.7)
M_STRIPE = mat("M_FP_Reflective", (0.75, 0.76, 0.78), 0.3, 0.5)


def cube(name, size, loc, material, bevel=0.0, seg=2):
    bpy.ops.mesh.primitive_cube_add(size=1, location=loc)
    o = bpy.context.object; o.name = name; o.scale = size
    bpy.ops.object.transform_apply(scale=True)
    if bevel:
        m = o.modifiers.new("b", "BEVEL"); m.width = bevel; m.segments = seg
        bpy.ops.object.modifier_apply(modifier="b")
    o.data.materials.append(material)
    return o


def capsule(name, a, b, r, material, seg=10):
    a, b = Vector(a), Vector(b)
    d = b - a
    bpy.ops.mesh.primitive_cylinder_add(vertices=seg, radius=r, depth=d.length, location=(a + b) / 2)
    o = bpy.context.object; o.name = name
    o.rotation_euler = d.to_track_quat("Z", "Y").to_euler()
    bpy.ops.object.transform_apply(rotation=True)
    tips = []
    for p in (a, b):   # rounded tips
        bpy.ops.mesh.primitive_uv_sphere_add(segments=seg, ring_count=6, radius=r, location=p)
        tips.append(bpy.context.object)
    bpy.ops.object.select_all(action="DESELECT")
    for s in tips: s.select_set(True)
    o.select_set(True); bpy.context.view_layer.objects.active = o
    bpy.ops.object.join()
    o.data.materials.append(material)
    return o


def join(name, objs):
    bpy.ops.object.select_all(action="DESELECT")
    for o in objs: o.select_set(True)
    bpy.context.view_layer.objects.active = objs[0]
    bpy.ops.object.join()
    o = bpy.context.object; o.name = name
    return o


# ---- tablet ----
body = cube("TabletBody", (W, T, H), (0, 0, 0), M_BODY, bevel=0.006, seg=3)
parts = [body]
for sx in (-1, 1):
    for sz in (-1, 1):   # yellow rubber corner bumpers
        parts.append(cube("Bumper", (0.055, T + 0.012, 0.055), (sx * (W / 2 - 0.018), 0, sz * (H / 2 - 0.018)), M_BUMP, bevel=0.008, seg=3))
parts.append(cube("Grip", (W * 0.55, 0.008, 0.03), (0, T / 2 + 0.004, -H * 0.2), M_BUMP, bevel=0.003))   # back hand strap
tablet = join("Tablet", parts)
bpy.ops.mesh.primitive_plane_add(size=1, location=(0, -T / 2 - 0.0012, 0.004))
screen = bpy.context.object; screen.name = "Screen"
screen.scale = (W - 0.04, H - 0.047, 1); screen.rotation_euler = (math.radians(90), 0, 0)
bpy.ops.object.transform_apply(scale=True, rotation=True)
screen.data.materials.append(M_SCREEN)


# ---- gloves: fingers wrap the side edge behind the tablet, thumb rests on the front bezel ----
def glove(side):
    s = side                                   # -1 left, +1 right
    ex = s * (W / 2)                           # tablet edge x
    parts = []
    palm_c = Vector((ex + s * 0.03, 0.028, -0.02))
    palm = cube("Palm", (0.035, 0.05, 0.085), palm_c, M_PALM, bevel=0.012, seg=3)
    palm.rotation_euler = (0, s * math.radians(-12), 0); parts.append(palm)
    back = cube("Back", (0.03, 0.052, 0.087), palm_c + Vector((s * 0.012, 0.004, 0)), M_GLOVE, bevel=0.012, seg=3)
    back.rotation_euler = palm.rotation_euler; parts.append(back)
    # four fingers: out from the palm behind the tablet, curling inward across its back
    for i, z in enumerate((0.03, 0.01, -0.01, -0.03)):
        l = (0.03, 0.034, 0.032, 0.026)[i]
        a = Vector((ex + s * 0.01, T / 2 + 0.012, z - 0.02))
        b = a + Vector((-s * l, 0.004, 0))
        c = b + Vector((-s * l * 0.8, -0.006, 0))
        parts.append(capsule("F1", a, b, 0.0095, M_GLOVE))
        parts.append(capsule("F2", b, c, 0.0088, M_GLOVE))
    # thumb: around the edge and onto the front bezel
    ta = Vector((ex + s * 0.02, -0.004, -0.035))
    tb = Vector((ex - s * 0.004, -T / 2 - 0.009, -0.03))
    tc = Vector((ex - s * 0.03, -T / 2 - 0.01, -0.022))
    parts.append(capsule("T1", ta, tb, 0.011, M_GLOVE))
    parts.append(capsule("T2", tb, tc, 0.0105, M_PALM))
    # knit cuff + hi-vis sleeve running back toward the viewer's shoulders (down and out of frame)
    wrist = palm_c + Vector((s * 0.012, 0.01, -0.07))
    elbow = wrist + Vector((s * 0.09, -0.22, -0.2))
    parts.append(capsule("Cuff", wrist + Vector((0, 0, 0.01)), wrist + Vector((s * 0.02, -0.03, -0.05)), 0.03, M_CUFF, 12))
    sleeve = capsule("Sleeve", wrist + Vector((s * 0.02, -0.03, -0.05)), elbow, 0.045, M_SLEEVE, 12)
    parts.append(sleeve)
    stripe = capsule("Stripe", wrist + Vector((s * 0.035, -0.07, -0.1)), wrist + Vector((s * 0.045, -0.095, -0.123)), 0.047, M_STRIPE, 12)
    parts.append(stripe)
    g = join("Glove_" + ("L" if s < 0 else "R"), parts)
    m = g.modifiers.new("smooth", "SUBSURF"); m.levels = 1; m.render_levels = 1
    bpy.context.view_layer.objects.active = g; bpy.ops.object.modifier_apply(modifier="smooth")
    bpy.ops.object.shade_smooth()
    return g


gl, gr = glove(-1), glove(1)
for o in (tablet, gl, gr):
    bpy.context.view_layer.objects.active = o
    m = o.modifiers.new("ws", "WEIGHTED_NORMAL") if hasattr(bpy.types, "WeightedNormalModifier") else None

tris = sum(len(o.data.polygons) for o in bpy.context.scene.objects if o.type == "MESH")
print(f"[fp] objects={[o.name for o in bpy.context.scene.objects]} faces={tris}")

# Preview from the viewer's eye.
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); bpy.context.scene.collection.objects.link(cam)
cam.location = (0, -0.46, 0.14); cam.rotation_euler = (math.radians(74), 0, 0); cam.data.lens = 22
bpy.context.scene.camera = cam
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); bpy.context.scene.collection.objects.link(sun)
sun.rotation_euler = (math.radians(40), math.radians(20), math.radians(30)); sun.data.energy = 3
w = bpy.data.worlds.new("w"); bpy.context.scene.world = w; w.use_nodes = True
w.node_tree.nodes["Background"].inputs[0].default_value = (0.55, 0.6, 0.66, 1)
sc = bpy.context.scene; sc.render.engine = "BLENDER_EEVEE_NEXT" if "BLENDER_EEVEE_NEXT" in [e.identifier for e in bpy.types.RenderSettings.bl_rna.properties["engine"].enum_items] else sc.render.engine
sc.render.resolution_x, sc.render.resolution_y = 960, 540
sc.render.filepath = preview
bpy.ops.render.render(write_still=True)
cam.select_set(False)
bpy.data.objects.remove(cam); bpy.data.objects.remove(sun)
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, object_types={"MESH"}, apply_scale_options="FBX_SCALE_UNITS", mesh_smooth_type="FACE")
print("[fp] exported", out)
