using System.Collections.Generic;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Site-plan minimap (no extra camera: the logistics plan from Tools/layout is the map, 8 px/m).
    // North-up; player arrow rotates. Shows crew and the conditions you've already reported, never unfound hazards.
    // M toggles the full-site plan.
    public sealed class Minimap : MonoBehaviour
    {
        const float X0 = -4, Z0 = -4, W = 128, H = 88;   // metres covered by Resources/UI/Minimap.png
        const float LocalSpan = 36f;                     // metres shown across the corner map

        [SerializeField] private RawImage map;
        [SerializeField] private RectTransform frame, arrow;
        private readonly List<(Transform target, RectTransform dot, SiteCondition condition)> marks = new List<(Transform, RectTransform, SiteCondition)>();
        private ShiftDirector director;
        private SitePlayer player;
        private bool full;

        public bool Full => full;
        public void Toggle() { full = !full; Layout(); }

        public void Build(Transform canvas)
        {
            frame = new GameObject("Minimap", typeof(RectTransform), typeof(Image), typeof(Mask)).GetComponent<RectTransform>();
            frame.SetParent(canvas, false);
            frame.GetComponent<Image>().color = new Color(0.05f, 0.06f, 0.07f, 0.85f);
            map = new GameObject("Plan", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            map.transform.SetParent(frame, false); Fill(map.rectTransform);
            map.texture = Resources.Load<Texture2D>("UI/Minimap");
            arrow = Dot(new Color(0.9f, 0.2f, 0.17f), 18, "");
            var nose = new GameObject("Heading", typeof(RectTransform), typeof(Image)).GetComponent<RectTransform>();
            nose.SetParent(arrow, false); nose.anchorMin = nose.anchorMax = new Vector2(0.5f, 1); nose.pivot = new Vector2(0.5f, 0);
            nose.sizeDelta = new Vector2(5, 14); nose.GetComponent<Image>().color = Color.white;
            Layout();
        }

        private void Start()
        {
            director = FindFirstObjectByType<ShiftDirector>();
            player = FindFirstObjectByType<SitePlayer>();
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
        }

        private void Update()
        {
            if (player == null || frame == null) return;
            AddConditions();
            var k = Keyboard.current;
            if (k != null && k.mKey.wasPressedThisFrame) Toggle();
            frame.gameObject.SetActive(director == null || !director.MenuOpen || full);

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
