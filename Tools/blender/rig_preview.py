"""Pose-check a rig FBX: open the driver door 60 deg, turn front wheels 25 deg, render driver-side 3/4.
blender --background --factory-startup --python Tools/blender/rig_preview.py -- SM_CrewPickup_Rig"""
import sys, math
from pathlib import Path
import bpy
from mathutils import Vector
ROOT = Path(__file__).resolve().parents[2]
name = sys.argv[sys.argv.index("--") + 1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=str(ROOT / "Assets/_Game/Art/Models/TR-3D/Rigs" / (name + ".fbx")))
objs = {o.name.split(".")[0]: o for o in bpy.context.scene.objects}
POSE = {"DriverDoor": ("Z", -60),   # front-hinged +Y door opens outward
        "WheelFL": ("Z", 25), "WheelFR": ("Z", 25),
        "DumpBed": ("Y", -40),       # rear hinge: raise the front of the bed
        "House": ("Z", 35), "Boom": ("Y", -30), "Tele1": ("X", 0), "Stick": ("Y", 25), "Bucket": ("Y", 35)}
for part, (axis, deg) in POSE.items():
    if part in objs: objs[part].rotation_euler.rotate_axis(axis, math.radians(deg))
bpy.context.view_layer.update()
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
pts = [o.matrix_world @ Vector(c) for o in meshes for c in o.bound_box]
lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
ctr = (lo + hi) / 2; r = (hi - lo).length
scene = bpy.context.scene; scene.render.resolution_x, scene.render.resolution_y = 1400, 800
w = bpy.data.worlds.new("w"); scene.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.75, 0.78, 0.8, 1)
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); scene.collection.objects.link(sun); sun.data.energy = 3; sun.rotation_euler = (math.radians(45), 0, math.radians(200))
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); scene.collection.objects.link(cam); scene.camera = cam
cam.location = ctr + Vector((0.9, 1.0, 0.35)).normalized() * r * 1.1
cam.rotation_euler = (ctr - cam.location).to_track_quat("-Z", "Y").to_euler()
out = ROOT / "Captures/Props/rigs"; out.mkdir(parents=True, exist_ok=True)
scene.render.filepath = str(out / (name + "_posed.png")); bpy.ops.render.render(write_still=True)
print("[preview]", sorted(objs))
