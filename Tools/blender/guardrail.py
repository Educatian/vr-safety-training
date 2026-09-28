"""Additive training-geometry prototype. NOT a certified fall-protection design.
Run with Blender 4.2+ --background --factory-startup --python <this file> -- [--render]
Writes new B-PROC candidate assets only; refuses overwrites unless --overwrite is explicit.
No existing scene, source .blend, assessment logic or Unity settings are modified.
"""
from pathlib import Path
import argparse
import json
import math
import sys
import bpy
from mathutils import Vector

TOP = 42 * 0.0254  # OSHA 1926.502(b)(1): top EDGE, not rail center.
MID = TOP / 2     # OSHA 1926.502(b)(2)(i): midway above walking surface.
WIDTH = 2.0      # Overall plate-to-plate width; authored, not a regulatory span.
POST_X = 0.91
TOE_TOP = 3.5 * 0.0254  # Top edge measured above the working surface.
TOE_GAP = 0.005         # Authored clearance, less than the 1/4-inch maximum.
DEPTH = 0.18     # Authored plate geometry, no anchorage/strength verification.
THICK = 0.05     # Authored tube exterior, not a verified member specification.
VARIANTS = ('clean', 'service_worn', 'missing_midrail', 'missing_midrail_worn')


def material(name, color, metallic, roughness):
    mat = bpy.data.materials.new(name)
    mat.use_nodes = True
    shader = mat.node_tree.nodes.get('Principled BSDF')
    shader.inputs['Base Color'].default_value = (*color, 1)
    shader.inputs['Metallic'].default_value = metallic
    shader.inputs['Roughness'].default_value = roughness
    mat.diffuse_color = (*color, 1)
    mat.metallic, mat.roughness = metallic, roughness
    return mat


def box(collection, name, center, dimensions, mat, bevel=0.002):
    bpy.ops.mesh.primitive_cube_add(size=1, location=center)
    obj = bpy.context.object
    obj.name = name
    obj.dimensions = dimensions
    bpy.ops.object.transform_apply(location=False, rotation=False, scale=True)
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)
    obj.data.materials.append(mat)
    if bevel:
        modifier = obj.modifiers.new('Small fabricated edge', 'BEVEL')
        modifier.width = min(bevel, min(dimensions) / 8)
        modifier.segments = 2
        bpy.ops.object.modifier_apply(modifier=modifier.name)
    return obj


def bolt(collection, x, y, mat):
    bpy.ops.mesh.primitive_cylinder_add(vertices=6, radius=0.009,
                                      depth=0.008, location=(x, y, 0.014))
    obj = bpy.context.object
    obj.name = 'Visual bolt head - anchorage unverified'
    for owner in list(obj.users_collection):
        owner.objects.unlink(obj)
    collection.objects.link(obj)
    obj.data.materials.append(mat)


def build(scene, variant, steel, worn, dark):
    collection = bpy.data.collections.new('SM_Guardrail_' + variant)
    scene.collection.children.link(collection)
    finish = worn if variant in ('service_worn', 'missing_midrail_worn') else steel
    for x in (-POST_X, POST_X):
        box(collection, 'Base plate - geometry only', (x, 0, 0.005),
            (0.18, DEPTH, 0.01), finish)
        box(collection, 'Post', (x, 0, (TOP + 0.01) / 2),
            (THICK, THICK, TOP - 0.01), finish)
        for dx in (-0.06, 0.06):
            for dy in (-0.06, 0.06):
                bolt(collection, x + dx, dy, dark)
    box(collection, 'Top rail - top edge 42 inches', (0, 0, TOP - THICK / 2),
        (2 * POST_X - THICK, THICK, THICK), finish)
    if not variant.startswith('missing_midrail'):
        box(collection, 'Midrail - midway', (0, 0, MID),
            (2 * POST_X - THICK, THICK, THICK), finish)
    box(collection, 'Solid toeboard - source-grounded top edge',
        (0, -0.035, (TOE_TOP + TOE_GAP) / 2),
        (2 * POST_X, 0.015, TOE_TOP - TOE_GAP), finish, bevel=0.001)
    clamp_heights = [TOP - THICK / 2]
    if not variant.startswith('missing_midrail'):
        clamp_heights.append(MID)
    for x in (-POST_X, POST_X):
        for z in clamp_heights:
            box(collection, 'Illustrative clamp sleeve - connection unverified',
                (x, 0, z), (0.065, 0.065, 0.045), finish, bevel=0.001)
        box(collection, 'Illustrative toeboard bracket',
            (x, -0.045, 0.04), (0.065, 0.018, 0.07), finish, bevel=0.001)
    # Fine, geometry-based scuffs survive FBX export; no baked-in lighting or rust holes.
    # All are inside the nominal overall bounds. Cosmetic wear is not a safe/unsafe cue.
    if variant in ('service_worn', 'missing_midrail_worn'):
        for i in range(8):
            box(collection, 'Cosmetic scuff', (-0.72 + i * 0.2, -0.0252, TOP - 0.027),
                (0.025 + (i % 3) * 0.01, 0.0003, 0.0015), dark, bevel=0)
    collection['representation'] = 'training geometry; NOT strength/anchorage certified'
    collection['variant'] = variant
    collection['top_edge_m'] = TOP
    collection['midrail_center_m'] = MID
    collection['toeboard_top_m'] = TOE_TOP
    collection['toeboard_gap_m'] = TOE_GAP
    collection['source'] = 'https://www.osha.gov/laws-regs/regulations/standardnumber/1926/1926.502'
    return collection


def bounds(collection):
    points = [obj.matrix_world @ Vector(corner)
              for obj in collection.objects if obj.type == 'MESH'
              for corner in obj.bound_box]
    low = Vector(tuple(min(p[i] for p in points) for i in range(3)))
    high = Vector(tuple(max(p[i] for p in points) for i in range(3)))
    return low, high


def component_measurements(collection, variant):
    def find(prefix):
        return [obj for obj in collection.objects if obj.name.startswith(prefix)]

    def z_bounds(obj):
        values = [(obj.matrix_world @ Vector(corner)).z for corner in obj.bound_box]
        return min(values), max(values)

    top = find('Top rail -')
    toe = find('Solid toeboard -')
    mid = find('Midrail -')
    if len(top) != 1 or len(toe) != 1:
        raise RuntimeError('Expected exactly one top rail and solid toeboard')
    if len(mid) != (0 if variant.startswith('missing_midrail') else 1):
        raise RuntimeError('Midrail presence does not match variant: ' + variant)
    top_low, top_high = z_bounds(top[0])
    toe_low, toe_high = z_bounds(toe[0])
    if abs(top_high - TOP) > 0.00001 or abs(toe_high - TOE_TOP) > 0.00001 or abs(toe_low - TOE_GAP) > 0.00001:
        raise RuntimeError('Measured component edges disagree with recipe: ' + variant)
    midpoint = None
    if mid:
        mid_low, mid_high = z_bounds(mid[0])
        midpoint = (mid_low + mid_high) / 2
        if abs(midpoint - MID) > 0.00001:
            raise RuntimeError('Measured midrail center is not at recipe midpoint')
    return {'top_edge_m': top_high, 'midrail_present': bool(mid),
            'midrail_center_m': midpoint, 'toeboard_top_m': toe_high, 'toeboard_gap_m': toe_low}


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument('--output-root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--render', action='store_true')
    parser.add_argument('--overwrite', action='store_true')
    args = parser.parse_args(sys.argv[sys.argv.index('--') + 1:] if '--' in sys.argv else [])
    if bpy.app.version < (4, 2, 0):
        raise RuntimeError('Blender 4.2 or newer is required; no outputs were written.')
    engine = 'BLENDER_EEVEE' if bpy.app.version >= (5, 0, 0) else 'BLENDER_EEVEE_NEXT'
    # Preflight the engine before exports, so unsupported builds fail without partial output.
    preflight_scene = bpy.data.scenes.new('Realism_Engine_Preflight')
    try:
        preflight_scene.render.engine = engine
    finally:
        bpy.data.scenes.remove(preflight_scene)
    root = args.output_root.resolve()
    models = root / 'Assets/_Game/Art/Models/B-PROC'
    sources = root / 'Tools/blender/source'
    captures = root / 'Captures/Props/guardrail'
    source_file = sources / 'SM_Guardrail_Source.blend'
    report_file = captures / 'geometry-report.json'
    destinations = [source_file, report_file] + [models / ('SM_Guardrail_' + v + '.fbx') for v in VARIANTS]
    # Keep a separate capture directory: all 12 frames are the service-worn variant only.
    if args.render:
        destinations += [captures / ('shot_%02d.png' % i) for i in range(12)]
    existing = [str(p) for p in destinations if p.exists()]
    if existing and not args.overwrite:
        raise RuntimeError('Refusing to overwrite existing outputs: ' + ', '.join(existing))
    for directory in (models, sources, captures):
        directory.mkdir(parents=True, exist_ok=True)
    # Create a new scene rather than deleting objects from an opened source file.
    scene = bpy.data.scenes.new('Realism_Guardrail_Review')
    bpy.context.window.scene = scene
    scene.unit_settings.system = 'METRIC'
    scene.unit_settings.scale_length = 1
    steel = material('Galvanized steel - illustrative', (0.42, 0.46, 0.49), 0.8, 0.4)
    worn = material('Service-worn galvanized steel - illustrative', (0.37, 0.40, 0.42), 0.75, 0.58)
    dark = material('Bolt and shallow scuff steel', (0.15, 0.17, 0.18), 0.75, 0.48)
    collections = [build(scene, v, steel, worn, dark) for v in VARIANTS]
    bpy.context.view_layer.update()
    reports = []
    for variant, collection in zip(VARIANTS, collections):
        low, high = bounds(collection)
        measured_components = component_measurements(collection, variant)
        triangles = 0
        for obj in collection.objects:
            obj.data.calc_loop_triangles()
            triangles += len(obj.data.loop_triangles)
        size = high - low
        if max(abs(size[i] - (WIDTH, DEPTH, TOP)[i]) for i in range(3)) > 0.001:
            raise RuntimeError('Unexpected bounds for ' + variant + ': ' + str(tuple(size)))
        if triangles > 40000:
            raise RuntimeError('Project 40,000-triangle hero-prop budget exceeded')
        bpy.ops.object.select_all(action='DESELECT')
        for obj in collection.objects:
            obj.select_set(True)
        bpy.ops.export_scene.fbx(filepath=str(models / ('SM_Guardrail_' + variant + '.fbx')),
            use_selection=True, object_types={'MESH'}, apply_unit_scale=True,
            apply_scale_options='FBX_SCALE_ALL', axis_forward='-Z', axis_up='Y',
            use_mesh_modifiers=True, bake_anim=False, add_leaf_bones=False, path_mode='AUTO')
        reports.append({'variant': variant, 'blender_size_xyz_m': list(size),
                        'triangles': triangles, 'measured_components': measured_components,
                        'strength_verified': False,
                        'anchorage_verified': False, 'scenario_bound': False})
    for collection in collections:
        collection.hide_render = collection != collections[1]
        collection.hide_viewport = collection != collections[1]
    world = bpy.data.worlds.new('Neutral review world')
    scene.world = world
    world.use_nodes = True
    world.node_tree.nodes['Background'].inputs['Color'].default_value = (0.35, 0.4, 0.45, 1)
    world.node_tree.nodes['Background'].inputs['Strength'].default_value = 0.6
    bpy.ops.object.light_add(type='AREA', location=(1, -3, 4))
    light = bpy.context.object
    light.data.energy, light.data.shape, light.data.size = 700, 'DISK', 4
    light.rotation_euler = (Vector((0, 0, 0.5)) - light.location).to_track_quat('-Z', 'Y').to_euler()
    bpy.ops.object.camera_add(location=(3, -3, 2))
    camera = bpy.context.object
    camera.data.type = 'ORTHO'
    camera.data.ortho_scale = 3.1
    scene.camera = camera
    scene.render.engine = engine
    scene.render.resolution_x, scene.render.resolution_y = 1280, 960
    scene.render.resolution_percentage = 100
    scene.render.image_settings.file_format = 'PNG'
    shots = []
    for level, elevation in enumerate((0.45, 1.25, 2.2)):
        for azimuth in range(4):
            index = level * 4 + azimuth
            angle = math.radians(45 + azimuth * 90)
            camera.location = (3.2 * math.cos(angle), 3.2 * math.sin(angle), elevation)
            target = Vector((0, 0, TOP / 2))
            camera.rotation_euler = (target - camera.location).to_track_quat('-Z', 'Y').to_euler()
            shots.append({'id': index, 'position_blender_m': list(camera.location),
                          'target_blender_m': list(target), 'variant': 'service_worn'})
            if args.render:
                scene.render.filepath = str(captures / ('shot_%02d.png' % index))
                bpy.ops.render.render(write_still=True)
    report_file.write_text(json.dumps({'geometry_only': True, 'variants': reports,
        'rendered': args.render, 'shots': shots}, indent=2), encoding='utf-8')
    bpy.ops.wm.save_as_mainfile(filepath=str(source_file))
    print('Created isolated training guardrails. Not installed in any Unity scene.')


if __name__ == '__main__':
    main()
