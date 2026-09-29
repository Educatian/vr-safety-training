using System.Collections.Generic;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Site-plan minimap (no extra camera: the logistics plan from Tools/layout is the map, 8 px/m).
    // North-up; player arrow rotates. Shows crew and the conditions you've already reported, never unfound hazards.
    // M toggles the full-site plan; the full plan also closes with Esc, Tab, a click/tap on it, or its X button.
    // Clicking the corner map opens it (playtest 2026-09-29: "the map maxed and would not come back").
    public sealed class Minimap : MonoBehaviour
    {
        const float X0 = -4, Z0 = -4, W = 128, H = 88;   // metres covered by Resources/UI/Minimap.png
        const float LocalSpan = 36f;                     // metres shown across the corner map

        [SerializeField] private RawImage map;
        [SerializeField] private RectTransform frame, arrow, scale;
        [SerializeField] private RawImage bezel;
        private readonly List<(Transform target, RectTransform dot, SiteCondition condition)> marks = new List<(Transform, RectTransform, SiteCondition)>();
        private ShiftDirector director;
        private SitePlayer player;
        private bool full;

        public bool Full => full;
        private GameObject closeButton, closeHint;
        public void Toggle() { full = !full; Layout(); }
        public void Close() { if (full) Toggle(); }

        public void Build(Transform canvas)
        {
            frame = new GameObject("Minimap", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>();
            frame.SetParent(canvas, false);
            frame.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.07f, 0.85f);
            map = new GameObject("Plan", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            map.transform.SetParent(frame, false); Fill(map.rectTransform);
            map.texture = Resources.Load<Texture2D>("UI/Minimap");   // plan fallback; Start swaps in the day's drone survey
            bezel = new GameObject("Bezel", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            bezel.transform.SetParent(frame.parent, false);
            bezel.texture = Resources.Load<Texture2D>("UI/MinimapBezel"); bezel.raycastTarget = false;
            scale = new GameObject("Scale", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            scale.SetParent(frame, false); scale.GetComponent<Image>().color = Color.white;
            scale.anchorMin = scale.anchorMax = scale.pivot = new Vector2(0, 0); scale.anchoredPosition = new Vector2(14, 14);
            scale.sizeDelta = new Vector2(300f * 10f / LocalSpan, 5);
            var label = new GameObject("ScaleText", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            label.transform.SetParent(scale, false);
            label.rectTransform.anchorMin = new Vector2(0, 1); label.rectTransform.anchorMax = new Vector2(1, 1);
            label.rectTransform.pivot = new Vector2(0, 0); label.rectTransform.sizeDelta = new Vector2(0, 22); label.rectTransform.anchoredPosition = new Vector2(0, 2);
            label.font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            label.text = "10 m"; label.fontSize = 18; label.color = Color.white; label.raycastTarget = false;
            arrow = Dot(new Color(0.9f, 0.2f, 0.17f), 18, "");
            var nose = new GameObject("Heading", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            nose.SetParent(arrow, false); nose.anchorMin = nose.anchorMax = new Vector2(0.5f, 1); nose.pivot = new Vector2(0.5f, 0);
            nose.sizeDelta = new Vector2(5, 14); nose.GetComponent<Image>().color = Color.white;
            Layout();
        }

        // Runtime-only controls: Build() runs in the editor and the scene is saved, and listeners added there are not
        // serialized, so the click-to-toggle, the X and the hint are wired here at play time.
        private void BuildControls()
        {
            if (closeButton != null) return;
            var stale = frame.Find("CloseMap"); if (stale != null) Destroy(stale.gameObject);
            stale = frame.Find("CloseHint"); if (stale != null) Destroy(stale.gameObject);
            // Click/tap the map: corner -> full plan, full plan -> corner.
            var btn = frame.GetComponent<Button>(); if (btn == null) btn = frame.gameObject.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.targetGraphic = frame.GetComponent<Image>();
            btn.onClick.AddListener(Toggle);
            // X on the full plan, plus how to close it.
            closeButton = new GameObject("CloseMap", typeof(RectTransform), typeof(Image), typeof(Button));
            closeButton.transform.SetParent(frame, false);
            var cr = (RectTransform)closeButton.transform;
            cr.anchorMin = cr.anchorMax = cr.pivot = Vector2.one; cr.sizeDelta = new Vector2(64, 64); cr.anchoredPosition = new Vector2(-14, -14);
            closeButton.GetComponent<Image>().color = new Color(0.1f, 0.12f, 0.13f, 0.9f);
            closeButton.GetComponent<Button>().onClick.AddListener(Close);
            foreach (var angle in new[] { 45f, -45f })
            {
                var bar = new GameObject("Bar", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
                bar.SetParent(cr, false); bar.anchorMin = bar.anchorMax = bar.pivot = new Vector2(0.5f, 0.5f);
                bar.sizeDelta = new Vector2(38, 6); bar.localEulerAngles = new Vector3(0, 0, angle);
                var bi = bar.GetComponent<Image>(); bi.color = new Color(1f, .78f, .1f); bi.raycastTarget = false;
            }
            var hint = new GameObject("CloseHint", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            hint.transform.SetParent(frame, false);
            hint.rectTransform.anchorMin = new Vector2(0, 0); hint.rectTransform.anchorMax = new Vector2(1, 0); hint.rectTransform.pivot = new Vector2(0.5f, 0);
            hint.rectTransform.sizeDelta = new Vector2(0, 40); hint.rectTransform.anchoredPosition = new Vector2(0, 10);
            hint.font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf"); hint.fontSize = 26; hint.color = Color.white; hint.alignment = TextAnchor.MiddleCenter; hint.raycastTarget = false;
            hint.text = "M, Esc or click to close";
            closeHint = hint.gameObject;
            Layout();
        }

        private void Start()
        {
            BuildControls();
            director = FindFirstObjectByType<ShiftDirector>();
            player = FindFirstObjectByType<SitePlayer>();
            var phases = FindFirstObjectByType<SitePhaseController>();
            var aerial = phases != null ? Resources.Load<Texture2D>("UI/Aerial_" + phases.Day) : null;
            if (aerial != null) map.texture = aerial;
            foreach (var crew in FindObjectsByType<CrewMember>(FindObjectsSortMode.None))
                marks.Add((crew.transform, Dot(new Color(0.2f, 0.7f, 1f), 16, crew.DisplayName.Substring(0, 1)), null));
        }

        private bool conditionsAdded;
        private void AddConditions()
        {
            if (conditionsAdded || director == null || director.Session == null) return;
            conditionsAdded = true;
            foreach (var c in director.Conditions)
                marks.Add((c.transform, Dot(new Color(1f, .78f, .1f), 14, "!"), c));
            arrow.SetAsLastSibling();
        }

        private void Layout()
        {
            // Corner: 300 px square, top-right. Full: most of the screen at the plan's aspect.
            frame.anchorMin = frame.anchorMax = frame.pivot = full ? new Vector2(0.5f, 0.5f) : new Vector2(1, 1);
            frame.anchoredPosition = full ? Vector2.zero : new Vector2(-24, -24);
            frame.sizeDelta = full ? new Vector2(1500, 1500 * H / W) : new Vector2(300, 300);
            // Rugged bezel (same kit as the tablet) wraps the corner map only; the scale bar only makes sense zoomed in.
            var b = bezel.rectTransform; b.anchorMin = b.anchorMax = b.pivot = new Vector2(1, 1);
            b.anchoredPosition = new Vector2(-24 + 300f * 26 / 460, -24 + 300f * 26 / 460); b.sizeDelta = Vector2.one * 300f * 512 / 460;
            bezel.gameObject.SetActive(!full); scale.gameObject.SetActive(!full);
            if (closeButton != null) { closeButton.SetActive(full); closeButton.transform.SetAsLastSibling(); }
            if (closeHint != null) { closeHint.SetActive(full); closeHint.transform.SetAsLastSibling(); }
        }

        private void Update()
        {
            if (player == null || frame == null) return;
            AddConditions();
            var k = Keyboard.current;
            // Not while typing (crew chat input): an "m" in a question used to flip the map.
            // Not while paused or with the tablet up; the full plan closes when the tablet (or an alert on it) opens.
            var tabletUp = director != null && director.MenuOpen;
            if (k != null && !SitePlayer.Typing && !PauseMenu.Paused && !tabletUp && k.mKey.wasPressedThisFrame) Toggle();
            if (full && (tabletUp || PauseMenu.Paused)) Close();
            frame.gameObject.SetActive(!tabletUp);
            bezel.gameObject.SetActive(frame.gameObject.activeSelf && !full);

            var p = player.transform.position;
            Rect view = full ? new Rect(0, 0, 1, 1)
                : new Rect((p.x - X0 - LocalSpan / 2) / W, (p.z - Z0 - LocalSpan / 2) / H, LocalSpan / W, LocalSpan / H);
            map.uvRect = view;
            Place(arrow, p, view);
            arrow.localRotation = Quaternion.Euler(0, 0, -player.transform.eulerAngles.y);
            foreach (var (target, dot, condition) in marks)
            {
                var show = target != null && target.gameObject.activeInHierarchy &&
                           (condition == null || director.Session.GetState(condition.Id) != HazardState.Latent);
                dot.gameObject.SetActive(show && Place(dot, target.position, view));
            }
        }

        // Positions a marker from world metres into the visible window; false if it falls outside.
        private bool Place(RectTransform dot, Vector3 world, Rect view)
        {
            var u = ((world.x - X0) / W - view.x) / view.width;
            var v = ((world.z - Z0) / H - view.y) / view.height;
            dot.anchorMin = dot.anchorMax = new Vector2(u, v);
            dot.anchoredPosition = Vector2.zero;
            return u >= 0 && u <= 1 && v >= 0 && v <= 1;
        }

        private RectTransform Dot(Color color, float size, string glyph)
        {
            var rt = new GameObject("Mark", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            rt.SetParent(frame, false); rt.sizeDelta = new Vector2(size, size);
            rt.GetComponent<Image>().color = color;
            var t = new GameObject("Glyph", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(rt, false); Fill(t.rectTransform);
            t.font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            t.text = glyph; t.fontSize = (int)(size * 0.8f); t.color = Color.black; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            return rt;
        }

        static void Fill(RectTransform rt) { rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one; rt.offsetMin = rt.offsetMax = Vector2.zero; }
    }
}
