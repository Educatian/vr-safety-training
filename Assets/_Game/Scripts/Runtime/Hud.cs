using System;
using System.Collections.Generic;
using System.Linq;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Crosshair + "E  Photograph"-style prompt, and the Episode 1 walk-through checklist with Dolores's guided first find.
    // Lives on the tablet canvas (so it is in every camera capture) and scales with the text-size setting.
    public sealed class Hud : MonoBehaviour
    {
        private ShiftDirector director;
        private SitePlayer player;
        private Image dot;
        private Text prompt, steps;
        private Vector3 start;
        private bool everCaptured;
        private GameObject beacon;
        private Font font;

        // Tutorial steps: text + done-check, evaluated in order (EP1, or whenever the setting is on for a new learner).
        private List<(string text, Func<bool> done)> tutorial;

        private void Start()
        {
            director = FindFirstObjectByType<ShiftDirector>();
            player = FindFirstObjectByType<SitePlayer>();
            font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            // Players draw the HUD after post-processing so heat blur / grading never smear text. (Editor captures keep
            // camera-space so screenshots include the UI.)
            var canvas = GetComponent<Canvas>();
            if (!Application.isEditor && canvas != null) canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = GetComponent<CanvasScaler>();
            if (scaler != null) scaler.referenceResolution /= GameSettings.TextScale;

            dot = new GameObject("Crosshair", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
            dot.transform.SetParent(transform, false); dot.raycastTarget = false;
            dot.rectTransform.sizeDelta = new Vector2(8, 8);
            prompt = Label("Prompt", 30, TextAnchor.UpperCenter, new Vector2(0.3f, 0.36f), new Vector2(0.7f, 0.47f));
            steps = Label("Tutorial", 24, TextAnchor.UpperLeft, new Vector2(0.015f, 0.55f), new Vector2(0.3f, 0.97f));
            if (player != null) start = player.transform.position;

            tutorial = new List<(string, Func<bool>)>
            {
                (MobileControls.Active ? "Drag the right side of the screen to look" : "Drag the mouse to look around", () => everCaptured || MobileControls.Active),
                (MobileControls.Active ? "Move with the left stick" : "Walk: W A S D, hold Shift to hurry", () => player != null && Vector3.Distance(player.transform.position, start) > 2.5f),
                (MobileControls.Active ? "Aim at the sign-in board and tap ACT" : "Aim at the sign-in board and press E", () => director.CheckedIn.Contains(CheckInStation.Kind.SignIn)),
                ("Take your hard hat, vest, glasses and gloves", () => director.CheckInComplete),
                ("Tablet: rank the controls, answer the toolbox talk, begin", () => director.Started),
                (MobileControls.Active ? "Dolores marked a cord. Frame it, tap ACT" : "Dolores marked a cord. Frame it, press E", () => director.Selected != null || AnyDetected()),
                ("Tag the energy, rate P and S, submit", AnyDetected),
                ("Choose a control. Engineering beats PPE.", () => director.Conditions.Any(c => director.Session.GetEvidence(c.Id).AppliedControl.HasValue)),
                (MobileControls.Active ? "MAP for the site plan. TABLET for your reports." : "Minimap: M. Tablet: Tab. Radio help: Dolores (E).", () => director.Session.Clock > 200 || director.Finished),
            };
        }

        // Sweat (GDD §20): drops bead at the top/sides and run down as heat strain builds over the shift.
        private HeatStrain heat;
        private Sprite dropSprite;
        private readonly System.Collections.Generic.List<(RectTransform rt, Image img, float speed, float life)> drops = new System.Collections.Generic.List<(RectTransform, Image, float, float)>();
        private float dropClock;

        private void Sweat()
        {
            if (heat == null) heat = FindFirstObjectByType<HeatStrain>();
            var s = heat != null && director.Current == ShiftDirector.Phase.Shift && !director.MenuOpen ? Mathf.InverseLerp(0.15f, 1f, heat.Strain) : 0f;
            if (dropSprite == null) dropSprite = MakeDrop();
            dropClock += Time.deltaTime * s * 2.2f;                  // up to ~2 new drops a second at full strain
            while (dropClock > 1f && drops.Count < 26)
            {
                dropClock -= 1f;
                var img = new GameObject("Sweat", typeof(RectTransform), typeof(Image)).GetComponent<Image>();
                img.transform.SetParent(transform, false); img.transform.SetAsFirstSibling(); img.sprite = dropSprite; img.raycastTarget = false;
                var rt = img.rectTransform;
                var edge = UnityEngine.Random.value;                              // mostly along the top and the sides, like brow sweat
                var x = edge < 0.6f ? UnityEngine.Random.Range(0.08f, 0.92f) : (edge < 0.8f ? UnityEngine.Random.Range(0.01f, 0.1f) : UnityEngine.Random.Range(0.9f, 0.99f));
                rt.anchorMin = rt.anchorMax = new Vector2(x, UnityEngine.Random.Range(0.86f, 1f));
                var size = UnityEngine.Random.Range(18f, 46f); rt.sizeDelta = new Vector2(size, size * 1.35f);
                drops.Add((rt, img, UnityEngine.Random.Range(0.02f, 0.07f), UnityEngine.Random.Range(3f, 6f)));
            }
            for (var i = drops.Count - 1; i >= 0; i--)
            {
                var (rt, img, speed, life) = drops[i];
                life -= Time.deltaTime;
                var a = rt.anchorMin; a.y -= speed * Time.deltaTime * (0.6f + Mathf.PerlinNoise(a.x * 9f, Time.time) * 0.8f);
                a.x += (Mathf.PerlinNoise(Time.time * 0.5f, a.y * 7f) - 0.5f) * 0.002f;
                rt.anchorMin = rt.anchorMax = a;
                img.color = new Color(1, 1, 1, Mathf.Clamp01(life / 1.5f) * 0.75f * Mathf.Max(0.35f, s));
                if (life <= 0 || a.y < -0.05f || s <= 0.01f && life < 1.5f) { Destroy(rt.gameObject); drops.RemoveAt(i); }
                else drops[i] = (rt, img, speed, life);
            }
        }

        // A clear bead: bright rim + highlight, faint body (reads as water on the lens/brow at any size).
        static Sprite MakeDrop()
        {
            const int w = 48, h = 64;
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (var y = 0; y < h; y++)
                for (var x = 0; x < w; x++)
                {
                    var u = (x - w / 2f) / (w / 2f); var v = (y - h * 0.42f) / (h * 0.42f);
                    var r = Mathf.Sqrt(u * u + (v < 0 ? v * v : v * v * 0.35f));   // round bottom, tapered top
                    var body = Mathf.Clamp01(1f - r);
                    var rim = Mathf.Clamp01(1f - Mathf.Abs(r - 0.85f) * 9f);
                    var hl = Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(-0.35f, 0.25f)) * 4f);
                    var a = r > 1 ? 0 : Mathf.Clamp01(body * 0.18f + rim * 0.55f + hl * 0.9f);
                    tex.SetPixel(x, y, new Color(0.9f + hl * 0.1f, 0.95f, 1f, a));
                }
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f));
        }

        private bool AnyDetected() => director.Session != null && director.Conditions.Any(c => director.Session.GetEvidence(c.Id).Detected);

        private void Update()
        {
            if (director == null) return;
            everCaptured |= player != null && player.HasLooked;
            Sweat();
            var text = director.AimPrompt(out var actionable);
            prompt.text = MobileControls.Active && text.StartsWith("E  ") ? "ACT: " + text.Substring(3) : text;
            var inWorld = !director.MenuOpen && !director.Finished && !PauseMenu.Paused;
            dot.enabled = inWorld;
            dot.color = actionable ? new Color(1f, .78f, .1f) : new Color(1, 1, 1, .75f);
            dot.rectTransform.sizeDelta = Vector2.one * (actionable ? 14 : 8);

            var show = GameSettings.Tutorial && director.Episode.Number == 1 && tutorial != null;
            var next = show ? tutorial.FindIndex(s => !s.done()) : -1;
            steps.enabled = show && next >= 0;
            if (steps.enabled)
                steps.text = "FIRST SHIFT\n" + string.Join("\n", tutorial.Select((s, i) => (i < next ? "[x] " : i == next ? "> " : "   ") + s.text).Take(next + 2));
            Beacon(show && next == 5);
        }

        // Guided first find (worked example, no hint-token cost): a slow-spinning marker over the damaged cord while
        // that step is active. XP is unaffected, but the find is flagged as cued so it is not read as unaided recognition.
        private void Beacon(bool on)
        {
            if (on && beacon == null)
            {
                var cord = director.Conditions.FirstOrDefault(c => c.Id == "mon-damaged-cord" && c.IsHazard)
                           ?? director.Conditions.FirstOrDefault(c => c.IsHazard);
                if (cord == null) return;
                director.MarkCued(cord.Id);
                beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(beacon.GetComponent<Collider>());
                beacon.name = "GuideBeacon";
                beacon.transform.position = cord.PhotoBounds.center + Vector3.up * (cord.PhotoBounds.extents.y + 1.2f);
                beacon.transform.localScale = new Vector3(0.24f, 0.34f, 0.24f);   // same diamond language as the mission and hint cues
                var r = beacon.GetComponent<Renderer>();
                r.material.color = new Color(1f, .78f, .1f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (beacon != null)
            {
                beacon.SetActive(on);
                beacon.transform.rotation = Quaternion.Euler(0, Time.time * 90, 0) * Quaternion.Euler(0, 0, 45);
            }
        }

        private Text Label(string name, int size, TextAnchor anchor, Vector2 min, Vector2 max)
        {
            var t = new GameObject(name, typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            t.transform.SetParent(transform, false);
            t.rectTransform.anchorMin = min; t.rectTransform.anchorMax = max; t.rectTransform.offsetMin = t.rectTransform.offsetMax = Vector2.zero;
            t.font = font; t.fontSize = size; t.alignment = anchor; t.color = Color.white; t.raycastTarget = false;
            t.GetComponent<Outline>().effectColor = new Color(0, 0, 0, .85f);
            return t;
        }
    }
}
