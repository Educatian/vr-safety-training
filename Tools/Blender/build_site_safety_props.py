"""Generate twelve optimized US safety props for the four non-construction sites."""

from pathlib import Path
import math
import bpy
from mathutils import Vector

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "Assets" / "SafetyTraining" / "Models" / "SiteSafetyCustom"
SOURCE_OUT = ROOT / "SourceAssets" / "Blender"

def mat(name, color, metallic=0.0, roughness=0.5):
    m = bpy.data.materials.get(name) or bpy.data.materials.new(name)
    m.diffuse_color, m.metallic, m.roughness = (*color, 1), metallic, roughness
    return m

STEEL = mat("Industrial Steel", (.32, .35, .37), .7, .35)
DARK = mat("Powdercoat Black", (.025, .03, .035), .45, .38)
RED = mat("Safety Red", (.72, .025, .018), .25, .34)
YELLOW = mat("Safety Yellow", (.95, .52, .015), .18, .38)
GREEN = mat("Safety Green", (.025, .32, .12), .18, .4)
WHITE = mat("Equipment White", (.88, .9, .9), .05, .42)
ORANGE = mat("Warning Orange", (.95, .22, .015), .15, .4)
BLUE = mat("Valve Blue", (.015, .16, .65), .25, .34)
BRASS = mat("Padlock Brass", (.64, .39, .08), .72, .28)

def assign(obj, material):
    obj.data.materials.append(material)
    return obj

def box(name, loc, size, material, rot=(0, 0, 0), edge=.015):
    bpy.ops.mesh.primitive_cube_add(location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name, obj.scale = name, Vector(size) * .5
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    if edge:
        mod = obj.modifiers.new("Manufactured edge", "BEVEL")
        mod.width, mod.segments, mod.limit_method = min(edge, min(size) * .2), 1, "ANGLE"
        bpy.context.view_layer.objects.active = obj
        bpy.ops.object.modifier_apply(modifier=mod.name)
    return assign(obj, material)

def cyl(name, loc, radius, depth, material, rot=(0, 0, 0), vertices=12):
    bpy.ops.mesh.primitive_cylinder_add(vertices=vertices, radius=radius, depth=depth,
        location=loc, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    return assign(obj, material)

def between(name, a, b, radius, material):
    a, b = Vector(a), Vector(b)
    d = b - a
    obj = cyl(name, (a + b) * .5, radius, d.length, material)
    obj.rotation_mode = "QUATERNION"
    obj.rotation_quaternion = d.to_track_quat("Z", "Y")
    return obj

def torus(name, loc, major, minor, material, rot=(0, 0, 0)):
    bpy.ops.mesh.primitive_torus_add(major_segments=12, minor_segments=4,
        location=loc, major_radius=major, minor_radius=minor, rotation=rot)
    obj = bpy.context.object
    obj.name = name
    return assign(obj, material)

def text_mesh(text, loc, size, material, rot=(math.pi / 2, 0, 0)):
    bpy.ops.object.text_add(location=loc, rotation=rot)
    obj = bpy.context.object
    obj.data.body, obj.data.align_x, obj.data.size, obj.data.extrude = text, "CENTER", size, .004
    bpy.ops.object.convert(target="MESH")
    obj.name = "English Label " + " ".join(
        "".join(character if character.isalnum() else " " for character in text).split())
    return assign(obj, material)

def begin(name):
    c = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(c)
    bpy.context.view_layer.active_layer_collection = bpy.context.view_layer.layer_collection.children[name]
    return c

def export(c, filename):
    bpy.ops.object.select_all(action="DESELECT")
    for obj in c.objects:
        obj.select_set(True)
    bpy.ops.export_scene.fbx(filepath=str(OUT / filename), use_selection=True,
        apply_unit_scale=True, apply_scale_options="FBX_SCALE_ALL", object_types={"MESH"},
        use_mesh_modifiers=True, mesh_smooth_type="FACE", add_leaf_bones=False,
        bake_anim=False, path_mode="AUTO", axis_forward="-Z", axis_up="Y")

def forklift():
    c = begin("WarehouseForklift")
    box("Lower chassis", (0, 0, .48), (1.65, 1.05, .5), DARK, edge=.08)
    box("Counterweight", (-.62, 0, .86), (.78, 1.08, .7), YELLOW, edge=.14)
    box("Operator deck", (.05, 0, .82), (.72, .92, .14), DARK)
    box("Seat base", (-.05, 0, 1.08), (.48, .48, .18), DARK, edge=.05)
    box("Seat back", (-.28, 0, 1.42), (.16, .5, .62), DARK, rot=(0, -.18, 0), edge=.05)
    for x in (-.5, .45):
        for y in (-.46, .46):
            cyl("Industrial tire", (x, y, .42), .27 if x < 0 else .22, .18, DARK,
                 rot=(math.pi / 2, 0, 0), vertices=16)
            cyl("Wheel hub", (x, y * 1.02, .42), .11, .2, STEEL,
                 rot=(math.pi / 2, 0, 0), vertices=12)
    for x in (-.42, .42):
        for y in (-.43, .43):
            between("Overhead guard post", (x, y, .92), (x, y, 2.25), .035, DARK)
    box("Overhead guard roof", (0, 0, 2.26), (1.02, 1.02, .1), DARK)
    for y in (-.37, .37):
        box("Mast rail", (.88, y, 1.35), (.1, .1, 2.35), DARK)
        box("Fork tine", (1.67, y, .16), (1.65, .12, .09), DARK, edge=.012)
    box("Fork carriage", (.94, 0, .72), (.12, .92, .48), DARK)
    cyl("Warning beacon", (-.2, 0, 2.39), .09, .16, ORANGE, vertices=12)
    export(c, "US_Electric_Warehouse_Forklift.fbx")

def pallet_rack():
    c = begin("WarehousePalletRack")
    for x in (-1.4, 1.4):
        for y in (-.45, .45):
            box("Rack upright", (x, y, 1.7), (.09, .09, 3.4), STEEL)
            box("Rack foot", (x, y, .035), (.28, .24, .07), YELLOW)
    for z in (.55, 1.65, 2.75):
        for y in (-.45, .45):
            box("Load beam", (0, y, z), (2.84, .1, .13), ORANGE)
    for x in (-1.4, 1.4):
        between("Rack diagonal brace", (x, -.45, .5), (x, .45, 1.55), .025, STEEL)
        between("Rack diagonal brace", (x, .45, 1.55), (x, -.45, 2.6), .025, STEEL)
    export(c, "US_Selective_Pallet_Rack_Bay.fbx")

def dock_leveler():
    c = begin("WarehouseDockLeveler")
    box("Dock leveler frame", (0, 0, .09), (2.65, 2.0, .18), DARK)
    box("Diamond plate deck", (0, 0, .2), (2.45, 1.82, .13), STEEL, rot=(.035, 0, 0))
    box("Hinged approach lip", (0, -1.13, .28), (2.4, .62, .1), STEEL, rot=(-.22, 0, 0))
    for x in (-1.27, 1.27):
        box("Side curb", (x, 0, .34), (.1, 2.0, .42), DARK)
        box("Yellow edge marker", (x * 1.01, -.98, .5), (.11, .08, .48), YELLOW)
    between("Hydraulic support", (-.55, -.2, .12), (0, -.82, .52), .045, DARK)
    export(c, "US_Loading_Dock_Leveler.fbx")

def extinguisher():
    c = begin("FireExtinguisher")
    cyl("Extinguisher cylinder", (0, 0, .5), .22, .9, RED, vertices=20)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=(0, 0, .92))
    top = bpy.context.object
    top.name, top.scale = "Rounded extinguisher shoulder", (.22, .22, .18)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign(top, RED)
    cyl("Valve neck", (0, 0, 1.1), .07, .18, STEEL)
    box("Operating lever", (.08, 0, 1.22), (.34, .07, .055), STEEL, rot=(0, -.2, 0))
    torus("Pressure gauge", (-.1, -.03, 1.18), .065, .016, STEEL, rot=(math.pi / 2, 0, 0))
    for a, b in [((.08, .05, 1.13), (.27, .08, 1.02)), ((.27, .08, 1.02), (.31, .12, .58))]:
        between("Flexible discharge hose", a, b, .025, DARK)
    cyl("Discharge nozzle", (.31, .12, .48), .04, .22, DARK)
    box("English instruction label", (0, -.225, .57), (.27, .018, .38), WHITE, edge=.004)
    text_mesh("ABC / P.A.S.S.", (0, -.238, .63), .055, DARK)
    export(c, "US_ABC_Fire_Extinguisher.fbx")

def hose_cabinet():
    c = begin("FireHoseCabinet")
    box("Recessed cabinet shell", (0, .12, 1.05), (1.22, .28, 1.8), RED)
    box("Cabinet dark recess", (0, -.04, 1.05), (1.0, .08, 1.55), DARK)
    for x in (-.56, .56):
        box("Cabinet side frame", (x, -.1, 1.05), (.1, .1, 1.82), RED)
    for z in (.16, 1.94):
        box("Cabinet horizontal frame", (0, -.1, z), (1.2, .1, .1), RED)
    for radius in (.18, .26, .34, .42):
        torus("Coiled fire hose", (0, -.18, 1.02), radius, .035, WHITE,
              rot=(math.pi / 2, 0, 0))
    cyl("Hose nozzle", (.34, -.23, .55), .065, .42, BRASS, rot=(0, 0, .35))
    box("Cabinet handle", (.46, -.2, 1.05), (.045, .05, .34), STEEL)
    text_mesh("FIRE HOSE", (0, -.22, 1.68), .095, WHITE)
    export(c, "US_Recessed_Fire_Hose_Cabinet.fbx")

def exit_door():
    c = begin("EmergencyExitDoor")
    box("Exit door slab", (0, .05, 1.05), (1.25, .12, 2.1), DARK)
    for x in (-.69, .69):
        box("Steel door jamb", (x, .05, 1.12), (.12, .2, 2.3), STEEL)
    box("Steel header", (0, .05, 2.23), (1.5, .2, .12), STEEL)
    box("Crash bar", (0, -.09, .92), (.9, .11, .1), STEEL, edge=.03)
    box("Exit sign housing", (0, .02, 2.55), (.72, .16, .32), WHITE, edge=.04)
    text_mesh("EXIT", (0, -.075, 2.54), .18, RED)
    for x in (-.9, .9):
        cyl("Protective bollard", (x, -.18, .48), .085, .96, YELLOW, vertices=14)
        cyl("Bollard foot", (x, -.18, .025), .16, .05, YELLOW, vertices=14)
    export(c, "US_Commercial_Emergency_Exit_Door.fbx")

def ibc_tote():
    c = begin("ChemicalIbcTote")
    box("IBC black pallet", (0, 0, .12), (1.25, 1.05, .24), DARK, edge=.04)
    box("IBC polymer tank", (0, 0, .83), (1.08, .9, 1.22), WHITE, edge=.13)
    for x in (-.58, .58):
        for y in (-.48, .48):
            box("IBC cage upright", (x, y, .88), (.035, .035, 1.38), STEEL, edge=.005)
    for z in (.34, .65, .96, 1.27, 1.5):
        for y in (-.48, .48):
            box("IBC cage horizontal", (0, y, z), (1.2, .035, .035), STEEL, edge=.005)
        for x in (-.58, .58):
            box("IBC cage side rail", (x, 0, z), (.035, .96, .035), STEEL, edge=.005)
    cyl("IBC fill cap", (0, 0, 1.51), .15, .06, DARK, vertices=16)
    cyl("IBC outlet valve", (0, -.52, .38), .105, .18, DARK, rot=(math.pi / 2, 0, 0))
    box("IBC blue valve handle", (0, -.64, .48), (.34, .08, .07), BLUE)
    export(c, "US_275_Gallon_IBC_Tote.fbx")

def eyewash_shower():
    c = begin("ChemicalEyewashShower")
    cyl("Galvanized riser", (0, 0, 1.25), .055, 2.5, STEEL)
    cyl("Floor flange", (0, 0, .035), .18, .07, STEEL, vertices=14)
    between("Shower arm", (0, 0, 2.35), (.52, 0, 2.35), .045, STEEL)
    cyl("Emergency shower head", (.52, 0, 2.23), .22, .12, GREEN, vertices=16)
    between("Eyewash supply arm", (0, 0, 1.05), (.42, 0, 1.05), .035, STEEL)
    bpy.ops.mesh.primitive_uv_sphere_add(segments=16, ring_count=8, location=(.43, 0, 1.02))
    bowl = bpy.context.object
    bowl.name, bowl.scale = "Eyewash bowl", (.28, .28, .09)
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    assign(bowl, GREEN)
    for y in (-.1, .1):
        cyl("Eyewash nozzle", (.43, y, 1.14), .035, .1, GREEN)
    box("Pull handle", (.23, 0, 1.72), (.24, .04, .18), GREEN, rot=(0, .42, 0))
    box("Emergency equipment sign", (-.2, .02, 1.82), (.42, .035, .5), GREEN)
    text_mesh("EYEWASH", (-.2, -.005, 1.83), .07, WHITE)
    export(c, "US_Emergency_Eyewash_Shower.fbx")

def flammable_cabinet():
    c = begin("ChemicalFlammableCabinet")
    box("Flammable cabinet shell", (0, 0, .95), (1.25, .62, 1.9), YELLOW, edge=.035)
    for x in (-.31, .31):
        box("Double cabinet door", (x, -.325, 1.03), (.6, .055, 1.62), YELLOW, edge=.018)
        box("Door pull", (x + (-.2 if x > 0 else .2), -.37, 1.04), (.06, .04, .27), DARK)
        for z in (.46, 1.58):
            for offset in (-.08, 0, .08):
                box("Cabinet vent slot", (x + offset, -.36, z), (.055, .025, .012), DARK, edge=.002)
    box("Raised spill sill", (0, -.34, .18), (1.16, .08, .23), YELLOW)
    box("Flammable warning plate", (0, -.37, 1.28), (.44, .025, .38), WHITE, edge=.005)
    text_mesh("FLAMMABLE", (0, -.388, 1.32), .065, RED)
    cyl("Grounding lug", (.64, .18, .3), .035, .05, BRASS, rot=(0, math.pi / 2, 0))
    export(c, "US_Flammable_Liquid_Cabinet.fbx")

def nema_panel():
    c = begin("ElectricalNemaPanel")
    box("NEMA panel enclosure", (0, .12, .95), (1.05, .34, 1.75), STEEL, edge=.035)
    box("Open panel door", (-.72, -.02, .95), (.38, .06, 1.68), STEEL,
        rot=(0, 0, -.08), edge=.025)
    box("Breaker recess", (0, -.075, 1.02), (.78, .08, 1.32), DARK)
    for row in range(8):
        for x in (-.2, .2):
            box("Circuit breaker", (x, -.13, .55 + row * .13), (.28, .06, .085), DARK, edge=.012)
    cyl("Top conduit", (0, .12, 1.98), .11, .35, STEEL)
    box("Arc flash warning plate", (0, -.13, 1.68), (.55, .025, .22), ORANGE, edge=.004)
    text_mesh("ARC FLASH", (0, -.148, 1.71), .065, DARK)
    export(c, "US_NEMA_Electrical_Panel.fbx")

def loto_station():
    c = begin("ElectricalLotoStation")
    box("LOTO station backboard", (0, .05, .7), (1.2, .1, 1.4), YELLOW, edge=.025)
    box("LOTO title strip", (0, -.02, 1.22), (1.02, .035, .18), DARK, edge=.008)
    text_mesh("LOCKOUT / TAGOUT", (0, -.046, 1.24), .075, YELLOW)
    for index, x in enumerate((-.42, -.14, .14, .42)):
        box("Red padlock body", (x, -.04, .91), (.18, .08, .22), RED, edge=.035)
        torus("Padlock shackle", (x, -.03, 1.08), .075, .018, STEEL,
              rot=(math.pi / 2, 0, 0))
        box("Danger tag", (x, -.045, .48), (.21, .025, .32), WHITE, edge=.006)
        box("Danger tag header", (x, -.062, .58), (.19, .012, .08), RED, edge=.002)
    text_mesh("DANGER", (0, -.068, .6), .045, WHITE)
    box("Hasp tray", (0, -.04, .23), (.88, .15, .12), YELLOW)
    export(c, "US_Lockout_Tagout_Station.fbx")

def disconnect_switch():
    c = begin("ElectricalDisconnect")
    box("Disconnect enclosure", (0, .08, .72), (.82, .42, 1.25), STEEL, edge=.035)
    box("Disconnect cover", (0, -.15, .72), (.7, .06, 1.08), STEEL, edge=.025)
    box("External red handle", (.46, -.14, .9), (.13, .12, .52), RED,
        rot=(0, 0, -.3), edge=.035)
    cyl("Top conduit hub", (0, .08, 1.46), .12, .32, STEEL)
    cyl("Bottom conduit hub", (0, .08, -.02), .12, .3, STEEL)
    box("Danger warning plate", (0, -.19, .58), (.42, .025, .25), WHITE, edge=.005)
    box("Danger warning header", (0, -.207, .66), (.4, .012, .075), RED, edge=.002)
    text_mesh("DANGER", (0, -.215, .68), .055, WHITE)
    torus("Lock point", (.47, -.17, .65), .055, .014, STEEL, rot=(math.pi / 2, 0, 0))
    box("Brass lock body", (.47, -.2, .55), (.13, .07, .16), BRASS, edge=.025)
    export(c, "US_Safety_Disconnect_Switch.fbx")

BUILDERS = (forklift, pallet_rack, dock_leveler, extinguisher, hose_cabinet, exit_door,
            ibc_tote, eyewash_shower, flammable_cabinet, nema_panel, loto_station,
            disconnect_switch)

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    SOURCE_OUT.mkdir(parents=True, exist_ok=True)
    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.object.delete(use_global=False)
    for collection in list(bpy.data.collections):
        bpy.data.collections.remove(collection)
    for builder in BUILDERS:
        builder()
    for index, collection in enumerate(bpy.data.collections):
        x, y = (index % 4) * 4.0, (index // 4) * 4.0
        for obj in collection.objects:
            obj.location.x += x
            obj.location.y += y
    bpy.ops.wm.save_as_mainfile(filepath=str(SOURCE_OUT / "US_Site_Safety_Props_Source.blend"))

if __name__ == "__main__":
    main()
