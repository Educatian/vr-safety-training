"""Clean every Tripo GLB listed in Tools/tripo/prop_specs.json that has Tools/tripo/out/<name>/model.glb.

blender --background --factory-startup --python Tools/blender/cleanup_batch.py -- [--only name1,name2]
Per model: join meshes, apply transforms, longest horizontal axis -> X, uniform scale to length_m
(or height_m when fit=height), decimate to the spec's triangle budget, origin at base centre,
export Assets/_Game/Art/Models/TR-3D/SM_<Name>.fbx with embedded textures. Prints a JSON report.
Uniform scale only: proportions stay true; the non-fitted dimension is reported, never stretched.
"""
import json, math, sys
from pathlib import Path
import bpy
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
argv = sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else []
only = set(argv[argv.index("--only") + 1].split(",")) if "--only" in argv else None
specs = json.loads((ROOT / "Tools/tripo/prop_specs.json").read_text())


def bounds(o):
    # From vertex data: obj.bound_box is stale after mesh.transform() until a depsgraph update.
    import numpy as np
    co = np.empty(len(o.data.vertices) * 3); o.data.vertices.foreach_get("co", co); co = co.reshape(-1, 3)
    return Vector(co.min(axis=0)), Vector(co.max(axis=0))


report = []
for name, spec in specs.items():
    glb = ROOT / "Tools/tripo/out" / name / "model.glb"
    if name.startswith("_") or not glb.exists() or (only and name not in only):
        continue
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=str(glb))
    meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
    bpy.ops.object.select_all(action="DESELECT")
    for o in meshes: o.select_set(True)
    bpy.context.view_layer.objects.active = meshes[0]
    if len(meshes) > 1: bpy.ops.object.join()
    obj = bpy.context.view_layer.objects.active
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    lo, hi = bounds(obj); size = hi - lo
    # Transform mesh data directly: transform_apply silently skipped the rotation on some imports.
    if size.y > size.x:
        obj.data.transform(Matrix.Rotation(math.pi / 2, 4, "Z")); obj.data.update()
        lo, hi = bounds(obj); size = hi - lo
    s = spec["height_m"] / size.z if spec.get("fit") == "height" else spec["length_m"] / size.x
    obj.data.transform(Matrix.Scale(s, 4)); obj.data.update()
    tris0 = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    budget = spec.get("tris", 8000)
    # Tripo meshes are split at every UV seam; weld first, then collapse in <=10x passes.
    # A single 0.001 ratio pass stalls on seams and spikes vertices (seen: 2x bounding box).
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="SELECT")
    bpy.ops.mesh.remove_doubles(threshold=0.0005); bpy.ops.object.mode_set(mode="OBJECT")
    pre_lo, pre_hi = bounds(obj)
    tris = tris0
    for _ in range(8):
        if tris <= budget: break
        dec = obj.modifiers.new("decimate", "DECIMATE"); dec.ratio = max(0.1, budget / tris)
        dec.use_collapse_triangulate = True
        bpy.ops.object.modifier_apply(modifier="decimate")
        tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    post_lo, post_hi = bounds(obj)
    growth = max((post_hi - post_lo)[i] / max((pre_hi - pre_lo)[i], 1e-6) for i in range(3))
    # Web: cap every texture at the spec size (download size + GPU memory dominate on the web).
    cap = spec.get("tex", 512)
    for img in bpy.data.images:
        w, h = img.size
        if max(w, h) > cap:
            img.scale(max(1, w * cap // max(w, h)), max(1, h * cap // max(w, h)))
    lo, hi = bounds(obj)
    obj.data.transform(Matrix.Translation(-Vector(((lo.x + hi.x) / 2, (lo.y + hi.y) / 2, lo.z)))); obj.data.update()
    lo, hi = bounds(obj); size = hi - lo
    base = "SM_" + "".join(w.capitalize() for w in name.split("_"))
    obj.name = base
    if spec.get("lod1_ratio"):
        # Unity builds a LODGroup automatically from *_LOD0 / *_LOD1 children in one FBX.
        obj.name = base + "_LOD0"
        lod1 = obj.copy(); lod1.data = obj.data.copy(); bpy.context.collection.objects.link(lod1)
        lod1.name = base + "_LOD1"
        d = lod1.modifiers.new("d", "DECIMATE"); d.ratio = spec["lod1_ratio"]
        bpy.context.view_layer.objects.active = lod1; bpy.ops.object.modifier_apply(modifier="d")
        bpy.ops.object.select_all(action="DESELECT"); obj.select_set(True); lod1.select_set(True)
        bpy.context.view_layer.objects.active = obj
    out = ROOT / "Assets/_Game/Art/Models/TR-3D" / (base + ".fbx"); out.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", path_mode="COPY", embed_textures=True)
    tris = sum(len(p.vertices) - 2 for p in obj.data.polygons)
    report.append({"name": base, "size_m": [round(v, 2) for v in size], "tris_in": tris0, "tris": tris, "bbox_growth": round(growth, 3),
                   "mb": round(out.stat().st_size / 1e6, 1)})
    print("[cleanup]", json.dumps(report[-1]), flush=True)
(ROOT / "Tools/tripo/cleanup_report.json").write_text(json.dumps(report, indent=1))
