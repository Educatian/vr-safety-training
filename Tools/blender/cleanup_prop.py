"""Clean a Tripo GLB into a Unity-ready static mesh at real-world scale.

blender --background --factory-startup --python Tools/blender/cleanup_prop.py -- --name portable_toilet
Reads Tools/tripo/out/<name>/model.glb and Tools/tripo/prop_specs.json; writes
Assets/_Game/Art/Models/TR-3D/SM_<Name>.fbx and prints measured size + triangle count.
Uniform scale only (fits length_m); height is reported, never stretched, so proportions stay true.
"""
import argparse, json, math, sys
from pathlib import Path
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
args = argparse.ArgumentParser().parse_args if False else None
p = argparse.ArgumentParser(); p.add_argument("--name", required=True)
a = p.parse_args(sys.argv[sys.argv.index("--") + 1:])
spec = json.loads((ROOT / "Tools/tripo/prop_specs.json").read_text())[a.name]
glb = next((ROOT / "Tools/tripo/out" / a.name).glob("*.glb"))

bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=str(glb))
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
obj = bpy.context.view_layer.objects.active
obj.parent = None
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)

def bounds(o):
    pts = [o.matrix_world @ Vector(c) for c in o.bound_box]
    lo = Vector([min(p[i] for p in pts) for i in range(3)]); hi = Vector([max(p[i] for p in pts) for i in range(3)])
    return lo, hi

lo, hi = bounds(obj); size = hi - lo
if size.y > size.x:  # longest horizontal axis -> X
    obj.rotation_euler = (0, 0, math.pi / 2); bpy.ops.object.transform_apply(rotation=True)
    lo, hi = bounds(obj); size = hi - lo
s = spec["length_m"] / size.x
obj.scale = (s, s, s); bpy.ops.object.transform_apply(scale=True)
lo, hi = bounds(obj)
obj.location -= Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z))  # origin at base centre
bpy.ops.object.transform_apply(location=True)
lo, hi = bounds(obj); size = hi - lo
obj.name = "SM_" + "".join(w.capitalize() for w in a.name.split("_"))
out = ROOT / "Assets/_Game/Art/Models/TR-3D" / (obj.name + ".fbx"); out.parent.mkdir(parents=True, exist_ok=True)
bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
                         axis_forward="-Z", axis_up="Y", path_mode="COPY", embed_textures=True)
tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
print(json.dumps({"name": obj.name, "size_m": [round(v, 3) for v in size], "target_height_m": spec["height_m"],
                  "height_error_pct": round(100 * (size.z - spec["height_m"]) / spec["height_m"], 1), "tris": tris, "fbx": str(out)}))
