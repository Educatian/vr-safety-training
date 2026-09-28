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
                cine.fieldOfView = 38; cine.nearClipPlane = 0.1f; cine.depth = 10; cine.tag = "MainCamera";
                var go = new GameObject("CinematicCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
                canvas = go.GetComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera; canvas.worldCamera = cine; canvas.planeDistance = 0.3f;
                var scaler = go.GetComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize; scaler.referenceResolution = new Vector2(1920, 1080) / GameSettings.TextScale; scaler.matchWidthOrHeight = 1;
            }
            if (cine != null) { cine.gameObject.SetActive(on); canvas.gameObject.SetActive(on); }
            if (player != null) { player.enabled = !on; player.View.enabled = !on; var l = player.View.GetComponent<AudioListener>(); if (l) l.enabled = !on; }
            if (tabletCanvas != null) tabletCanvas.SetActive(!on);
        }

        public Camera CinematicCamera => cine;

        // ---------- episode select / gear locker / crew ----------
        public enum Tab { Episodes, Gear, Crew }
        private RectTransform body;
        private Text profile;
        private Career career;

        private void ShowMenu()
        {
            Current = State.Menu;
            Rig(true);
            Cursor.lockState = CursorLockMode.None; Cursor.visible = true;
            career = CareerStore.Load();
            var root = canvas.transform;
            Panel(root, "Shade", Vector2.zero, Vector2.one, new Color(0.03f, 0.04f, 0.05f, 0.6f));
            Text(root, "COMPETENT PERSON", 64, Accent, new Vector2(0.06f, 0.85f), new Vector2(0.6f, 0.95f), TextAnchor.MiddleLeft);
            Text(root, "One week on the Loblolly Creek Lift Station, Autauga County, Alabama.", 28, Color.white, new Vector2(0.06f, 0.79f), new Vector2(0.7f, 0.85f), TextAnchor.MiddleLeft);
            profile = Text(root, "", 26, Color.white, new Vector2(0.6f, 0.85f), new Vector2(0.94f, 0.95f), TextAnchor.MiddleRight);
            var tabs = new[] { (Tab.Episodes, "EPISODES"), (Tab.Gear, "GEAR LOCKER"), (Tab.Crew, "CREW") };
            for (var i = 0; i < tabs.Length; i++)
            {
                var (tab, label) = tabs[i];
                var b = Panel(root, "Tab" + label, new Vector2(0.06f + i * 0.13f, 0.72f), new Vector2(0.18f + i * 0.13f, 0.775f), new Color(.15f, .18f, .19f, .95f));
                Text(b, label, 24, Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                b.gameObject.AddComponent<Button>().onClick.AddListener(() => ShowTab(tab));
            }
            // Roster sign-in: pseudonymous codes from the instructor. Blank = practice (nothing reaches a class report).
            Field(root, "CLASS CODE", GameSettings.ClassCode, v => GameSettings.ClassCode = v, new Vector2(0.56f, 0.72f), new Vector2(0.74f, 0.775f));
            Field(root, "STUDENT ID", GameSettings.LearnerId, v => GameSettings.LearnerId = v, new Vector2(0.76f, 0.72f), new Vector2(0.94f, 0.775f));
            Text(root, "Play data (no names) goes to your course's report. AI crew chat asks first. Esc = pause/settings.", 18, new Color(.7f, .75f, .75f),
                new Vector2(0.06f, 0.02f), new Vector2(0.94f, 0.06f), TextAnchor.MiddleLeft);
            body = Panel(root, "Body", new Vector2(0.06f, 0.08f), new Vector2(0.94f, 0.7f), new Color(0, 0, 0, 0));
            ShowTab(Tab.Episodes);
            StartCoroutine(Orbit());
        }

        public void ShowTab(Tab tab)
        {
            foreach (Transform c in body) Destroy(c.gameObject);
            profile.text = $"Level {career.Level} · {Career.Rank(career.Level)}\n{career.Points} Safety Points · {career.LifetimeXp} XP";
            if (tab == Tab.Episodes) EpisodeCards();
            else if (tab == Tab.Gear) GearCards();
            else CrewCards();
        }

        private void GearCards()
        {
            Text(body, "Real instruments a competent person carries. They give you readings, not protection: you still make the call.", 24, new Color(.85f, .88f, .88f), new Vector2(0, 0.92f), new Vector2(1, 1), TextAnchor.MiddleLeft);
            var n = GearCatalog.All.Count;
            for (var i = 0; i < n; i++)
            {
                var g = GearCatalog.All[i];
                float x0 = i * 1f / n, x1 = x0 + 1f / n - 0.012f;
                var card = Panel(body, g.Id.ToString(), new Vector2(x0, 0), new Vector2(x1, 0.9f), new Color(0.07f, 0.09f, 0.1f, 0.92f));
                Art(card, "Gear/" + g.Id, new Vector2(0.1f, 0.58f), new Vector2(0.9f, 0.97f), new Rect(0, 0, 1, 1), true);
                Text(card, g.Name, 30, Color.white, new Vector2(0.06f, 0.46f), new Vector2(0.94f, 0.57f), TextAnchor.MiddleLeft);
                Text(card, g.Effect, 21, new Color(.55f, .85f, 1f), new Vector2(0.06f, 0.28f), new Vector2(0.94f, 0.46f), TextAnchor.UpperLeft);
                Text(card, g.RealWorld, 16, new Color(.6f, .66f, .66f), new Vector2(0.06f, 0.14f), new Vector2(0.94f, 0.28f), TextAnchor.UpperLeft);
                var owned = career.Has(g.Id);
                var locked = career.Level < g.MinLevel;
                var can = !owned && !locked && career.Points >= g.Cost;
                var label = owned ? "OWNED" : locked ? $"NEEDS LEVEL {g.MinLevel}" : $"BUY · {g.Cost} SP";
                var button = Panel(card, "Buy", new Vector2(0.06f, 0.03f), new Vector2(0.94f, 0.12f), can ? Accent : new Color(.25f, .27f, .28f));
                Text(button, label, 22, can ? new Color(.08f, .08f, .08f) : new Color(.7f, .7f, .7f), Vector2.zero, Vector2.one, TextAnchor.MiddleCenter);
                if (can) button.gameObject.AddComponent<Button>().onClick.AddListener(() => Buy(g.Id));
            }
        }

        public BuyResult Buy(GearId id)
        {
            var result = career.Buy(id);
            if (result == BuyResult.Bought) CareerStore.Save(career);
            ShowTab(Tab.Gear);
            return result;
        }

        private void CrewCards()
        {
            Text(body, Cast.Player, 22, new Color(.85f, .88f, .88f), new Vector2(0, 0.88f), new Vector2(1, 1), TextAnchor.MiddleLeft);
            const int cols = 4;
            for (var i = 0; i < Cast.All.Count; i++)
            {
                var c = Cast.All[i];
                float x0 = i % cols * 1f / cols, x1 = x0 + 1f / cols - 0.01f, y1 = 0.86f - i / cols * 0.44f, y0 = y1 - 0.42f;
                var card = Panel(body, c.Id, new Vector2(x0, y0), new Vector2(x1, y1), new Color(0.07f, 0.09f, 0.1f, 0.92f));
                Art(card, "Cast/" + c.Id, new Vector2(0.02f, 0.05f), new Vector2(0.3f, 0.95f), new Rect(0.2f, 0, 0.6f, 1), true);
                Text(card, c.Name, 26, Color.white, new Vector2(0.33f, 0.8f), new Vector2(0.98f, 0.97f), TextAnchor.MiddleLeft);
                Text(card, $"{c.Age} · {c.Role}", 18, Accent, new Vector2(0.33f, 0.66f), new Vector2(0.98f, 0.8f), TextAnchor.UpperLeft);
                Text(card, c.Backstory, 15, new Color(.82f, .85f, .85f), new Vector2(0.33f, 0.16f), new Vector2(0.98f, 0.66f), TextAnchor.UpperLeft);
                Text(card, "Wants " + c.Want, 15, new Color(.55f, .85f, 1f), new Vector2(0.33f, 0.02f), new Vector2(0.98f, 0.16f), TextAnchor.UpperLeft);
            }
        }

        private void Field(Transform parent, string placeholder, string value, System.Action<string> save, Vector2 min, Vector2 max)
        {
            var box = Panel(parent, placeholder, min, max, new Color(.1f, .12f, .13f, .95f));
            var text = Text(box, "", 24, Color.white, Vector2.zero, Vector2.one, TextAnchor.MiddleLeft);
            text.rectTransform.offsetMin = new Vector2(14, 0);
            text.supportRichText = false;
            var hint = Text(box, placeholder, 22, new Color(1, 1, 1, .4f), Vector2.zero, Vector2.one, TextAnchor.MiddleLeft);
            hint.rectTransform.offsetMin = new Vector2(14, 0);
            var field = box.gameObject.AddComponent<InputField>();
            field.textComponent = text; field.placeholder = hint; field.characterLimit = 24; field.text = value;
            field.onEndEdit.AddListener(v => { save(v); PlayerPrefs.Save(); });
        }

        private static void Art(RectTransform parent, string resource, Vector2 min, Vector2 max, Rect uv, bool lit)
        {
            var tex = Resources.Load<Texture2D>(resource);
            if (tex == null) return;
            var img = new GameObject("Art", typeof(RectTransform), typeof(RawImage)).GetComponent<RawImage>();
            img.transform.SetParent(parent, false); Stretch(img.rectTransform, min, max);
            img.texture = tex; img.uvRect = uv; img.color = lit ? Color.white : new Color(.35f, .35f, .35f);
        }

        private void EpisodeCards()
        {
            var n = Episodes.All.Count;
            for (var i = 0; i < n; i++)
            {
                var ep = Episodes.All[i];
                float x0 = i * 1f / n, x1 = x0 + 1f / n - 0.012f;
                var card = Panel(body, "EP" + ep.Number, new Vector2(x0, 0), new Vector2(x1, 1), new Color(0.07f, 0.09f, 0.1f, 0.92f));
                Art(card, "Episodes/EP" + ep.Number, new Vector2(0, 0.52f), Vector2.one, new Rect(0.2f, 0, 0.6f, 1), ep.Playable);
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
            if (ep.ColdOpen.Count > 1) { Say(ep.ColdOpen[line]); Voice(ep, "open", line); }
            foreach (var shot in ep.Shots)
            {
                for (var t = 0f; t < shot.Seconds && !skip; t += Time.deltaTime)
                {
                    var k = Mathf.SmoothStep(0, 1, t / shot.Seconds);
                    cine.transform.position = Vector3.Lerp(Vec(shot.From), Vec(shot.To), k);
                    cine.transform.rotation = Quaternion.LookRotation(Vec(shot.LookAt) - cine.transform.position);
                    lineClock += Time.deltaTime;
                    if (line + 1 < ep.ColdOpen.Count && lineClock >= Seconds(ep, "open", line)) { line++; lineClock = 0; Say(ep.ColdOpen[line]); Voice(ep, "open", line); }
                    yield return null;
                }
            }
            // Let the last line finish if the shots ran short.
            while (!skip && line < ep.ColdOpen.Count && lineClock < Seconds(ep, "open", line)) { lineClock += Time.deltaTime; yield return null; }
            EndIntro();
        }

        private void EndIntro()
        {
            StopAllCoroutines();
            EndVoice();
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

        // Voice-over (Higgsfield TTS, Resources/Audio/VO/vo_ep{n}_{open|epi}_{i}); captions stay on screen either way.
        static AudioClip VoClip(Episode ep, string kind, int i) => Resources.Load<AudioClip>($"Audio/VO/vo_ep{ep.Number}_{kind}_{i}");
        static float Seconds(Episode ep, string kind, int i)
        {
            var lines = kind == "open" ? ep.ColdOpen : ep.Epilogue;
            var clip = VoClip(ep, kind, i);
            return Mathf.Max(lines[i].Seconds, clip != null ? clip.length + 0.5f : 0);
        }
        private static AudioSource voice;
        static void Voice(Episode ep, string kind, int i)
        {
            var clip = VoClip(ep, kind, i);
            if (clip == null) return;
            if (voice == null) { voice = new GameObject("Voice").AddComponent<AudioSource>(); voice.spatialBlend = 0; }
            voice.Stop(); voice.volume = GameSettings.VoiceVolume; voice.clip = clip; voice.Play();
        }

        private void EndVoice() { if (voice != null) voice.Stop(); }

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
            for (var i = 0; i < Selected.Epilogue.Count; i++)
            {
                var line = Selected.Epilogue[i];
                if (line.IfFound != null && !shift.Session.GetEvidence(line.IfFound).Detected) continue;
                shift.Say((line.Speaker.Length > 0 ? line.Speaker + ": " : "") + line.Text);
                Voice(Selected, "epi", i);
                yield return new WaitForSeconds(Seconds(Selected, "epi", i));
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
