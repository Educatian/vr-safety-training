using UnityEngine;
using UnityEngine.UI;

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
        // One-handed grip (playtest feedback 2026-09-29: two big gloves hid too much of the view). The right glove holds
        // the tablet's lower-right corner; the left glove is hidden. HandScale shrinks the gloves relative to the tablet.
        [SerializeField] private bool oneHanded = true;
        [SerializeField] private float handScale = 0.78f;

        private Camera cam;
        private ShiftDirector director;
        private Transform model, tablet, screen, gloveL, gloveR;
        private Quaternion fix = Quaternion.identity;
        private Vector3 screenOffsetUnit;     // camera-space offset root -> screen centre at scale 1
        private float screenWidthUnit = 1f, screenHeightUnit = 1f, bodyWidthUnit = 1f, bodyHeightUnit = 1f;
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
            FitPanel();
            if (oneHanded && gloveL != null) { gloveL.gameObject.SetActive(false); gloveL = null; }
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
                if (oneHanded && s == "_L") { smr.gameObject.SetActive(false); continue; }
                if (!bones.TryGetValue("Hand" + s, out var hand) || !bones.TryGetValue("Middle1" + s, out var mid)) continue;
                var rig = bones["Forearm" + s].parent;
                rig.localScale *= handScale;
                // Rest frame from the bones (world): finger direction, across the knuckles, palm normal (rest palms face down).
                Vector3 fingerW = (mid.position - hand.position).normalized;
                Vector3 acrossW = (bones["Index1" + s].position - bones["Pinky1" + s].position).normalized;
                // Palm normal from handedness (index-pinky points to the thumb side): right = f x a, left = a x f.
                // The Tripo hands were sculpted mid-grip, so "rest palms face down" is not a safe assumption.
                Vector3 palmW = (s == "_R" ? Vector3.Cross(fingerW, acrossW) : Vector3.Cross(acrossW, fingerW)).normalized;
                // Target (model-local -> world): left hand on the left edge; fingers up and inward, palm facing the edge and the back.
                var sideSign = s == "_L" ? -1f : 1f;
                var inward = -right * sideSign;
                // One hand, held like a notebook: the palm supports the back of the tablet from its lower-right, fingers
                // run straight up behind it, and the thumb comes over the front bezel. Palm faces the viewer (it presses
                // on the tablet's back), so only the heel of the hand and the thumb show below the screen.
                // One hand, pinch grip on the right edge: thumb along the front bezel, index finger behind, the other
                // three fingers tucked. Fingers point inward and slightly up; the palm faces the edge.
                var tFinger = model.TransformDirection(oneHanded ? (inward * 0.75f + up * 0.45f).normalized : (up * 0.9f + inward * 0.3f).normalized);
                var tPalm = model.TransformDirection(oneHanded ? (inward * 0.75f - back * 0.65f).normalized : (inward * 0.8f - back * 0.6f).normalized);
                var rot = Quaternion.LookRotation(tFinger, tPalm) * Quaternion.Inverse(Quaternion.LookRotation(fingerW, palmW));
                rig.rotation = rot * rig.rotation;
                // Palm centre just outside the edge, a little below mid-height.
                var edge = model.TransformPoint(centre + right * sideSign * halfW - up * halfH * (oneHanded ? 0.35f : 0.18f));
                var palmCentre = (hand.position + mid.position) * 0.5f;
                rig.position += edge - palmCentre - tPalm * 0.02f * model.lossyScale.x;
                // Curl the four fingers around the back; the thumb rests on the front bezel.
                var palmNow = rot * palmW;
                // Pinch: the index wraps the back edge, middle/ring/pinky tuck into the palm, the thumb lies on the bezel.
                foreach (var f in new[] { "Index", "Middle", "Ring", "Pinky" })
                    for (var j = 1; j <= 3; j++)
                        if (bones.TryGetValue(f + j + s, out var b))
                            Curl(b, palmNow, !oneHanded ? (j == 1 ? 62f : j == 2 ? 70f : 45f)
                                : f == "Index" ? (j == 1 ? 40f : j == 2 ? 48f : 30f)
                                : (j == 1 ? 82f : j == 2 ? 88f : 60f));
                for (var j = 1; j <= 3; j++)
                    if (bones.TryGetValue("Thumb" + j + s, out var th)) Curl(th, palmNow, oneHanded ? (j == 1 ? 4f : 10f) : (j == 1 ? 8f : 22f));
                if (s == "_L") gloveL = smr.transform; else gloveR = smr.transform;
            }
            if (oneHanded) gloveL = null;
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

        // Make the 2D panel the exact shape of the 3D display and keep the whole device on screen, so the UI reads as
        // being ON the tablet instead of floating over it (playtest feedback 2026-09-29).
        private void FitPanel()
        {
            if (screenRect == null || frame == null) return;
            var fw = frame.rect.width; var fh = frame.rect.height;
            if (fw < 1f || fh < 1f) return;
            var aspect = screenHeightUnit / screenWidthUnit;
            float ax0 = screenRect.anchorMin.x, ax1 = screenRect.anchorMax.x;
            var wUi = (ax1 - ax0) * fw;
            var hf = wUi * aspect / fh;
            if (hf > 0.96f)
            {   // display is taller than the frame allows: keep the height, narrow the width
                hf = 0.96f;
                var cx = (ax0 + ax1) / 2f; var wf = hf * fh / aspect / fw;
                ax0 = cx - wf / 2f; ax1 = cx + wf / 2f; wUi = wf * fw;
            }
            var cy = Mathf.Clamp((screenRect.anchorMin.y + screenRect.anchorMax.y) / 2f, hf / 2f, 1f - hf / 2f);
            screenRect.anchorMin = new Vector2(ax0, cy - hf / 2f); screenRect.anchorMax = new Vector2(ax1, cy + hf / 2f);
            screenRect.offsetMin = screenRect.offsetMax = Vector2.zero;

            // Whole device must fit: shrink the frame if the body is taller than the view, then slide it left until the
            // right bezel and bumpers end 24 px inside the screen edge.
            var hUi = hf * fh;
            var bodyUiH = hUi * bodyHeightUnit / screenHeightUnit;
            var ref0 = canvas != null && canvas.TryGetComponent<CanvasScaler>(out var sc) ? sc.referenceResolution.y : 1080f;
            var k = Mathf.Min(1f, (ref0 - 40f) / bodyUiH);
            frame.localScale = Vector3.one * k;
            var extra = (bodyWidthUnit / screenWidthUnit - 1f) * 0.5f * wUi;      // bezel beyond the display, canvas units
            var inset = (1f - ax1) * fw;                                           // frame right edge -> display right edge
            var maxX = -(extra - inset) * k - 24f;
            if (frame.anchoredPosition.x > maxX) frame.anchoredPosition = new Vector2(maxX, frame.anchoredPosition.y);
            frameBase = frame.anchoredPosition;
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
            // The 3D device follows the 2D panel's actual (sliding) rect: they rise together as one piece.
            var centre = cam.ScreenToWorldPoint(new Vector3((s0.x + s2.x) / 2, (s0.y + s2.y) / 2, distance));
            var left = cam.ScreenToWorldPoint(new Vector3(s0.x, (s0.y + s2.y) / 2, distance));
            var right = cam.ScreenToWorldPoint(new Vector3(s2.x, (s0.y + s2.y) / 2, distance));
            var scale = Vector3.Distance(left, right) / screenWidthUnit;
            // Held square to the view (90°) once raised: no idle wobble, so the 2D buttons sit exactly on the 3D screen.
            var breathe = 0f;
            var restPos = transform.InverseTransformPoint(centre) - screenOffsetUnit * scale + new Vector3(0, breathe, 0);
            model.localScale = Vector3.one * scale;
            model.localPosition = restPos;
            model.localRotation = fix;   // square to the view (90°): the 2D buttons stay on the 3D screen
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
