using UnityEngine;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // First-person gloved hand holding the rugged tablet (Tools/blender/fp_tablet_hands.py). The tablet UI stays in
    // screen space (readable, tested); this model sits exactly behind the UI's screen rect so the bezel and bumpers
    // frame it, and both rise together from below when the tablet opens. While carrying a control kit, the right glove
    // shows holding it.
    public sealed class FirstPersonTablet : MonoBehaviour
    {
        // Pinch grip on the tablet's right edge (playtest feedback 2026-09-29: "thumb and index should hold it").
        // Directions are in the tablet's frame: Tilt leans the fingers from straight up toward the tablet's centre,
        // Roll turns the palm toward the viewer. Curls are degrees per joint. EdgeY is the grip height on the right edge
        // (-1 bottom .. 1 top); Web pushes the thumb-index web onto the edge (fraction of the tablet thickness).
        [System.Serializable]
        public struct GripPose
        {
            public float Tilt, Roll, Thumb, Index, Others, EdgeY, Web;
            public GripPose(float tilt, float roll, float thumb, float index, float others, float edgeY, float web)
            { Tilt = tilt; Roll = roll; Thumb = thumb; Index = index; Others = others; EdgeY = edgeY; Web = web; }
            public override string ToString() => $"tilt {Tilt} roll {Roll} thumb {Thumb} index {Index} others {Others} edgeY {EdgeY} web {Web}";
        }
        public static readonly GripPose DefaultGrip = new GripPose(20f, 0f, 30f, 15f, 60f, -0.35f, 0.6f);

        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private RectTransform screenRect;     // FieldTablet content area
        [SerializeField] private RectTransform frame;          // 2D frame art (hidden: the 3D tablet is the frame)
        [SerializeField] private float distance = 0.45f;
        [SerializeField] private GameObject handsPrefab;       // Tripo gloved hands, rigged (Tools/blender/rig_fp_hands.py)
        [SerializeField] private Material handsMaterial;
        // One-handed grip (playtest feedback 2026-09-29: two big gloves hid too much of the view). The right glove holds
        // the tablet's right edge; the left glove is hidden. HandScale shrinks the gloves relative to the tablet.
        [SerializeField] private bool oneHanded = true;
        [SerializeField] private float handScale = 0.78f;
        [SerializeField] private GripPose grip = DefaultGrip;
        [SerializeField] private bool autoGrip = true;      // search a clean pinch at start (see SearchPinch)
        [SerializeField] private float handRoom = 0.1f;     // fraction of the view width kept free right of the tablet for the hand
        // Panel size caps (playtest feedback 2026-09-29: "smaller, don't cover so much of the view"): the whole device,
        // bumpers included, stays within these fractions of the view, and sits low (held at chest height).
        [SerializeField] private float maxHeightFrac = 0.7f;
        [SerializeField] private float maxWidthFrac = 0.42f;

        private Camera cam;
        private ShiftDirector director;
        private Transform model, tablet, screen, gloveL, gloveR, rigged;
        private Quaternion fix = Quaternion.identity;
        private Vector3 screenOffsetUnit;     // camera-space offset root -> screen centre at scale 1
        private float screenWidthUnit = 1f, screenHeightUnit = 1f, bodyWidthUnit = 1f, bodyHeightUnit = 1f;
        private float raise;                  // 0 lowered .. 1 in view
        private Vector2 frameBase;
        private Canvas canvas;
        private Vector3 gloveOffset;
        // Layout as authored, restored before every refit (the fit depends on the camera's pixel size).
        private bool fitSaved;
        private Vector2 frameOrig, aMinOrig, aMaxOrig, offMinOrig, offMaxOrig;
        private Vector3 frameScaleOrig;
        private int fitW, fitH;

        public void Configure(GameObject prefab, RectTransform uiScreen, RectTransform uiFrame, GameObject hands = null, Material handsMat = null)
        { modelPrefab = prefab; screenRect = uiScreen; frame = uiFrame; handsPrefab = hands; handsMaterial = handsMat; }
        public float Raise => raise;
        public GripPose Grip => grip;
        public string LastGripReport { get; private set; } = "";

        private void Start()
        {
            cam = GetComponent<Camera>();
            director = FindFirstObjectByType<ShiftDirector>();
            if (modelPrefab == null || cam == null) return;
            model = Instantiate(modelPrefab, transform).transform;
            model.name = "FP_TabletHands";
            foreach (Transform t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Tablet") tablet = t; else if (t.name == "Screen") screen = t;
                else if (t.name == "Glove_L") gloveL = t; else if (t.name == "Glove_R") gloveR = t;
            }
            foreach (var r in model.GetComponentsInChildren<Renderer>(true)) { r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false; }
            foreach (var c in model.GetComponentsInChildren<Collider>(true)) Destroy(c);
            canvas = screenRect != null ? screenRect.GetComponentInParent<Canvas>() : null;
            if (frame != null)
            {
                frameBase = frame.anchoredPosition;
                var img = frame.GetComponent<UnityEngine.UI.Image>();
                if (img != null) img.color = Color.clear;
            }
            Calibrate();
            FitPanel();
            if (oneHanded && gloveL != null) { gloveL.gameObject.SetActive(false); gloveL = null; }
            if (handsPrefab != null) RigHands();
        }

        // Re-pose the glove (grip tuning captures). Auto = search a pinch that satisfies the contact constraints.
        public void Regrip(GripPose pose, bool auto = true)
        {
            grip = pose; autoGrip = auto;
            if (model == null || handsPrefab == null) return;
            if (rigged != null) { rigged.gameObject.SetActive(false); Destroy(rigged.gameObject); rigged = null; }
            RigHands();
        }

        // Geometry of the tablet in model-local space, measured once per rig.
        private struct TabletFrame
        {
            public Vector3 Centre, Right, Up, Back, Inward;
            public float HalfW, HalfH, HalfT, ScreenHalfW;
        }

        // One hand's bones plus its rest pose, so a pose can be tried, measured and undone.
        private sealed class HandRig
        {
            public string S;
            public Transform Rig, Hand, Mid;
            public System.Collections.Generic.Dictionary<string, Transform> Bones = new System.Collections.Generic.Dictionary<string, Transform>();
            public Vector3 RestPos, RestScale; public Quaternion RestRot;
            public System.Collections.Generic.List<(Transform t, Quaternion q)> RestBones = new System.Collections.Generic.List<(Transform, Quaternion)>();
            public void Reset()
            {
                Rig.localPosition = RestPos; Rig.localRotation = RestRot; Rig.localScale = RestScale;
                foreach (var (t, q) in RestBones) t.localRotation = q;
            }
        }

        // ---- Tripo rigged hand: pinch the tablet's right edge, thumb on the front bezel, index behind ----
        private void RigHands()
        {
            var inst = Instantiate(handsPrefab, model, false);
            inst.name = "FP_RiggedHands";
            rigged = inst.transform;
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (handsMaterial != null) smr.sharedMaterial = handsMaterial;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.updateWhenOffscreen = true;
            }
            var tf = MeasureTablet();
            var report = "";
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var s = smr.name.EndsWith("_L") ? "_L" : "_R";
                if (oneHanded && s == "_L") { smr.gameObject.SetActive(false); continue; }
                var h = new HandRig { S = s };
                foreach (var b in smr.bones) if (b != null) h.Bones[b.name] = b;
                if (!h.Bones.TryGetValue("Hand" + s, out h.Hand) || !h.Bones.TryGetValue("Middle1" + s, out h.Mid) || !h.Bones.TryGetValue("Forearm" + s, out var fore)) continue;
                h.Rig = fore.parent;
                h.RestPos = h.Rig.localPosition; h.RestRot = h.Rig.localRotation; h.RestScale = h.Rig.localScale;
                foreach (var b in h.Bones.Values) if (b != h.Rig) h.RestBones.Add((b, b.localRotation));
                var sideSign = s == "_L" ? -1f : 1f;
                tf.Inward = -tf.Right * sideSign;
                if (oneHanded && h.Bones.ContainsKey("Thumb2" + s) && h.Bones.ContainsKey("Index1" + s))
                {
                    var pose = autoGrip ? SearchPinch(h, tf, grip, out _) : grip;
                    var shift = PinchWithCentring(h, tf, pose, 0f);
                    report = $"{pose} shift {shift:0.00} -> {Measure(h, tf, out _)}";
                }
                else TwoHandPose(h, tf, sideSign);
                if (s == "_L") gloveL = smr.transform; else gloveR = smr.transform;
            }
            if (oneHanded) gloveL = null;
            foreach (var n in new[] { "Glove_L", "Glove_R" })
            {
                var old = model.Find(n); if (old != null) old.gameObject.SetActive(false);
            }
            LastGripReport = report;
            if (report.Length > 0) Debug.Log($"[Grip] {report}");
        }

        private TabletFrame MeasureTablet()
        {
            var inv = Quaternion.Inverse(fix);
            var f = new TabletFrame { Right = inv * Vector3.right, Up = inv * Vector3.up, Back = inv * Vector3.back };
            var tb = tablet.GetComponent<MeshFilter>().sharedMesh.bounds;
            f.Centre = model.InverseTransformPoint(tablet.TransformPoint(tb.center));
            for (var i = 0; i < 8; i++)
            {
                var corner = model.InverseTransformPoint(tablet.TransformPoint(tb.center + Vector3.Scale(tb.extents, Sign(i))));
                f.HalfW = Mathf.Max(f.HalfW, Mathf.Abs(Vector3.Dot(corner - f.Centre, f.Right)));
                f.HalfH = Mathf.Max(f.HalfH, Mathf.Abs(Vector3.Dot(corner - f.Centre, f.Up)));
                f.HalfT = Mathf.Max(f.HalfT, Mathf.Abs(Vector3.Dot(corner - f.Centre, f.Back)));
            }
            // The body without the corner bumpers is what the thumb rests on: use the display width + the bezel.
            f.ScreenHalfW = f.HalfW * 0.8f;
            var smf = screen != null ? screen.GetComponent<MeshFilter>() : null;
            if (smf != null && smf.sharedMesh != null)
            {
                var sbnd = smf.sharedMesh.bounds; f.ScreenHalfW = 0f;
                for (var i = 0; i < 8; i++)
                {
                    var corner = model.InverseTransformPoint(screen.TransformPoint(sbnd.center + Vector3.Scale(sbnd.extents, Sign(i))));
                    f.ScreenHalfW = Mathf.Max(f.ScreenHalfW, Mathf.Abs(Vector3.Dot(corner - f.Centre, f.Right)));
                }
            }
            return f;
        }

        private static Vector3 Sign(int i) => new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1);

        // Orient, curl and place the hand for a pose; Shift moves it front/back in half-thicknesses.
        private void Pinch(HandRig h, TabletFrame f, GripPose g, float shift)
        {
            h.Reset();
            var s = h.S;
            h.Rig.localScale = h.RestScale * handScale;
            Vector3 fingerW = (h.Mid.position - h.Hand.position).normalized;
            Vector3 acrossW = (h.Bones["Index1" + s].position - h.Bones["Pinky1" + s].position).normalized;
            Vector3 palmW = (s == "_R" ? Vector3.Cross(fingerW, acrossW) : Vector3.Cross(acrossW, fingerW)).normalized;
            // Palm faces the edge, fingers run up (tilted in) behind the tablet, so the thumb points at the viewer and
            // lies on the front bezel once curled. Roll turns the palm toward the viewer.
            var t = g.Tilt * Mathf.Deg2Rad; var r = g.Roll * Mathf.Deg2Rad;
            var fL = (f.Up * Mathf.Cos(t) + f.Inward * Mathf.Sin(t)).normalized;
            var p0 = (f.Inward * Mathf.Cos(t) - f.Up * Mathf.Sin(t)).normalized;
            var pL = (p0 * Mathf.Cos(r) + f.Back * Mathf.Sin(r)).normalized;
            var rot = Quaternion.LookRotation(model.TransformDirection(fL), model.TransformDirection(pL)) * Quaternion.Inverse(Quaternion.LookRotation(fingerW, palmW));
            h.Rig.rotation = rot * h.Rig.rotation;
            var palmNow = rot * palmW;
            foreach (var fn in new[] { "Index", "Middle", "Ring", "Pinky" })
                for (var j = 1; j <= 3; j++)
                    if (h.Bones.TryGetValue(fn + j + s, out var b))
                        Curl(b, palmNow, (fn == "Index" ? g.Index : g.Others) * (j == 1 ? 1f : j == 2 ? 1.15f : 0.75f));
            for (var j = 1; j <= 3; j++)
                if (h.Bones.TryGetValue("Thumb" + j + s, out var th)) Curl(th, palmNow, g.Thumb * (j == 1 ? 0.5f : 1f));
            // The thumb-index web sits on the edge, pushed in by Web x thickness, at EdgeY height.
            var web = (h.Bones["Thumb2" + s].position + h.Bones["Index1" + s].position) * 0.5f;
            var edge = model.TransformPoint(f.Centre - f.Inward * f.HalfW + f.Up * f.HalfH * g.EdgeY + f.Inward * f.HalfT * 2f * g.Web + f.Back * f.HalfT * shift);
            h.Rig.position += edge - web;
        }

        // Pose, then shift front/back so the thumb and index straddle the tablet evenly. Returns the total shift.
        private float PinchWithCentring(HandRig h, TabletFrame f, GripPose g, float shift)
        {
            Pinch(h, f, g, shift);
            Measure(h, f, out var m);
            var extra = Mathf.Clamp(-(m.Thumb + m.Index) * 0.5f, -2f, 2f);
            Pinch(h, f, g, shift + extra);
            return shift + extra;
        }

        private struct Contact { public float Thumb, Index, ThumbU, ThumbV, Cost; }

        // Where the tips are. Depth in half-thicknesses (+1 front face, -1 back face); ThumbU across the right bezel
        // (0 = display edge, 1 = body edge); Cost = how badly the pinch constraints are broken (0 = clean).
        private string Measure(HandRig h, TabletFrame f, out Contact c)
        {
            var s = h.S;
            float D(Vector3 w) => Vector3.Dot(model.InverseTransformPoint(w) - f.Centre, f.Back) / Mathf.Max(1e-5f, f.HalfT);
            float X(Vector3 w) => Vector3.Dot(model.InverseTransformPoint(w) - f.Centre, -f.Inward);
            float Y(Vector3 w) => Vector3.Dot(model.InverseTransformPoint(w) - f.Centre, f.Up) / Mathf.Max(1e-5f, f.HalfH);
            var thumb = Tip(h.Bones, "Thumb", s); var index = Tip(h.Bones, "Index", s);
            c = new Contact { Thumb = D(thumb), Index = D(index) };
            var bez = Mathf.Max(1e-5f, f.HalfW - f.ScreenHalfW);
            c.ThumbU = (X(thumb) - f.ScreenHalfW) / bez; c.ThumbV = Y(thumb);
            float cost = 0f;
            void Below(float v, float lo) { if (v < lo) cost += (lo - v) * (lo - v); }
            void Above(float v, float hi) { if (v > hi) cost += (v - hi) * (v - hi); }
            Below(c.Thumb, 1.15f); Above(c.Thumb, 2.2f);          // thumb pad on the front face, not floating off it
            Above(c.Index, -1.1f);                                  // index behind the back face
            // Thumb on the bezel strip: past the display edge (the UI draws over anything on the display) and inside the
            // body edge (the corner bumpers stick out further, so the body edge sits at ~2/3 of this range).
            Below(c.ThumbU, 0.12f); Above(c.ThumbU, 0.55f);
            Above(Mathf.Abs(c.ThumbV), 0.9f);
            // Other fingertips and the palm must not pass through the tablet: behind it, or outside the edge.
            foreach (var fn in new[] { "Middle", "Ring", "Pinky" })
            {
                var p = Tip(h.Bones, fn, s);
                if (X(p) < f.HalfW) Above(D(p), -1.0f);
            }
            var palm = (h.Hand.position + h.Mid.position) * 0.5f;
            if (X(palm) < f.HalfW && Mathf.Abs(D(palm)) < 1.2f) cost += 1f + (f.HalfW - X(palm)) / bez;
            c.Cost = cost;
            return $"thumb {c.Thumb:0.00} (bezel {c.ThumbU:0.00}, y {c.ThumbV:0.00})  index {c.Index:0.00}  cost {cost:0.000}  (front face = +1, back face = -1)";
        }

        // Grid search around the authored pose for a pinch that breaks no contact constraint, preferring a relaxed hand.
        private GripPose SearchPinch(HandRig h, TabletFrame f, GripPose baseline, out float bestShift)
        {
            var best = baseline; var bestScore = float.MaxValue; bestShift = 0f;
            foreach (var tilt in new[] { 0f, 10f, 20f, 30f, 40f })
            foreach (var roll in new[] { -20f, -10f, 0f, 10f, 20f, 30f })
            foreach (var thumb in new[] { -30f, -15f, 0f, 15f, 30f, 45f })
            foreach (var index in new[] { 5f, 15f, 25f })
            foreach (var web in new[] { 0.2f, 0.6f, 1.0f })
            {
                var g = new GripPose(tilt, roll, thumb, index, baseline.Others, baseline.EdgeY, web);
                var shift = PinchWithCentring(h, f, g, 0f);
                Measure(h, f, out var c);
                var relax = 0.0003f * ((tilt - 20f) * (tilt - 20f) + roll * roll + (thumb - 30f) * (thumb - 30f)) / 100f + 0.02f * Mathf.Abs(shift);
                var score = c.Cost * 10f + relax;
                if (score < bestScore) { bestScore = score; best = g; bestShift = 0f; }
            }
            return best;
        }

        private void TwoHandPose(HandRig h, TabletFrame f, float sideSign)
        {
            var s = h.S;
            h.Rig.localScale = h.RestScale * handScale;
            Vector3 fingerW = (h.Mid.position - h.Hand.position).normalized;
            Vector3 acrossW = (h.Bones["Index1" + s].position - h.Bones["Pinky1" + s].position).normalized;
            Vector3 palmW = (s == "_R" ? Vector3.Cross(fingerW, acrossW) : Vector3.Cross(acrossW, fingerW)).normalized;
            var inward = -f.Right * sideSign;
            var tFinger = model.TransformDirection((f.Up * 0.9f + inward * 0.3f).normalized);
            var tPalm = model.TransformDirection((inward * 0.8f - f.Back * 0.6f).normalized);
            var rot = Quaternion.LookRotation(tFinger, tPalm) * Quaternion.Inverse(Quaternion.LookRotation(fingerW, palmW));
            h.Rig.rotation = rot * h.Rig.rotation;
            var palmNow = rot * palmW;
            foreach (var fn in new[] { "Index", "Middle", "Ring", "Pinky" })
                for (var j = 1; j <= 3; j++)
                    if (h.Bones.TryGetValue(fn + j + s, out var b)) Curl(b, palmNow, j == 1 ? 62f : j == 2 ? 70f : 45f);
            for (var j = 1; j <= 3; j++)
                if (h.Bones.TryGetValue("Thumb" + j + s, out var th)) Curl(th, palmNow, j == 1 ? 8f : 22f);
            var edge = model.TransformPoint(f.Centre + f.Right * sideSign * f.HalfW - f.Up * f.HalfH * 0.18f);
            var palmCentre = (h.Hand.position + h.Mid.position) * 0.5f;
            h.Rig.position += edge - palmCentre - tPalm * 0.02f * model.lossyScale.x;
        }

        private static Vector3 Tip(System.Collections.Generic.Dictionary<string, Transform> bones, string finger, string s)
        {
            if (!bones.TryGetValue(finger + "3" + s, out var last)) return Vector3.zero;
            if (last.childCount > 0) return last.GetChild(0).position;
            return bones.TryGetValue(finger + "2" + s, out var prev) ? last.position + (last.position - prev.position) * 0.8f : last.position;
        }

        // Bend a finger bone toward the palm about the axis (bone direction x palm normal); roll-independent.
        private static void Curl(Transform bone, Vector3 palm, float degrees)
        {
            var child = bone.childCount > 0 ? bone.GetChild(0).position : bone.position + bone.up * 0.02f;
            var dir = (child - bone.position).normalized;
            var axis = Vector3.Cross(dir, palm);
            if (axis.sqrMagnitude < 1e-6f) return;
            bone.rotation = Quaternion.AngleAxis(degrees, axis.normalized) * bone.rotation;
        }

        // Orient so the Screen mesh faces the camera (upright), then measure its size and offset at scale 1.
        private void Calibrate()
        {
            if (screen == null) return;
            // The display is a flat quad: its thin mesh axis is the viewing normal, its long in-plane axis is "up"
            // (portrait). Using body->screen centres instead tilted the device ~18 deg (the back strap and the screen's
            // off-centre placement skew that vector), which made the 2D panel and the 3D display disagree.
            var sb = screen.GetComponent<Renderer>().bounds.center;
            var tbc = tablet != null ? tablet.GetComponent<Renderer>().bounds.center : model.position;
            Vector3 nWorld, uWorld;
            var mf = screen.GetComponent<MeshFilter>();
            if (mf != null && mf.sharedMesh != null)
            {
                var sz = mf.sharedMesh.bounds.size;
                int thin = sz.x <= sz.y && sz.x <= sz.z ? 0 : sz.y <= sz.z ? 1 : 2;
                int a = (thin + 1) % 3, c = (thin + 2) % 3;
                int longAxis = sz[a] >= sz[c] ? a : c;
                nWorld = screen.TransformDirection(Axis(thin));
                uWorld = screen.TransformDirection(Axis(longAxis));
                if (Vector3.Dot(nWorld, sb - tbc) < 0f) nWorld = -nWorld;
                if (Vector3.Dot(model.InverseTransformDirection(uWorld), Vector3.up) < 0f) uWorld = -uWorld;
            }
            else
            {
                nWorld = (sb - tbc).sqrMagnitude > 1e-8f ? (sb - tbc) : -model.forward;
                uWorld = model.up;
            }
            var nLocal = model.InverseTransformDirection(nWorld).normalized;
            var upLocal = Vector3.ProjectOnPlane(model.InverseTransformDirection(uWorld), nLocal).normalized;
            fix = Quaternion.Inverse(Quaternion.LookRotation(-nLocal, upLocal));
            model.localScale = Vector3.one; model.localPosition = Vector3.forward; model.localRotation = fix;
            var b = screen.GetComponent<Renderer>().bounds;
            screenWidthUnit = Mathf.Abs(Vector3.Dot(b.size, transform.right));
            if (screenWidthUnit < 1e-4f) screenWidthUnit = b.size.magnitude * 0.6f;
            screenHeightUnit = Mathf.Max(1e-4f, Mathf.Abs(Vector3.Dot(b.size, transform.up)));
            // Whole device (body + bumpers, no gloves) so the panel can be placed with the bezel fully on screen.
            var body = new Bounds(b.center, b.size);
            foreach (var r in model.GetComponentsInChildren<MeshRenderer>(true))
                if (!r.name.Contains("Glove")) body.Encapsulate(r.bounds);
            bodyWidthUnit = Mathf.Max(screenWidthUnit, Mathf.Abs(Vector3.Dot(body.size, transform.right)));
            bodyHeightUnit = Mathf.Max(screenHeightUnit, Mathf.Abs(Vector3.Dot(body.size, transform.up)));
            screenOffsetUnit = transform.InverseTransformPoint(b.center) - model.localPosition;
            if (gloveR != null) gloveOffset = model.InverseTransformPoint(gloveR.GetComponent<Renderer>().bounds.center);
        }

        private static Vector3 Axis(int i) => i == 0 ? Vector3.right : i == 1 ? Vector3.up : Vector3.forward;

        // Make the 2D panel the exact shape of the 3D display and keep the whole device on screen, so the UI reads as
        // being ON the tablet instead of floating over it (playtest feedback 2026-09-29). Runs again whenever the
        // camera's pixel size changes (browser resize, fullscreen).
        private void FitPanel()
        {
            if (screenRect == null || frame == null || cam == null) return;
            if (!fitSaved)
            {
                fitSaved = true;
                frameOrig = frameBase; frameScaleOrig = frame.localScale;
                aMinOrig = screenRect.anchorMin; aMaxOrig = screenRect.anchorMax; offMinOrig = screenRect.offsetMin; offMaxOrig = screenRect.offsetMax;
            }
            fitW = cam.pixelWidth; fitH = cam.pixelHeight;
            frame.localScale = frameScaleOrig; frame.anchoredPosition = frameOrig;
            screenRect.anchorMin = aMinOrig; screenRect.anchorMax = aMaxOrig; screenRect.offsetMin = offMinOrig; screenRect.offsetMax = offMaxOrig;
            Canvas.ForceUpdateCanvases();
            var fw = frame.rect.width; var fh = frame.rect.height;
            if (fw < 1f || fh < 1f) { frameBase = frame.anchoredPosition; return; }

            // 1) Display aspect (portrait): keep the authored width, set the height; narrow instead if it would not fit.
            var aspect = screenHeightUnit / screenWidthUnit;
            float ax0 = aMinOrig.x, ax1 = aMaxOrig.x;
            var hf = (ax1 - ax0) * fw * aspect / fh;
            if (hf > 0.96f)
            {
                hf = 0.96f;
                var cx = (ax0 + ax1) / 2f; var wf = hf * fh / aspect / fw;
                ax0 = cx - wf / 2f; ax1 = cx + wf / 2f;
            }
            var cy = Mathf.Clamp((aMinOrig.y + aMaxOrig.y) / 2f, hf / 2f, 1f - hf / 2f);
            screenRect.anchorMin = new Vector2(ax0, cy - hf / 2f); screenRect.anchorMax = new Vector2(ax1, cy + hf / 2f);
            screenRect.offsetMin = screenRect.offsetMax = Vector2.zero;

            // 2) Size in pixels: the whole device (bumpers included) stays within maxHeightFrac of the view height and
            //    maxWidthFrac of its width so the site stays visible.
            var margin = 0.03f * fitH;
            PanelPixels(out var p0, out var p1);
            float wPx = p1.x - p0.x, hPx = p1.y - p0.y;
            if (wPx < 1f || hPx < 1f) { frameBase = frame.anchoredPosition; return; }
            var bodyWPx = wPx * bodyWidthUnit / screenWidthUnit; var bodyHPx = hPx * bodyHeightUnit / screenHeightUnit;
            var k = Mathf.Min(1f, Mathf.Min(Mathf.Min(fitH - 2f * margin, fitH * maxHeightFrac) / bodyHPx, fitW * maxWidthFrac / bodyWPx));
            frame.localScale = frameScaleOrig * k;
            Canvas.ForceUpdateCanvases();

            // 3) Nudge in pixels: hand room on the right, device resting near the bottom edge.
            PanelPixels(out p0, out p1); wPx = p1.x - p0.x; hPx = p1.y - p0.y;
            var ex = (bodyWidthUnit / screenWidthUnit - 1f) * 0.5f * wPx; var ey = (bodyHeightUnit / screenHeightUnit - 1f) * 0.5f * hPx;
            var dx = Mathf.Min(0f, fitW - (oneHanded ? Mathf.Max(margin, handRoom * fitW) : margin) - (p1.x + ex));   // room for the hand on the right
            var dy = margin - (p0.y - ey);   // bottom bumpers just above the bottom edge: the upper view stays clear
            var pxPerUnit = PixelsPerParentUnit();
            if (pxPerUnit > 1e-4f) frame.anchoredPosition += new Vector2(dx, dy) / pxPerUnit;
            frameBase = frame.anchoredPosition;
        }

        private Camera UiCam => canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;

        private void PanelPixels(out Vector2 p0, out Vector2 p1)
        {
            var corners = new Vector3[4]; screenRect.GetWorldCorners(corners);
            p0 = RectTransformUtility.WorldToScreenPoint(UiCam, corners[0]); p1 = RectTransformUtility.WorldToScreenPoint(UiCam, corners[2]);
        }

        private float PixelsPerParentUnit()
        {
            var parent = frame.parent;
            if (parent == null) return 1f;
            var a = RectTransformUtility.WorldToScreenPoint(UiCam, parent.TransformPoint(Vector3.zero));
            var b = RectTransformUtility.WorldToScreenPoint(UiCam, parent.TransformPoint(Vector3.right * 100f));
            return (b - a).magnitude / 100f;
        }

        private void LateUpdate()
        {
            if (model == null || director == null) return;
            if (cam.pixelWidth != fitW || cam.pixelHeight != fitH) FitPanel();
            var open = director.MenuOpen && !PauseMenu.Paused;
            raise = Application.isBatchMode ? (open ? 1f : 0f)          // headless captures: no mid-animation frames
                : Mathf.MoveTowards(raise, open ? 1f : 0f, Time.unscaledDeltaTime / 0.28f);
            var e = raise * raise * (3f - 2f * raise);     // smoothstep

            var carrying = director.Carrying && !open;
            model.gameObject.SetActive(e > 0.01f || carrying);
            if (tablet != null) tablet.gameObject.SetActive(e > 0.01f);
            if (screen != null) screen.gameObject.SetActive(e > 0.01f);
            if (gloveL != null) gloveL.gameObject.SetActive(e > 0.01f);
            if (frame != null) frame.anchoredPosition = frameBase + Vector2.down * (1f - e) * 1100f;
            if (!model.gameObject.activeSelf) return;

            if (carrying && e <= 0.01f) { HoldKit(); return; }

            // Place the tablet so its Screen lands on the UI content rect; it rises with the 2D panel as one piece.
            PanelPixels(out var s0, out var s2);
            var centre = cam.ScreenToWorldPoint(new Vector3((s0.x + s2.x) / 2, (s0.y + s2.y) / 2, distance));
            var left = cam.ScreenToWorldPoint(new Vector3(s0.x, (s0.y + s2.y) / 2, distance));
            var right = cam.ScreenToWorldPoint(new Vector3(s2.x, (s0.y + s2.y) / 2, distance));
            var scale = Vector3.Distance(left, right) / screenWidthUnit;
            model.localScale = Vector3.one * scale;
            model.localPosition = transform.InverseTransformPoint(centre) - screenOffsetUnit * scale;
            model.localRotation = fix;   // square to the view (90 deg): the 2D buttons stay on the 3D screen
        }

        // Right glove low in view, holding the control kit (the kit cube is parented to the camera by ShiftDirector).
        private void HoldKit()
        {
            // Palm under the kit (kit rides at camera-local 0.35, -0.35, 0.8; see ShiftDirector.Collect).
            model.localScale = Vector3.one;
            model.localRotation = Quaternion.Euler(18f, -18f, 22f) * fix;
            // Glove on the kit's near lower-left corner (the kit is 0.45 x 0.25 x 0.35 m), in front of it so it reads as a grip.
            model.localPosition = new Vector3(0.14f, -0.44f, 0.6f) - model.localRotation * gloveOffset;
        }
    }
}
