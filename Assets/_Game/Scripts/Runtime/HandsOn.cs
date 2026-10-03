using System;
using System.Collections.Generic;
using System.Linq;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Hands-on interactions (2026-10-02): the tablet hands the job to the player's hands in the world.
    //   Measure  : laser - mark two points on the condition and read the distance.
    //   Hold     : GFCI tester / penetrometer / dust monitor / plug-in GFCI - aim at the spot and hold until the reading.
    //   Midrail  : raise or lower the rail, set it (judged midway, 1926.502(b)(2)(i)).
    //   Ladder   : side-view setup - extension above the landing and base distance (3 ft; 1:4).
    //   Cover    : slide the cover over the opening, set it, mark it.
    //   Barricade: place cones around the swing path, then finish.
    //   Inspect  : turn the cord or sling in your hands, mark the defect or call it sound.
    // Controls: E / A act · F / Y finish · Q / B / Tab cancel · W-S, scroll or stick adjust · A-D second axis.
    // Judgements live in Core/HandsOn.cs; ShiftDirector logs the evidence. Test hooks are the Sim*/Set* methods.
    public sealed class HandsOn : MonoBehaviour
    {
        public enum Mode { None, Measure, Hold, Midrail, Ladder, Cover, Barricade, Inspect }
        public static HandsOn Instance { get; private set; }
        public static bool Active => Instance != null && Instance.mode != Mode.None;
        public static bool FreezeMove => Active && (Instance.mode == Mode.Midrail || Instance.mode == Mode.Ladder || Instance.mode == Mode.Inspect || Instance.mode == Mode.Cover && Instance.coverSet);
        public static bool FreezeLook => Active && (Instance.mode == Mode.Inspect || Instance.mode == Mode.Ladder);

        public Mode Current => mode;
        public string Hint => hint != null ? hint.text : "";
        public string Readout { get; private set; } = "";

        private Mode mode;
        private SiteCondition target;
        private Camera cam;
        private int startFrame;
        private Action<HandsOnResult> onAttempt;      // install / inspect: every judged attempt
        private Action onCancel;
        private Action<float, bool> onMeasured;
        private Action onHeld;
        private readonly List<GameObject> spawned = new List<GameObject>();
        private Material yellow, orange, dark, wood, white;

        // ---------- lifecycle ----------
        private void Awake() { Instance = this; }
        private void OnDestroy() { if (Instance == this) Instance = null; }

        private Camera Cam => cam != null ? cam : cam = FindFirstObjectByType<SitePlayer>()?.View;

        private void Begin(Mode m, SiteCondition t, string text)
        {
            End();
            mode = m; target = t; startFrame = Time.frameCount; Readout = ""; last = "";
            EnsureUi(); panel.SetActive(true); SetHint(text);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
        }

        public void End()
        {
            foreach (var g in spawned) if (g != null) Destroy(g);
            spawned.Clear(); cones.Clear(); hotspots.Clear();
            if (line != null) line.enabled = false;
            if (panel != null) panel.SetActive(false);
            if (ladderPanel != null) ladderPanel.SetActive(false);
            mode = Mode.None; target = null; onAttempt = null; onCancel = null; onMeasured = null; onHeld = null;
            aimOverride = null; progress = 0f; pointA = null;
        }

        public void Cancel() { var c = onCancel; End(); c?.Invoke(); }

        // ---------- inputs ----------
        private bool simPrimary, simDone, simCancel;
        private Vector3? aimOverride;
        public void SimPrimary() => simPrimary = true;
        public void SimDone() => simDone = true;
        public void SimCancel() => simCancel = true;
        public void SimAim(Vector3 world) => aimOverride = world;

        bool Fresh => Time.frameCount > startFrame;
        bool Take(ref bool flag) { var v = flag; flag = false; return v; }
        bool Primary()
        {
            if (Take(ref simPrimary)) return true;
            if (!Fresh) return false;
            var k = Keyboard.current; var p = Gamepad.current;
            return k != null && k.eKey.wasPressedThisFrame || p != null && p.buttonSouth.wasPressedThisFrame || MobileControls.TakeAct();
        }
        bool PrimaryHeld()
        {
            var k = Keyboard.current; var p = Gamepad.current;
            return Fresh && (k != null && k.eKey.isPressed || p != null && p.buttonSouth.isPressed);
        }
        bool Done()
        {
            if (Take(ref simDone)) return true;
            if (!Fresh) return false;
            var k = Keyboard.current; var p = Gamepad.current;
            return k != null && (k.fKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) || p != null && p.buttonNorth.wasPressedThisFrame || MobileControls.TakeTablet();
        }
        bool CancelPressed()
        {
            if (Take(ref simCancel)) return true;
            if (!Fresh) return false;
            var k = Keyboard.current; var p = Gamepad.current;
            return k != null && (k.qKey.wasPressedThisFrame || k.tabKey.wasPressedThisFrame) || p != null && p.buttonEast.wasPressedThisFrame || MobileControls.TakeMap();
        }
        static float Axis(bool frozen)
        {
            var k = Keyboard.current; var p = Gamepad.current; var m = Mouse.current;
            var v = 0f;
            if (k != null) v += (k.upArrowKey.isPressed || frozen && k.wKey.isPressed ? 1f : 0f) - (k.downArrowKey.isPressed || frozen && k.sKey.isPressed ? 1f : 0f);
            if (p != null) v += p.leftStick.ReadValue().y + p.dpad.ReadValue().y;
            if (frozen) v += MobileControls.Move.y;
            if (m != null) v += Mathf.Clamp(m.scroll.ReadValue().y / 120f, -1f, 1f) * 6f;
            return Mathf.Clamp(v, -6f, 6f);
        }
        static float Axis2()
        {
            var k = Keyboard.current; var p = Gamepad.current;
            var v = 0f;
            if (k != null) v += (k.rightArrowKey.isPressed || k.dKey.isPressed ? 1f : 0f) - (k.leftArrowKey.isPressed || k.aKey.isPressed ? 1f : 0f);
            if (p != null) v += p.leftStick.ReadValue().x + p.dpad.ReadValue().x;
            v += MobileControls.Move.x;
            return Mathf.Clamp(v, -1.5f, 1.5f);
        }

        // Crosshair aim: physics surfaces, plus the target area's renderers (greybox props carry no colliders).
        private List<Renderer> nearby = new List<Renderer>();
        private bool Aim(out Vector3 p)
        {
            if (aimOverride.HasValue) { p = aimOverride.Value; return true; }
            p = default;
            if (Cam == null) return false;
            var ray = Cam.ViewportPointToRay(new Vector3(.5f, .5f));
            var best = 14f; var hit = false;
            if (Physics.Raycast(ray, out var h, 14f, ~0, QueryTriggerInteraction.Ignore)) { best = h.distance; p = h.point; hit = true; }
            foreach (var r in nearby)
                if (r != null && r.enabled && r.gameObject.activeInHierarchy && r.bounds.IntersectRay(ray, out var d) && d < best && d > 0.05f) { best = d; p = ray.GetPoint(d); hit = true; }
            return hit;
        }

        private void CollectNearby(Bounds area)
        {
            area.Expand(6f);
            nearby = FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => area.Intersects(r.bounds) && r.bounds.size.magnitude < 30f).Cast<Renderer>().ToList();
        }

        // ---------- update ----------
        private void Update()
        {
            if (mode == Mode.None) return;
            if (PauseMenu.Paused) return;
            // An alert card (incident, weather, speak-up), a conversation or the end of the shift takes over.
            var d = GetComponent<ShiftDirector>();
            if (d != null && (d.Blocking || d.Finished || d.TalkingTo != null)) { Cancel(); return; }
            if (CancelPressed()) { Cancel(); return; }
            switch (mode)
            {
                case Mode.Measure: UpdateMeasure(); break;
                case Mode.Hold: UpdateHold(); break;
                case Mode.Midrail: UpdateMidrail(); break;
                case Mode.Ladder: UpdateLadder(); break;
                case Mode.Cover: UpdateCover(); break;
                case Mode.Barricade: UpdateBarricade(); break;
                case Mode.Inspect: UpdateInspect(); break;
            }
        }

        // ---------- measure (laser: two points) ----------
        private Vector3? pointA;
        private LineRenderer line;
        private Transform markA, markB;

        public void BeginMeasure(SiteCondition t, Action<float, bool> measured, Action cancelled)
        {
            Begin(Mode.Measure, t, "LASER MEASURE · aim at one end of what you want to measure, E to mark · Q cancel");
            onMeasured = measured; onCancel = cancelled;
            CollectNearby(t.PhotoBounds);
            line = line != null ? line : NewLine("MeasureLine", new Color(1f, .25f, .2f));
            markA = Marker("MeasureA", yellowMat()); markB = Marker("MeasureB", yellowMat());
        }

        private void UpdateMeasure()
        {
            var aimed = Aim(out var p);
            markB.gameObject.SetActive(aimed);
            if (aimed) markB.position = p;
            if (pointA.HasValue && aimed)
            {
                line.enabled = true; line.positionCount = 2; line.SetPosition(0, pointA.Value); line.SetPosition(1, p);
                Readout = HandsOnRules.FeetInches(Vector3.Distance(pointA.Value, p));
                SetHint($"LASER MEASURE · {Readout} · aim at the other end, E to read · Q cancel");
            }
            if (!Primary() || !aimed) return;
            if (!pointA.HasValue)
            {
                pointA = p; markA.position = p; markA.gameObject.SetActive(true); AudioDirector.Play("click");
                SetHint("LASER MEASURE · first point marked · aim at the other end, E to read · Q cancel");
                return;
            }
            var a = pointA.Value; var b = p;
            var bounds = target.PhotoBounds;
            var plausible = HandsOnRules.OnCondition(V(a), V(b), V(bounds.min), V(bounds.max));
            var cb = onMeasured; var meters = Vector3.Distance(a, b);
            AudioDirector.Play("click");
            End();
            cb?.Invoke(meters, plausible);
        }

        // ---------- hold at a spot (tester, probe, monitor, plug-in GFCI) ----------
        private float progress, holdSeconds;
        private string holdVerb;
        private Image bar;

        public void BeginHold(SiteCondition t, string tool, string verb, float seconds, Action held, Action cancelled)
        {
            Begin(Mode.Hold, t, $"{tool.ToUpperInvariant()} · aim at the {verb} spot and hold E · Q cancel");
            holdSeconds = Mathf.Max(0.3f, seconds); holdVerb = tool; onHeld = held; onCancel = cancelled; progress = 0f;
            CollectNearby(t.PhotoBounds);
            markB = Marker("HoldPoint", yellowMat());
        }

        // Test hook: the hold completes this frame (the player held long enough on target).
        public void SimHoldComplete() { if (mode == Mode.Hold) progress = 1f; }

        private void UpdateHold()
        {
            var aimed = Aim(out var p);
            var b = target.PhotoBounds; b.Expand(1.2f);
            var onTarget = aimed && b.Contains(p) && (aimOverride.HasValue || Cam != null && Vector3.Distance(Cam.transform.position, p) <= 3.5f);
            markB.gameObject.SetActive(aimed); if (aimed) markB.position = p;
            if (MobileControls.TakeAct() && onTarget) progress += 0.34f;
            if (onTarget && PrimaryHeld()) progress += Time.deltaTime / holdSeconds;
            else if (!onTarget) progress = Mathf.Max(0f, progress - Time.deltaTime);
            bar.fillAmount = Mathf.Clamp01(progress);
            bar.transform.parent.gameObject.SetActive(true);
            SetHint(onTarget ? $"{holdVerb.ToUpperInvariant()} · holding… {Mathf.RoundToInt(Mathf.Clamp01(progress) * 100)}%" : $"{holdVerb.ToUpperInvariant()} · move closer and aim at the condition · Q cancel");
            if (progress < 1f) return;
            var cb = onHeld;
            AudioDirector.Play("click");
            End();
            cb?.Invoke();
        }

        // ---------- midrail height ----------
        private Transform ghost;
        private float midIn = 8f;
        public float MidrailInches => midIn;
        public void SetMidrailInches(float inches) { midIn = Mathf.Clamp(inches, 2f, HandsOnRules.TopRailIn - 2f); PlaceMidrail(); }

        public void BeginMidrail(SiteCondition t, Action<HandsOnResult> attempt, Action cancelled)
        {
            Begin(Mode.Midrail, t, "MIDRAIL · W/S or scroll to raise or lower it, E to fasten · Q cancel");
            onAttempt = attempt; onCancel = cancelled; midIn = 8f;
            var b = t.PhotoBounds;
            var alongX = b.size.x >= b.size.z;
            ghost = Box("MidrailGhost", Vector3.zero, alongX ? new Vector3(b.size.x * 0.95f, 0.05f, 0.05f) : new Vector3(0.05f, 0.05f, b.size.z * 0.95f), yellowMat());
            PlaceMidrail();
        }

        private void PlaceMidrail()
        {
            if (ghost == null || target == null) return;
            var b = target.PhotoBounds;
            ghost.position = new Vector3(b.center.x, b.min.y + midIn / HandsOnRules.InchesPerMeter, b.center.z);
            Readout = $"{midIn:0} in above the deck (top rail {HandsOnRules.TopRailIn:0} in)";
        }

        private void UpdateMidrail()
        {
            var a = Axis(true);
            if (Mathf.Abs(a) > 0.05f) { midIn = Mathf.Clamp(midIn + a * 10f * Time.deltaTime, 2f, HandsOnRules.TopRailIn - 2f); PlaceMidrail(); }
            SetHint($"MIDRAIL · {Readout} · W/S adjust, E fasten · Q cancel" + Last());
            if (!Primary()) return;
            Judge(HandsOnRules.Midrail(midIn));
        }

        // ---------- ladder setup (side view) ----------
        private float extIn = 0f, runM = 0.6f, riseM = 3.66f;
        private GameObject ladderPanel;
        private RectTransform ladderBar, ladderExt;
        private Text ladderText;
        public void SetLadder(float extensionIn, float baseOutM) { extIn = Mathf.Clamp(extensionIn, 0f, 60f); runM = Mathf.Clamp(baseOutM, 0.15f, 2.5f); DrawLadder(); }

        public void BeginLadder(SiteCondition t, float riseMeters, Action<HandsOnResult> attempt, Action cancelled)
        {
            Begin(Mode.Ladder, t, "LADDER SETUP · W/S extension above the landing · A/D base distance · E set · Q cancel");
            onAttempt = attempt; onCancel = cancelled; riseM = riseMeters; extIn = 0f; runM = riseMeters / 6.5f;
            BuildLadderPanel(); ladderPanel.SetActive(true); DrawLadder();
        }

        private void UpdateLadder()
        {
            var a = Axis(true); var a2 = Axis2();
            if (Mathf.Abs(a) > 0.05f) extIn = Mathf.Clamp(extIn + a * 14f * Time.deltaTime, 0f, 60f);
            if (Mathf.Abs(a2) > 0.05f) runM = Mathf.Clamp(runM + a2 * 0.5f * Time.deltaTime, 0.15f, 2.5f);
            DrawLadder();
            SetHint($"LADDER SETUP · {Readout} · W/S extension · A/D base · E set · Q cancel" + Last());
            if (!Primary()) return;
            Judge(HandsOnRules.Ladder(extIn, riseM, runM));
        }

        private void BuildLadderPanel()
        {
            if (ladderPanel != null) return;
            ladderPanel = new GameObject("LadderSetup", typeof(RectTransform), typeof(Image));
            ladderPanel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)ladderPanel.transform; rt.anchorMin = new Vector2(0.62f, 0.3f); rt.anchorMax = new Vector2(0.96f, 0.86f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            ladderPanel.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.08f, 0.88f);
            UiRect(ladderPanel.transform, "Ground", new Vector2(0.05f, 0.1f), new Vector2(0.95f, 0.115f), new Color(.55f, .45f, .35f));
            UiRect(ladderPanel.transform, "Wall", new Vector2(0.78f, 0.1f), new Vector2(0.8f, 0.7f), new Color(.7f, .72f, .74f));
            UiRect(ladderPanel.transform, "Deck", new Vector2(0.79f, 0.69f), new Vector2(0.97f, 0.71f), new Color(.7f, .72f, .74f));
            ladderBar = UiRect(ladderPanel.transform, "Ladder", new Vector2(0, 0), new Vector2(0, 0), new Color(1f, .78f, .1f));
            ladderBar.pivot = new Vector2(0f, 0.5f);
            ladderExt = UiRect(ladderPanel.transform, "Extension", new Vector2(0, 0), new Vector2(0, 0), new Color(1f, .9f, .4f));
            ladderExt.pivot = new Vector2(0f, 0.5f);
            ladderText = UiText(ladderPanel.transform, "", 22, new Vector2(0.04f, 0.74f), new Vector2(0.96f, 0.98f));
        }

        private void DrawLadder()
        {
            Readout = $"{HandsOnRules.FeetInches(extIn / HandsOnRules.InchesPerMeter)} above the landing · base {HandsOnRules.FeetInches(runM)} out for {HandsOnRules.FeetInches(riseM)} up (1:{riseM / runM:0.0})";
            if (ladderPanel == null) return;
            ladderText.text = Readout.Replace(" · ", "\n");
            var rt = (RectTransform)ladderPanel.transform;
            var size = rt.rect.size; if (size.x < 1f) size = new Vector2(600, 500);
            var scale = (0.6f * size.y) / riseM;                          // px per metre: the deck sits at 60% height
            var wallX = 0.78f * size.x; var groundY = 0.1f * size.y;
            var baseX = wallX - runM * scale;
            var topLen = Mathf.Sqrt(runM * runM + riseM * riseM) * scale;
            var angle = Mathf.Atan2(riseM, runM) * Mathf.Rad2Deg;
            Place(ladderBar, new Vector2(baseX, groundY), topLen, angle, 8f);
            var extPx = extIn / HandsOnRules.InchesPerMeter * scale;
            Place(ladderExt, new Vector2(wallX, groundY + riseM * scale), extPx, angle, 8f);
        }

        static void Place(RectTransform r, Vector2 from, float length, float angleDeg, float thickness)
        {
            r.anchorMin = r.anchorMax = Vector2.zero; r.anchoredPosition = from; r.sizeDelta = new Vector2(Mathf.Max(0f, length), thickness);
            r.localRotation = Quaternion.Euler(0, 0, angleDeg);
        }

        // ---------- cover an opening ----------
        private static readonly string[] Labels = { "", "HOLE", "COVER", "KEEP OUT" };
        private int labelIndex;
        private bool coverSet;
        private Text coverLabel;
        public string CoverLabel => Labels[labelIndex];
        public void SetCover(Vector3 center, string label) { if (ghost != null) ghost.position = new Vector3(center.x, ghost.position.y, center.z); labelIndex = Mathf.Max(0, Array.IndexOf(Labels, label)); coverSet = true; RefreshCoverLabel(); }

        public void BeginCover(SiteCondition t, Action<HandsOnResult> attempt, Action cancelled)
        {
            Begin(Mode.Cover, t, "COVER · aim to slide the cover over the opening, E to set it down · Q cancel");
            onAttempt = attempt; onCancel = cancelled; coverSet = false; labelIndex = 0;
            CollectNearby(t.PhotoBounds);
            var b = t.PhotoBounds;
            ghost = Box("CoverGhost", new Vector3(b.center.x, b.min.y + 0.03f, b.center.z) + new Vector3(1.5f, 0, 0),
                new Vector3(b.size.x + 0.4f, 0.04f, b.size.z + 0.4f), woodMat());
            var c = new GameObject("CoverMark", typeof(RectTransform), typeof(Canvas));
            c.transform.SetParent(ghost, false);
            c.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            var crt = (RectTransform)c.transform; crt.sizeDelta = new Vector2(1000, 400); crt.localPosition = new Vector3(0, 0.6f, 0);
            crt.localRotation = Quaternion.Euler(90, 0, 0);
            var s = ghost.lossyScale; crt.localScale = new Vector3(0.0008f / Mathf.Max(0.01f, s.x), 0.0008f / Mathf.Max(0.01f, s.z), 1f);
            coverLabel = UiText(c.transform, "", 220, Vector2.zero, Vector2.one);
            coverLabel.color = new Color(.1f, .1f, .1f); coverLabel.alignment = TextAnchor.MiddleCenter;
        }

        private void RefreshCoverLabel() { if (coverLabel != null) coverLabel.text = Labels[labelIndex]; }

        private void UpdateCover()
        {
            if (!coverSet)
            {
                if (Aim(out var p)) ghost.position = new Vector3(p.x, p.y + 0.03f, p.z);
                SetHint("COVER · slide it over the opening, E to set it down · Q cancel" + Last());
                if (Primary()) { coverSet = true; AudioDirector.Play("click"); }
                return;
            }
            var step = Input2Step();
            if (step != 0) { labelIndex = (labelIndex + step + Labels.Length) % Labels.Length; RefreshCoverLabel(); }
            SetHint($"COVER · mark it: A/D to choose [{(Labels[labelIndex].Length == 0 ? "no marking" : Labels[labelIndex])}], E to finish · Q cancel" + Last());
            if (!Primary()) return;
            var b = target.PhotoBounds; var g = ghost.position; var half = ghost.lossyScale / 2f;
            var r = HandsOnRules.Cover(b.min.x, b.min.z, b.max.x, b.max.z, g.x, g.z, half.x, half.z, Labels[labelIndex]);
            if (!r.Ok) coverSet = false;    // pick it up again
            Judge(r);
        }

        private float stepCooldown;
        private int Input2Step()
        {
            var a = Axis2();
            if (Mathf.Abs(a) < 0.5f) { stepCooldown = 0f; return 0; }
            if (Time.unscaledTime < stepCooldown) return 0;
            stepCooldown = Time.unscaledTime + 0.25f;
            return a > 0 ? 1 : -1;
        }

        // ---------- barricade the swing path ----------
        private readonly List<Transform> cones = new List<Transform>();
        public int Cones => cones.Count;
        public float SwingRadius { get; private set; }
        public Vector3 SwingCenter { get; private set; }

        public void BeginBarricade(SiteCondition t, Action<HandsOnResult> attempt, Action cancelled)
        {
            Begin(Mode.Barricade, t, "BARRICADE · E to place a cone just outside the swing path · F when the ring is closed · Q cancel");
            onAttempt = attempt; onCancel = cancelled;
            var b = t.PhotoBounds;
            SwingCenter = new Vector3(b.center.x, b.min.y, b.center.z);
            SwingRadius = Mathf.Max(b.extents.x, b.extents.z);
            CollectNearby(b);
            line = line != null ? line : NewLine("SwingPath", new Color(1f, .2f, .15f, .8f));
            line.enabled = true; line.loop = true; line.positionCount = 48;
            for (var i = 0; i < 48; i++)
            {
                var ang = i * Mathf.PI * 2f / 48f;
                line.SetPosition(i, SwingCenter + new Vector3(Mathf.Cos(ang) * SwingRadius, 0.06f, Mathf.Sin(ang) * SwingRadius));
            }
        }

        public void PlaceCone(Vector3 at)
        {
            var root = new GameObject("BarricadeCone").transform; spawned.Add(root.gameObject);
            root.position = at;
            var cone = Mesh("Cone", root, PrimitiveType.Cylinder, new Vector3(0, 0.36f, 0), new Vector3(0.22f, 0.34f, 0.22f), orangeMat());
            Mesh("ConeBand", root, PrimitiveType.Cylinder, new Vector3(0, 0.46f, 0), new Vector3(0.2f, 0.05f, 0.2f), whiteMat());
            Mesh("ConeBase", root, PrimitiveType.Cube, new Vector3(0, 0.02f, 0), new Vector3(0.4f, 0.04f, 0.4f), darkMat());
            cones.Add(root);
        }

        private void UpdateBarricade()
        {
            var aimed = Aim(out var p);
            SetHint($"BARRICADE · {cones.Count} cone{(cones.Count == 1 ? "" : "s")} · E place, F finish, Backspace undo · Q cancel" + Last());
            var k = Keyboard.current; var pad = Gamepad.current;
            if (Fresh && (k != null && k.backspaceKey.wasPressedThisFrame || pad != null && pad.buttonWest.wasPressedThisFrame) && cones.Count > 0)
            { var c = cones[cones.Count - 1]; cones.RemoveAt(cones.Count - 1); Destroy(c.gameObject); }
            if (Primary() && aimed) { PlaceCone(p); AudioDirector.Play("click"); }
            if (!Done()) return;
            Judge(HandsOnRules.Barricade(cones.Select(c => (c.position.x, c.position.z)).ToList(), SwingCenter.x, SwingCenter.z, SwingRadius));
        }

        // ---------- close inspection ----------
        private Transform item;
        private readonly List<(Vector3 local, Vector3 normal, bool defect)> hotspots = new List<(Vector3, Vector3, bool)>();
        private bool hasDefect, found;
        private int falseMarks;
        private string itemName;
        public bool InspectFound => found;
        public int InspectFalseMarks => falseMarks;

        public void BeginInspect(SiteCondition t, InspectItem what, bool defective, Action<HandsOnResult> attempt, Action cancelled)
        {
            Begin(Mode.Inspect, t, "");
            onAttempt = attempt; onCancel = cancelled; hasDefect = defective; found = false; falseMarks = 0;
            itemName = what == InspectItem.Sling ? "sling" : "cord";
            item = new GameObject("InspectItem").transform; spawned.Add(item.gameObject);
            if (Cam != null) { item.SetParent(Cam.transform, false); item.localPosition = new Vector3(0f, -0.02f, 0.55f); }
            if (what == InspectItem.Sling) BuildSling(defective); else BuildCord(defective);
            item.localRotation = Quaternion.Euler(20f, 160f, 0f);
        }

        // Test hook: turn the item so the defect (or the first spot) faces the crosshair.
        public void FaceHotspot(bool defect)
        {
            if (item == null || Cam == null || hotspots.Count == 0) return;
            var h = hotspots.FirstOrDefault(x => x.defect == defect);
            if (h.normal == Vector3.zero) h = hotspots[0];
            // Align the hotspot's outward normal with -camera forward, then shift the item so the spot is centred.
            item.rotation = Quaternion.FromToRotation(item.TransformDirection(h.normal), -Cam.transform.forward) * item.rotation;
            var world = item.TransformPoint(h.local);
            var desired = Cam.transform.position + Cam.transform.forward * 0.45f;
            item.position += desired - world;
        }

        private void UpdateInspect()
        {
            var m = Mouse.current; var p = Gamepad.current; var k = Keyboard.current;
            var rot = Vector2.zero;
            if (m != null && (m.leftButton.isPressed || m.rightButton.isPressed)) rot += m.delta.ReadValue() * 0.4f;
            if (p != null) rot += p.rightStick.ReadValue() * 160f * Time.deltaTime;
            if (k != null) rot += new Vector2((k.dKey.isPressed ? 1 : 0) - (k.aKey.isPressed ? 1 : 0), (k.wKey.isPressed ? 1 : 0) - (k.sKey.isPressed ? 1 : 0)) * 120f * Time.deltaTime;
            rot += MobileControls.TakeLook() * 0.6f;
            if (rot.sqrMagnitude > 0f && Cam != null)
            {
                item.Rotate(Cam.transform.up, -rot.x, Space.World);
                item.Rotate(Cam.transform.right, rot.y, Space.World);
            }
            SetHint($"INSPECT THE {itemName.ToUpperInvariant()} · drag / WASD / stick to turn it · centre a damaged spot and press E · F if it's sound · Q cancel" + Last());
            if (Primary())
            {
                var hit = HotspotAtCentre();
                if (hit == 1) { found = true; SetHint("Defect marked."); AudioDirector.Play("click"); FinishInspect(false); return; }
                falseMarks++; last = "Nothing wrong there.";
            }
            if (Done()) FinishInspect(true);
        }

        // 1 = defect under the crosshair (facing the camera), 0 = a sound spot / nothing.
        private int HotspotAtCentre()
        {
            if (Cam == null) return 0;
            foreach (var h in hotspots)
            {
                if (!h.defect) continue;
                var w = item.TransformPoint(h.local);
                var sp = Cam.WorldToViewportPoint(w);
                var facing = Vector3.Dot(item.TransformDirection(h.normal), (Cam.transform.position - w).normalized);
                if (sp.z > 0 && Mathf.Abs(sp.x - .5f) < .09f && Mathf.Abs(sp.y - .5f) < .12f && facing > 0.15f) return 1;
            }
            return 0;
        }

        private void FinishInspect(bool declaredSound)
        {
            var r = HandsOnRules.Inspection(hasDefect, found, falseMarks, declaredSound, itemName);
            var cb = onAttempt;
            End();
            cb?.Invoke(r);
        }

        private void BuildCord(bool defective)
        {
            const int n = 16; const float radius = 0.13f;
            var jacket = orangeMat();
            var bad = defective ? 5 : -1;
            for (var i = 0; i < n; i++)
            {
                var a0 = i * Mathf.PI * 2f / n; var a1 = (i + 1) * Mathf.PI * 2f / n;
                var p0 = new Vector3(Mathf.Cos(a0) * radius, Mathf.Sin(a0 * 2f) * 0.015f, Mathf.Sin(a0) * radius);
                var p1 = new Vector3(Mathf.Cos(a1) * radius, Mathf.Sin(a1 * 2f) * 0.015f, Mathf.Sin(a1) * radius);
                var mid = (p0 + p1) / 2f; var dir = p1 - p0;
                var outward = new Vector3(mid.x, 0, mid.z).normalized;
                if (i == bad)
                {
                    // Jacket split: three conductors show through (black, white, green), frayed ends of the jacket.
                    var colors = new[] { new Color(.08f, .08f, .08f), new Color(.92f, .92f, .9f), new Color(.15f, .6f, .2f) };
                    for (var c = 0; c < 3; c++)
                        Seg(item, "Conductor", mid + outward * (c - 1) * 0.007f + Vector3.up * 0.004f * (c - 1), dir, 0.0075f, dir.magnitude * 0.92f, Unlit(colors[c]));
                    Seg(item, "JacketEnd", p0 + dir * 0.08f, dir, 0.024f, dir.magnitude * 0.14f, jacket);
                    Seg(item, "JacketEnd", p1 - dir * 0.08f, dir, 0.024f, dir.magnitude * 0.14f, jacket);
                    hotspots.Add((mid + outward * 0.012f, outward, true));
                }
                else
                {
                    Seg(item, "Jacket", mid, dir, 0.022f, dir.magnitude * 1.06f, jacket);
                    if (i % 4 == 2) hotspots.Add((mid + outward * 0.012f, outward, false));
                }
            }
            // Plug with blades and the ground pin.
            var plug = Mesh("Plug", item, PrimitiveType.Cube, new Vector3(radius + 0.03f, 0, 0), new Vector3(0.05f, 0.03f, 0.035f), darkMat());
            foreach (var z in new[] { -0.008f, 0.008f }) Mesh("Blade", item, PrimitiveType.Cube, new Vector3(radius + 0.065f, 0.004f, z), new Vector3(0.02f, 0.012f, 0.002f), Unlit(new Color(.8f, .75f, .5f)));
            Mesh("GroundPin", item, PrimitiveType.Cylinder, new Vector3(radius + 0.063f, -0.009f, 0), new Vector3(0.005f, 0.009f, 0.005f), Unlit(new Color(.8f, .75f, .5f))).localRotation = Quaternion.Euler(0, 0, 90);
        }

        private void BuildSling(bool defective)
        {
            const int n = 18;
            var strap = Unlit(new Color(.95f, .78f, .15f));
            var bad = defective ? 7 : -1;
            for (var i = 0; i < n; i++)
            {
                var a0 = i * Mathf.PI * 2f / n; var a1 = (i + 1) * Mathf.PI * 2f / n;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.2f, 0, Mathf.Sin(a0) * 0.08f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.2f, 0, Mathf.Sin(a1) * 0.08f);
                var mid = (p0 + p1) / 2f; var dir = p1 - p0;
                var outward = new Vector3(mid.x / 0.2f, 0, mid.z / 0.08f).normalized;
                var seg = Mesh(i == bad ? "StrapCut" : "Strap", item, PrimitiveType.Cube, mid, new Vector3(dir.magnitude * 1.05f, 0.05f, 0.006f), strap);
                seg.localRotation = Quaternion.LookRotation(outward, Vector3.up);   // length along the loop, thickness outward
                if (i == bad)
                {
                    // Cut through the webbing: red warning core yarn exposed, edges frayed.
                    var core = Mesh("RedCore", item, PrimitiveType.Cube, mid + outward * 0.006f, new Vector3(dir.magnitude * 0.5f, 0.018f, 0.004f), Unlit(new Color(.85f, .1f, .08f)));
                    core.localRotation = seg.localRotation;
                    var fray = Mesh("Fray", item, PrimitiveType.Cube, mid + outward * 0.004f + Vector3.up * 0.026f, new Vector3(dir.magnitude * 0.7f, 0.012f, 0.008f), Unlit(new Color(.98f, .9f, .6f)));
                    fray.localRotation = seg.localRotation * Quaternion.Euler(0, 0, 9);
                    hotspots.Add((mid + outward * 0.008f, outward, true));
                }
                else if (i % 5 == 1) hotspots.Add((mid + outward * 0.006f, outward, false));
            }
            Mesh("Tag", item, PrimitiveType.Cube, new Vector3(0.21f, 0f, 0f), new Vector3(0.012f, 0.05f, 0.035f), Unlit(new Color(.2f, .45f, .85f)));
        }

        // ---------- judging (install / inspect attempts) ----------
        private string last = "";
        private string Last() => last.Length > 0 ? "\n" + last : "";

        private void Judge(HandsOnResult r)
        {
            var cb = onAttempt;
            if (r.Ok) { AudioDirector.Play("success"); End(); cb?.Invoke(r); return; }
            last = r.Feedback;
            AudioDirector.Play("click");
            cb?.Invoke(r);                // the director logs the failed attempt; the task stays open to try again
        }

        // ---------- small helpers ----------
        private GameObject panel;
        private Canvas canvas;
        private Text hint;

        private void EnsureUi()
        {
            if (canvas != null) return;
            var go = new GameObject("HandsOnCanvas", typeof(Canvas), typeof(CanvasScaler));
            go.transform.SetParent(transform, false);
            canvas = go.GetComponent<Canvas>(); canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.sortingOrder = 260;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; sc.referenceResolution = new Vector2(1920, 1080) / GameSettings.TextScale; sc.matchWidthOrHeight = 1;
            panel = new GameObject("HandsOnPanel", typeof(RectTransform), typeof(Image));
            panel.transform.SetParent(canvas.transform, false);
            var rt = (RectTransform)panel.transform; rt.anchorMin = new Vector2(0.2f, 0.06f); rt.anchorMax = new Vector2(0.8f, 0.2f); rt.offsetMin = rt.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(0.04f, 0.05f, 0.06f, 0.82f);
            hint = UiText(panel.transform, "", 26, new Vector2(0.03f, 0.1f), new Vector2(0.97f, 0.95f));
            hint.alignment = TextAnchor.MiddleCenter;
            var barBg = UiRect(panel.transform, "HoldBarBg", new Vector2(0.2f, 0.04f), new Vector2(0.8f, 0.12f), new Color(1, 1, 1, .15f));
            bar = UiRect(barBg, "HoldBar", Vector2.zero, Vector2.one, new Color(1f, .78f, .1f)).GetComponent<Image>();
            bar.type = Image.Type.Filled; bar.fillMethod = Image.FillMethod.Horizontal; bar.fillAmount = 0f;
            barBg.gameObject.SetActive(false);
        }

        private void SetHint(string text) { if (hint != null) hint.text = text; if (bar != null && mode != Mode.Hold) bar.transform.parent.gameObject.SetActive(false); }

        private static Font font;
        private static Text UiText(Transform parent, string value, int size, Vector2 min, Vector2 max)
        {
            font ??= Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            t.rectTransform.anchorMin = min; t.rectTransform.anchorMax = max; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.font = font; t.fontSize = size; t.color = Color.white; t.text = value; t.alignment = TextAnchor.MiddleLeft; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }

        private static RectTransform UiRect(Transform parent, string name, Vector2 min, Vector2 max, Color c)
        {
            var img = new GameObject(name, typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            img.transform.SetParent(parent, false);
            img.rectTransform.anchorMin = min; img.rectTransform.anchorMax = max; img.rectTransform.offsetMin = img.rectTransform.offsetMax = Vector2.zero;
            img.color = c; img.raycastTarget = false;
            return img.rectTransform;
        }

        private LineRenderer NewLine(string name, Color c)
        {
            var lr = new GameObject(name).AddComponent<LineRenderer>();
            lr.transform.SetParent(transform, false);
            lr.material = new Material(Shader.Find("Sprites/Default"));
            lr.startColor = lr.endColor = c; lr.widthMultiplier = 0.025f; lr.numCapVertices = 2;
            lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return lr;
        }

        private Transform Marker(string name, Material m)
        {
            var t = Mesh(name, null, PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.06f, m);
            t.gameObject.SetActive(false);
            return t;
        }

        private Transform Box(string name, Vector3 at, Vector3 size, Material m) { var t = Mesh(name, null, PrimitiveType.Cube, at, size, m); return t; }

        private Transform Mesh(string name, Transform parent, PrimitiveType type, Vector3 localPos, Vector3 scale, Material m)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos; go.transform.localScale = scale;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = m; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            if (parent == null) spawned.Add(go);
            return go.transform;
        }

        private void Seg(Transform parent, string name, Vector3 mid, Vector3 dir, float diameter, float length, Material m)
        {
            var t = Mesh(name, parent, PrimitiveType.Cylinder, mid, new Vector3(diameter, length / 2f, diameter), m);
            t.localRotation = Quaternion.FromToRotation(Vector3.up, dir.normalized);
        }

        private Material Lit(Color c, float smooth = 0.35f)
        {
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit")); m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return m;
        }
        // Held items sit in front of the camera, close to the near plane and outside the site lighting: unlit reads best.
        private Material Unlit(Color c)
        {
            var sh = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Sprites/Default");
            var m = new Material(sh); if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c); else m.color = c;
            return m;
        }
        private Material yellowMat() => yellow != null ? yellow : yellow = Lit(new Color(1f, .78f, .1f), 0.5f);
        private Material orangeMat() => orange != null ? orange : orange = Lit(new Color(1f, .42f, .05f), 0.45f);
        private Material darkMat() => dark != null ? dark : dark = Lit(new Color(.1f, .1f, .11f));
        private Material whiteMat() => white != null ? white : white = Lit(new Color(.95f, .95f, .95f), 0.6f);
        private Material woodMat() => wood != null ? wood : wood = Lit(new Color(.72f, .58f, .38f), 0.2f);

        static float[] V(Vector3 v) => new[] { v.x, v.y, v.z };
    }
}
