using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using Jobsite.Core;
using Jobsite.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Jobsite.PlayTests
{
    // Demo / promo video footage (explicit, not part of the regular run). Real gameplay, scripted: every segment is a
    // 30 fps JPEG sequence in Captures/demo/<segment>/ that the edit (Tools/video/) cuts to the narration. Screen
    // overlays (hands-on panel, toasts, flash) are pulled into the filming camera so they show in the frames.
    // Only test hooks and the game's own UI buttons drive the scenes; nothing here changes runtime behaviour.
    [Explicit("Demo video capture: run by hand (Logs/claude/run_grip.bat)")]
    public sealed class DemoVideoTests
    {
        const int W = 1920, H = 1080, Fps = 30;
        static string seg; static int frame;
        static RenderTexture rt; static Texture2D tex;
        static Camera cam;                       // filming camera for the current shot
        static readonly List<string> manifest = new List<string>();
        static float unscaledSum; static int unscaledN;
        int consent;

        [SetUp]
        public void Begin()
        {
            consent = GameSettings.AiConsent; GameSettings.AiConsent = 0;   // offline set questions + rubrics, no model calls
            Time.captureFramerate = Fps;
            LogAssert.ignoreFailingMessages = true;
            rt = new RenderTexture(W, H, 24); tex = new Texture2D(W, H, TextureFormat.RGB24, false);
        }

        [TearDown]
        public void End()
        {
            Time.captureFramerate = 0; GameSettings.AiConsent = consent; ArcadeMode.Exit(); CareerStore.Reset();
            foreach (var c in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                if (c.targetTexture == rt) c.targetTexture = null;
            FirstPersonTablet.FullView = false;
            UnityEngine.Object.Destroy(rt); UnityEngine.Object.Destroy(tex);
            Directory.CreateDirectory("Captures/demo");
            manifest.Add($"unscaled_dt_avg={(unscaledN > 0 ? unscaledSum / unscaledN : 0f):F4} (1/{Fps}={1f / Fps:F4})");
            File.AppendAllLines("Captures/demo/manifest.txt", manifest); manifest.Clear();
        }

        // ---------- filming ----------
        static void Seg(string name)
        {
            if (seg != null) manifest.Add($"{seg} {frame}");
            seg = name; frame = 0;
            var dir = $"Captures/demo/{name}";
            if (Directory.Exists(dir)) foreach (var f in Directory.GetFiles(dir)) File.Delete(f);
            Directory.CreateDirectory(dir);
        }
        static void Close() { if (seg != null) manifest.Add($"{seg} {frame}"); seg = null; }

        static void PullOverlays()
        {
            // Overlays belong to the player's view (or the menu camera), never to a free orbit/dolly camera.
            var player = UnityEngine.Object.FindFirstObjectByType<SitePlayer>();
            var view = player != null && player.View != null ? player.View : cam;
            foreach (var c in UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None))
                if (c.isRootCanvas && c.renderMode == RenderMode.ScreenSpaceOverlay)
                { c.renderMode = RenderMode.ScreenSpaceCamera; c.worldCamera = view; c.planeDistance = view.nearClipPlane + 0.02f; }
        }

        static void Grab()
        {
            if (cam == null || seg == null) return;
            PullOverlays();
            // The target stays on the camera between frames so pixelWidth/Height (tablet fit, canvas scale) are 16:9.
            if (cam.targetTexture != rt) cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0); tex.Apply();
            RenderTexture.active = null;
            File.WriteAllBytes($"Captures/demo/{seg}/f{frame:D5}.jpg", tex.EncodeToJPG(88));
            frame++;
            unscaledSum += Time.unscaledDeltaTime; unscaledN++;
        }

        // Runs `seconds` of game time at 30 fps, calling step(t, 0..1) before each frame is grabbed.
        static IEnumerator Film(float seconds, Action<float> step = null)
        {
            var n = Mathf.Max(1, Mathf.RoundToInt(seconds * Fps));
            for (var i = 0; i < n; i++) { step?.Invoke(i / (float)(n - 1 == 0 ? 1 : n - 1)); yield return null; Grab(); }
        }

        static IEnumerator Frames(int n) { for (var i = 0; i < n; i++) yield return null; }

        // ---------- scene helpers ----------
        static ShiftDirector D => UnityEngine.Object.FindFirstObjectByType<ShiftDirector>();
        static SitePlayer P => UnityEngine.Object.FindFirstObjectByType<SitePlayer>();
        static SiteCondition C(string id) => D.Conditions.FirstOrDefault(c => c != null && c.Id == id);
        static CrewMember Crew(string name) => UnityEngine.Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None).First(c => c.DisplayName.StartsWith(name));
        static Vector3 V(float[] v) => new Vector3(v[0], v[1], v[2]);
        static float Smooth(float t) => t * t * (3f - 2f * t);

        static IEnumerator Load(int episode, bool arcade, bool daily = false)
        {
            var career = new Career(lifetimeXp: 1200, points: 900);
            career.Buy(GearId.LaserMeasure); career.Buy(GearId.GfciTester); career.Buy(GearId.Penetrometer);
            CareerStore.Save(career);
            ArcadeMode.Active = arcade; ArcadeMode.Daily = daily; ArcadeMode.DailyNumber = DailySite.Number(DateTime.UtcNow); ArcadeMode.Seed = 7; ArcadeMode.Replay = false;
            EpisodeDirector.Selected = Episodes.Get(episode); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return Frames(12);   // crews settle into their working poses
            P.enabled = false;         // the script walks and looks; no input reads
            cam = P.View; cam.targetTexture = rt;
            yield return Frames(3);
        }

        // Places the player (eye 1.6 m above `at`) looking at `look`.
        static void Pose(Vector3 at, Vector3 look)
        {
            var p = P; var cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            p.transform.position = at;
            var flat = look - at; flat.y = 0;
            if (flat.sqrMagnitude > 0.001f) p.transform.rotation = Quaternion.LookRotation(flat);
            var eye = p.View.transform.position;
            var pitch = -Mathf.Atan2(look.y - eye.y, new Vector2(look.x - eye.x, look.z - eye.z).magnitude) * Mathf.Rad2Deg;
            p.View.transform.localRotation = Quaternion.Euler(Mathf.Clamp(pitch, -75f, 75f), 0, 0);
        }

        static Vector3 Ground(Vector3 p)
        {
            return Physics.Raycast(p + Vector3.up * 3f, Vector3.down, out var g, 8f, ~0, QueryTriggerInteraction.Ignore) ? g.point + Vector3.up * 0.05f : new Vector3(p.x, 0.05f, p.z);
        }

        // A standing spot ~dist m from the target with ground under it and a clear line of sight (EpisodeShowcaseTests).
        static Vector3 Vantage(Bounds b, float dist, float bias = 0.3f)
        {
            var c = b.center;
            foreach (var d in new[] { dist, dist * 0.7f, dist * 1.4f })
                for (var i = 0; i < 16; i++)
                {
                    var a = i * Mathf.PI * 2 / 16 + bias;
                    var p = new Vector3(c.x + Mathf.Cos(a) * d, b.min.y + 3f, c.z + Mathf.Sin(a) * d);
                    if (!Physics.Raycast(p, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (g.point.y < b.min.y - 0.5f || g.point.y > b.min.y + 3.5f || g.point.y > 1.5f && b.min.y < 1f) continue;
                    var eye = g.point + Vector3.up * 1.6f;
                    if (Physics.CheckSphere(g.point + Vector3.up * 1f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    if (Physics.Linecast(eye, c, out var hit, ~0, QueryTriggerInteraction.Ignore) && Vector3.Distance(hit.point, c) > b.extents.magnitude + 0.5f) continue;
                    if (CrewInTheWay(eye, c)) continue;
                    return g.point + Vector3.up * 0.05f;
                }
            return Ground(c + new Vector3(0, 0, -dist));
        }

        // A crew member standing between the eye and the target (greybox crews carry no colliders).
        static bool CrewInTheWay(Vector3 eye, Vector3 target)
        {
            foreach (var m in UnityEngine.Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None))
            {
                if (!m.isActiveAndEnabled) continue;
                var p = m.transform.position + Vector3.up * 1.1f;
                var t = Vector3.Dot(p - eye, (target - eye).normalized);
                if (t < -0.5f || t > Vector3.Distance(eye, target)) continue;
                if (Vector3.Distance(eye + (target - eye).normalized * t, p) < 0.9f) return true;
            }
            return false;
        }

        // Standing spot around an elevated target (decks, roofs): the direction whose sight line stays farthest from the crew.
        static Vector3 ClearSpot(Bounds b, float dist)
        {
            var c = b.center; var best = Ground(c + new Vector3(0, 0, -dist)); var bestScore = -1f;
            for (var i = 0; i < 12; i++)
            {
                var a = i * Mathf.PI / 6f;
                var p = new Vector3(c.x + Mathf.Cos(a) * dist, b.max.y + 2f, c.z + Mathf.Sin(a) * dist);
                if (!Physics.Raycast(p, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Mathf.Abs(g.point.y - b.min.y) > 0.8f) continue;            // same floor as the target
                var eye = g.point + Vector3.up * 1.6f;
                var score = 99f;
                foreach (var m in UnityEngine.Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None))
                {
                    if (!m.isActiveAndEnabled) continue;
                    var q = m.transform.position + Vector3.up * 1.1f;
                    var dir = (c - eye).normalized; var t = Mathf.Clamp(Vector3.Dot(q - eye, dir), -1f, Vector3.Distance(eye, c));
                    score = Mathf.Min(score, Vector3.Distance(eye + dir * t, q));
                }
                if (score > bestScore) { bestScore = score; best = g.point + Vector3.up * 0.05f; }
            }
            return best;
        }

        static Vector3 LookPoint(SiteCondition c) => c.PhotoBounds.center;

        // Walk from where the player stands to `to`, eyes drifting from a scan toward `look`.
        static IEnumerator Walk(Vector3 to, Vector3 look, float seconds, float scan = 0f)
        {
            var from = P.transform.position;
            var startLook = P.View.transform.position + P.View.transform.forward * 8f;
            yield return Film(seconds, t =>
            {
                var s = Smooth(t);
                var pos = Vector3.Lerp(from, to, s);
                pos = Ground(pos);
                pos.y += Mathf.Sin(t * seconds * 9f) * 0.012f * (1f - Mathf.Abs(2 * t - 1));   // light step bob
                var side = scan * Mathf.Sin(t * Mathf.PI * 2f) * (1f - s);
                var l = Vector3.Lerp(startLook, look, Mathf.Clamp01(s * 1.4f));
                var right = Vector3.Cross(Vector3.up, (l - pos).normalized);
                Pose(pos, l + right * side);
            });
        }

        static IEnumerator Turn(Vector3 look, float seconds)
        {
            var at = P.transform.position;
            var start = P.View.transform.position + P.View.transform.forward * Vector3.Distance(P.View.transform.position, look);
            yield return Film(seconds, t => Pose(at, Vector3.Lerp(start, look, Smooth(t))));
        }

        static bool Click(string prefix)
        {
            var b = UnityEngine.Object.FindObjectsByType<Button>(FindObjectsSortMode.None)
                .Where(x => x.isActiveAndEnabled && x.name.StartsWith(prefix, StringComparison.Ordinal)).OrderBy(x => x.name.Length).FirstOrDefault();
            if (b == null) { Debug.LogWarning("[Demo] no button " + prefix); return false; }
            b.onClick.Invoke(); return true;
        }

        static InputField Field() => UnityEngine.Object.FindObjectsByType<InputField>(FindObjectsSortMode.None).Where(f => f.isActiveAndEnabled).LastOrDefault();

        // Types into the tablet's active text box at ~22 characters a second.
        static IEnumerator Type(string text, float extraHold = 0.4f)
        {
            var seconds = text.Length / 22f;
            yield return Film(seconds + extraHold, t =>
            {
                var f = Field(); if (f == null) return;
                var n = Mathf.Min(text.Length, Mathf.CeilToInt(Mathf.Clamp01(t * (seconds + extraHold) / seconds) * text.Length));
                var s = text.Substring(0, n);
                if (f.text != s) { f.text = s; f.caretPosition = s.Length; }
            });
        }

        static IEnumerator Scroll(float from, float to, float seconds)
        {
            yield return Film(seconds, t =>
            {
                var sr = UnityEngine.Object.FindObjectsByType<ScrollRect>(FindObjectsSortMode.None).FirstOrDefault(s => s.isActiveAndEnabled);
                if (sr != null) sr.verticalNormalizedPosition = Mathf.Lerp(from, to, Smooth(t));
            });
        }

        static void NameTags(bool on)
        {
            foreach (var t in UnityEngine.Object.FindObjectsByType<NameTag>(FindObjectsSortMode.None))
                foreach (var c in t.GetComponentsInChildren<Canvas>(true)) c.enabled = on;
        }

        static void GoldenSun(Vector3 camForward)
        {
            var sun = UnityEngine.Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
            if (sun == null) return;
            var yaw = Quaternion.LookRotation(new Vector3(camForward.x, 0, camForward.z)).eulerAngles.y;
            sun.transform.rotation = Quaternion.Euler(13f, yaw + 140f, 0f);
            sun.color = new Color(1f, .78f, .55f); sun.intensity *= 1.15f;
        }

        // Free camera (no HUD) orbiting `center`.
        static IEnumerator Orbit(Vector3 center, float radius, float height, float a0, float a1, float seconds, float fov = 40f)
        {
            var go = new GameObject("DemoOrbit"); var oc = go.AddComponent<Camera>(); oc.CopyFrom(P.View); oc.enabled = false; oc.fieldOfView = fov; oc.targetTexture = rt;
            var keep = cam; cam = oc;
            NameTags(false);
            yield return Film(seconds, t =>
            {
                var a = Mathf.Lerp(a0, a1, t) * Mathf.Deg2Rad;
                oc.transform.position = center + new Vector3(Mathf.Cos(a) * radius, height, Mathf.Sin(a) * radius);
                oc.transform.rotation = Quaternion.LookRotation(center + Vector3.up * 0.5f - oc.transform.position);
            });
            NameTags(true);
            cam = keep; UnityEngine.Object.Destroy(go);
        }

        // Free camera (no HUD) dollying from -> to, looking at `look`.
        static IEnumerator Dolly(Vector3 from, Vector3 to, Vector3 look, float seconds, float fov = 42f)
        {
            var go = new GameObject("DemoDolly"); var oc = go.AddComponent<Camera>(); oc.CopyFrom(P.View); oc.enabled = false; oc.fieldOfView = fov; oc.targetTexture = rt;
            var keep = cam; cam = oc;
            NameTags(false);
            yield return Film(seconds, t =>
            {
                oc.transform.position = Vector3.Lerp(from, to, Smooth(t));
                oc.transform.rotation = Quaternion.LookRotation(look - oc.transform.position);
            });
            NameTags(true);
            cam = keep; UnityEngine.Object.Destroy(go);
        }

        static IEnumerator Install(SiteCondition c)
        {
            var d = D;
            d.Photograph(c); yield return null;
            d.Report(c.Spec.Energy, c.Spec.Probability, c.Spec.Severity); yield return null;
            d.Control(ControlLevel.Engineering); yield return null;
            if (d.KitOptions != null) d.ChooseKit(d.KitCorrect);
            d.PickUpKit(); yield return null;
            d.PlaceKit(c, c.PhotoBounds.center); yield return Frames(16);   // past the shutter flash
        }

        static void NearMiss(ShiftDirector d, string id)
        {
            d.Session.GetEvidence(id).BecameIncident = true;
            typeof(ShiftDirector).GetMethod("Handle", BindingFlags.NonPublic | BindingFlags.Instance)
                .Invoke(d, new object[] { new DayEvent(DayEventKind.NearMiss, id, d.Session.Clock) });
        }

        // ---------- the shoot ----------
        [UnityTest, Timeout(3600000)]
        public IEnumerator A_Menu_And_Hero()
        {
            File.Delete("Captures/demo/manifest.txt");
            // Menu: the course and the Hazard Hunt strip.
            ArcadeMode.Exit();
            EpisodeDirector.Selected = null; ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = false;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return Frames(10);
            var eps = UnityEngine.Object.FindFirstObjectByType<EpisodeDirector>();
            cam = eps.CinematicCamera; cam.targetTexture = rt; yield return Frames(3);
            Seg("02_menu");
            yield return Film(9f);
            eps.ShowTab(EpisodeDirector.Tab.Gear); yield return Film(2.5f);
            eps.ShowTab(EpisodeDirector.Tab.Crew); yield return Film(2.5f);
            Close();

            // Hero: EP2 trench in a low warm sun, no HUD (hook and outro).
            yield return Load(2, true);
            var ep = Episodes.Get(2);
            var s0 = ep.Shots[0];
            var center = V(s0.LookAt);
            var r = Mathf.Clamp(new Vector2(s0.From[0] - center.x, s0.From[2] - center.z).magnitude, 10f, 22f);
            var a = Mathf.Atan2(s0.From[2] - center.z, s0.From[0] - center.x) * Mathf.Rad2Deg;
            Seg("01_hook");
            yield return Orbit(center, r * 0.85f, 4.2f, a + 95f, a + 132f, 13f, 42f);
            Close();
            Close();

            // Outro: Thursday's pick, the crane flying the roof beams with the signal person on the radio.
            yield return Load(4, true);
            var crane = UnityEngine.Object.FindFirstObjectByType<CraneRig>();
            var beams = GameObject.Find("SuspendedBeams");
            var mid = beams != null && crane != null ? (beams.transform.position + crane.Slew.position) * 0.5f + Vector3.up * 2.5f : center;
            Seg("12_outro");
            yield return Orbit(mid, 19f, 6.5f, 70f, 105f, 13f, 46f);
            Close();
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator B_HazardHunt_Monday()
        {
            yield return Load(1, true, daily: true);
            var d = D;
            var gfci = C("mon-no-gfci");
            var look = d.Conditions.FirstOrDefault(c => c != null && c.Id == "mon-gfci-ok" && c.isActiveAndEnabled)
                       ?? d.Conditions.First(c => c != null && !c.IsHazard && c.isActiveAndEnabled);
            var cord = C("mon-damaged-cord");
            var ladder = C("mon-trailer-ladder");

            // 03: walk onto the site with the round's HUD running.
            var near = Vantage(gfci.PhotoBounds, 3.5f);
            var start = P.transform.position;
            if (Vector3.Distance(start, near) > 16f) Pose(Ground(near + (start - near).normalized * 16f), LookPoint(gfci) + Vector3.up * 1.5f);
            Seg("03_hunt");
            yield return Walk(Vector3.Lerp(P.transform.position, near, 0.85f), LookPoint(gfci), 11f, scan: 4f);
            Close();

            // 04: photograph -> energy, risk, control -> kit -> install; then a look-alike reported by mistake.
            Seg("04_report");
            yield return Walk(near, LookPoint(gfci), 1.5f);
            d.Photograph(gfci); yield return Film(3f);
            d.Report(gfci.Spec.Energy, gfci.Spec.Probability, gfci.Spec.Severity); yield return Film(3.5f);
            d.Control(ControlLevel.Engineering); yield return Film(0.5f);
            if (d.KitOptions != null) { yield return Film(2.2f); d.ChooseKit(d.KitCorrect); yield return Film(0.8f); }
            d.PickUpKit(); yield return Film(1.2f);
            d.PlaceKit(gfci, gfci.PhotoBounds.center); yield return null;
            d.HandsOn.SimAim(gfci.PhotoBounds.center); yield return Film(1.6f);
            d.HandsOn.SimHoldComplete(); yield return Film(2.4f);
            if (d.MenuOpen) d.ToggleTablet();
            var lnear = Vantage(look.PhotoBounds, 3f);
            yield return Walk(lnear, LookPoint(look), 3f);
            d.Photograph(look); yield return Film(2.2f);
            d.Report(look.Spec.Energy, Mathf.Max(1, look.Spec.Probability), Mathf.Max(1, look.Spec.Severity)); yield return Film(4f);
            if (d.MenuOpen) d.ToggleTablet();
            Close();

            // 05b: pick up the cord and turn it until the damage faces you.
            var cnear = Vantage(cord.PhotoBounds, 2.4f);
            Pose(cnear, LookPoint(cord));
            Seg("05b_cord");
            yield return Film(0.6f);
            d.Photograph(cord); yield return Film(1.6f);
            d.BeginInspect(); yield return null;
            var item = GameObject.Find("InspectItem")?.transform;
            if (item != null)
            {
                var (r0, p0) = (item.rotation, item.position);
                d.HandsOn.FaceHotspot(true);
                var (r1, p1) = (item.rotation, item.position);
                item.SetPositionAndRotation(p0, r0);
                yield return Film(1.2f, t => item.rotation = Quaternion.AngleAxis(t * 60f, cam.transform.up) * r0);
                var rMid = item.rotation;
                yield return Film(2.2f, t => { var s = Smooth(t); item.SetPositionAndRotation(Vector3.Lerp(p0, p1, s), Quaternion.Slerp(rMid, r1, s)); });
                yield return Film(0.6f);
            }
            d.HandsOn.SimPrimary(); yield return Film(3f);
            Close();
            if (!d.MenuOpen) d.Photograph(cord);
            d.Report(cord.Spec.Energy, cord.Spec.Probability, cord.Spec.Severity); yield return null;
            d.Control(cord.Spec.BestFeasibleControl); yield return Frames(2);
            if (d.MenuOpen) d.ToggleTablet();

            // 09: a hazard left alone becomes a near miss.
            var lad = Vantage(ladder.PhotoBounds, 9f, 1.2f);
            Pose(lad, LookPoint(ladder));
            Seg("09_incident");
            yield return Film(1.5f, t => Pose(lad, LookPoint(ladder) + Vector3.right * (t - .5f) * 0.6f));
            NearMiss(d, ladder.Id);
            yield return Film(3.2f);
            // The stop-down review in the learner's own words (IncidentReview rubric).
            yield return Scroll(1f, 0f, 0.8f);
            yield return Type("The ladder kicked out because it wasn't tied off or extended 3 ft, and nobody checked it. We tie it off before anyone climbs.", 0.2f);
            if (!Click("File the review")) d.AcknowledgeIncident();
            yield return Film(2.2f);
            Close();

            // 10: results grid, share card and the daily board.
            d.EndShift(); yield return Frames(3);
            FirstPersonTablet.FullView = true; yield return Frames(3);
            Seg("10_results");
            yield return Film(4.5f);
            yield return Scroll(1f, 0f, 6f);
            yield return Film(2.5f);
            Close();
            FirstPersonTablet.FullView = false;
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator C_Tuesday_Measure_Barricade_Interview_SpeakUp()
        {
            yield return Load(2, true);
            var d = D;

            // 05a: laser across the spoil pile.
            var spoil = C("tue-spoil-at-edge");
            var b = spoil.PhotoBounds;
            Pose(Vantage(b, 6f, 1.9f), b.center);
            Seg("05a_laser");
            yield return Film(0.6f);
            d.Photograph(spoil); yield return Film(1.5f);
            d.BeginMeasure(GearId.LaserMeasure); yield return Film(0.8f);
            var pa = new Vector3(b.min.x, b.min.y, b.center.z); var pb = new Vector3(b.max.x, b.min.y, b.center.z);
            d.HandsOn.SimAim(pa); d.HandsOn.SimPrimary(); yield return Film(0.5f);
            yield return Film(1.6f, t => d.HandsOn.SimAim(Vector3.Lerp(pa, pb, Smooth(t))));
            d.HandsOn.SimPrimary(); yield return Film(3f);
            if (d.MenuOpen) d.ToggleTablet();
            Close();

            // 06c: cones around the excavator's swing path.
            var swing = C("tue-swing-radius");
            yield return Install(swing);
            var h = d.HandsOn;
            if (h.Current == HandsOn.Mode.Barricade)
            {
                var ctr = h.SwingCenter; var r = h.SwingRadius;
                var stand = Ground(ctr + new Vector3(0, 0, -(r * 1.15f + 6f)));
                Pose(stand, ctr);
                Seg("06c_barricade");
                yield return Film(0.6f);
                for (var i = 0; i < 7; i++)
                {
                    var a = -Mathf.PI / 2 + i * Mathf.PI * 2 / 7f;
                    var at = ctr + new Vector3(Mathf.Cos(a) * r * 1.15f, 0, Mathf.Sin(a) * r * 1.15f);
                    h.PlaceCone(at); yield return Film(0.45f, t => Pose(stand, Vector3.Lerp(ctr, at, 0.35f)));
                }
                yield return Orbit(ctr, r * 1.7f + 3f, r * 1.1f + 4f, 250f, 290f, 3f, 46f);
                h.SimDone(); yield return Film(1.6f);
                if (d.MenuOpen) d.ToggleTablet();
                Close();
            }
            else Debug.LogWarning("[Demo] barricade mode did not open: " + h.Current);

            // 07: ask Dolores in your own words; her answer is a lead.
            var dolores = Crew("Dolores");
            var dp = dolores.transform.position;
            Pose(Vantage(new Bounds(dp + Vector3.up, Vector3.one), 2.4f), dp + Vector3.up * 1.6f);
            Seg("07_interview");
            yield return Film(0.8f);
            d.StartTalk(dolores); yield return Film(1.2f);
            yield return Type("Did last night's rain do anything to the trench walls?");
            Click("Send"); yield return Film(4.5f);
            d.EndTalk(); yield return null;
            var trench = C("tue-no-protective-system");
            yield return Turn(LookPoint(trench), 1.6f);
            yield return Film(0.8f);
            Close();

            // 08b: stop the work; Ray pushes back; answer in your own words.
            Pose(Vantage(trench.PhotoBounds, 4.5f), LookPoint(trench));
            Seg("08b_speakup");
            d.Photograph(trench); yield return Film(1.4f);
            d.Report(trench.Spec.Energy, trench.Spec.Probability, trench.Spec.Severity); yield return Film(1.6f);
            d.StopWork(); yield return Film(2.4f);
            if (d.PendingSpeakUp != null)
            {
                yield return Type("It stays stopped until the box is in. I'll help the crew set it so we lose as little time as possible.");
                Click("Say it"); yield return Film(3.5f);
            }
            Close();
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator D_Wednesday_Midrail_Cover()
        {
            yield return Load(3, true);
            var d = D;
            var mid = C("wed-missing-midrail");
            Pose(Vantage(mid.PhotoBounds, 3f), LookPoint(mid));
            yield return Install(mid);
            if (d.HandsOn.Current == HandsOn.Mode.Midrail)
            {
                Seg("06a_midrail");
                d.HandsOn.SetMidrailInches(6f);
                yield return Film(0.8f);
                yield return Film(2.6f, t => d.HandsOn.SetMidrailInches(Mathf.Lerp(6f, 21f, Smooth(t))));
                yield return Film(0.4f);
                d.HandsOn.SimPrimary(); yield return Film(2f);
                if (d.MenuOpen) d.ToggleTablet();
                Close();
            }
            else Debug.LogWarning("[Demo] midrail mode did not open: " + d.HandsOn.Current);

            var hole = C("wed-open-hole");
            var hb = hole.PhotoBounds;
            Pose(ClearSpot(hb, 2.8f), hb.center);
            yield return Install(hole);
            if (d.HandsOn.Current == HandsOn.Mode.Cover)
            {
                Seg("06b_cover");
                var from = hb.center + new Vector3(1.1f, 0, -0.6f);
                d.HandsOn.SetCover(from, "");
                yield return Film(0.6f);
                yield return Film(1.8f, t => d.HandsOn.SetCover(Vector3.Lerp(from, hb.center, Smooth(t)), ""));
                yield return Film(0.5f);
                d.HandsOn.SetCover(hb.center, "HOLE"); yield return Film(1.3f);
                d.HandsOn.SimPrimary(); yield return Film(2f);
                if (d.MenuOpen) d.ToggleTablet();
                Close();
            }
            else Debug.LogWarning("[Demo] cover mode did not open: " + d.HandsOn.Current);
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator E_Thursday_Coaching()
        {
            yield return Load(4, true);
            var d = D;
            var edge = C("thu-roof-edge");
            Pose(Vantage(edge.PhotoBounds, 8f), LookPoint(edge));
            Seg("08a_coaching");
            yield return Film(1f);
            d.Photograph(edge); yield return Film(1.4f);
            d.Report(edge.Spec.Energy, edge.Spec.Probability, edge.Spec.Severity); yield return Film(1.4f);
            d.Control(ControlLevel.Administrative); yield return Film(2f);
            if (d.PendingCoaching != null)
            {
                yield return Type(CoachingRubric.Samples(edge.Id)[0]);
                Click("Say it"); yield return Film(3f);
            }
            Close();
        }

        [UnityTest, Timeout(3600000)]
        public IEnumerator F_Course_Tuesday()
        {
            yield return Load(2, false);
            var d = D;
            Seg("11_course");
            yield return Film(1.6f);
            foreach (CheckInStation.Kind k in Enum.GetValues(typeof(CheckInStation.Kind))) { d.CheckIn(k); yield return Film(0.35f); }
            d.SubmitHierarchy(HierarchyOrdering.Correct); yield return Film(1.8f);
            var q = 0;
            while (d.Quiz != null && !d.Quiz.Done && q++ < 6) { d.AnswerQuiz(d.Quiz.Current.Correct); yield return Film(0.5f); }
            d.Begin(); yield return null;
            var trench = C("tue-no-protective-system"); var spoil = C("tue-spoil-at-edge");
            Pose(Vantage(trench.PhotoBounds, 5f), LookPoint(trench));
            d.Photograph(trench); yield return null; d.ToggleTablet();
            d.Photograph(spoil); yield return null; d.ToggleTablet();
            yield return Film(0.8f);
            d.ToggleTablet(); yield return Film(0.6f);
            Click("Daily excavation inspection log"); yield return Film(1f);
            for (var i = 0; i < 3; i++) { Click("Soil classification"); yield return Film(0.3f); }
            Click("Water in or around"); yield return Film(0.35f);
            Click("Protective system"); yield return Film(0.35f);
            foreach (var c in new[] { trench, spoil }) { Click("[ ] "); yield return Film(0.35f); }
            yield return Type("Trench box set over the crew; spoil pulled back 2 ft.", 0.3f);
            Click("Sign and file the log"); yield return Film(1.6f);
            if (d.MenuOpen) d.ToggleTablet();
            d.Photograph(trench); yield return null;
            d.Report(trench.Spec.Energy, trench.Spec.Probability, trench.Spec.Severity); yield return null;
            d.EndShift(); yield return null;
            q = 0;
            while (d.Quiz != null && !d.Quiz.Done && q++ < 8) { d.AnswerQuiz(d.Quiz.Current.Correct); yield return null; }
            FirstPersonTablet.FullView = true; yield return Frames(3);
            yield return Film(1.2f);
            yield return Scroll(1f, 0f, 2.4f);
            yield return Type("Morning, everyone. The trench walls can cave in and bury someone. Nobody goes in until the box is set; OSHA requires it at 5 ft. Tell me if you see a crack.", 0.2f);
            Click("Brief the crew"); yield return Frames(2);
            yield return Scroll(1f, 0f, 1.6f);
            yield return Film(2f);
            Close();
            FirstPersonTablet.FullView = false;
            Assert.That(d.OwnWordsTalk.HasValue, Is.True, "toolbox talk scored");
        }
    
        [UnityTest, Timeout(3600000)]
        public IEnumerator P_Prologue()
        {
            ArcadeMode.Exit();
            EpisodeDirector.Selected = null; EpisodeDirector.SkipIntro = false; EpisodeDirector.ForcePrologue = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var eps = UnityEngine.Object.FindFirstObjectByType<EpisodeDirector>();
            cam = eps.CinematicCamera; cam.targetTexture = rt;
            Seg("p_prologue");
            for (var i = 0; i < Fps * 120 && eps.Current == EpisodeDirector.State.Intro; i++) { yield return null; Grab(); }
            yield return Film(1.5f);   // the menu comes up
            Close();
        }
    }
}
