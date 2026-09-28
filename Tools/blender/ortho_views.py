"""Orthographic side/front/top renders of cleaned vehicle FBX with a 10% grid, for authoring part cut boxes.
blender --background --factory-startup --python Tools/blender/ortho_views.py -- name1,name2
Writes Captures/Props/ortho/<SM_Name>_<view>.png. Grid lines every 10% of the bounding box (0..1 coords).
"""
import sys, math
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
names = sys.argv[sys.argv.index("--") + 1].split(",")
out = ROOT / "Captures/Props/ortho"; out.mkdir(parents=True, exist_ok=True)
for name in names:
    base = "SM_" + "".join(w.capitalize() for w in name.split("_"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT / "Assets/_Game/Art/Models/TR-3D" / (base + ".fbx")))
    objs = [o for o in bpy.context.scene.objects if o.type == "MESH" and not o.name.endswith("LOD1")]
    for o in bpy.context.scene.objects:
        if o.name.endswith("LOD1"): o.hide_render = True
    pts = [o.matrix_world @ Vector(c) for o in objs for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    size = hi - lo; ctr = (lo + hi) / 2
    grid = bpy.data.materials.new("grid"); grid.use_nodes = True
    em = grid.node_tree.nodes.new("ShaderNodeEmission"); em.inputs[0].default_value = (1, 0, 0, 1)
    grid.node_tree.links.new(em.outputs[0], grid.node_tree.nodes["Material Output"].inputs[0])
    def line(a, b):
        mid = (a + b) / 2; d = b - a
        bpy.ops.mesh.primitive_cube_add(size=1, location=mid); c = bpy.context.object
        c.scale = (max(abs(d.x), 0.01), max(abs(d.y), 0.01), max(abs(d.z), 0.01)); c.data.materials.append(grid)
    for k in range(11):
        f = k / 10
        # side view grid (X-Z) placed just in front (-Y) of the model
        y = lo.y - 0.05
        line(Vector((lo.x + f * size.x, y, lo.z)), Vector((lo.x + f * size.x, y, hi.z)))
        line(Vector((lo.x, y, lo.z + f * size.z)), Vector((hi.x, y, lo.z + f * size.z)))
        # top grid (X-Y) above
        z = hi.z + 0.05
        line(Vector((lo.x + f * size.x, lo.y, z)), Vector((lo.x + f * size.x, hi.y, z)))
        line(Vector((lo.x, lo.y + f * size.y, z)), Vector((hi.x, lo.y + f * size.y, z)))
    scene = bpy.context.scene
    scene.render.resolution_x, scene.render.resolution_y = 1400, 800
    w = bpy.data.worlds.new("w"); scene.world = w; w.use_nodes = True
    w.node_tree.nodes["Background"].inputs[0].default_value = (0.8, 0.8, 0.8, 1)
    sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun); sun.data.energy = 3
    sun.rotation_euler = (math.radians(40), 0, math.radians(30))
    cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
    cam.data.type = "ORTHO"
    for view, loc, rot, scale in (("side", ctr + Vector((0, -50, 0)), (math.pi / 2, 0, 0), max(size.x, size.z) * 1.1),
                                  ("top", ctr + Vector((0, 0, 50)), (0, 0, 0), max(size.x, size.y) * 1.1)):
        cam.location = loc; cam.rotation_euler = rot; cam.data.ortho_scale = scale
        scene.render.filepath = str(out / f"{base}_{view}.png")
        bpy.ops.render.render(write_still=True)
    print("[ortho]", base, [round(v, 2) for v in size])
