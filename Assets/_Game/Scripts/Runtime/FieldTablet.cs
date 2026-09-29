using System;
using System.Collections;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Diegetic field tablet (GDD §4): photo -> energy tag -> P x S card -> Fix/Assign/PPE/Stop.
    // Art: Higgsfield rugged frame + icon sheets under Resources/UI; cards stay <= 12 words.
    public sealed class FieldTablet : MonoBehaviour
    {
        [SerializeField] private ShiftDirector director;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform frame;
        [SerializeField] private Text radioText;
        [SerializeField] private Font font;
        [SerializeField] private Image flash;
        private EnergySource energy = EnergySource.Gravity;
        private int probability = 3, severity = 3;

        static readonly Color Ink = new Color(.84f, .87f, .86f);
        static readonly Color Accent = new Color(1f, .78f, .1f);
        static readonly Color Chip = new Color(.14f, .17f, .18f);

        public void Configure(ShiftDirector shift, RectTransform screen, Text radio, Font face, RectTransform tabletFrame = null, Image photoFlash = null)
        { director = shift; content = screen; radioText = radio; font = face; frame = tabletFrame; flash = photoFlash; }

        private void Update() { if (director != null) radioText.text = director.Notice; }

        // Camera-shutter feedback on a valid photo: white flash fading over 0.18 s.
        public void Flash()
        {
            if (flash == null || !isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            for (var t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(1, 1, 1, 0.85f * (1 - t / 0.18f));
                yield return null;
            }
            flash.color = Color.clear;
        }

        public void Refresh()
        {
            if (content == null || director == null) return;
            (frame != null ? frame : content).gameObject.SetActive(director.MenuOpen);
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            if (!director.MenuOpen) return;
            Label($"EP{director.Episode.Number} {director.Episode.Title.ToUpperInvariant()} · XP {director.Xp} · {Career.Rank(director.Career.Level)}", 22, Accent);
            var w = director.Weather;
            if (w != null) Label($"SITE WEATHER  {w.Summary} · heat risk {HeatIndex.Risk(w.HeatIndexF)}", 17, new Color(.55f, .85f, 1f));
            if (director.PendingWeather != null) { WeatherAlert(director.PendingWeather); return; }
            if (director.TalkingTo != null) { Chat(director.TalkingTo); return; }
            if (director.Current == ShiftDirector.Phase.Briefing) { Briefing(); return; }
            if (director.Finished) { Closing(); return; }
            if (director.Selected == null)
            {
                Label("SITE WALK", 34, Color.white);
                Label("Center a condition. Move close. Press E.");
                Button($"Hint from Dolores · {director.Hints.Tokens} left (half XP on that find)", director.UseHint);
                Button("Return to site", director.ToggleTablet);
                Button("Finish shift", director.EndShift);
                return;
            }
            var target = director.Selected;
            var state = director.Session.GetState(target.Id);
            Label(target.DisplayName, 32, Color.white);
            foreach (var gear in director.Career.Owned)
            {
                var reading = target.Reading(gear);
                if (reading != null) Label(GearCatalog.Get(gear).Name + ": " + reading, 19, new Color(.55f, .85f, 1f));
            }
            if (state == HazardState.Latent || state == HazardState.Lapsed)
            {
                Label("Energy source", 20, Accent);
                EnergyGrid();
                Button("Probability  " + Dots(probability), () => { probability = probability % 5 + 1; Refresh(); });
                Button("Severity     " + Dots(severity), () => { severity = severity % 5 + 1; Refresh(); });
                Button("Submit report", () => director.Report(energy, probability, severity), null, true);
            }
            else
            {
                Label("Reported · " + state, 22, Ink);
                if (!string.IsNullOrEmpty(director.LastFeedback)) Label(director.LastFeedback, 20, new Color(.55f, .85f, 1f));
                Standard(target);
                if (state == HazardState.Reported || state == HazardState.Stopped)
                {
                    Button("Fix · engineered control", () => director.Control(ControlLevel.Engineering), Icon("control_Engineering"));
                    Button("Assign · crew reminder", () => director.Control(ControlLevel.Administrative), Icon("control_Administrative"));
                    Button("PPE · individual protection", () => director.Control(ControlLevel.Ppe), Icon("control_Ppe"));
                    Button("Radio · stop work", director.StopWork, Icon("control_StopWork"));
                }
            }
            Button("Return to site", director.ToggleTablet);
            Button("Finish shift", director.EndShift);
        }

        // Gate briefing: drag the controls into rank order, then the toolbox quiz, then start the shift.
        private void Briefing()
        {
            if (director.HierarchyScore < 5)
            {
                Label("Rank the controls: most effective on top.", 26, Color.white);
                Label("Drag each card onto a slot.", 20, Ink);
                HierarchyBoard.Build(content, font, order => director.SubmitHierarchy(order));
                return;
            }
            var quiz = director.Quiz;
            if (quiz != null && !quiz.Done)
            {
                Label($"Toolbox talk · {quiz.Index + 1}/{quiz.Items.Count}", 20, Accent);
                Label(quiz.Current.Prompt, 26, Color.white);
                for (var i = 0; i < quiz.Current.Options.Length; i++)
                {
                    var k = i;
                    Button(quiz.Current.Options[i], () => director.AnswerQuiz(k));
                }
                return;
            }
            Label($"Briefing done · {quiz?.CorrectCount ?? 0}/{quiz?.Items.Count ?? 0} correct", 26, Color.white);
            Button("Begin shift", director.Begin, null, true);
        }

        private void Closing()
        {
            Label("SHIFT CLOSED", 34, Color.white);
            Label($"Hazards found {director.Session.HazardIdentificationIndex:P0} · Precision {director.Session.ReportPrecision:P0}");
            Label($"Crew trust {director.Session.CrewTrust:+0;-0;0} · Incidents {director.Session.NearMisses + director.Session.Recordables} · +{director.Xp} XP");
            foreach (var condition in director.Conditions)
                if (condition.IsHazard)
                {
                    var st = director.Session.GetState(condition.Id);
                    // Missed = never reported, whatever the timer did to it (an unreported hazard can still become an incident).
                    var missed = !director.Session.GetEvidence(condition.Id).Detected;
                    Label((missed ? "MISSED  " : "") + condition.DisplayName + " · " + st, 20,
                        missed ? new Color(1f, .45f, .35f) : Ink);
                    if (missed && !string.IsNullOrEmpty(condition.Cfr))
                        Label("   " + condition.Cfr + " — " + condition.Threshold, 18, Accent);
                }
            var quiz = director.Quiz;
            if (quiz != null && !quiz.Done)
            {
                Label("Check · " + quiz.Current.Prompt, 22, Accent);
                for (var i = 0; i < quiz.Current.Options.Length; i++) { var k = i; Button(quiz.Current.Options[i], () => director.AnswerQuiz(k)); }
                return;
            }
            if (director.EpisodeComplete)
            {
                Label($"EPISODE {director.Episode.Number} COMPLETE · {director.Xp} XP", 26, Accent);
                foreach (var (ev, q) in director.WeatherCalls)
                    Label($"Weather call ({ev.Id}): " + (q == 2 ? "good" : q == 1 ? "partial" : "unsafe") + $"  +{WeatherPlan.Xp(q)} XP", 19, q == 2 ? Ink : new Color(1f, .6f, .45f));
                foreach (var b in director.BadgesEarned) Label("BADGE · " + BadgeName(b) + $"  +{Career.BadgeBonus} SP", 22, Color.white);
                Label($"+{director.PointsAwarded} Safety Points · {director.Career.Points} SP to spend in the gear locker", 22, Ink);
                Label($"Level {director.Career.Level} · {Career.Rank(director.Career.Level)}", 22, Ink);
                Label("COMPLETION RECORD", 22, Accent);
                Label($"Student {(string.IsNullOrEmpty(GameSettings.LearnerId) ? "(practice, not signed in)" : GameSettings.LearnerId)} · Class {(string.IsNullOrEmpty(GameSettings.ClassCode) ? "-" : GameSettings.ClassCode)} · {System.DateTime.Now:yyyy-MM-dd}", 20, Ink);
                Label("Completion code: " + (director.CompletionCode ?? "sending..."), 26, Color.white);
                Label("Give this code to your instructor. Training record only: not an OSHA 10/30 card or a competent-person designation.", 17, Ink);
                Button("Episode select", EpisodeDirector.BackToMenu, null, true);
                return;
            }
            Label("Tomorrow's toolbox talk opens with:", 22, Accent);
            Button("Protect edges. Clear access. Verify controls.", () => director.ExplainBack(0));
            Button("Keep schedule. Rely on reminders and PPE.", () => director.ExplainBack(1));
        }

        // Crew conversation: transcript + typed question (LLM via OpenRouter; offline fallback).
        private void Chat(CrewMember crew)
        {
            Label(crew.DisplayName, 30, Color.white);
            if (GameSettings.AiConsent < 0)
            {
                Label("Crew replies are written by an AI service (OpenRouter, Anthropic Claude). What you type is sent there to answer you. " +
                      "Do not type names or personal information. The game logs only that you asked, never the text.", 19, Ink);
                Button("OK, use AI replies", () => { GameSettings.AiConsent = 1; PlayerPrefs.Save(); Refresh(); }, null, true);
                Button("No thanks, use built-in answers", () => { GameSettings.AiConsent = 0; PlayerPrefs.Save(); Refresh(); });
                Button("Close (Tab)", director.EndTalk);
                return;
            }
            var lines = crew.Transcript;
            for (var i = System.Math.Max(0, lines.Count - 6); i < lines.Count; i++)
                Label(lines[i], 19, lines[i].StartsWith("You:") ? Accent : Ink);
            if (crew.Thinking) Label("…", 22, Ink);
            var input = InputBox("Ask about this condition…");
            Button("Send", () => { director.AskCrew(input.text); });
            input.onSubmit.AddListener(s => director.AskCrew(s));
            Button("Close (Tab)", director.EndTalk);
            input.ActivateInputField();
        }

        private InputField InputBox(string placeholder)
        {
            var go = new GameObject("Ask", typeof(RectTransform), typeof(Image), typeof(InputField), typeof(LayoutElement));
            go.transform.SetParent(content, false);
            go.GetComponent<Image>().color = new Color(.12f, .15f, .16f);
            go.GetComponent<LayoutElement>().preferredHeight = 56;
            var text = new GameObject("Text", typeof(RectTransform), typeof(Text)).GetComponent<Text>();
            text.transform.SetParent(go.transform, false);
            var tr = (RectTransform)text.transform; tr.anchorMin = Vector2.zero; tr.anchorMax = Vector2.one; tr.offsetMin = new Vector2(12, 4); tr.offsetMax = new Vector2(-12, -4);
            text.font = font; text.fontSize = 22; text.color = Color.white; text.supportRichText = false;
            var ph = UnityEngine.Object.Instantiate(text, go.transform); ph.text = placeholder; ph.color = new Color(1, 1, 1, .35f);
            var field = go.GetComponent<InputField>(); field.textComponent = text; field.placeholder = ph; field.lineType = InputField.LineType.SingleLine;
            return field;
        }

        private void WeatherAlert(WeatherEvent ev)
        {
            Label("WEATHER ALERT", 30, new Color(1f, .45f, .3f));
            Label(ev.Prompt, 24, Color.white);
            if (!string.IsNullOrEmpty(ev.Cfr)) Label(ev.Cfr, 17, Accent);
            for (var i = 0; i < ev.Options.Count; i++) { var k = i; Button(ev.Options[i].Text, () => director.ChooseWeather(k)); }
        }

        static string BadgeName(Badge b) => b switch
        {
            Badge.StoppedTheLine => "Stopped the Line", Badge.ZeroRecordablesDay => "Zero Recordables", _ => "Hierarchy Hawk",
        };

        // OSHA citation chip + plain-language requirement (GDD §15).
        private void Standard(SiteCondition target)
        {
            if (string.IsNullOrEmpty(target.Cfr)) return;
            Label(target.Cfr + "  ·  " + target.Threshold, 20, Accent);
            Label(target.RequirementPlain, 20, Ink);
        }

        static string Dots(int n) => new string('●', n) + new string('○', 5 - n);

        static Sprite Icon(string name) => Resources.Load<Sprite>("UI/" + name);

        private void EnergyGrid()
        {
            var row = new GameObject("EnergyGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(content, false);
            var grid = row.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(78, 78); grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
            row.GetComponent<LayoutElement>().preferredHeight = 170;
            foreach (EnergySource e in Enum.GetValues(typeof(EnergySource)))
            {
                var value = e;
                var cell = new GameObject(e.ToString(), typeof(RectTransform), typeof(Image), typeof(Button));
                cell.transform.SetParent(row.transform, false);
                cell.GetComponent<Image>().color = e == energy ? Accent : Chip;
                cell.GetComponent<Button>().onClick.AddListener(() => { energy = value; Refresh(); });
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(cell.transform, false);
                var r = icon.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                r.offsetMin = new Vector2(8, 8); r.offsetMax = new Vector2(-8, -8);
                var img = icon.GetComponent<Image>(); img.sprite = Icon("energy_" + e); img.preserveAspect = true; img.raycastTarget = false;
                if (e == energy) img.color = new Color(.1f, .1f, .1f);
            }
            Label(energy.ToString(), 22, Color.white);
        }

        private void Label(string value, int size = 22, Color? color = null)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(content, false);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size;
            text.color = color ?? Ink; text.text = value;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap; text.verticalOverflow = VerticalWrapMode.Overflow;
            go.GetComponent<LayoutElement>().minHeight = size + 14;
        }

        private void Button(string title, UnityEngine.Events.UnityAction action, Sprite icon = null, bool primary = false)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(content, false);
            go.GetComponent<Image>().color = primary ? Accent : Chip;
            // ~40 characters per line at 24 px on the tablet screen; long answers get taller buttons instead of clipping.
            go.GetComponent<LayoutElement>().preferredHeight = Mathf.Max(58, 22 + Mathf.CeilToInt(title.Length / 40f) * 28);
            go.GetComponent<Button>().onClick.AddListener(() => { AudioDirector.Play("click"); action(); });
            var left = 16f;
            if (icon != null)
            {
                var ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                ic.transform.SetParent(go.transform, false);
                var ir = ic.GetComponent<RectTransform>(); ir.anchorMin = new Vector2(0, .5f); ir.anchorMax = new Vector2(0, .5f);
                ir.sizeDelta = new Vector2(44, 44); ir.anchoredPosition = new Vector2(34, 0);
                var ii = ic.GetComponent<Image>(); ii.sprite = icon; ii.preserveAspect = true; ii.raycastTarget = false;
                left = 64f;
            }
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var rect = label.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, 0); rect.offsetMax = new Vector2(-10, 0);
            var text = label.GetComponent<Text>(); text.font = font; text.fontSize = 24; text.color = primary ? new Color(.08f, .08f, .08f) : Color.white;
            text.text = title; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
        }
    }
}
