"""Decimate a heavy library FBX to a web tri budget, keeping materials/UVs. Usage:
blender -b --factory-startup --python decimate_fbx.py -- in.fbx out.fbx max_tris"""
import sys
import bpy

src, dst, budget = sys.argv[-3], sys.argv[-2], int(sys.argv[-1])
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.fbx(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]


def tris(objs):
    dg = bpy.context.evaluated_depsgraph_get()
    return sum(sum(len(p.vertices) - 2 for p in o.evaluated_get(dg).data.polygons) for o in objs)


before = tris(meshes)
ratio = min(1.0, budget / max(before, 1))
print(f"[decimate] {len(meshes)} meshes, {before} tris -> ratio {ratio:.4f}")
if ratio < 1.0:
    for o in meshes:
        m = o.modifiers.new("dec", "DECIMATE")
        m.decimate_type = "COLLAPSE"; m.ratio = ratio; m.use_collapse_triangulate = True
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes:
        o.select_set(True); bpy.context.view_layer.objects.active = o
        bpy.ops.object.modifier_apply(modifier="dec")
        o.select_set(False)
print(f"[decimate] after {tris(meshes)} tris")
bpy.ops.export_scene.fbx(filepath=dst, use_selection=False, path_mode="RELATIVE", embed_textures=False, bake_anim=False,
                         apply_scale_options="FBX_SCALE_UNITS", mesh_smooth_type="FACE")
