"""Split a cleaned Tripo vehicle into rig parts from Tools/tripo/rig_specs.json.

blender --background --factory-startup --python Tools/blender/rig_vehicle.py -- crew_pickup [...]
Input  Assets/_Game/Art/Models/TR-3D/SM_<Name>.fbx (LOD0 only)
Output Assets/_Game/Art/Models/TR-3D/Rigs/SM_<Name>_Rig.fbx with hierarchy
       <Root> > Body > {parts with origin on their hinge/axle pivot}, plus a simple cab interior
       (Tripo meshes are hollow shells; an opened door must show a seat, dash and wheel).
"""
import json, sys
from pathlib import Path
import bpy, bmesh
from mathutils import Matrix, Vector

ROOT = Path(__file__).resolve().parents[2]
specs = json.loads((ROOT / "Tools/tripo/rig_specs.json").read_text())
names = sys.argv[sys.argv.index("--") + 1:]


def norm_to_world(n, lo, size):
    return Vector((lo.x + n[0] * size.x, lo.y + n[1] * size.y, lo.z + n[2] * size.z))


for name in names:
    spec = specs[name]
    base = "SM_" + "".join(w.capitalize() for w in name.split("_"))
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.fbx(filepath=str(ROOT / "Assets/_Game/Art/Models/TR-3D" / (base + ".fbx")))
    for o in list(bpy.context.scene.objects):
        if o.name.endswith("LOD1") or o.type != "MESH":
            bpy.data.objects.remove(o, do_unlink=True)
    body = bpy.context.scene.objects[0]
    bpy.context.view_layer.objects.active = body; body.select_set(True)
    bpy.ops.object.parent_clear(type="CLEAR_KEEP_TRANSFORM")
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    body.name = "Body"
    co = [v.co.copy() for v in body.data.vertices]
    lo = Vector([min(c[i] for c in co) for i in range(3)]); hi = Vector([max(c[i] for c in co) for i in range(3)])
    size = hi - lo

    root = bpy.data.objects.new(base + "_Rig", None); bpy.context.collection.objects.link(root)
    body.parent = root
    report = {}
    pieces = {}
    for part in spec["parts"]:
        # "box" or "boxes": a part may need several regions where Tripo fused it to a neighbour.
        regions = []
        for b in part.get("boxes", [part.get("box")]):
            regions.append((norm_to_world((b[0], b[2], b[4]), lo, size), norm_to_world((b[1], b[3], b[5]), lo, size)))
        bm = bmesh.new(); bm.from_mesh(body.data)
        count = 0
        for f in bm.faces:
            c = f.calc_center_median()
            f.select = any(a.x <= c.x <= z.x and a.y <= c.y <= z.y and a.z <= c.z <= z.z for a, z in regions)
            count += f.select
        bm.to_mesh(body.data); bm.free()
        report[part["name"]] = count
        if count == 0:
            continue
        bpy.ops.object.select_all(action="DESELECT"); body.select_set(True); bpy.context.view_layer.objects.active = body
        bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.separate(type="SELECTED"); bpy.ops.object.mode_set(mode="OBJECT")
        piece = [o for o in bpy.context.selected_objects if o != body][0]
        piece.name = part["name"]
        pivot = norm_to_world(part["pivot"], lo, size)
        piece.data.transform(Matrix.Translation(-pivot)); piece.location = pivot
        piece.parent = root
        pieces[part["name"]] = piece

    # Chain hierarchy (e.g. House > Boom > Stick > Bucket); keep world placement.
    links = {p["name"]: p["parent"] for p in spec["parts"] if p.get("parent")}
    links.update(spec.get("parent_fix", {}))
    bpy.context.view_layer.update()
    for child, par in links.items():
        if child in pieces and par in pieces:
            c, pa = pieces[child], pieces[par]
            mw = c.matrix_world.copy(); c.parent = pa; c.matrix_world = mw

    # Minimal cab interior so an opened door does not reveal an empty shell.
    it = spec.get("interior")
    if it:
        dark = bpy.data.materials.new("M_CabInterior"); dark.use_nodes = True
        next(n for n in dark.node_tree.nodes if n.type == "BSDF_PRINCIPLED").inputs["Base Color"].default_value = (0.05, 0.05, 0.05, 1)
        def add(kind, n, scale, rot=(0, 0, 0)):
            loc = norm_to_world(n, lo, size)
            if kind == "cyl": bpy.ops.mesh.primitive_torus_add(location=loc, rotation=rot, major_radius=scale[0], minor_radius=scale[1])
            else: bpy.ops.mesh.primitive_cube_add(size=1, location=loc); bpy.context.object.scale = scale
            o = bpy.context.object; o.data.materials.append(dark); o.parent = root; return o
        add("box", it["seat"], (0.55, 0.5, 0.12)).name = "Interior_Seat"
        add("box", it["dash"], (0.3, size.y * 0.7, 0.25)).name = "Interior_Dash"
        add("cyl", it["wheel"], (0.19, 0.02), (0, 1.2, 0)).name = "Interior_SteeringWheel"
        seat_pt = bpy.data.objects.new("SeatEye", None); bpy.context.collection.objects.link(seat_pt)
        seat_pt.location = norm_to_world(it["seat"], lo, size) + Vector((0, 0, 0.75)); seat_pt.parent = root
        exit_pt = bpy.data.objects.new("ExitPoint", None); bpy.context.collection.objects.link(exit_pt)
        exit_pt.location = norm_to_world((it["seat"][0], 1.15 if spec.get("driver_side_y", 1) > 0.5 else -0.15, 0), lo, size)
        exit_pt.parent = root

    out = ROOT / "Assets/_Game/Art/Models/TR-3D/Rigs" / (base + "_Rig.fbx"); out.parent.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(filepath=str(out), use_selection=True, apply_scale_options="FBX_SCALE_UNITS",
                             axis_forward="-Z", axis_up="Y", path_mode="COPY", embed_textures=True, object_types={"MESH", "EMPTY"})
    print("[rig]", base, json.dumps(report))
