using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Touch controls for phones/tablets (auto on touch devices; force with ?touch=1):
    // left thumb = move stick, right-side drag = look, buttons = ACT (E), TABLET (Tab), MAP (M), PAUSE (Esc).
    public sealed class MobileControls : MonoBehaviour
    {
        private static bool? active;
        public static bool Active => active ??= Detect();   // lazy: other Start()s may ask before ours runs
        public static Vector2 Move { get; private set; }
        private static Vector2 look;
        private static bool act, tablet, map, pause;

        public static Vector2 TakeLook() { var l = look; look = Vector2.zero; return l; }
        public static bool TakeAct() { var a = act; act = false; return a; }
        public static bool TakeTablet() { var a = tablet; tablet = false; return a; }
        public static bool TakeMap() { var a = map; map = false; return a; }
        public static bool TakePause() { var a = pause; pause = false; return a; }

        private RectTransform root, knob;
        private ShiftDirector director;

        public static bool Detect()
        {
            var url = Application.absoluteURL ?? "";
            if (url.Contains("touch=1")) return true;
            if (url.Contains("touch=0")) return false;
            return Application.isMobilePlatform || (Touchscreen.current != null && Mouse.current == null);
        }

        private void Start()
        {
            if (!Active) return;
            director = FindFirstObjectByType<ShiftDirector>();
            if (!PlayerPrefs.HasKey("set_quality")) GameSettings.Quality = 0;   // phones start on Low
            var font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            root = new GameObject("MobileControls", typeof(RectTransform)).GetComponent<RectTransform>();
            root.SetParent(transform, false); root.SetAsFirstSibling();              // tablet and menus stay on top
            Stretch(root, Vector2.zero, Vector2.one);

            // Look pad: the right 60% of the screen.
            var pad = Panel("LookPad", new Vector2(0.4f, 0), Vector2.one, new Color(0, 0, 0, 0));
            pad.gameObject.AddComponent<Drag>().OnDragged = d => look += d * 0.12f;

            // Move stick: bottom-left.
            var baseRing = Panel("Stick", new Vector2(0.03f, 0.17f), new Vector2(0.03f, 0.17f), new Color(1, 1, 1, 0.12f));   // above the radio line
            baseRing.sizeDelta = new Vector2(240, 240); baseRing.pivot = Vector2.zero;
            knob = Panel("Knob", new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Color(1f, .78f, .1f, 0.6f));
            knob.SetParent(baseRing, false); knob.sizeDelta = new Vector2(96, 96); knob.anchoredPosition = Vector2.zero;
            var stick = baseRing.gameObject.AddComponent<Drag>();
            stick.OnPointer = (pos, down) =>
            {
                if (!down) { Move = Vector2.zero; knob.anchoredPosition = Vector2.zero; return; }
                RectTransformUtility.ScreenPointToLocalPointInRectangle(baseRing, pos, stick.EventCamera, out var local);
                var v = Vector2.ClampMagnitude(local - baseRing.rect.size / 2, 100f);
                knob.anchoredPosition = v; Move = v / 100f;
            };

            // Buttons: bottom-right column.
            Button("ACT", new Vector2(0.86f, 0.14f), 150, () => act = true, font, true);
            Button("TABLET", new Vector2(0.74f, 0.14f), 110, () => tablet = true, font);
            Button("MAP", new Vector2(0.86f, 0.4f), 110, () => map = true, font);
            Button("PAUSE", new Vector2(0.72f, 0.84f), 90, () => pause = true, font);
        }

        private void Update()
        {
            if (!Active || root == null) return;
            var inWorld = director == null || (!director.MenuOpen && !director.Finished && !PauseMenu.Paused);
            root.gameObject.SetActive(inWorld);
            if (!inWorld) { Move = Vector2.zero; look = Vector2.zero; }
        }

        private RectTransform Panel(string name, Vector2 min, Vector2 max, Color c)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(root, false);
            var rt = (RectTransform)go.transform; Stretch(rt, min, max);
            go.GetComponent<Image>().color = c;
            return rt;
        }

        private void Button(string label, Vector2 anchor, float size, System.Action onTap, Font font, bool primary = false)
        {
            var rt = Panel(label, anchor, anchor, primary ? new Color(1f, .78f, .1f, 0.85f) : new Color(0.1f, 0.12f, 0.13f, 0.75f));
            rt.sizeDelta = new Vector2(size, size); rt.pivot = new Vector2(0.5f, 0);
            rt.gameObject.AddComponent<UnityEngine.UI.Button>().onClick.AddListener(() => onTap());
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(rt, false); Stretch(t.rectTransform, Vector2.zero, Vector2.one);
            t.text = label; t.font = font; t.fontSize = primary ? 40 : 28; t.alignment = TextAnchor.MiddleCenter; t.raycastTarget = false;
            t.color = primary ? new Color(.08f, .08f, .08f) : Color.white;
        }

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max) { rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        private sealed class Drag : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IDragHandler
        {
            public System.Action<Vector2> OnDragged;
            public System.Action<Vector2, bool> OnPointer;
            public Camera EventCamera { get; private set; }
            public void OnPointerDown(PointerEventData e) { EventCamera = e.pressEventCamera; OnPointer?.Invoke(e.position, true); }
            public void OnPointerUp(PointerEventData e) => OnPointer?.Invoke(e.position, false);
            public void OnDrag(PointerEventData e) { OnDragged?.Invoke(e.delta); OnPointer?.Invoke(e.position, true); }
        }
    }
}
