"""Tripo-rigged NPC GLB -> Unity FBX with a separate facial rig (headless Blender 5.x).

  blender -b --factory-startup -P Tools/blender/npc_face_rig.py -- <name> [--height 1.78] [--amp 1.0]

in : Tools/tripo/out/<name>/npc_<name>.glb   (Tripo auto-rig, mixamorig:* skeleton, one textured mesh)
out: Assets/_Game/Art/Models/TR-3D/NPC/SM_NPC_<Name>.fbx + T_NPC_<Name>.png (1024)
     Tools/tripo/out/<name>/face_sheet.png, body.png, face_rig.json

Face rig (separate step from the Tripo body rig): bones jaw/eye_L/eye_R under mixamorig:Head, body-mesh shape keys
blink_L blink_R smile frown surprise angry brow_up brow_down jaw_open AA EE OO FV MBP.
Landmark finding + expression shapes reuse CyberPlay tools/charpipe/face_rig.py (CHARPIPE env var overrides its dir);
blink is done here as an on-mesh lid collapse because Tripo faces are one surface with painted eyes.
"""
import argparse, json, math, os, sys
import bpy
import numpy as np
from mathutils import Matrix, Vector

CHARPIPE = os.environ.get("CHARPIPE", r"C:\Users\jewoo\Desktop\_projects\CyberPlay_Lab\tools\charpipe")
sys.path.insert(0, CHARPIPE)
import face_rig as fr_lib  # noqa: E402  (frame/landmarks/expression shapes; its main() is __name__-guarded)

ROOT = os.path.abspath(os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", ".."))
OUT_DIR = os.path.join(ROOT, "Assets", "_Game", "Art", "Models", "TR-3D", "NPC")
FEMALE = {"kiara", "tasha", "dolores"}
KEYS = ["blink_L", "blink_R", "smile", "frown", "surprise", "angry", "brow_up", "brow_down", "jaw_open",
        "AA", "EE", "OO", "FV", "MBP"]


def parse():
    ap = argparse.ArgumentParser()
    ap.add_argument("name")
    ap.add_argument("--height", type=float, default=0, help="total mesh height in m (0 = 1.80 male / 1.70 female, incl. hard hat)")
    ap.add_argument("--amp", type=float, default=1.2, help="expression strength (charpipe: 0.7 realistic, 1.3 stylized)")
    return ap.parse_args(sys.argv[sys.argv.index("--") + 1:] if "--" in sys.argv else [])


# ------------------------------------------------------------------------------------------------ import + normalise
def load(path, height):
    bpy.ops.wm.read_factory_settings(use_empty=True)
    bpy.ops.import_scene.gltf(filepath=path)
    for o in [o for o in bpy.data.objects if o.name.startswith("Icosphere")]:
        bpy.data.objects.remove(o, do_unlink=True)
    arm = next(o for o in bpy.data.objects if o.type == 'ARMATURE')
    body = next(o for o in bpy.data.objects if o.type == 'MESH' and any(m.type == 'ARMATURE' for m in o.modifiers))
    arm.name, body.name = "Armature", "Body"
    # facing: toes point the way the character faces; rotate about Z so that is Blender -Y (= Unity +Z)
    fw = Vector()
    for b in arm.data.bones:
        if "toe" in b.name.lower() and "end" not in b.name.lower():
            d = arm.matrix_world.to_3x3() @ (b.tail_local - b.head_local); d.z = 0; fw += d
    ang = -math.pi / 2 - math.atan2(fw.y, fw.x) if fw.length > 1e-6 else 0.0
    V = np.array([body.matrix_world @ v.co for v in body.data.vertices])
    s = height / np.ptp(V[:, 2])
    hips = arm.matrix_world @ arm.data.bones["mixamorig:Hips"].head_local
    arm.matrix_world = Matrix.Translation((0, 0, -V[:, 2].min() * s)) @ Matrix.Rotation(ang, 4, 'Z') @ \
        Matrix.Scale(s, 4) @ Matrix.Translation((-hips.x, -hips.y, 0)) @ arm.matrix_world
    bpy.context.view_layer.update()
    bpy.ops.object.select_all(action='DESELECT')
    for o in (arm, body):
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.object.transform_apply(location=True, rotation=True, scale=True)
    return arm, body, math.degrees(ang)


def texture(body, name):
    """Base colour -> 1024 PNG next to the FBX; drop normal/RM nodes so the FBX references only that file."""
    mat = body.data.materials[0]
    nt = mat.node_tree
    bsdf = next(n for n in nt.nodes if n.type == 'BSDF_PRINCIPLED')
    base = bsdf.inputs['Base Color'].links[0].from_node
    img = base.image
    for n in [n for n in nt.nodes if n.type in ('TEX_IMAGE', 'NORMAL_MAP', 'SEPARATE_COLOR', 'SEPRGB') and n.name != base.name]:
        nt.nodes.remove(n)
    img.scale(1024, 1024)
    png = os.path.join(OUT_DIR, f"T_NPC_{name}.png")
    img.filepath_raw = png; img.file_format = 'PNG'; img.save()
    img.name = f"T_NPC_{name}"; mat.name = f"M_NPC_{name}"
    return png


# ------------------------------------------------------------------------------------------------ landmarks
# whole-image MediaPipe on the head render (landmarks.detect() crops to the top of a white-bg silhouette, which on a
# head close-up is the hard hat); tries charpipe's brightened/equalised variants, keeps the first plausible face
MP_RUNNER = r"""
import sys, json
sys.path.insert(0, sys.argv[1]); import landmarks as L
from PIL import Image
img = Image.open(sys.argv[2]).convert("RGB"); out = {"method": "none", "points": {}}
for vname, im in L.variants(img):
    p = L.mediapipe_points(im, [0, 0, 1, 1])
    if p and p["eye_L"][0] > p["eye_R"][0] and p["nose"][1] > max(p["eye_L"][1], p["eye_R"][1]):
        out = {"method": "mediapipe" + ("" if vname == "plain" else "_" + vname), "points": p}; break
json.dump(out, open(sys.argv[3], "w"))
"""


def mediapipe_landmarks(fr, body, outdir):
    """Render the head front-on (ortho, flat texture, white bg), run charpipe landmarks.py (MediaPipe) on it and map
    the pixels back to the mesh exactly. Hard hats break the head-box proportions, so this beats proportional anchors."""
    import subprocess
    hb = fr_lib.head_box(fr)
    sc, cam = setup_render()
    sc.world.color = (1, 1, 1)
    cz, cx, S = (hb["u0"] + hb["u1"]) / 2, hb["lc"], 1.25 * (hb["u1"] - hb["u0"])
    cam.location = (cx, -3, cz); cam.data.ortho_scale = S
    img = os.path.join(outdir, "_head_front.png"); js = os.path.join(outdir, "_landmarks.json")
    render(sc, img, (800, 800))
    sc.world.color = (0.05, 0.05, 0.05)
    subprocess.run([os.path.join(CHARPIPE, ".venv", "Scripts", "python.exe"), "-c", MP_RUNNER, CHARPIPE, img, js],
                   check=True, capture_output=True)
    lm = json.load(open(js))
    if not os.environ.get("NPC_DEBUG"):
        for f in (img, js):
            os.remove(f)
    cast = fr_lib.raycaster(fr, body)
    P = {k: cast(*v)[0] for k, v in fr_lib.proportional(hb).items()}
    pts = lm.get("points") or {}
    if not lm.get("method", "").startswith("mediapipe") or "eye_L" not in pts:
        return P, "proportional(" + lm.get("method", "none") + ")", hb
    for k, (x, y) in pts.items():                              # left axis = world +X = image right
        P[k] = cast(cx + (x - 0.5) * S, cz + (0.5 - y) * S)[0]
    return P, lm["method"], hb


def densify(body, fr, P):
    """Tripo faces are ~5 mm triangles: an eye opening is barely one row. Subdivide eye bands (2 cuts) and the mouth
    (1 cut) so lid/lip shape keys have vertices to move; UVs and skin weights are interpolated by bmesh."""
    import bmesh
    d = abs(P["eye_L"][0] - P["eye_R"][0])
    LU = np.stack([fr["L"], fr["U"]], 1)
    front = fr["F"] > (P["nose"][2] - 1.0 * d)
    M = (P["mouth_L"] + P["mouth_R"]) / 2
    def near(c, rl, ru):
        return front & (((LU[:, 0] - c[0]) / rl) ** 2 + ((LU[:, 1] - c[1]) / ru) ** 2 < 1)
    eyes = near(P["eye_L"], 0.45 * d, 0.4 * d) | near(P["eye_R"], 0.45 * d, 0.4 * d)
    mouth = near(M, 0.55 * d, 0.35 * d)
    before = len(body.data.polygons)
    bm = bmesh.new(); bm.from_mesh(body.data)
    for mask, cuts in ((eyes, 2), (mouth, 1)):
        bm.verts.ensure_lookup_table()
        sel = {bm.verts[int(i)] for i in np.nonzero(mask)[0]} if len(bm.verts) == len(mask) else None
        if sel is None:                                         # after the first pass: re-test by position
            sel = set()
            for v in bm.verts:
                l, u, f = v.co @ Vector(fr["left"]), v.co @ Vector(fr["up"]), v.co @ Vector(fr["front"])
                if f > P["nose"][2] - d and ((l - M[0]) / (0.55 * d)) ** 2 + ((u - M[1]) / (0.35 * d)) ** 2 < 1:
                    sel.add(v)
        edges = [e for e in bm.edges if e.verts[0] in sel and e.verts[1] in sel]
        bmesh.ops.subdivide_edges(bm, edges=edges, cuts=cuts, use_grid_fill=True)
    bmesh.ops.triangulate(bm, faces=[f for f in bm.faces if len(f.verts) > 4])
    bm.to_mesh(body.data); bm.free(); body.data.update()
    return len(body.data.polygons) - before


# ------------------------------------------------------------------------------------------------ face rig
def rigidify_face(body, fr, P, d):
    """Front-of-head verts from brow to chin that are mostly Head-weighted: make them 100 % Head (no neck drift)."""
    hb = fr["hbn"]
    fam = {hb} | {c.name for c in fr["hb"].children_recursive}
    hg = body.vertex_groups[hb]
    eye_u = (P["eye_L"][1] + P["eye_R"][1]) / 2
    m = (fr["W"] > 0.5) & (fr["U"] > P["chin"][1] - 0.1 * d) & (fr["F"] > (P["eye_L"][2] - 1.2 * d))
    n = 0
    for i in np.nonzero(m)[0]:
        v = body.data.vertices[int(i)]
        other = [g for g in v.groups if body.vertex_groups[g.group].name not in fam and g.weight > 0]
        if other:
            for g in other:
                body.vertex_groups[g.group].remove([v.index])
            hg.add([v.index], 1.0, 'REPLACE'); n += 1
    return n, m


def blink_offsets(fr, P, d, side):
    """Upper-lid collapse: verts from the lid crease down to the lower lid slide down so lid skin covers the eye."""
    L, U, F = fr["L"], fr["U"], fr["F"]
    c = P[f"eye_{side}"]
    top_eye = P[f"eye_{side}_top"][1] + 0.02 * d if f"eye_{side}_top" in P else c[1] + 0.10 * d
    lo = P[f"eye_{side}_bot"][1] if f"eye_{side}_bot" in P else c[1] - 0.10 * d
    crease = top_eye + 0.22 * d
    wl = 0.30 * d                                                      # eye half-width
    hmask = fr_lib.smooth((wl * 1.25 - np.abs(L - c[0])) / (0.3 * wl))
    fmask = fr_lib.smooth((F - (c[2] - 0.35 * d)) / (0.1 * d)) * np.clip(fr["W"], 0, 1)
    band = (U >= lo) & (U <= crease)
    t = np.clip((U - top_eye) / (crease - top_eye), 0, 1)             # eye rows -> 0, crease -> 1
    new_u = lo + t * (crease - lo)
    du = np.where(band, new_u - U, 0.0) * hmask * fmask
    df = np.where(band, 0.04 * d * (1 - t), 0.0) * hmask * fmask     # lid bulges slightly forward over the eyeball
    return du, df


def build_face(arm, body, fr, P, hb, amp):
    # Tripo leaves small loose shards near the eyes that charpipe's eye_islands() takes for eyeballs (offset ~2 cm
    # from the painted irises on Dale); eyes here are painted, so eye bones stay unweighted look-at bones
    fr_lib.eye_islands = lambda *_: None
    keys, eye_mode, J, _ = fr_lib.build_mesh_face(arm, body, fr, P, hb, amp)
    sk = body.data.shape_keys.key_blocks
    for k in [k.name for k in sk if k.name != "Basis" and k.name not in KEYS and not k.name.startswith("viseme_")]:
        body.shape_key_remove(sk[k])
    for k in sk:
        if k.name.startswith("viseme_"):
            k.name = k.name[len("viseme_"):]
    d = abs(P["eye_L"][0] - P["eye_R"][0])
    Mw = np.array(body.matrix_world.to_3x3().inverted())
    basis = np.array([v.co for v in body.data.vertices])
    for side in "LR":
        du, df = blink_offsets(fr, P, d, side)
        world = np.outer(du, fr["up"]) + np.outer(df, fr["front"])
        k = body.shape_key_add(name=f"blink_{side}", from_mix=False)
        k.data.foreach_set("co", (basis + world @ Mw.T).ravel())
    hat = hat_mask(body, fr, P, d)
    for k in body.data.shape_keys.key_blocks[1:]:              # brow/lid moves must not bend the hard-hat brim
        co = np.array([v.co for v in k.data])
        k.data.foreach_set("co", (basis + (co - basis) * (1 - hat)[:, None]).ravel())
    # order keys like KEYS (Unity lists blendshapes in file order)
    bpy.context.view_layer.objects.active = body
    for name in reversed(KEYS):
        body.active_shape_key_index = body.data.shape_keys.key_blocks.find(name)
        bpy.ops.object.shape_key_move(type='TOP')                  # TOP = first after the Basis key
    # eye bones (painted eyes -> unweighted look-at bones at the iris, parented to Head)
    if eye_mode != "bones":
        to_arm = arm.matrix_world.inverted()
        bpy.context.view_layer.objects.active = arm
        bpy.ops.object.mode_set(mode='EDIT')
        for side in "LR":
            p = P[f"eye_{side}"]
            w = Vector(fr["left"] * p[0] + fr["up"] * p[1] + fr["front"] * (p[2] - 0.012))
            e = arm.data.edit_bones.new(f"eye_{side}"); e.head = to_arm @ w
            e.tail = e.head + to_arm.to_3x3() @ Vector(fr["front"] * 0.03)
            e.parent = arm.data.edit_bones[fr["hbn"]]
        bpy.ops.object.mode_set(mode='OBJECT')
    return eye_mode, J


def hat_mask(body, fr, P, d):
    """1 for hard-hat verts above the eyes: vertex colour (texture at its UVs) close to the crown colour."""
    me = body.data
    img = next(n.image for n in me.materials[0].node_tree.nodes if n.type == 'TEX_IMAGE')
    w, h = img.size
    px = np.array(img.pixels[:], np.float32).reshape(h, w, 4)[..., :3]
    uv = np.zeros(len(me.loops) * 2); me.uv_layers.active.data.foreach_get("uv", uv); uv = uv.reshape(-1, 2)
    vi = np.zeros(len(me.loops), int); me.loops.foreach_get("vertex_index", vi)
    c = px[np.clip((uv[:, 1] % 1) * h, 0, h - 1).astype(int), np.clip((uv[:, 0] % 1) * w, 0, w - 1).astype(int)]
    col = np.zeros((len(me.vertices), 3)); np.add.at(col, vi, c)
    col /= np.maximum(np.bincount(vi, minlength=len(me.vertices)), 1)[:, None]
    U = fr["U"]; head = fr["W"] > 0.5
    crown = head & (U > U[head].max() - 0.04)
    hat_col = np.median(col[crown], 0)
    de = fr_lib.delta_e(np.clip(col, 0, 1), hat_col)
    eye_u = (P["eye_L"][1] + P["eye_R"][1]) / 2
    above = fr_lib.smooth((U - (eye_u + 0.15 * d)) / (0.1 * d))
    # geometry: brim/overhang verts have another surface right behind them (skin verts look into the skull)
    from mathutils.bvhtree import BVHTree
    bvh = BVHTree.FromPolygons([tuple(v) for v in fr["V"]], [tuple(p.vertices) for p in me.polygons])
    back = Vector(-fr["front"]); geo = np.zeros(len(U))
    for i in np.nonzero(above > 0)[0]:
        hit = bvh.ray_cast(Vector(fr["V"][i]) + back * 1e-3, back, 0.04)
        geo[i] = hit[0] is not None
    return np.maximum(fr_lib.smooth((25 - de) / 10), geo) * above


def blink_skin_check(fr, body, P):
    """With blink_L/R = 1, the surface seen at each eye centre must be skin (median delta E to cheek/brow skin < 20)."""
    d = abs(P["eye_L"][0] - P["eye_R"][0])
    kb = body.data.shape_keys.key_blocks
    out = {}
    for side in "LR":
        c = P[f"eye_{side}"]
        closed = np.array([v.co for v in kb[f"blink_{side}"].data])
        tex_open = fr_lib.texture_sampler(fr, body)
        tex_closed = fr_lib.texture_sampler(dict(fr, V=closed, F=closed @ fr["front"]), body)
        cheek = [tex_open(c[0] + a * d, c[1] - 0.4 * d) for a in (-0.1, 0, 0.1)]
        brow = [tex_open(c[0] + a * d, c[1] + 0.3 * d) for a in (-0.1, 0, 0.1)]
        skin = np.median(np.array([x for x in cheek + brow if x is not None]), 0)
        pts = [(c[0] + a * d, c[1] + b * d) for a in (-0.12, 0, 0.12) for b in (-0.04, 0, 0.04)]
        de_o = [float(fr_lib.delta_e(tex_open(*p), skin)) for p in pts if tex_open(*p) is not None]
        de_c = [float(fr_lib.delta_e(tex_closed(*p), skin)) for p in pts if tex_closed(*p) is not None]
        out[side] = dict(open_dE=round(float(np.median(de_o)), 1), closed_dE=round(float(np.median(de_c)), 1),
                         closed_max_dE=round(float(np.max(de_c)), 1))
    out["pass"] = all(out[s]["closed_dE"] < 20 for s in "LR")          # < 20: a closed lash line may remain
    return out


def drift_check(arm, body, fr, mask):
    """Pose spine/neck/head through a turn + nod; face verts must stay within 5 mm in head-bone space."""
    hb = arm.pose.bones[fr["hbn"]]
    idx = np.nonzero(mask)[0]

    def local():
        dg = bpy.context.evaluated_depsgraph_get()
        ev = body.evaluated_get(dg); me = ev.to_mesh()
        inv = np.array((arm.matrix_world @ hb.matrix).inverted())
        co = np.array([me.vertices[int(i)].co for i in idx])
        ev.to_mesh_clear()
        return co @ inv[:3, :3].T + inv[:3, 3]
    rest = local()
    worst = 0.0
    for rots in ({"mixamorig:Neck": (20, 0, 15), "mixamorig:Head": (-15, 25, 0)},
                 {"mixamorig:Spine2": (10, -15, 0), "mixamorig:Neck": (-15, 0, -20), "mixamorig:Head": (25, -30, 10)}):
        for n, r in rots.items():
            pb = arm.pose.bones[n]; pb.rotation_mode = 'XYZ'; pb.rotation_euler = [math.radians(x) for x in r]
        bpy.context.view_layer.update()
        worst = max(worst, float(np.linalg.norm(local() - rest, axis=1).max()))
        for pb in arm.pose.bones:
            pb.rotation_euler = (0, 0, 0)
    bpy.context.view_layer.update()
    return round(worst * 1000, 2)


# ------------------------------------------------------------------------------------------------ QA renders
def setup_render():
    sc = bpy.context.scene
    sc.render.engine = 'BLENDER_WORKBENCH'
    sh = sc.display.shading
    sh.light = 'FLAT'; sh.color_type = 'TEXTURE'; sh.show_specular_highlight = False
    sc.display.shading.show_cavity = False
    sc.render.film_transparent = False
    sc.world = sc.world or bpy.data.worlds.new("W")
    if sc.camera:
        return sc, sc.camera
    sc.render.image_settings.file_format = 'PNG'
    cam = bpy.data.objects.new("Cam", bpy.data.cameras.new("Cam")); sc.collection.objects.link(cam)
    cam.data.type = 'ORTHO'; cam.rotation_euler = (math.pi / 2, 0, 0)    # looks along +Y at a -Y-facing character
    sc.camera = cam
    return sc, cam


def render(sc, path, res):
    sc.render.resolution_x, sc.render.resolution_y = res
    sc.render.filepath = path
    bpy.ops.render.render(write_still=True)
    im = bpy.data.images.load(path); a = np.array(im.pixels[:]).reshape(res[1], res[0], 4); bpy.data.images.remove(im)
    return a


def qa(body, P, fr, outdir):
    sc, cam = setup_render()
    kb = body.data.shape_keys.key_blocks
    d = abs(P["eye_L"][0] - P["eye_R"][0])
    eye = (P["eye_L"] + P["eye_R"]) / 2
    ctr = fr["left"] * eye[0] + fr["up"] * (eye[1] - 0.3 * d)
    cam.location = (ctr[0], -3, ctr[2]); cam.data.ortho_scale = 3.2 * d
    tiles = []
    tmp = os.path.join(outdir, "_tile.png")
    for name, vals in (("neutral", {}), ("blink", {"blink_L": 1, "blink_R": 1}), ("smile", {"smile": 1}),
                       ("jaw_open", {"jaw_open": 1}), ("angry", {"angry": 1}), ("surprise", {"surprise": 1})):
        for k in kb[1:]:
            k.value = vals.get(k.name, 0.0)
        tiles.append(render(sc, tmp, (400, 400)))
    for k in kb[1:]:
        k.value = 0.0
    sheet = np.concatenate([np.concatenate(tiles[3:], 1), np.concatenate(tiles[:3], 1)], 0)  # rows bottom-up: first 3 on top
    img = bpy.data.images.new("sheet", sheet.shape[1], sheet.shape[0]); img.pixels.foreach_set(sheet.ravel().astype(np.float32))
    img.filepath_raw = os.path.join(outdir, "face_sheet.png"); img.file_format = 'PNG'; img.save()
    V = fr["V"]
    cam.location = (0, -4, V[:, 2].max() / 2); cam.data.ortho_scale = V[:, 2].max() * 1.1
    render(sc, os.path.join(outdir, "body.png"), (600, 900))
    os.remove(tmp)


# ------------------------------------------------------------------------------------------------ export
def export(arm, body, fbx):
    kd = body.data.shape_keys
    for k in kd.key_blocks[1:]:
        k.value = 0.0
    bpy.ops.object.select_all(action='DESELECT')
    for o in (arm, body):
        o.select_set(True)
    bpy.context.view_layer.objects.active = arm
    bpy.ops.export_scene.fbx(filepath=fbx, use_selection=True, object_types={'ARMATURE', 'MESH'},
                             apply_scale_options='FBX_SCALE_ALL', add_leaf_bones=False, bake_anim=False,
                             use_mesh_modifiers=False, mesh_smooth_type='FACE', path_mode='STRIP', embed_textures=False,
                             armature_nodetype='NULL')


def main():
    a = parse()
    name = a.name.lower(); Name = name.capitalize()
    src_dir = os.path.join(ROOT, "Tools", "tripo", "out", name)
    height = a.height or (1.70 if name in FEMALE else 1.80)
    os.makedirs(OUT_DIR, exist_ok=True)
    arm, body, rot = load(os.path.join(src_dir, f"npc_{name}.glb"), height)
    png = texture(body, Name)
    arm.data.pose_position = 'REST'; bpy.context.view_layer.update()
    fr = fr_lib.frame(arm, body)
    P, method, hb = mediapipe_landmarks(fr, body, src_dir)
    d = abs(P["eye_L"][0] - P["eye_R"][0])
    added = densify(body, fr, P)
    fr = fr_lib.frame(arm, body)
    n_rigid, face_mask = rigidify_face(body, fr, P, d)
    fr = fr_lib.frame(arm, body)                                   # weights changed
    eye_mode, J = build_face(arm, body, fr, P, hb, a.amp)
    arm.data.pose_position = 'POSE'
    info = dict(name=Name, height_m=round(float(np.ptp(fr["V"][:, 2])), 3), rotated_deg=round(rot, 1), landmarks=method,
                head_bone=fr["hbn"], eye_distance_m=round(float(d), 4), rigidified_verts=n_rigid, faces_added=added,
                shape_keys=[k.name for k in body.data.shape_keys.key_blocks[1:]],
                bones_added=["jaw", "eye_L", "eye_R"], eyes=eye_mode, tris=sum(len(p.vertices) - 2 for p in body.data.polygons),
                anchors={k: [round(float(x), 4) for x in v] for k, v in P.items()})
    info["blink_check"] = blink_skin_check(fr, body, P)
    info["drift_mm"] = drift_check(arm, body, fr, face_mask)
    qa(body, P, fr, src_dir)
    fbx = os.path.join(OUT_DIR, f"SM_NPC_{Name}.fbx")
    export(arm, body, fbx)
    info.update(fbx=fbx, png=png)
    json.dump(info, open(os.path.join(src_dir, "face_rig.json"), "w"), indent=1)
    print("NPC_FACE_OK", json.dumps({k: info[k] for k in ("name", "height_m", "landmarks", "blink_check", "drift_mm", "shape_keys")}))


if __name__ == "__main__":
    main()
