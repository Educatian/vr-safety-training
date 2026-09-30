using System;
using System.Collections;
using System.Linq;
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
        private string reopened;            // condition id the learner re-opened after logging it compliant
        private bool confirmEarlyEnd;       // "Finish shift" before the whistle asks once
        private string chatDraft = "";      // unsent crew-chat text, kept across page rebuilds
        private CrewMember chatCrew;

        static readonly Color Ink = new Color(.84f, .87f, .86f);
        static readonly Color Accent = new Color(1f, .78f, .1f);
        static readonly Color Chip = new Color(.14f, .17f, .18f, .88f);   // slightly see-through in the full view
        static readonly Color Good = new Color(.6f, .9f, .6f);
        static readonly Color Bad = new Color(1f, .45f, .35f);

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
            var scroll = content.GetComponentInParent<ScrollRect>();
            if (scroll != null) scroll.verticalNormalizedPosition = 1f;    // each page opens at the top
            if (!director.MenuOpen) { confirmEarlyEnd = false; return; }
            Label($"EP{director.Episode.Number} {director.Episode.Title.ToUpperInvariant()} · XP {director.Xp} · {Career.Rank(director.Career.Level)}", 22, Accent);
            var w = director.Weather;
            if (w != null) Label($"SITE WEATHER  {w.Summary} · heat risk {HeatIndex.Risk(w.HeatIndexF)}", 17, new Color(.55f, .85f, 1f));
            if (director.PendingWeather != null) { WeatherAlert(director.PendingWeather); return; }
            if (director.PendingIncident != null) { IncidentCard(director.PendingIncident); return; }
            if (director.PendingSpeakUp != null && director.Current == ShiftDirector.Phase.Shift) { SpeakUpCard(); return; }
            if (director.TalkingTo != null) { Chat(director.TalkingTo); return; }
            if (director.Current == ShiftDirector.Phase.Briefing) { Briefing(); return; }
            if (director.Finished) { Closing(); return; }
            if (director.Selected == null)
            {
                Label("SITE WALK", 34, Color.white);
                Label("Center a condition. Move close. Press E.");
                Label(ShiftLine(), 18, Ink);
                MissionCard();
                foreach (var r in director.OpenRequests) Label($"CREW REQUEST · {r.Step.Text}  ({r.Npc})", 19, new Color(.55f, .85f, 1f));
                if (!string.IsNullOrEmpty(director.LastKsa)) Label(director.LastKsa, 19, new Color(.75f, 1f, .7f));
                Button($"Hint from Dolores · {director.Hints.Tokens} left (half XP on that find)", director.UseHint);
                Button("Return to site", director.ToggleTablet);
                FinishShiftButton();
                return;
            }
            var target = director.Selected;
            var state = director.Session.GetState(target.Id);
            // The real title is the diagnosis: it stays hidden until the learner reports the condition.
            Label(director.Session.Revealed(target.Id) ? target.DisplayName : target.NeutralName, 32, Color.white);
            // Instruments: a deliberate measurement (shift time) that returns a raw value, never the verdict.
            foreach (var gear in director.Instruments.ToList())
            {
                var item = GearCatalog.Get(gear);
                if (gear == GearId.FieldNotebook) continue;
                if (director.Measured(target, gear))
                    Label(item.Name + ": " + (target.Reading(gear) ?? "no useful reading here."), 19, new Color(.55f, .85f, 1f));
                else
                {
                    var g = gear;
                    Button($"Measure · {item.Name} ({ShiftDirector.MeasureSeconds:0} s)", () => director.Measure(g));
                }
            }
            var logged = director.Session.LoggedCompliant(target.Id) && reopened != target.Id;
            if (state == HazardState.Latent && logged)
            {
                Label("Logged as compliant", 22, Ink);
                Button("Change my call: report it", () => { reopened = target.Id; Refresh(); });
            }
            else if (!target.IsHazard && director.Session.Revealed(target.Id))
            {
                Label("Reported · compliant", 22, Ink);
                if (!string.IsNullOrEmpty(director.LastKsa)) Label(director.LastKsa, 19, new Color(.75f, 1f, .7f));
                Standard(target);
            }
            else if (state == HazardState.Latent || state == HazardState.Lapsed)
            {
                Label("Energy source", 20, Accent);
                EnergyGrid();
                Button("Probability  " + Dots(probability), () => { probability = probability % 5 + 1; Refresh(); });
                Button("Severity     " + Dots(severity), () => { severity = severity % 5 + 1; Refresh(); });
                Button("Submit report", () => director.Report(energy, probability, severity), null, true);
                if (state == HazardState.Latent && !director.Session.Judged(target.Id))
                    Button("Checked · compliant, nothing to report", director.ConfirmCompliant);
            }
            else
            {
                Label("Reported · " + state, 22, Ink);
                if (!string.IsNullOrEmpty(director.LastFeedback)) Label(director.LastFeedback, 20, new Color(.55f, .85f, 1f));
                if (!string.IsNullOrEmpty(director.LastKsa)) Label(director.LastKsa, 19, new Color(.75f, 1f, .7f));
                Standard(target);
                if (state == HazardState.Installing && director.PendingInstall == target.Id && director.KitOptions != null)
                {
                    if (director.ChosenKit < 0)
                    {
                        Label("Which control goes in?", 22, Accent);
                        for (var i = 0; i < director.KitOptions.Count; i++) { var k = i; Button(director.KitOptions[i], () => director.ChooseKit(k)); }
                    }
                    else Label("Kit: " + director.KitOptions[director.ChosenKit] + " · collect it at the supply rack.", 19, Ink);
                }
                if (state == HazardState.Reported || state == HazardState.Stopped)
                {
                    Button("Eliminate · remove it from service", () => director.Control(ControlLevel.Elimination));
                    Button("Fix · engineered control", () => director.Control(ControlLevel.Engineering), Icon("control_Engineering"));
                    Button("Assign · crew reminder", () => director.Control(ControlLevel.Administrative), Icon("control_Administrative"));
                    Button("PPE · individual protection", () => director.Control(ControlLevel.Ppe), Icon("control_Ppe"));
                    Button("Radio · stop work", director.StopWork, Icon("control_StopWork"));
                }
            }
            Button("Return to site", director.ToggleTablet);
            FinishShiftButton();
        }

        // Shift clock and schedule cost of stops (GDD §5.2 Schedule meter; §7 clarity).
        private string ShiftLine()
        {
            var s = director.Session;
            if (s == null) return "";
            var line = $"Shift {s.Clock / 60f:0.0} of {ShiftDirector.ShiftLength / 60f:0} min";
            if (s.StoppedSeconds > 0) line += $" · crew idle {s.StoppedSeconds / 60f:0.0} min (stops)";
            line += $" · crew {ShiftDirector.AffectWord(s.Affect.BandOf(CrewAffect.Crew))}, Ray {ShiftDirector.AffectWord(s.Affect.BandOf(CrewAffect.Foreman))}";
            return line;
        }

        // Ending before the whistle asks once: unfound hazards count as missed and no Zero Recordables badge.
        private void FinishShiftButton()
        {
            var early = director.Session != null && director.Session.Clock < ShiftDirector.ShiftLength;
            if (!early || confirmEarlyEnd) { Button(early ? "Confirm: end early (hazards not found count as missed)" : "Finish shift", () => { confirmEarlyEnd = false; director.EndShift(); }, null, early); return; }
            Button("Finish shift", () => { confirmEarlyEnd = true; Refresh(); });
        }

        // Gate briefing: drag the controls into rank order, then the toolbox quiz, then start the shift.
        private void Briefing()
        {
            if (!string.IsNullOrEmpty(director.CarryLine)) Label(director.CarryLine, 19, new Color(.75f, 1f, .7f));
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
            var s = director.Session;
            Label("SHIFT CLOSED", 34, Color.white);
            Label($"Hazards found {s.HazardIdentificationIndex:P0} · Precision {s.ReportPrecision:P0} · Incidents {s.NearMisses + s.Recordables}");
            Label($"Crew trust {s.CrewTrust:+0;-0;0} · Schedule slip {s.ScheduleSlipMinutes:0.0} min · +{director.Xp} XP");
            Label($"Crew: {ShiftDirector.AffectWord(s.Affect.BandOf(CrewAffect.Crew))} · Ray: {ShiftDirector.AffectWord(s.Affect.BandOf(CrewAffect.Foreman))}", 18, Ink);
            if (director.RequestsIssued > 0) Label($"Crew requests answered {director.RequestsDone}/{director.RequestsIssued}", 18, director.RequestsDone == director.RequestsIssued ? new Color(.6f, .9f, .6f) : Accent);
            if (director.EndedEarly) Label($"Shift ended early at {s.Clock / 60f:0.0} min: anything not found counts as missed.", 18, Accent);

            // Hazards grouped by outcome, with the control chosen vs. the best feasible one (GDD §15 item 4).
            foreach (DebriefGroup g in Enum.GetValues(typeof(DebriefGroup)))
            {
                var rows = director.Conditions.Where(c => c.IsHazard && Debrief.Group(c.Spec, s.GetEvidence(c.Id), s.GetState(c.Id)) == g).ToList();
                if (rows.Count == 0) continue;
                var color = g == DebriefGroup.ControlledAtBest ? Good : g == DebriefGroup.Missed ? Bad : Accent;
                Label(Debrief.GroupTitle(g), 20, color);
                foreach (var c in rows)
                {
                    var ev = s.GetEvidence(c.Id);
                    Label($"{c.DisplayName} · yours: {Debrief.Level(ev.AppliedControl)} · best: {Debrief.Level(c.Spec.BestFeasibleControl)}", 18, Ink);
                    if (g == DebriefGroup.Missed && ev.DismissedAsCompliant) Label("   You logged this one as compliant.", 17, Bad);
                    if (g != DebriefGroup.ControlledAtBest && !string.IsNullOrEmpty(c.Cfr)) Label("   " + c.Cfr + " — " + c.Threshold, 17, Accent);
                }
            }
            // What almost happened (no gore), for every near miss this shift.
            if (director.Incidents.Count > 0)
            {
                Label("WHAT ALMOST HAPPENED", 20, Bad);
                foreach (var id in director.Incidents.Distinct()) Label(Debrief.WhatAlmostHappened(id), 17, Ink);
            }
            // Look-alikes: the compliant conditions and the learner's call on each (discrimination feedback).
            var lookAlikes = director.Conditions.Where(c => !c.IsHazard).ToList();
            if (lookAlikes.Count > 0) Label("COMPLIANT CONDITIONS (LOOK-ALIKES)", 20, Accent);
            foreach (var condition in lookAlikes)
            {
                var call = s.LoggedCompliant(condition.Id) ? "confirmed compliant ✓"
                    : s.Revealed(condition.Id) ? "reported as a hazard (false alarm)" : "not checked";
                Label(condition.DisplayName + " · " + call, 18, call.EndsWith("✓") ? Ink : new Color(.7f, .72f, .72f));
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
                if (!string.IsNullOrEmpty(director.TalkFeedback)) Label(director.TalkFeedback, 19, new Color(.75f, 1f, .7f));
                MasteryBars();
                KsaProfile();
                // Post-debrief support (area 12): the incident scenes are fictional but can echo real experiences.
                Label("If anything in today's incidents brings up a real experience, talk to your instructor or campus counseling. You can stop or withdraw your research data any time (Esc).", 16, new Color(.7f, .75f, .75f));
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
            ToolboxTalkWriter();
        }

        // Tomorrow's toolbox talk (GDD N7): order up to three of today's findings, then answer the "why".
        private void ToolboxTalkWriter()
        {
            Label("TOMORROW'S TOOLBOX TALK", 24, Accent);
            var found = director.Findings.ToList();
            var picks = director.TalkPicks.ToList();
            if (found.Count > 0)
            {
                Label($"Tap up to {ToolboxTalk.Picks} of today's findings, in the order you'll brief the crew.", 19, Ink);
                foreach (var c in found)
                {
                    var i = picks.IndexOf(c.Id); var id = c.Id;
                    Button((i >= 0 ? $"#{i + 1}   " : "      ") + c.DisplayName, () => director.ToggleTalkPick(id), null, i >= 0);
                }
                if (picks.Count > 0) Button("Clear the order", director.ClearTalk);
            }
            else Label("You reported nothing today. You'll still brief the principle.", 19, Ink);
            var why = director.TalkWhy;
            if (why == null || why.Done) return;
            Label(why.Current.Prompt, 22, Color.white);
            for (var i = 0; i < why.Current.Options.Length; i++) { var k = i; Button(why.Current.Options[i], () => director.SubmitToolboxTalk(k)); }
        }

        // Per-area CP mastery (the stealth assessment, GDD §5.2) and the Friday gate.
        private void MasteryBars()
        {
            if (director.MasteryToday == null) return;
            Label("CP MASTERY · this shift / your best", 22, Accent);
            foreach (var kv in director.MasteryToday.OrderBy(k => k.Key))
            {
                var best = director.MasteryBest != null && director.MasteryBest.TryGetValue(kv.Key, out var b) ? b : kv.Value;
                var bars = Mathf.RoundToInt(kv.Value * 10);
                var competent = best >= DaySession.CompetentThreshold;
                Label($"{MasteryGate.AreaName(kv.Key)}  {new string('█', bars)}{new string('░', 10 - bars)} {kv.Value:P0} · best {best:P0}" + (competent ? " · competent" : ""), 18, competent ? Ink : Accent);
            }
            var missing = MasteryStore.CapstoneMissing();
            Label(missing.Count == 0 ? "Friday capstone (EP5): unlocked."
                : "Friday capstone (EP5) needs competent in: " + string.Join(", ", missing.Select(a => $"{MasteryGate.AreaName(a)} ({MasteryGate.Practice(a)})")), 18, missing.Count == 0 ? Good : Accent);
            Label("Finds made with a guide marker or a hint count half. Replay without them to show it's yours.", 16, Ink);
        }

        // Stop-down after a near miss: what almost happened, the standard, then back to work.
        private void IncidentCard(string id)
        {
            var c = director.Conditions.FirstOrDefault(x => x.Id == id);
            var recordable = c != null && c.Spec.IsHighSeverity;
            Label(recordable ? "STOP-DOWN · RECORDABLE INCIDENT" : "STOP-DOWN · NEAR MISS", 30, Bad);
            if (c != null) Label(c.DisplayName, 26, Color.white);
            Label("What almost happened: " + Debrief.WhatAlmostHappened(id), 20, Ink);
            if (c != null && !string.IsNullOrEmpty(c.Cfr)) { Label(c.Cfr + "  ·  " + c.Threshold, 19, Accent); Label(c.RequirementPlain, 19, Ink); }
            var reported = c != null && director.Session.GetEvidence(id).Detected;
            Label(reported ? "You reported it, but no control was in place in time." : "It was on site all along. Nobody reported it in time.", 18, Ink);
            Button("Secure the area · back to work", director.AcknowledgeIncident, null, true);
        }

        // Production pressure: Ray pushes back on the stop (GDD N4).
        private void SpeakUpCard()
        {
            var c = director.Conditions.FirstOrDefault(x => x.Id == director.PendingSpeakUp);
            Label("RAY PUSHES BACK", 28, Accent);
            Label(SpeakUp.Pushback(c != null ? c.DisplayName : "job"), 22, Color.white);
            Label("Your answer:", 19, Ink);
            for (var i = 0; i < director.SpeakUpOptions.Count; i++) { var k = i; Button(director.SpeakUpOptions[i].Text, () => director.ChooseSpeakUp(k)); }
        }

        // Field-practice mission: the CP's real checklist for today; steps tick off as you do the work on site.
        private void MissionCard()
        {
            var run = director.Mission;
            if (run == null) return;
            Label($"MISSION · {run.Mission.Title}  ({run.Completed}/{run.Mission.Steps.Count})", 22, Accent);
            Label(run.Mission.Form + (run.Mission.Issued.Count > 0 ? " · issued: " + string.Join(", ", run.Mission.Issued.Select(g => GearCatalog.Get(g).Name)) : ""), 17, Ink);
            for (var i = 0; i < run.Mission.Steps.Count; i++)
            {
                var s = run.Mission.Steps[i];
                Label((run.IsDone(i) ? "✓  " : "□  ") + s.Text + "  ·  " + s.Cfr, 19, run.IsDone(i) ? new Color(.6f, .9f, .6f) : Color.white);
                if (!run.IsDone(i) && s == run.Next) Label("     Why: " + s.Why, 17, Ink);
            }
        }

        // End-of-shift KSA profile with OSHA review list (what to study before the next shift).
        private void KsaProfile()
        {
            var run = director.Mission;
            if (run != null) Label($"Mission: {run.Mission.Title} · {run.Completed}/{run.Mission.Steps.Count} steps" + (run.Complete ? " · signed" : ""), 20, Ink);
            Label("COMPETENT-PERSON PROFILE (KSA)", 22, Accent);
            foreach (Ksa k in Enum.GetValues(typeof(Ksa)))
            {
                if (!(director.Competence.Mean(k) is float m)) continue;
                var bars = Mathf.RoundToInt(m * 10);
                Label($"{KsaInfo.Domain(k)}  {KsaInfo.Name(k)}  {new string('█', bars)}{new string('░', 10 - bars)} {m:P0}", 18, m >= .8f ? Ink : m >= .5f ? Accent : new Color(1f, .55f, .45f));
                if (m < .8f) Label("     On the job: " + KsaInfo.OnTheJob(k), 16, Ink);
            }
            var review = director.Competence.ByStandard().Where(t => t.mean < .8f).Take(3).ToList();
            if (review.Count > 0) Label("Review before next shift: " + string.Join(" · ", review.Select(t => $"{t.cfr} ({t.mean:P0})")), 18, Accent);
        }

        // Crew conversation: transcript + typed question (online LLM, see docs/Deploy.md; offline fallback).
        private void Chat(CrewMember crew)
        {
            Label(crew.DisplayName, 30, Color.white);
            if (GameSettings.AiConsent < 0)
            {
                // In the fiction (playtest 2026-09-29: vendor names read out of place); the privacy facts stay. The provider
                // is named in docs/Deploy.md and the facilitator's consent sheet.
                Label($"Ask {crew.DisplayName.Split(' ')[0]} anything about the job, in your own words.", 21, Color.white);
                Label("Free-text replies come from an online AI model, so what you type is sent to it. " +
                      "Don't type names or personal details. The game only records that you asked, never your words.", 18, Ink);
                Button("Ask in my own words", () => { GameSettings.AiConsent = 1; PlayerPrefs.Save(); Refresh(); }, null, true);
                Button("Use set questions instead (offline)", () => { GameSettings.AiConsent = 0; PlayerPrefs.Save(); Refresh(); });
                Button("Close (Tab)", director.EndTalk);
                return;
            }
            var lines = crew.Transcript;
            for (var i = System.Math.Max(0, lines.Count - 6); i < lines.Count; i++)
                Label(lines[i], 19, lines[i].StartsWith("You:") ? Accent : Ink);
            if (crew.Thinking) Label("…", 22, Ink);
            var input = InputBox("Ask about this condition…");
            // A reply rebuilds the page; keep what the learner was typing (it used to be wiped mid-sentence).
            if (chatCrew != crew) { chatCrew = crew; chatDraft = ""; }
            input.text = chatDraft; input.caretPosition = chatDraft.Length;
            input.onValueChanged.AddListener(v => chatDraft = v);
            Button("Send", () => { chatDraft = ""; director.AskCrew(input.text); });
            input.onSubmit.AddListener(v => { chatDraft = ""; director.AskCrew(v); });
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
            Badge.StoppedTheLine => "Stopped the Line", Badge.ZeroRecordablesDay => "Zero Recordables", Badge.OnSchedule => "On Schedule", _ => "Hierarchy Hawk",
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
