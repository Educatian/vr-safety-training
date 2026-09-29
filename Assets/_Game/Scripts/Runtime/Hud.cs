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
                (MobileControls.Active ? "Drag the right side of the screen to look" : "Click the view to look around (Esc frees the mouse)", () => everCaptured || MobileControls.Active),
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

        private bool AnyDetected() => director.Session != null && director.Conditions.Any(c => director.Session.GetEvidence(c.Id).Detected);

        private void Update()
        {
            if (director == null) return;
            everCaptured |= player != null && player.Captured;
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

        // Guided first find (no hint penalty): a slow-spinning marker over the damaged cord while that step is active.
        private void Beacon(bool on)
        {
            if (on && beacon == null)
            {
                var cord = director.Conditions.FirstOrDefault(c => c.Id == "mon-damaged-cord" && c.IsHazard)
                           ?? director.Conditions.FirstOrDefault(c => c.IsHazard);
                if (cord == null) return;
                beacon = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Destroy(beacon.GetComponent<Collider>());
                beacon.name = "GuideBeacon";
                beacon.transform.position = cord.PhotoBounds.center + Vector3.up * (cord.PhotoBounds.extents.y + 1.2f);
                beacon.transform.localScale = Vector3.one * 0.3f;
                var r = beacon.GetComponent<Renderer>();
                r.material.color = new Color(1f, .78f, .1f);
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }
            if (beacon != null)
            {
                beacon.SetActive(on);
                beacon.transform.rotation = Quaternion.Euler(45, Time.time * 90, 45);
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
