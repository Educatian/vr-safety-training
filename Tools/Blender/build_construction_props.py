"""Build Quest-friendly US construction props and export individual FBX assets."""

from pathlib import Path
import math
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "SafetyTraining" / "Models" / "ConstructionCustom"
SOURCE_OUT = ROOT / "SourceAssets" / "Blender"

def mat(name, color, metallic=0.0, roughness=0.55):
    value = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    value.diffuse_color = (*color, 1.0)
    value.metallic, value.roughness = metallic, roughness
    return value

STEEL = mat("US Site Galvanized Steel", (0.32, 0.35, 0.36), 0.72, 0.34)
DARK = mat("US Site Rebar Steel", (0.075, 0.082, 0.086), 0.78, 0.42)
PLY = mat("Phenolic Formwork Plywood", (0.17, 0.075, 0.028), 0.0, 0.58)
ORANGE = mat("OSHA Safety Orange", (0.95, 0.18, 0.025), 0.08, 0.4)
WOOD = mat("Construction Dunnage", (0.32, 0.17, 0.07), 0.0, 0.72)

def assign(obj, material):
    obj.data.materials.append(material)
    return obj

def box(name, location, scale, material, edge=0.018):
    bpy.ops.mesh.primitive_cube_add(location=location)
    obj = bpy.context.object
    obj.name, obj.scale = name, Vector(scale) * 0.5
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if edge:
        mod = obj.modifiers.new("Fabrication edge", "BEVEL")
        mod.width, mod.segments, mod.limit_method = min(edge, min(scale) * 0.22), 1, "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return assign(obj, material)

def cyl(name, location, radius, depth, material, rotation=(0, 0, 0), vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
                                       location=location, rotation=rotation)
    obj = bpy.context.object
    obj.name = name
    return assign(obj, material)

def between(name, start, end, radius, material):
    start, end = Vector(start), Vector(end)
    direction = end - start
    obj = cyl(name, (start + end) * 0.5, radius, direction.length, material)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = direction.to_track_quat("Z", "Y")
    return obj

def begin(name):
    collection = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(collection)
    bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection.children[name]
    return collection

def export(collection, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in collection.objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT / filename), use_selection=True,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", object_types={"MESH"},
        use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
        bake_anim=False, path_mode="AUTO", axis_forward="-Z", axis_up="Y")

def build_formwork():
    collection = begin("FormworkPanel")
    box("Phenolic plywood face", (0, 0, 1.35), (2.4, 0.09, 2.7), PLY, 0.012)
    for x in (-1.16, 1.16):
        box("Vertical steel rail", (x, -0.075, 1.35), (0.08, 0.15, 2.78), STEEL)
    for z in (0.04, 0.9, 1.8, 2.66):
        box("Horizontal steel rail", (0, -0.075, z), (2.35, 0.15, 0.08), STEEL)
    for x in (-0.58, 0.58):
        box("Intermediate stiffener", (x, -0.09, 1.35), (0.055, 0.12, 2.58), STEEL, 0.012)
        for z in (0.88, 1.78):
            cyl("Form tie collar", (x, -0.145, z), 0.075, 0.05, STEEL,
                 rotation=(math.pi / 2, 0, 0))
    between("Diagonal adjustable brace", (0.82, -0.18, 1.75),
            (1.65, -1.05, 0.12), 0.045, STEEL)
    between("Brace sleeve", (1.1, -0.48, 1.18), (1.48, -0.88, 0.42), 0.062, STEEL)
    box("Brace foot plate", (1.68, -1.08, 0.035), (0.38, 0.34, 0.07), STEEL)
    export(collection, "US_Modular_Formwork_Panel.fbx")

def build_rebar():
    collection = begin("RebarBundle")
    length = 3.2
    for row in range(3):
        for level in range(4):
            y, z = (row - 1) * 0.16, 0.34 + level * 0.16
            cyl("Bundled reinforcing bar", (0, y, z), 0.042, length, DARK,
                 rotation=(0, math.pi / 2, 0), vertices=10)
            cyl("Orange impalement cap", (length * 0.5 + 0.045, y, z), 0.068, 0.09,
                 ORANGE, rotation=(0, math.pi / 2, 0))
    for x in (-0.95, 0.95):
        bpy.ops.mesh.primitive_torus_add(major_segments=12, minor_segments=4,
            location=(x, 0, 0.58), major_radius=0.29, minor_radius=0.012,
            rotation=(0, math.pi / 2, 0))
        assign(bpy.context.object, DARK).name = "Binding wire loop"
    for x in (-1.05, 1.05):
        box("Timber dunnage", (x, 0, 0.1), (0.18, 0.72, 0.2), WOOD)
    export(collection, "US_Capped_Rebar_Bundle.fbx")

def build_shoring():
    collection = begin("ShoringRack")
    box("Rack base left", (-0.92, 0, 0.08), (0.12, 1.0, 0.16), STEEL)
    box("Rack base right", (0.92, 0, 0.08), (0.12, 1.0, 0.16), STEEL)
    for y in (-0.45, 0.45):
        box("Rack base cross rail", (0, y, 0.08), (1.96, 0.12, 0.16), STEEL)
        box("Rack retaining rail", (0, y, 1.02), (1.86, 0.08, 0.09), STEEL)
    for x in (-0.92, 0.92):
        for y in (-0.45, 0.45):
            box("Rack upright", (x, y, 0.72), (0.1, 0.1, 1.35), STEEL)
    locations = [(-0.62, -0.2), (-0.2, -0.2), (0.2, -0.2), (0.62, -0.2),
                 (-0.62, 0.2), (-0.2, 0.2), (0.2, 0.2), (0.62, 0.2)]
    for index, (x, y) in enumerate(locations):
        lift = (index % 2) * 0.08
        cyl("Shoring post outer", (x, y, 1.35), 0.055, 2.35, STEEL)
        cyl("Shoring post inner", (x, y, 2.42 + lift), 0.042, 0.72, STEEL)
        cyl("Threaded adjustment collar", (x, y, 2.08), 0.082, 0.12, DARK)
        box("Shoring base plate", (x, y, 0.18), (0.2, 0.2, 0.035), STEEL, 0.008)
        box("Shoring head plate", (x, y, 2.8 + lift), (0.22, 0.22, 0.035), STEEL, 0.008)
    export(collection, "US_Adjustable_Shoring_Rack.fbx")

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    SOURCE_OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    build_formwork()
    build_rebar()
    build_shoring()
    for offset, name in zip((-4.5, 0.0, 4.5),
                            ("FormworkPanel", "RebarBundle", "ShoringRack")):
        for obj in bpy.data.collections[name].objects:
            obj.location.x += offset
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_OUT / "US_Construction_Props_Source.blend"))

if __name__ == "__main__":
    main()
