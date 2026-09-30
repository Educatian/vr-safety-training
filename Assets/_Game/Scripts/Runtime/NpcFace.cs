using UnityEngine;

namespace Jobsite.Runtime
{
    // Face layer for the Tripo NPCs (shape keys from Tools/blender/npc_face_rig.py): auto-blink, situational
    // expressions and viseme cycling while the NPC is speaking. Body gestures stay in CrewGestures, which drives this.
    public sealed class NpcFace : MonoBehaviour
    {
        public enum Mood { Neutral, Smile, Frown, Surprise, Angry, BrowUp }

        private SkinnedMeshRenderer face;
        private int blinkL = -1, blinkR = -1, jaw = -1;
        private int[] moods;          // index per Mood (Neutral unused)
        private int[] visemes;
        private float nextBlink, blinkT = -1, moodUntil, speakUntil, nextViseme;
        private Mood mood; private int viseme = -1;
        private readonly float[] moodW = new float[6];
        // Ambient mood from the bounded affect state (CrewAffect band); situational expressions override it briefly.
        public Mood Ambient { get; set; } = Mood.Neutral;
        public Mood Current => Time.time < moodUntil ? mood : Ambient;
        public bool HasFace => face != null;

        private void Awake()
        {
            foreach (var r in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if (r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("blink_L") >= 0) { face = r; break; }
            if (face == null) { enabled = false; return; }
            var m = face.sharedMesh;
            int I(string n) => m.GetBlendShapeIndex(n);
            blinkL = I("blink_L"); blinkR = I("blink_R"); jaw = I("jaw_open");
            moods = new[] { -1, I("smile"), I("frown"), I("surprise"), I("angry"), I("brow_up") };
            visemes = new[] { I("AA"), I("EE"), I("OO"), I("MBP"), I("FV") };
            nextBlink = Time.time + Random.Range(1f, 4f);
            BuildEyes();
        }

        public void Express(Mood m, float seconds = 2.5f) { mood = m; moodUntil = Time.time + seconds; }
        public void Speak(float seconds) => speakUntil = Time.time + seconds;

        private void LateUpdate()
        {
            // Blink: ~0.14 s close-open every 2.5-6 s (starting values; real adults blink ~15-20/min).
            if (blinkT < 0 && Time.time >= nextBlink) blinkT = 0;
            var blink = 0f;
            if (blinkT >= 0)
            {
                blinkT += Time.deltaTime;
                blink = Mathf.Clamp01(1f - Mathf.Abs(blinkT / 0.07f - 1f));
                if (blinkT > 0.14f) { blinkT = -1; nextBlink = Time.time + Random.Range(2.5f, 6f); }
            }
            Set(blinkL, blink * 100f); Set(blinkR, blink * 100f);
            // Overlay eyes close with the lids (the shape key collapses the painted eye underneath).
            var open = 1f - blink;
            if (Current == Mood.Surprise) open *= 1.15f; else if (Current == Mood.Angry || Current == Mood.Frown) open *= 0.8f;
            foreach (var (t, baseScale) in eyes) { t.localScale = new Vector3(baseScale.x, baseScale.y * Mathf.Max(0.02f, open), baseScale.z); t.gameObject.SetActive(open > 0.08f); }

            // Moods ease in and out.
            for (var i = 1; i < moods.Length; i++)
            {
                var target = Current == (Mood)i ? (i == (int)Mood.BrowUp ? 60f : 70f) : 0f;
                moodW[i] = Mathf.MoveTowards(moodW[i], target, Time.deltaTime * 300f);
                Set(moods[i], moodW[i]);
            }

            // Speech: a new viseme every ~90 ms with the jaw following.
            var speaking = Time.time < speakUntil;
            if (speaking && Time.time >= nextViseme) { viseme = Random.Range(0, visemes.Length); nextViseme = Time.time + Random.Range(0.07f, 0.12f); }
            for (var i = 0; i < visemes.Length; i++) Set(visemes[i], speaking && i == viseme ? 80f : 0f);
            Set(jaw, speaking ? (viseme == 3 ? 0f : 35f + Mathf.Sin(Time.time * 20f) * 15f) : 0f);
        }

        private void Set(int index, float w) { if (index >= 0) face.SetBlendShapeWeight(index, w); }

        // ---- Overlay eyes -------------------------------------------------------------------------------------------
        // Tripo faces are one textured surface with the eyes painted at 1024 px for the whole body, so up close they read
        // as smudges (playtest 2026-09-29: "the characters' eyes aren't done properly"). A small almond eye (sclera,
        // iris, pupil, highlight, lash line) sits on each eye bone the face rig added (eye_L / eye_R at the MediaPipe eye
        // landmarks), just proud of the skin, and closes with the blink.
        private readonly System.Collections.Generic.List<(Transform t, Vector3 baseScale)> eyes = new System.Collections.Generic.List<(Transform, Vector3)>();
        private static Mesh eyeQuad;
        private static readonly System.Collections.Generic.Dictionary<int, Material> eyeMats = new System.Collections.Generic.Dictionary<int, Material>();
        static readonly Color[] Irises = { new Color(.36f, .22f, .12f), new Color(.2f, .13f, .08f), new Color(.43f, .33f, .17f), new Color(.33f, .43f, .52f) };
        public int EyeCount => eyes.Count;
        public string EyeDebug { get; private set; } = "";

        private void BuildEyes()
        {
            Transform l = null, r = null;
            foreach (var t in GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "eye_L" || t.name.EndsWith(":eye_L")) l = t;
                else if (t.name == "eye_R" || t.name.EndsWith(":eye_R")) r = t;
            }
            if (l == null || r == null) return;
            var d = Vector3.Distance(l.position, r.position);
            if (d < 0.03f || d > 0.12f) return;          // not a face-scale rig: leave the painted eyes alone
            // Facing from the rig itself (this component can sit on an FBX child whose axes are rotated): head bone -> the
            // midpoint between the eyes, flattened.
            Transform head = null;
            foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name.EndsWith("Head") && !t.name.Contains("Top")) { head = t; break; }
            var mid = (l.position + r.position) * 0.5f;
            var fwd = head != null ? mid - head.position : transform.forward; fwd.y = 0f;
            if (fwd.sqrMagnitude < 1e-6f) { fwd = Vector3.Cross(r.position - l.position, Vector3.up); fwd.y = 0f; }
            fwd.Normalize();
            EyeDebug = $"head={(head != null ? head.name : "none")} fwd={fwd} eyeDist={d:0.000}";
            var w = d * 0.37f; var h = w * 0.35f;
            var iris = (name.GetHashCode() & 0x7fffffff) % Irises.Length;
            var mat = EyeMaterial(iris);
            // The eye bones sit at the eyeball centre, inside the head; find the skin just in front of each one on the baked
            // mesh (the furthest-forward vertex within the eye's footprint) and sit the overlay 1.5 mm proud of it.
            var baked = new Mesh(); face.BakeMesh(baked, true);
            var verts = baked.vertices; var toWorld = Matrix4x4.TRS(face.transform.position, face.transform.rotation, Vector3.one);
            var world = new Vector3[verts.Length]; for (var i = 0; i < verts.Length; i++) world[i] = toWorld.MultiplyPoint3x4(verts[i]);
            Destroy(baked);
            float Surface(Vector3 at)
            {
                var best = float.NegativeInfinity; var reach = w * 0.35f;
                foreach (var v in world)
                {
                    var o = v - at; var ahead = Vector3.Dot(o, fwd);
                    if (ahead < -0.01f || ahead > 0.04f) continue;
                    if ((o - fwd * ahead).sqrMagnitude > reach * reach) continue;
                    if (ahead > best) best = ahead;
                }
                return float.IsNegativeInfinity(best) ? 0.004f : best;
            }
            var depths = "";
            foreach (var bone in new[] { l, r })
            {
                var ahead = Surface(bone.position); depths += $" {ahead * 1000f:0.0}mm";
                var go = new GameObject("EyeOverlay", typeof(MeshFilter), typeof(MeshRenderer));
                go.GetComponent<MeshFilter>().sharedMesh = Quad();
                var mr = go.GetComponent<MeshRenderer>(); mr.sharedMaterial = mat;
                mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; mr.receiveShadows = false;
                go.transform.SetPositionAndRotation(bone.position + fwd * (ahead + 0.0015f), Quaternion.LookRotation(-fwd, Vector3.up));
                go.transform.localScale = new Vector3(w, h, 1f);
                go.transform.SetParent(bone, true);
                eyes.Add((go.transform, go.transform.localScale));
            }
            EyeDebug += " skin ahead of eye bones:" + depths;
        }

        private static Mesh Quad()
        {
            if (eyeQuad != null) return eyeQuad;
            eyeQuad = new Mesh { name = "EyeQuad" };
            eyeQuad.vertices = new[] { new Vector3(-.5f, -.5f, 0), new Vector3(.5f, -.5f, 0), new Vector3(-.5f, .5f, 0), new Vector3(.5f, .5f, 0) };
            eyeQuad.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };
            eyeQuad.triangles = new[] { 0, 2, 1, 2, 3, 1 };
            var tone = new Color(.74f, .72f, .71f);   // unlit: tone down to sit in the lit skin
            eyeQuad.colors = new[] { tone, tone, tone, tone };
            eyeQuad.RecalculateNormals(); eyeQuad.RecalculateBounds();
            return eyeQuad;
        }

        private static Material EyeMaterial(int iris)
        {
            if (eyeMats.TryGetValue(iris, out var m) && m != null) return m;
            const int W = 128, H = 48;
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear, name = "EyeOverlay" + iris };
            var px = new Color[W * H];
            var ic = Irises[iris];
            for (var y = 0; y < H; y++)
            for (var x = 0; x < W; x++)
            {
                float u = (x + .5f) / W * 2f - 1f, v = (y + .5f) / H * 2f - 1f;                 // -1..1
                // Almond: the upper lid arches higher than the lower one; soft 1.5 px edge.
                var lid = Mathf.Pow(Mathf.Max(0f, 1f - u * u), 0.75f) * (v > 0 ? 0.98f : 0.8f);
                var edge = (lid - Mathf.Abs(v)) * H * 0.5f;
                var a = Mathf.Clamp01(edge / 2.5f);
                if (a <= 0f) { px[y * W + x] = new Color(0, 0, 0, 0); continue; }
                var c = new Color(.84f, .8f, .76f);                                              // sclera (never pure white)
                c *= Mathf.Lerp(0.62f, 1f, Mathf.Clamp01((lid - v) * 2.2f));                    // shadow under the upper lid
                c *= Mathf.Lerp(0.8f, 1f, Mathf.Clamp01(edge / 4f));                             // corners and rims fall off
                // Iris (slightly above centre, partly under the upper lid), limbal ring, pupil, highlight.
                float ix = x + .5f - W * .5f, iy = y + .5f - H * .54f;
                var rr = Mathf.Sqrt(ix * ix + iy * iy); const float R = 17.5f;
                if (rr < R)
                {
                    var ring = Mathf.Clamp01((rr - R * 0.72f) / (R * 0.28f));
                    var col = Color.Lerp(ic * 1.15f, ic * 0.45f, ring);
                    col = Color.Lerp(col, new Color(.03f, .03f, .03f), Mathf.Clamp01((R * 0.36f - rr) / 1.2f));   // pupil
                    c = Color.Lerp(c, col, Mathf.Clamp01((R - rr) / 1.2f));
                }
                float hx = ix + 5f, hy = iy - 5f;
                if (hx * hx + hy * hy < 9f) c = Color.Lerp(c, Color.white, 0.85f);
                // Lash line along the upper lid.
                if (v > 0 && edge < 3.2f) c = Color.Lerp(c, new Color(.12f, .08f, .06f), Mathf.Clamp01((3.2f - edge) / 2f));
                c.a = a;
                px[y * W + x] = c;
            }
            tex.SetPixels(px); tex.Apply(false, true);
            m = new Material(Shader.Find("Sprites/Default")) { mainTexture = tex, name = "EyeOverlay" + iris, renderQueue = 2450 };
            eyeMats[iris] = m;
            return m;
        }
    }
}
