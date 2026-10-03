"""Render the Poly Haven pine (17M tris) to an alpha impostor texture for a web treeline.
blender -b --factory-startup --python pine_impostor.py -- in.fbx textures_dir out.png"""
import math
import sys
import bpy

src, texdir, out = sys.argv[-3], sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]

# Materials: base color from the Poly Haven diffuse maps; twig alpha from its texture, tinted to loblolly green (as in Unity).
for o in meshes:
    for slot in o.material_slots:
        m = slot.material
        if m is None:
            continue
        m.use_nodes = True
        nt = m.node_tree
        bsdf = next(n for n in nt.nodes if n.type == "BSDF_PRINCIPLED")
        name = m.name.lower()
        key = "twig" if "twig" in name else ("trunk_b" if "trunk_b" in name else ("trunk_c" if "trunk_c" in name else "trunk"))
        img_path = f"{texdir}/pine_tree_01_{key}_diff_1k.png"
        try:
            img = bpy.data.images.load(img_path, check_existing=True)
        except RuntimeError:
            continue
        tex = nt.nodes.new("ShaderNodeTexImage"); tex.image = img
        if key == "twig":
            mix = nt.nodes.new("ShaderNodeMix"); mix.data_type = "RGBA"; mix.blend_type = "MULTIPLY"
            mix.inputs[0].default_value = 1.0
            nt.links.new(tex.outputs["Color"], mix.inputs[6])
            mix.inputs[7].default_value = (0.42 * 1.6, 0.55 * 1.6, 0.30 * 1.6, 1)
            nt.links.new(mix.outputs[2], bsdf.inputs["Base Color"])
            nt.links.new(tex.outputs["Alpha"], bsdf.inputs["Alpha"])
        else:
            nt.links.new(tex.outputs["Color"], bsdf.inputs["Base Color"])
        bsdf.inputs["Roughness"].default_value = 0.8

# Bounds -> orthographic side camera that exactly frames the tree.
xs, ys, zs = [], [], []
for o in meshes:
    for c in o.bound_box:
        w = o.matrix_world @ __import__("mathutils").Vector(c)
        xs.append(w.x); ys.append(w.y); zs.append(w.z)
h = max(zs) - min(zs); wdt = max(max(xs) - min(xs), max(ys) - min(ys))
cx, cy, cz = (max(xs) + min(xs)) / 2, (max(ys) + min(ys)) / 2, (max(zs) + min(zs)) / 2
print(f"[impostor] height {h:.2f} width {wdt:.2f}")

scn = bpy.context.scene
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam"))
scn.collection.objects.link(cam); scn.camera = cam
cam.data.type = "ORTHO"; cam.data.ortho_scale = max(h, wdt * 2) * 1.02
cam.location = (cx, cy - 100, cz); cam.rotation_euler = (math.radians(90), 0, 0)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scn.collection.objects.link(sun)
sun.data.energy = 3.5; sun.rotation_euler = (math.radians(50), 0, math.radians(30))
world = bpy.data.worlds.new("w"); scn.world = world; world.use_nodes = True
world.node_tree.nodes["Background"].inputs[1].default_value = 0.9
scn.render.engine = "CYCLES"
try:
    scn.cycles.device = "GPU"
    prefs = bpy.context.preferences.addons["cycles"].preferences; prefs.compute_device_type = "OPTIX"; prefs.get_devices()
    for d in prefs.devices: d.use = True
except Exception:
    pass
scn.cycles.samples = 64
scn.render.film_transparent = True
scn.render.resolution_x = 512 if h > wdt * 2 else 1024
scn.render.resolution_y = int(scn.render.resolution_x * h / max(wdt, h / 2)) if h > wdt * 2 else 1024
scn.render.resolution_x, scn.render.resolution_y = 1024, 2048
cam.data.ortho_scale = max(h, wdt * 2) * 1.02
scn.render.image_settings.file_format = "PNG"; scn.render.image_settings.color_mode = "RGBA"
scn.render.filepath = out
bpy.ops.render.render(write_still=True)
print(f"[impostor] wrote {out}  aspect_w_over_h {cam.data.ortho_scale / 2 / cam.data.ortho_scale:.3f}  height_m {h:.2f}")
