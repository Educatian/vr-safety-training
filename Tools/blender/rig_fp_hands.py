"""Rig a Tripo "two gloved forearms + open hands" mesh for first-person use.
blender -b --factory-startup --python rig_fp_hands.py -- in.glb out.fbx preview.png
- Splits the mesh into left/right hands (by x), normalises scale (wrist -> middle fingertip = 0.19 m).
- Finds landmarks from geometry: forearm axis (PCA), wrist, knuckles, the 5 finger clusters (k-means across the
  hand width in the finger band), thumb = the cluster farthest from the hand axis.
- Builds per side: Forearm, Hand, Thumb1-3, Index1-3, Middle1-3, Ring1-3, Pinky1-3; automatic (heat) weights.
Output objects: Hand_L / Hand_R (skinned meshes), Rig_L / Rig_R (armatures). Bone names end in _L / _R."""
import math
import sys
import bmesh
import bpy
import numpy as np
from mathutils import Vector

src, out, preview = sys.argv[-3], sys.argv[-2], sys.argv[-1]
bpy.ops.wm.read_factory_settings(use_empty=True)
bpy.ops.import_scene.gltf(filepath=src)
meshes = [o for o in bpy.context.scene.objects if o.type == "MESH"]
bpy.ops.object.select_all(action="DESELECT")
for o in meshes: o.select_set(True)
bpy.context.view_layer.objects.active = meshes[0]
if len(meshes) > 1: bpy.ops.object.join()
mesh = bpy.context.view_layer.objects.active
bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
for o in list(bpy.context.scene.objects):
    if o.type != "MESH": bpy.data.objects.remove(o)

# Web budget + fast weighting: collapse to ~40k tris total (Tripo exports ~2M).
target = 40000
if len(mesh.data.polygons) > target:
    m = mesh.modifiers.new("dec", "DECIMATE"); m.ratio = target / len(mesh.data.polygons); m.use_collapse_triangulate = True
    bpy.ops.object.modifier_apply(modifier="dec")
print("[rig] faces after decimate", len(mesh.data.polygons))
# Weld near-duplicate vertices so heat weighting has a connected surface.
bm = bmesh.new(); bm.from_mesh(mesh.data)
bmesh.ops.remove_doubles(bm, verts=bm.verts, dist=max(mesh.dimensions) * 1e-4)
bm.to_mesh(mesh.data); bm.free()

V = np.array([mesh.matrix_world @ v.co for v in mesh.data.vertices])
print("[rig] verts", len(V), "dims", mesh.dimensions[:])

# Split left/right at the widest gap along the axis with the biggest extent among x/y/z that separates two blobs.
ext = V.max(0) - V.min(0)
side_axis = int(np.argmax(ext * np.array([1, 1, 0.2])))   # hands sit side by side; prefer x/y
mid = np.median(V[:, side_axis])
hist, edges = np.histogram(V[:, side_axis], bins=80)
lo, hi = int(len(hist) * 0.3), int(len(hist) * 0.7)
gap = lo + int(np.argmin(hist[lo:hi]))
split = (edges[gap] + edges[gap + 1]) / 2
print("[rig] side axis", side_axis, "split", split)


def separate(mask_fn, name):
    bpy.ops.object.select_all(action="DESELECT")
    bpy.context.view_layer.objects.active = mesh; mesh.select_set(True)
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.select_all(action="DESELECT"); bpy.ops.object.mode_set(mode="OBJECT")
    for v in mesh.data.vertices: v.select = bool(mask_fn(mesh.matrix_world @ v.co))
    bpy.ops.object.mode_set(mode="EDIT"); bpy.ops.mesh.separate(type="SELECTED"); bpy.ops.object.mode_set(mode="OBJECT")
    new = [o for o in bpy.context.selected_objects if o != mesh][0]
    new.name = name
    return new


a = separate(lambda p: p[side_axis] < split, "HandA")
b = mesh; b.name = "HandB"
# Viewer's left in the reference image is the character's right hand (palms down, seen from above) -> decide below.

def pca(P):
    c = P.mean(0); u, s, vt = np.linalg.svd(P - c, full_matrices=False)
    return c, vt


def landmarks(obj):
    P = np.array([obj.matrix_world @ v.co for v in obj.data.vertices])
    c, vt = pca(P)
    axis = vt[0]
    t = (P - c) @ axis
    # Fingers are the narrow end: compare cross-section width at both ends.
    def width(sel): return np.ptp((P[sel] - c) @ vt[1]) if sel.sum() > 10 else 0
    lo_w = width(t < np.percentile(t, 10)); hi_w = width(t > np.percentile(t, 90))
    # The finger end is where the silhouette splits into gaps -> use point count density: sleeve end is wider & denser.
    if lo_w > hi_w: axis = -axis; t = -t   # make +axis point toward the fingertips (fingertips spread less than sleeve)
    tmin, tmax = t.min(), t.max(); L = tmax - tmin
    width_dir = vt[1]; normal = np.cross(axis, width_dir)
    w_all = (P - c) @ width_dir
    # Palm: lateral extent just behind the knuckles.
    palm = (t > tmax - 0.36 * L) & (t < tmax - 0.26 * L)
    pw_lo, pw_hi = np.percentile(w_all[palm], 3), np.percentile(w_all[palm], 97)
    # Four fingers: tip band, k-means (k=4) across the width.
    band = t > tmax - 0.17 * L
    Q = P[band]; w = w_all[band]
    ks = np.percentile(w, [12, 37, 63, 88])
    for _ in range(25):
        lab = np.argmin(np.abs(w[:, None] - ks[None, :]), 1)
        ks = np.array([w[lab == k].mean() if (lab == k).any() else ks[k] for k in range(4)])
    fingers = []
    for k in np.argsort(ks):
        F = Q[lab == k]; ft = (F - c) @ axis
        fingers.append((F[np.argmax(ft)], float(ks[k])))
    # Thumb: points beside the palm (outside its lateral range) between wrist and knuckles; pick the side with more.
    zone = (t > tmax - 0.42 * L) & (t < tmax - 0.12 * L)
    out_hi = zone & (w_all > pw_hi + 0.004); out_lo = zone & (w_all < pw_lo - 0.004)
    side = 1 if out_hi.sum() >= out_lo.sum() else -1
    T = P[out_hi if side > 0 else out_lo]
    if len(T) < 5: T = P[zone & ((w_all > pw_hi) if side > 0 else (w_all < pw_lo))]
    thumb_tip = T[np.argmax(np.abs((T - c) @ width_dir) + 0.5 * ((T - c) @ axis))]
    thumb_w = float((thumb_tip - c) @ width_dir)
    wrist_t = tmax - 0.42 * L; knuckle_t = tmax - 0.24 * L; elbow_t = tmin + 0.02 * L
    def on_axis(tt): return c + axis * tt
    return dict(c=c, axis=axis, width=width_dir, normal=normal, L=L, fingers=fingers, thumb_tip=thumb_tip, thumb_w=thumb_w, thumb_side=side,
                wrist=on_axis(wrist_t), knuckle_t=knuckle_t, elbow=on_axis(elbow_t), tmax=tmax)


def build(obj, side):
    lm = landmarks(obj)
    arm_data = bpy.data.armatures.new("Rig_" + side); rig = bpy.data.objects.new("Rig_" + side, arm_data)
    bpy.context.scene.collection.objects.link(rig)
    bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode="EDIT")
    eb = arm_data.edit_bones
    def bone(name, h, t, parent=None, roll_to=None):
        b = eb.new(name + "_" + side); b.head = Vector(h); b.tail = Vector(t)
        if parent: b.parent = parent; b.use_connect = False
        if roll_to is not None: b.align_roll(Vector(roll_to))
        return b
    fore = bone("Forearm", lm["elbow"], lm["wrist"], roll_to=lm["normal"])
    knuck_c = lm["c"] + lm["axis"] * lm["knuckle_t"]
    hand = bone("Hand", lm["wrist"], knuck_c, fore, roll_to=lm["normal"])
    # Fingers sorted across the width; the one nearest the thumb side is the index.
    fl = sorted(lm["fingers"], key=lambda f: f[1] * lm["thumb_side"])        # pinky first
    for n, (tip, fw) in zip(["Pinky", "Ring", "Middle", "Index"], fl):
        tip = np.array(tip)
        base = knuck_c + lm["width"] * fw
        seg = [(base + (tip - base) * f) for f in (0, 0.45, 0.75, 1.0)]
        p = hand
        for j in range(3): p = bone(f"{n}{j + 1}", seg[j], seg[j + 1], p, lm["normal"])
    tip = np.array(lm["thumb_tip"])
    tbase = np.array(lm["wrist"]) + (knuck_c - lm["wrist"]) * 0.25 + lm["width"] * lm["thumb_w"] * 0.45
    seg = [(tbase + (tip - tbase) * f) for f in (0, 0.4, 0.72, 1.0)]
    p = hand
    for j in range(3): p = bone(f"Thumb{j + 1}", seg[j], seg[j + 1], p, lm["normal"])
    bpy.ops.object.mode_set(mode="OBJECT")
    obj.name = "Hand_" + side
    segs = [(bb.name, np.array(rig.matrix_world @ bb.head_local), np.array(rig.matrix_world @ bb.tail_local)) for bb in rig.data.bones]
    groups = {n: obj.vertex_groups.new(name=n) for n, _, _ in segs}
    for v in obj.data.vertices:
        p = np.array(obj.matrix_world @ v.co)
        d = []
        for n, h, tl in segs:
            ab = tl - h; s = np.clip(np.dot(p - h, ab) / max(np.dot(ab, ab), 1e-9), 0, 1)
            d.append((np.linalg.norm(p - (h + ab * s)), n))
        d.sort()
        (d0, n0), (d1, n1) = d[0], d[1]
        w0, w1 = 1 / (d0 + 1e-4) ** 4, 1 / (d1 + 1e-4) ** 4
        if d1 > d0 * 2.2: w1 = 0      # far second bone (e.g. neighbouring finger): rigid to the nearest
        groups[n0].add([v.index], w0 / (w0 + w1), "REPLACE")
        if w1 > 0: groups[n1].add([v.index], w1 / (w0 + w1), "REPLACE")
    mod = obj.modifiers.new("Armature", "ARMATURE"); mod.object = rig
    obj.parent = rig
    print(f"[rig] {side}: fingers {[round(f[1], 3) for f in lm['fingers']]} thumb_w={lm['thumb_w']:.3f} side={lm['thumb_side']} groups={len(obj.vertex_groups)}")
    return rig, lm


# Normalise scale: whole model so that one hand's wrist->fingertip ~ 0.19 m.
lmA = landmarks(a)
scale = 0.19 / (lmA["L"] * 0.42)
for o in (a, b):
    o.scale = (scale,) * 3
    bpy.context.view_layer.objects.active = o; o.select_set(True)
    bpy.ops.object.transform_apply(scale=True); o.select_set(False)
# Side: with palms down seen from above, the hand whose thumb points to +side_axis is the LEFT hand.
la, lb = landmarks(a), landmarks(b)
a_thumb_w = la["thumb_w"] * la["width"][side_axis]
side_a = "R" if a_thumb_w > 0 else "L"
rigA, _ = build(a, side_a)
rigB, _ = build(b, "L" if side_a == "R" else "R")

# Preview: curl every finger toward the palm (palms face -Z here), axis = bone direction x palm normal (roll-independent).
from mathutils import Quaternion
for rig in (rigA, rigB):
    bpy.context.view_layer.objects.active = rig; bpy.ops.object.mode_set(mode="POSE")
    for pb in rig.pose.bones:
        n = pb.name
        if not any(n.startswith(k) for k in ("Index", "Middle", "Ring", "Pinky", "Thumb")): continue
        d = (rig.matrix_world.to_3x3() @ (pb.bone.tail_local - pb.bone.head_local)).normalized()
        axis_w = d.cross(Vector((0, 0, -1)))
        if axis_w.length < 1e-4: continue
        axis_l = (rig.matrix_world.to_3x3() @ pb.bone.matrix_local.to_3x3()).inverted() @ axis_w.normalized()
        pb.rotation_mode = "QUATERNION"; pb.rotation_quaternion = Quaternion(axis_l, math.radians(35 if n.startswith("Thumb") else 50))
    bpy.ops.object.mode_set(mode="OBJECT")
for o in (a, b):
    counts = {g.name: 0 for g in o.vertex_groups}
    for v in o.data.vertices:
        for ge in v.groups:
            if ge.weight > 0.3: counts[o.vertex_groups[ge.group].name] += 1
    print("[rig] weights", o.name, counts)
cam = bpy.data.objects.new("cam", bpy.data.cameras.new("cam")); bpy.context.scene.collection.objects.link(cam)
allv = np.array([o.matrix_world @ v.co for o in (a, b) for v in o.data.vertices])
cen = allv.mean(0); size = np.ptp(allv, 0).max()
cam.location = Vector(cen) + Vector((0, -size * 1.6, size * 0.9)); cam.rotation_euler = (math.radians(60), 0, 0)
bpy.context.scene.camera = cam
sun = bpy.data.objects.new("sun", bpy.data.lights.new("sun", "SUN")); bpy.context.scene.collection.objects.link(sun); sun.data.energy = 3
sc = bpy.context.scene; sc.render.resolution_x, sc.render.resolution_y = 960, 540; sc.render.filepath = preview
w = bpy.data.worlds.new("w"); sc.world = w; w.use_nodes = True; w.node_tree.nodes["Background"].inputs[0].default_value = (0.5, 0.52, 0.55, 1)
bpy.ops.render.render(write_still=True)
# Reset pose for export.
for rig in (rigA, rigB):
    for pb in rig.pose.bones: pb.rotation_quaternion = (1, 0, 0, 0); pb.rotation_euler = (0, 0, 0)
bpy.data.objects.remove(cam); bpy.data.objects.remove(sun)
# Base colour out as a 1024 px PNG for the web build (Unity material is made from it).
import os
tex_dir = os.path.join(os.path.dirname(os.path.dirname(os.path.dirname(os.path.dirname(out)))), "Textures", "FP")   # .../Art/Textures/FP
os.makedirs(tex_dir, exist_ok=True)
for img in bpy.data.images:
    if img.size[0] > 0 and img.users:
        img.scale(1024, 1024); img.filepath_raw = os.path.join(tex_dir, "T_FP_Hands.png"); img.file_format = "PNG"; img.save()
        print("[rig] texture", img.name, "->", img.filepath_raw); break
bpy.ops.export_scene.fbx(filepath=out, use_selection=False, object_types={"MESH", "ARMATURE"}, add_leaf_bones=False,
                         path_mode="STRIP", embed_textures=False, apply_scale_options="FBX_SCALE_UNITS", bake_anim=False)
print("[rig] exported", out)
