using UnityEngine;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Small name + trade label over a crew member's head; faces the camera and fades out past ~14 m.
    public sealed class NameTag : MonoBehaviour
    {
        [SerializeField] private string displayName = "";
        [SerializeField] private string trade = "";
        [SerializeField] private float height = 2.12f;
        private CanvasGroup group;
        private Transform label;

        public string DisplayName => displayName;
        public static bool Hidden;   // cinematics (game opening, episode intros) frame faces without labels
        public void Configure(string name, string role, float headHeight = 2.12f) { displayName = name; trade = role; height = headHeight; }

        private void Start()
        {
            var font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var go = new GameObject("NameTag", typeof(Canvas), typeof(CanvasGroup));
            label = go.transform;
            label.SetParent(transform, false);
            label.localPosition = Vector3.up * height;
            label.localScale = Vector3.one * 0.005f;                   // ~1 m wide at 200 px
            go.GetComponent<Canvas>().renderMode = RenderMode.WorldSpace;
            ((RectTransform)label).sizeDelta = new Vector2(420, 90);
            group = go.GetComponent<CanvasGroup>(); group.interactable = group.blocksRaycasts = false;
            Line(label, displayName, 44, Color.white, new Vector2(0, 0.42f), Vector2.one, font);
            Line(label, trade, 28, new Color(1f, .78f, .1f), Vector2.zero, new Vector2(1, 0.45f), font);
        }

        private void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null || label == null) return;
            label.rotation = Quaternion.LookRotation(label.position - cam.transform.position);
            var d = Vector3.Distance(cam.transform.position, label.position);
            group.alpha = Hidden ? 0f : Mathf.Clamp01((14f - d) / 4f);   // full inside 10 m, gone past 14 m
        }

        static void Line(Transform parent, string text, int size, Color color, Vector2 min, Vector2 max, Font font)
        {
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Outline)).GetComponent<Text>();
            t.transform.SetParent(parent, false);
            var rt = t.rectTransform; rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero;
            t.text = text; t.font = font; t.fontSize = size; t.color = color; t.alignment = TextAnchor.MiddleCenter;
            t.horizontalOverflow = HorizontalWrapMode.Overflow; t.verticalOverflow = VerticalWrapMode.Overflow; t.raycastTarget = false;
            t.GetComponent<Outline>().effectColor = new Color(0, 0, 0, .8f);
        }
    }
}
