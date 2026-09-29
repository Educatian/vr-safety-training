using UnityEngine;

namespace Jobsite.Runtime
{
    // First-person gloved hands holding the rugged tablet (Tools/blender/fp_tablet_hands.py). The tablet UI stays in
    // screen space (readable, tested); this model sits exactly behind the UI's screen rect so the gloves and bumpers
    // frame it, and both rise together from below when the tablet opens. While carrying a control kit, the right glove
    // shows holding it.
    public sealed class FirstPersonTablet : MonoBehaviour
    {
        [SerializeField] private GameObject modelPrefab;
        [SerializeField] private RectTransform screenRect;     // FieldTablet content area
        [SerializeField] private RectTransform frame;          // 2D frame art (hidden: the 3D tablet is the frame)
        [SerializeField] private float distance = 0.45f;
        [SerializeField] private GameObject handsPrefab;       // Tripo gloved hands, rigged (Tools/blender/rig_fp_hands.py)
        [SerializeField] private Material handsMaterial;

        private Camera cam;
        private ShiftDirector director;
        private Transform model, tablet, screen, gloveL, gloveR;
        private Quaternion fix = Quaternion.identity;
        private Vector3 screenOffsetUnit;     // camera-space offset root -> screen centre at scale 1
        private float screenWidthUnit = 1f;
        private float raise;                  // 0 lowered .. 1 in view
        private Vector2 frameBase;
        private Canvas canvas;
        private Vector3 gloveOffset;

        public void Configure(GameObject prefab, RectTransform uiScreen, RectTransform uiFrame, GameObject hands = null, Material handsMat = null)
        { modelPrefab = prefab; screenRect = uiScreen; frame = uiFrame; handsPrefab = hands; handsMaterial = handsMat; }
        public float Raise => raise;

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
            if (handsPrefab != null) RigHands();
        }

        // ---- Tripo rigged hands: grip the tablet's side edges, fingers wrapped behind, thumbs on the bezel ----
        private void RigHands()
        {
            var inst = Instantiate(handsPrefab, model, false);
            inst.name = "FP_RiggedHands";
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (handsMaterial != null) smr.sharedMaterial = handsMaterial;
                smr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; smr.updateWhenOffscreen = true;
            }
            // Tablet frame in model-local space (fix maps model -> camera), measured from the tablet mesh bounds.
            var inv = Quaternion.Inverse(fix);
            Vector3 right = inv * Vector3.right, up = inv * Vector3.up, back = inv * Vector3.back;
            var tb = tablet.GetComponent<MeshFilter>().sharedMesh.bounds;
            var centre = model.InverseTransformPoint(tablet.TransformPoint(tb.center));
            float halfW = 0, halfH = 0;
            for (var i = 0; i < 8; i++)
            {
                var corner = model.InverseTransformPoint(tablet.TransformPoint(tb.center + Vector3.Scale(tb.extents, new Vector3((i & 1) * 2 - 1, (i & 2) - 1, (i & 4) / 2 - 1))));
                halfW = Mathf.Max(halfW, Mathf.Abs(Vector3.Dot(corner - centre, right)));
                halfH = Mathf.Max(halfH, Mathf.Abs(Vector3.Dot(corner - centre, up)));
            }
            foreach (var smr in inst.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var bones = new System.Collections.Generic.Dictionary<string, Transform>();
                foreach (var b in smr.bones) if (b != null) bones[b.name] = b;
                var s = smr.name.EndsWith("_L") ? "_L" : "_R";
                if (!bones.TryGetValue("Hand" + s, out var hand) || !bones.TryGetValue("Middle1" + s, out var mid)) continue;
                var rig = bones["Forearm" + s].parent;
                // Rest frame from the bones (world): finger direction, across the knuckles, palm normal (rest palms face down).
                Vector3 fingerW = (mid.position - hand.position).normalized;
                Vector3 acrossW = (bones["Index1" + s].position - bones["Pinky1" + s].position).normalized;
                Vector3 palmW = Vector3.Cross(fingerW, acrossW).normalized;
                if (Vector3.Dot(palmW, inst.transform.TransformDirection(Vector3.down)) < 0) palmW = -palmW;
                // Target (model-local -> world): left hand on the left edge; fingers up and inward, palm facing the edge and the back.
                var sideSign = s == "_L" ? -1f : 1f;
                var inward = -right * sideSign;
                var tFinger = model.TransformDirection((up * 0.9f + inward * 0.3f).normalized);
                var tPalm = model.TransformDirection((inward * 0.8f - back * 0.6f).normalized);
                var rot = Quaternion.LookRotation(tFinger, tPalm) * Quaternion.Inverse(Quaternion.LookRotation(fingerW, palmW));
                rig.rotation = rot * rig.rotation;
                // Palm centre just outside the edge, a little below mid-height.
                var edge = model.TransformPoint(centre + right * sideSign * halfW - up * halfH * 0.18f);
                var palmCentre = (hand.position + mid.position) * 0.5f;
                rig.position += edge - palmCentre - tPalm * 0.02f * model.lossyScale.x;
                // Curl the four fingers around the back; the thumb rests on the front bezel.
                var palmNow = rot * palmW;
                foreach (var f in new[] { "Index", "Middle", "Ring", "Pinky" })
                    for (var j = 1; j <= 3; j++)
                        if (bones.TryGetValue(f + j + s, out var b)) Curl(b, palmNow, j == 1 ? 62f : j == 2 ? 70f : 45f);
                for (var j = 1; j <= 3; j++)
                    if (bones.TryGetValue("Thumb" + j + s, out var th)) Curl(th, palmNow, j == 1 ? 8f : 22f);
                if (s == "_L") gloveL = smr.transform; else gloveR = smr.transform;
            }
            foreach (var n in new[] { "Glove_L", "Glove_R" })
            {
                var old = model.Find(n); if (old != null) old.gameObject.SetActive(false);
            }
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

        // Orient so the Screen mesh faces the camera (upright), then measure its width and offset at scale 1.
        private void Calibrate()
        {
            if (screen == null) return;
            // The display sits on the front face: body centre -> screen centre is the viewing normal (no mesh read needed).
            var sb = screen.GetComponent<Renderer>().bounds.center;
            var tb = tablet != null ? tablet.GetComponent<Renderer>().bounds.center : model.position;
            var nWorld = (sb - tb).sqrMagnitude > 1e-8f ? (sb - tb) : -model.forward;
            var nLocal = model.InverseTransformDirection(nWorld).normalized;
            var upLocal = Vector3.ProjectOnPlane(Vector3.up, nLocal).normalized;
            fix = Quaternion.Inverse(Quaternion.LookRotation(-nLocal, upLocal));
            model.localScale = Vector3.one; model.localPosition = Vector3.forward; model.localRotation = fix;
            var b = screen.GetComponent<Renderer>().bounds;
            screenWidthUnit = Mathf.Abs(Vector3.Dot(b.size, transform.right));
            if (screenWidthUnit < 1e-4f) screenWidthUnit = b.size.magnitude * 0.6f;
            screenOffsetUnit = transform.InverseTransformPoint(b.center) - model.localPosition;
            if (gloveR != null) gloveOffset = model.InverseTransformPoint(gloveR.GetComponent<Renderer>().bounds.center);
        }

        private void LateUpdate()
        {
            if (model == null || director == null) return;
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

            // Place the tablet so its Screen lands on the UI content rect; slide/tilt in from below.
            var corners = new Vector3[4]; screenRect.GetWorldCorners(corners);
            var uiCam = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
            Vector2 s0 = RectTransformUtility.WorldToScreenPoint(uiCam, corners[0]), s2 = RectTransformUtility.WorldToScreenPoint(uiCam, corners[2]);
            if (frame != null) { var shift = (1f - e) * 1100f * (canvas != null ? canvas.scaleFactor : 1f); s0.y += shift; s2.y += shift; }   // undo the slide to get the resting rect
            var centre = cam.ScreenToWorldPoint(new Vector3((s0.x + s2.x) / 2, (s0.y + s2.y) / 2, distance));
            var left = cam.ScreenToWorldPoint(new Vector3(s0.x, (s0.y + s2.y) / 2, distance));
            var right = cam.ScreenToWorldPoint(new Vector3(s2.x, (s0.y + s2.y) / 2, distance));
            var scale = Vector3.Distance(left, right) / screenWidthUnit;
            var breathe = Mathf.Sin(Time.time * 1.6f) * 0.002f;
            var restPos = transform.InverseTransformPoint(centre) - screenOffsetUnit * scale + new Vector3(0, breathe, 0);
            model.localScale = Vector3.one * scale;
            model.localPosition = restPos + new Vector3(0, -0.32f, -0.05f) * (1f - e);
            model.localRotation = Quaternion.Euler(35f * (1f - e) + Mathf.Sin(Time.time * 0.9f) * 0.4f, 0, Mathf.Sin(Time.time * 0.7f) * 0.3f) * fix;
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
