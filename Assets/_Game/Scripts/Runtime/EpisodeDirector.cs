using System.Collections;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Topic episodes (GDD §18): episode select -> title card + narrated flythrough (skippable) -> the day's shift.
    // Picking an episode reloads the one site scene with that day's phase; the week is the story arc.
    [DefaultExecutionOrder(-500)]
    public sealed class EpisodeDirector : MonoBehaviour
    {
        public static Episode Selected;          // null = show the episode menu
        public static bool SkipIntro;            // tests / "replay without intro"

        public enum State { Menu, Intro, Playing }
        public State Current { get; private set; } = State.Playing;
        public string Caption { get; private set; } = "";
        public string Speaker { get; private set; } = "";

        private Camera cine;
        private Canvas canvas;
        private Font font;
        private Text captionText, speakerText;
        private RectTransform titleCard;
        private SitePlayer player;
        private GameObject tabletCanvas;
        private bool skip;

        static readonly Color Accent = new Color(1f, .78f, .1f);

        public static WorkDay DayOf(Episode e) => (WorkDay)(1 << e.DayIndex);
        public static string Key(Episode e) => "episode_best_xp_" + e.Number;

        private void Awake()
        {
            font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var phases = FindFirstObjectByType<SitePhaseController>();
            if (Selected != null && phases != null) phases.SetDay(DayOf(Selected));
        }

        private void Start()
        {
            player = FindFirstObjectByType<SitePlayer>();
            var tablet = FindFirstObjectByType<FieldTablet>();
            tabletCanvas = tablet != null ? tablet.GetComponentInParent<Canvas>(true)?.rootCanvas.gameObject : null;
            if (Selected == null) { ShowMenu(); return; }
            if (!SkipIntro && Selected.Shots.Count > 0) StartCoroutine(Intro(Selected));
        }

        // ---------- shared cinematic rig ----------
        private void Rig(bool on)
        {
            if (on && cine == null)
            {
                cine = new GameObject("CinematicCamera", typeof(Camera), typeof(AudioListener)).GetComponent<Camera>();
                cine.fieldOfView = 38; cine.nearClipPlane = 0.1f; cine.depth = 10;
                var go = new GameObject("CinematicCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cine; canvas.planeDistance = 0.3f;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080); scaler.matchWidthOrHeight = 1;
            }
            if (cine != null) { cine.gameObject.SetActive(on); canvas.gameObject.SetActive(on); }
            if (player != null) { player.enabled = !on; player.View.enabled = !on; var l = player.View.GetComponent<AudioListener>(); if (l) l.enabled = !on; }
            if (tabletCanvas != null) tabletCanvas.SetActive(!on);
        }

        public Camera CinematicCamera => cine;

        // ---------- episode select ----------
        private void ShowMenu()
        {
            Current = State.Menu;
            Rig(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            var root = canvas.transform;
            Panel(root, "Shade", Vector2.zero, Vector2.one, new Color(0.03f, 0.04f, 0.05f, 0.55f));
            Text(root, "COMPETENT PERSON", 64, Accent, new Vector2(0.06f, 0.83f), new Vector2(0.94f, 0.94f), TextAnchor.MiddleLeft);
            Text(root, "One week on the Loblolly Creek Lift Station. Pick a topic to train.", 30, Color.white, new Vector2(0.06f, 0.76f), new Vector2(0.94f, 0.83f), TextAnchor.MiddleLeft);
            var n = Episodes.All.Count;
            for (var i = 0; i < n; i++)
            {
                var ep = Episodes.All[i];
                float x0 = 0.06f + i * 0.88f / n, x1 = x0 + 0.88f / n - 0.012f;
                var card = Panel(root, "EP" + ep.Number, new Vector2(x0, 0.12f), new Vector2(x1, 0.72f), new Color(0.07f, 0.09f, 0.1f, 0.92f));
                var art = Resources.Load<Texture2D>("Episodes/EP" + ep.Number);
                if (art != null)
                {
                    var img = new GameObject("Art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                    img.transform.SetParent(card, false); Stretch(img.rectTransform, new Vector2(0, 0.52f), Vector2.one);
                    img.texture = art; img.uvRect = new Rect(0.2f, 0, 0.6f, 1); // centre-crop 16:9 into the card
                    img.color = ep.Playable ? Color.white : new Color(.35f, .35f, .35f);
                }
                Text(card, $"EPISODE {ep.Number}", 22, Accent, new Vector2(0.06f, 0.43f), new Vector2(0.94f, 0.5f), TextAnchor.MiddleLeft);
                Text(card, ep.Title, 38, Color.white, new Vector2(0.06f, 0.34f), new Vector2(0.94f, 0.44f), TextAnchor.MiddleLeft);
                Text(card, ep.Topic, 22, new Color(.8f, .84f, .84f), new Vector2(0.06f, 0.22f), new Vector2(0.94f, 0.34f), TextAnchor.UpperLeft);
                Text(card, "29 CFR " + string.Join(" · ", ep.Standards), 16, new Color(.6f, .66f, .66f), new Vector2(0.06f, 0.13f), new Vector2(0.94f, 0.22f), TextAnchor.UpperLeft);
                var best = PlayerPrefs.GetInt(Key(ep), -1);
                var label = !ep.Playable ? "IN PRODUCTION" : best >= 0 ? $"REPLAY · BEST {best} XP" : "START";
                var button = Panel(card, "Play", new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.11f), ep.Playable ? Accent : new Color(.25f, .27f, .28f));
                Text(button, label, 22, ep.Playable ? new Color(.08f, .08f, .08f) : new Color(.6f, .6f, .6f), Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                if (ep.Playable) button.gameObject.AddComponent<Button>().onClick.AddListener(() => Play(ep));
            }
            StartCoroutine(Orbit());
        }

        private IEnumerator Orbit()
        {
            for (var t = 0f; Current == State.Menu; t += Time.unscaledDeltaTime)
            {
                var a = 0.35f + t * 0.03f;
                cine.transform.position = new Vector3(45 + Mathf.Cos(a) * 55, 26, 30 + Mathf.Sin(a) * 45);
                cine.transform.LookAt(new Vector3(42, 0, 30));
                yield return null;
            }
        }

        public static void Play(Episode ep)
        {
            Selected = ep;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        public static void BackToMenu()
        {
            Selected = null; SkipIntro = false;
            SceneManager.LoadScene(SceneManager.GetActiveScene().path);
        }

        // ---------- intro: title card -> narrated shots -> hand over to the player ----------
        private IEnumerator Intro(Episode ep)
        {
            Current = State.Intro;
            Rig(true);
            Panel(canvas.transform, "LetterboxTop", new Vector2(0, 0.88f), Vector2.one, Color.black);
            Panel(canvas.transform, "LetterboxBottom", Vector2.zero, new Vector2(1, 0.12f), Color.black);
            speakerText = Text(canvas.transform, "", 30, Accent, new Vector2(0.15f, 0.125f), new Vector2(0.85f, 0.17f), TextAnchor.LowerCenter);
            captionText = Text(canvas.transform, "", 34, Color.white, new Vector2(0.12f, 0.015f), new Vector2(0.88f, 0.115f), TextAnchor.MiddleCenter);
            Text(canvas.transform, "Space · skip", 20, new Color(1, 1, 1, .45f), new Vector2(0.85f, 0.89f), new Vector2(0.98f, 0.99f), TextAnchor.MiddleRight);
            StartCoroutine(WatchSkip());

            // Title card over the Higgsfield key art.
            titleCard = Panel(canvas.transform, "TitleCard", Vector2.zero, Vector2.one, Color.black);
            var art = Resources.Load<Texture2D>("Episodes/EP" + ep.Number);
            if (art != null)
            {
                var img = new GameObject("Art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
                img.transform.SetParent(titleCard, false); Stretch(img.rectTransform, new Vector2(0, 0.12f), new Vector2(1, 0.88f));
                img.texture = art; img.uvRect = new Rect(0, 0.06f, 1, 0.88f);
            }
            Panel(titleCard, "Scrim", Vector2.zero, new Vector2(0.55f, 1), new Color(0, 0, 0, 0.45f));
            Text(titleCard, $"EPISODE {ep.Number}", 34, Accent, new Vector2(0.06f, 0.56f), new Vector2(0.6f, 0.64f), TextAnchor.LowerLeft);
            Text(titleCard, ep.Title.ToUpperInvariant(), 110, Color.white, new Vector2(0.06f, 0.4f), new Vector2(0.7f, 0.57f), TextAnchor.MiddleLeft);
            Text(titleCard, ep.Topic, 32, new Color(.85f, .88f, .88f), new Vector2(0.06f, 0.33f), new Vector2(0.7f, 0.4f), TextAnchor.UpperLeft);
            titleCard.SetAsFirstSibling(); // letterbox + captions stay on top
            var fade = titleCard.gameObject.AddComponent<CanvasGroup>();
            Say(ep.ColdOpen[0]);
            cine.transform.position = Vec(ep.Shots[0].From); cine.transform.LookAt(Vec(ep.Shots[0].LookAt));
            yield return Wait(ep.ColdOpen[0].Seconds);
            for (var t = 0f; t < 1f && !skip; t += Time.deltaTime) { fade.alpha = 1 - t; yield return null; }
            titleCard.gameObject.SetActive(false);

            // Remaining lines play over the camera shots; the line index advances on its own clock.
            var line = 1; var lineClock = 0f;
            if (ep.ColdOpen.Count > 1) Say(ep.ColdOpen[line]);
            foreach (var shot in ep.Shots)
            {
                for (var t = 0f; t < shot.Seconds && !skip; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0, 1, t / shot.Seconds);
                    cine.transform.position = Vector3.Lerp(Vec(shot.From), Vec(shot.To), k);
                    cine.transform.rotation = Quaternion.LookRotation(Vec(shot.LookAt) - cine.transform.position);
                    lineClock += Time.deltaTime;
                    if (line + 1 < ep.ColdOpen.Count && lineClock >= ep.ColdOpen[line].Seconds) { line++; lineClock = 0; Say(ep.ColdOpen[line]); }
                    yield return null;
                }
            }
            // Let the last line finish if the shots ran short.
            while (!skip && line < ep.ColdOpen.Count && lineClock < ep.ColdOpen[line].Seconds) { lineClock += Time.deltaTime; yield return null; }
            EndIntro();
        }

        private void EndIntro()
        {
            StopAllCoroutines();
            Rig(false);
            Current = State.Playing;
            var shift = FindFirstObjectByType<ShiftDirector>();
            if (shift != null) shift.Say($"Episode {Selected.Number} · {Selected.Title}. Sign in at the gate and put on your PPE.");
        }

        public void Skip() => skip = true;

        private IEnumerator WatchSkip()
        {
            while (Current == State.Intro)
            {
                var k = Keyboard.current;
                if (k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame || k.escapeKey.wasPressedThisFrame)) { skip = true; EndIntro(); yield break; }
                if (skip) { EndIntro(); yield break; }
                yield return null;
            }
        }

        private IEnumerator Wait(float seconds) { for (var t = 0f; t < seconds && !skip; t += Time.deltaTime) yield return null; }

        private void Say(Line line)
        {
            Caption = line.Text; Speaker = line.Speaker;
            if (captionText != null) captionText.text = line.Text;
            if (speakerText != null) speakerText.text = line.Speaker.ToUpperInvariant();
        }

        // Epilogue after the closing debrief: plays as radio lines on the tablet notice.
        public static IEnumerator Epilogue(ShiftDirector shift)
        {
            if (Selected == null) yield break;
            foreach (var line in Selected.Epilogue)
            {
                shift.Say((line.Speaker.Length > 0 ? line.Speaker + ": " : "") + line.Text);
                yield return new WaitForSeconds(line.Seconds);
            }
        }

        static Vector3 Vec(float[] v) => new Vector3(v[0], v[1], v[2]);

        static void Stretch(RectTransform rt, Vector2 min, Vector2 max) { rt.anchorMin = min; rt.anchorMax = max; rt.offsetMin = rt.offsetMax = Vector2.zero; }

        static RectTransform Panel(Transform parent, string name, Vector2 min, Vector2 max, Color color)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = (RectTransform)go.transform; Stretch(rt, min, max);
            go.GetComponent<Image>().color = color;
            return rt;
        }

        private Text Text(Transform parent, string value, int size, Color color, Vector2 min, Vector2 max, TextAnchor anchor)
        {
            var go = new GameObject("Text", typeof(RectTransform), typeof(Text), typeof(Shadow));
            go.transform.SetParent(parent, false);
            Stretch((RectTransform)go.transform, min, max);
            var t = go.GetComponent<Text>();
            t.text = value; t.font = font; t.fontSize = size; t.color = color; t.alignment = anchor; t.raycastTarget = false;
            t.horizontalOverflow = HorizontalWrapMode.Wrap; t.verticalOverflow = VerticalWrapMode.Overflow;
            return t;
        }
    }
}
