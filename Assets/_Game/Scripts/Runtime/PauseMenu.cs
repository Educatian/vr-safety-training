using System;
using UnityEngine;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Esc: pause + settings + episode select. Time stops (the shift clock too).
    public sealed class PauseMenu : MonoBehaviour
    {
        public static bool Paused { get; private set; }
        private static PauseMenu instance;
        private RectTransform panel;
        private Font font;

        public static void Open() { if (instance != null) instance.Show(true); }
        public static void Close() { if (instance != null) instance.Show(false); }

        private void Awake() { instance = this; Paused = false; Time.timeScale = 1; }
        private void OnDestroy() { if (instance == this) { Paused = false; Time.timeScale = 1; } }

        private void Show(bool on)
        {
            Paused = on; Time.timeScale = on ? 0 : 1;
            if (panel != null) Destroy(panel.gameObject);
            if (!on) return;
            font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = new GameObject("Pause", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            panel.SetParent(transform, false);
            panel.anchorMin = new Vector2(0.36f, 0.12f); panel.anchorMax = new Vector2(0.64f, 0.88f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(.06f, .07f, .08f, .95f);
            var v = panel.GetComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(28, 28, 24, 24); v.spacing = 10;
            v.childControlHeight = v.childControlWidth = true; v.childForceExpandHeight = false;
            Row("PAUSED", null, 40, new Color(1f, .78f, .1f));
            Row("Resume", Close);
            Cycle(() => $"Volume  {Mathf.RoundToInt(GameSettings.MasterVolume * 100)}%", () => GameSettings.MasterVolume = GameSettings.MasterVolume > 0.99f ? 0f : Mathf.Min(1f, GameSettings.MasterVolume + 0.2f));
            Cycle(() => $"Mouse speed  {(GameSettings.MouseSensitivity < .1f ? "Slow" : GameSettings.MouseSensitivity < .17f ? "Medium" : "Fast")}",
                () => GameSettings.MouseSensitivity = GameSettings.MouseSensitivity < .1f ? .13f : GameSettings.MouseSensitivity < .17f ? .22f : .07f);
            Cycle(() => $"Invert look  {(GameSettings.InvertY ? "On" : "Off")}", () => GameSettings.InvertY = !GameSettings.InvertY);
            Cycle(() => $"Graphics  {new[] { "Low", "Medium", "High" }[GameSettings.Quality]}", () => GameSettings.Quality = (GameSettings.Quality + 1) % 3);
            Cycle(() => $"Text size  {Mathf.RoundToInt(GameSettings.TextScale * 100)}% (next episode)", () => GameSettings.TextScale = GameSettings.TextScale >= 1.5f ? 1f : GameSettings.TextScale + 0.25f);
            Cycle(() => $"Tutorial  {(GameSettings.Tutorial ? "On" : "Off")}", () => GameSettings.Tutorial = !GameSettings.Tutorial);
            Cycle(() => $"Visual cues  {new[] { "Auto (by level)", "Off", "Light", "Full" }[GameSettings.Guidance + 1]} (next shift)", () => GameSettings.Guidance = GameSettings.Guidance >= 2 ? -1 : GameSettings.Guidance + 1);
            Cycle(() => $"AI crew chat  {(GameSettings.AiConsent == 1 ? "Allowed" : "Offline answers")}", () => GameSettings.AiConsent = GameSettings.AiConsent == 1 ? 0 : 1);
            Row("Episode select", () => { Show(false); EpisodeDirector.BackToMenu(); });
            Row("Controls: click = look · WASD · E act · Tab tablet · M map · Esc pause", null, 18, new Color(.75f, .8f, .8f));
        }

        private void Cycle(Func<string> label, Action next)
        {
            Text text = null;
            text = Row(label(), () => { next(); PlayerPrefs.Save(); text.text = label(); AudioDirector.Play("click"); });
        }

        private Text Row(string label, Action onClick, int size = 26, Color? color = null)
        {
            var go = new GameObject("Row", typeof(RectTransform), typeof(Image), typeof(LayoutElement));
            go.transform.SetParent(panel, false);
            go.GetComponent<LayoutElement>().preferredHeight = size + 26;
            go.GetComponent<Image>().color = onClick == null ? Color.clear : new Color(.16f, .19f, .2f);
            if (onClick != null) go.AddComponent<Button>().onClick.AddListener(() => onClick());
            var t = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            t.transform.SetParent(go.transform, false);
            t.rectTransform.anchorMin = Vector2.zero; t.rectTransform.anchorMax = Vector2.one; t.rectTransform.offsetMin = new Vector2(14, 0); t.rectTransform.offsetMax = Vector2.zero;
            t.font = font; t.fontSize = size; t.color = color ?? Color.white; t.alignment = TextAnchor.MiddleLeft; t.text = label;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
