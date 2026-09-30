using System;
using UnityEngine;
using UnityEngine.InputSystem;
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

        private Transform host;
        private static int openedFrame = -1;
        private static int closedFrame = -1;
        public static int OpenedFrame => openedFrame;
        // True on the frame the menu opened or closed: other Esc handlers skip it (script order would otherwise reopen it).
        public static bool EscHandledThisFrame => Time.frameCount == openedFrame || Time.frameCount == closedFrame;

        // The panel lives on its own overlay canvas, so it also works on the episode menu (which hides the tablet
        // canvas this component sits on) and while driving; PauseKeys there handles Esc to open/close.
        private void Awake()
        {
            instance = this; Paused = false; Time.timeScale = 1;
            var go = new GameObject("PauseCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(PauseKeys));
            var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 500;
            var sc = go.GetComponent<CanvasScaler>(); sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            sc.referenceResolution = new Vector2(1920, 1080); sc.matchWidthOrHeight = 1;
            host = go.transform;
        }
        private void OnDestroy() { if (instance == this) { Paused = false; Time.timeScale = 1; } }

        private void Show(bool on)
        {
            Paused = on; Time.timeScale = on ? 0 : 1;
            if (on) openedFrame = Time.frameCount; else closedFrame = Time.frameCount;
            if (panel != null) Destroy(panel.gameObject);
            if (!on) return;
            font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            panel = new GameObject("Pause", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup)).GetComponent<RectTransform>();
            panel.SetParent(host != null ? host : transform, false);
            panel.anchorMin = new Vector2(0.34f, 0.04f); panel.anchorMax = new Vector2(0.66f, 0.96f); panel.offsetMin = panel.offsetMax = Vector2.zero;
            panel.GetComponent<Image>().color = new Color(.06f, .07f, .08f, .95f);
            var v = panel.GetComponent<VerticalLayoutGroup>(); v.padding = new RectOffset(28, 28, 16, 16); v.spacing = 3;
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
            Cycle(() => $"Reduce motion  {(GameSettings.ReduceMotion ? "On (no camera sway or dolly)" : "Off")}", () => GameSettings.ReduceMotion = !GameSettings.ReduceMotion);
            Cycle(() => $"Sound captions  {(GameSettings.SoundCaptions ? "On" : "Off")}", () => GameSettings.SoundCaptions = !GameSettings.SoundCaptions);
            Cycle(() => $"Crew voices  {(GameSettings.CrewVoices ? "On (tone follows mood)" : "Off (captions only)")}", () => { GameSettings.CrewVoices = !GameSettings.CrewVoices; if (!GameSettings.CrewVoices) CrewVoice.Stop(); });
            Cycle(() => $"Visual cues  {new[] { "Auto (by level)", "Off", "Light", "Full" }[GameSettings.Guidance + 1]} (next shift)", () => GameSettings.Guidance = GameSettings.Guidance >= 2 ? -1 : GameSettings.Guidance + 1);
            Cycle(() => $"AI crew chat  {(GameSettings.AiConsent == 1 ? "Allowed" : "Offline answers")}", () => GameSettings.AiConsent = GameSettings.AiConsent == 1 ? 0 : 1);
            Cycle(() => $"Research data  {(GameSettings.ResearchConsent == 1 ? "Sharing (opted in)" : "Not shared")}", () => GameSettings.ResearchConsent = GameSettings.ResearchConsent == 1 ? 0 : 1);
            Cycle(() => $"Facilitator: all episodes  {(GameSettings.UnlockAll ? "Unlocked" : "Mastery gate")}", () => GameSettings.UnlockAll = !GameSettings.UnlockAll);
            WithdrawRow();
            ResetRow();
            Row("Episode select", () => { Show(false); EpisodeDirector.BackToMenu(); });
            Row("Controls: drag mouse = look · WASD · E act · Tab tablet (F full view) · M map · Esc pause", null, 18, new Color(.75f, .8f, .8f));
        }

        private void Cycle(Func<string> label, Action next)
        {
            Text text = null;
            text = Row(label(), () => { next(); PlayerPrefs.Save(); text.text = label(); AudioDirector.Play("click"); }, 20);
        }

        // Participant withdrawal (asks twice): deletes the research events this browser sent, on the server.
        private void WithdrawRow()
        {
            var n = Telemetry.RememberedSessions;
            if (n == 0) return;
            Text text = null; var armed = false;
            text = Row($"Withdraw my research data ({n} session{(n == 1 ? "" : "s")})", () =>
            {
                if (!armed) { armed = true; text.text = "Tap again to delete it from the research server"; return; }
                armed = false; text.text = "Withdrawing…";
                StartCoroutine(Telemetry.Withdraw(msg => { if (text != null) text.text = msg; }));
            }, 22);
        }

        // Facilitator: fresh participant on the same browser (career, mastery, banked hints, best scores). Asks twice.
        private void ResetRow()
        {
            Text text = null; var armed = false;
            text = Row("Reset progress (new participant)", () =>
            {
                if (!armed) { armed = true; text.text = "Tap again to erase career, mastery and scores"; return; }
                CareerStore.Reset(); MasteryStore.Reset();
                PlayerPrefs.DeleteKey("banked_hints");
                foreach (var ep in Jobsite.Core.Episodes.All) PlayerPrefs.DeleteKey(EpisodeDirector.Key(ep));
                PlayerPrefs.Save(); armed = false; text.text = "Progress erased";
            }, 22);
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

    // Esc on the pause canvas: closes the menu (any state), opens it on the episode menu (SitePlayer is off there).
    public sealed class PauseKeys : MonoBehaviour
    {
        private void Update()
        {
            var k = Keyboard.current;
            if (k == null || !k.escapeKey.wasPressedThisFrame || SitePlayer.Typing) return;
            if (PauseMenu.Paused) { if (Time.frameCount != PauseMenu.OpenedFrame) PauseMenu.Close(); return; }
            var ed = FindFirstObjectByType<EpisodeDirector>();
            if (ed != null && ed.Current == EpisodeDirector.State.Menu) PauseMenu.Open();
        }
    }
}
