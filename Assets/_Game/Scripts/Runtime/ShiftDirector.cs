using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Day flow (GDD §3/§4/§14): CheckIn (sign in + don PPE at the gate) -> Briefing (hierarchy drag-and-drop
    // + toolbox quiz on the tablet) -> Shift (photo/report/control/carry-and-place/stop-work, crew talk)
    // -> Closed (debrief + end-of-day quiz). Deterministic engine = DaySession; the LLM only talks.
    public sealed class ShiftDirector : MonoBehaviour
    {
        public enum Phase { CheckIn, Briefing, Shift, Closed }

        [SerializeField] private SiteCondition[] conditions;
        [SerializeField] private SitePlayer player;
        [SerializeField] private FieldTablet tablet;
        [SerializeField] private AudioSource radio;
        [SerializeField] private float placeTolerance = 1.5f;   // starting value: metres from the hazard centre

        private SiteCondition selected;
        private string pendingInstall;
        private string logPath;
        private GameObject carriedVisual;
        private string notice = "Check in: sign the gate sheet and put on your PPE.";
        private readonly HashSet<CheckInStation.Kind> checkedIn = new HashSet<CheckInStation.Kind>();

        public DaySession Session { get; private set; }
        public Phase Current { get; private set; } = Phase.CheckIn;
        public bool MenuOpen { get; private set; }
        public bool Finished => Current == Phase.Closed;
        public bool Started => Current == Phase.Shift || Current == Phase.Closed;
        public string Notice => notice;
        public SiteCondition Selected => selected;
        public bool Carrying => carriedVisual != null;
        public SiteCondition[] Conditions => conditions;
        public IReadOnlyCollection<CheckInStation.Kind> CheckedIn => checkedIn;
        public bool CheckInComplete => Enum.GetValues(typeof(CheckInStation.Kind)).Cast<CheckInStation.Kind>().All(checkedIn.Contains);
        public QuizSession Quiz { get; private set; }
        public int HierarchyScore { get; private set; } = -1;
        public CrewMember TalkingTo { get; private set; }
        public bool EpisodeComplete { get; private set; }
        public int Xp { get; private set; }
        public Episode Episode => EpisodeDirector.Selected ?? Episodes.Get(1);

        public void Configure(SiteCondition[] targets, SitePlayer explorer, FieldTablet ui, AudioSource speaker)
        { conditions = targets; player = explorer; tablet = ui; radio = speaker; }

        private void Start()
        {
            // The day's hazards are whatever SitePhaseController left active (Mon, Tue, Wed ...).
            conditions = FindObjectsByType<SiteCondition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Session = new DaySession(conditions.Select(c => c.Spec));
            logPath = Path.Combine(Application.persistentDataPath, "jobsite-" + Guid.NewGuid().ToString("N") + ".jsonl");
            Log("session_start", "day", "conditions=" + conditions.Length);
            tablet.Refresh();
        }

        private void Update()
        {
            if (Session == null || Current != Phase.Shift) return;
            Session.Paused = MenuOpen;
            foreach (var ev in Session.Advance(Time.deltaTime))
            {
                Say(ev.Kind == DayEventKind.Lapsed ? "Temporary control lapsed. Revisit your report." : "Near-miss reported. Secure the area; review follows.");
                Log(ev.Kind.ToString(), ev.HazardId, "");
            }
            if (Session.Clock >= 600) EndShift();
        }

        // ---------- check-in and briefing ----------
        public void CheckIn(CheckInStation.Kind kind)
        {
            if (Current != Phase.CheckIn || !checkedIn.Add(kind)) return;
            Log("checkin", kind.ToString(), "");
            if (CheckInComplete)
            {
                Current = Phase.Briefing; MenuOpen = true;
                Quiz = new QuizSession(Episode.ToolboxQuiz());
                Say("Checked in. Dolores: order the controls, then three quick questions.");
            }
            else Say($"Checked in: {string.Join(", ", checkedIn)}. {Enum.GetValues(typeof(CheckInStation.Kind)).Length - checkedIn.Count} to go.");
            tablet.Refresh();
        }

        public void SubmitHierarchy(IReadOnlyList<string> order)
        {
            HierarchyScore = HierarchyOrdering.Score(order);
            Log("hierarchy_order", "gate", HierarchyScore + "/5:" + string.Join(">", order));
            Say(HierarchyScore == 5 ? "Right order. Remove it, swap it, guard it, manage it, then PPE." :
                "Not yet: most effective controls go on top. PPE is last.");
            if (HierarchyScore == 5) Xp += 50;
            tablet.Refresh();
        }

        public void AnswerQuiz(int option)
        {
            if (Quiz == null || Quiz.Done) return;
            var item = Quiz.Current;
            var ok = Quiz.Answer(option);
            if (ok) Xp += 25;
            Log("quiz", item.Id, (ok ? "correct:" : "wrong:") + option);
            Say((ok ? "Correct. " : "Not quite. ") + item.Explanation + (string.IsNullOrEmpty(item.Cfr) ? "" : " (" + item.Cfr + ")"));
            tablet.Refresh();
        }

        public void Begin()
        {
            Current = Phase.Shift; MenuOpen = false;
            Say("Walk the site. Photograph conditions with E. Right mouse looks.");
            tablet.Refresh(); Log("shift_begin", "day", "");
        }

        public void ToggleTablet()
        {
            if (TalkingTo != null) { EndTalk(); return; }
            if (Current != Phase.Shift) return;
            MenuOpen = !MenuOpen; tablet.Refresh();
        }

        // ---------- interaction ----------
        public void Interact()
        {
            if (MenuOpen || Finished) return;
            var ray = player.View.ViewportPointToRay(new Vector3(.5f, .5f));
            if (!Physics.Raycast(ray, out var hit, 5f, ~0, QueryTriggerInteraction.Collide)) { Say("Move closer. Center it in your view."); return; }

            var station = hit.collider.GetComponentInParent<CheckInStation>();
            if (station != null) { station.Use(this); return; }
            var crew = hit.collider.GetComponentInParent<CrewMember>();
            if (crew != null) { StartTalk(crew); return; }
            if (Current != Phase.Shift) { Say("Finish check-in at the gate first."); return; }

            var vehicle = hit.collider.GetComponentInParent<VehicleController>();
            if (vehicle != null) { vehicle.Interact(player); Log("vehicle_enter", vehicle.name, ""); return; }
            if (hit.collider.GetComponentInParent<ControlSupply>() != null) { Collect(); return; }

            var target = hit.collider.GetComponentInParent<SiteCondition>();
            if (Carrying) { Place(target, hit.point); return; }
            if (target == null) { Say("Photograph a condition, or pick up materials at the rack."); return; }
            if (!PhotoValid(target)) { Say("Move closer. Keep the whole condition in your frame."); return; }
            selected = target; MenuOpen = true; tablet.Flash(); tablet.Refresh(); Log("photo", target.Id, "valid-frame"); Ping();
        }

        private void Collect()
        {
            if (pendingInstall == null) { Say("Choose an engineered fix on the tablet first."); return; }
            if (Carrying) return;
            // Carried kit rides in front of the camera until it is set down at the hazard.
            carriedVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Destroy(carriedVisual.GetComponent<Collider>());
            carriedVisual.name = "CarriedControlKit";
            carriedVisual.transform.SetParent(player.View.transform, false);
            carriedVisual.transform.localPosition = new Vector3(0.35f, -0.35f, 0.8f);
            carriedVisual.transform.localScale = new Vector3(0.45f, 0.25f, 0.35f);
            var r = carriedVisual.GetComponent<Renderer>();
            r.material.color = new Color(0.95f, 0.75f, 0.1f);
            Say("Kit in hand. Carry it to the hazard and set it down (E) where it belongs.");
            Log("kit_collect", pendingInstall, "");
        }

        // Drag-and-drop in 3D: the release point must be on the hazard it fixes, within tolerance.
        private void Place(SiteCondition target, Vector3 point)
        {
            var goal = conditions.FirstOrDefault(c => c.Id == pendingInstall);
            if (goal == null) return;
            var error = Vector3.Distance(point, goal.PhotoBounds.ClosestPoint(point));
            var ok = target == goal || error <= placeTolerance;
            Log("placement_attempt", goal.Id, $"error={error:F2}m ok={ok}");
            if (!ok) { Say($"Wrong spot — {error:F1} m off. Set it at the hazard itself."); return; }
            if (Session.CompleteInstall(goal.Id, true))
            {
                goal.ShowControl();
                Destroy(carriedVisual); carriedVisual = null; pendingInstall = null;
                Xp += XpRules.BestControl + XpRules.EngineeredBonus;
                Say("Control installed. Crew can continue safely.");
                Log("install_success", goal.Id, "Engineering");
            }
        }

        public bool PhotoValid(SiteCondition target)
        {
            var bounds = target.PhotoBounds;
            var min = Vector2.one; var max = Vector2.zero;
            for (var i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = player.View.WorldToViewportPoint(corner);
                if (p.z <= 0) return false;
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            // Fully in frame and the longer side spans >= 30% of the view (tall, thin hazards included).
            return min.x >= 0 && min.y >= 0 && max.x <= 1 && max.y <= 1 && Mathf.Max(max.x - min.x, max.y - min.y) >= .3f;
        }

        public void Report(EnergySource energy, int probability, int severity)
        {
            if (selected == null || Finished) return;
            var outcome = Session.Report(selected.Id, energy, probability, severity);
            if (outcome == ReportOutcome.Reported) Xp += XpRules.HazardXp(selected.Spec, Session.GetEvidence(selected.Id));
            Say(outcome == ReportOutcome.FalseReport ? selected.Explanation : "Report recorded. Choose your control.");
            Log("report", selected.Id, outcome + ":" + energy + ":" + probability + ":" + severity); tablet.Refresh();
        }

        public void Control(ControlLevel level)
        {
            if (selected == null || Finished) return;
            if (pendingInstall != null && pendingInstall != selected.Id) { Say("Finish your current installation first."); return; }
            var result = Session.ChooseControl(selected.Id, level);
            Log("control_choose", selected.Id, result + ":" + level);
            if (result == ControlOutcome.Installing)
            { pendingInstall = selected.Id; MenuOpen = false; Say("Pick up materials at the supply rack (E)."); }
            else if (result == ControlOutcome.Assigned) Say("Temporary control assigned. Check it again later.");
            else if (result == ControlOutcome.NotFeasible) Say("That control is not feasible here. Choose another.");
            else Say("Report this condition before choosing a control.");
            tablet.Refresh();
        }

        public void StopWork()
        {
            if (selected == null || Finished) return;
            var result = Session.StopWork(selected.Id);
            if (result == StopOutcome.Justified) Xp += XpRules.JustifiedStop;
            Say(result == StopOutcome.Justified ? "Work stopped. Your safety rating is protected." : "Report an active hazard before stopping this crew.");
            Log("stop_work", selected.Id, result.ToString()); tablet.Refresh();
        }

        public void EndShift()
        {
            if (Current != Phase.Shift) return;
            Current = Phase.Closed; MenuOpen = true; Session.Paused = true;
            Quiz = new QuizSession(Episode.ClosingQuiz());
            Say("Shift closed. Review what your crew needed.");
            Log("shift_end", "day", "HII=" + Session.HazardIdentificationIndex.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " xp=" + Xp);
            tablet.Refresh();
        }

        public void ExplainBack(int choice)
        {
            if (!Finished) return;
            Log("toolbox_talk", "day", choice.ToString());
            Say(choice == 0 ? "Controls first, verified in the field. That's the job." : "Tomorrow: controls first. Speed and PPE alone are insufficient.");
            if (Xp > PlayerPrefs.GetInt(EpisodeDirector.Key(Episode), -1)) { PlayerPrefs.SetInt(EpisodeDirector.Key(Episode), Xp); PlayerPrefs.Save(); }
            EpisodeComplete = true;
            Log("episode_complete", "ep" + Episode.Number, "xp=" + Xp);
            StartCoroutine(EpisodeDirector.Epilogue(this));
            tablet.Refresh();
        }

        // ---------- crew conversation ----------
        private void StartTalk(CrewMember crew)
        {
            TalkingTo = crew; MenuOpen = true;
            crew.BeginTalk(player.transform);
            Log("radio_query_open", crew.DisplayName, "");
            tablet.Refresh();
        }

        public void EndTalk()
        {
            if (TalkingTo == null) return;
            TalkingTo.EndTalk();
            TalkingTo = null; MenuOpen = Current == Phase.Briefing || Current == Phase.Closed;
            tablet.Refresh();
        }

        public async void AskCrew(string text)
        {
            if (TalkingTo == null || string.IsNullOrWhiteSpace(text)) return;
            Log("coach_query", TalkingTo.DisplayName, "len=" + text.Length); // PII-free: length only, never the text
            var crew = TalkingTo;
            tablet.Refresh();
            await crew.Ask(text, selected != null ? selected.Cfr + " " + selected.RequirementPlain : "");
            if (TalkingTo == crew) tablet.Refresh();
        }

        public void Say(string text) { notice = text; Ping(); }
        private void Ping() { if (radio != null && radio.clip != null) radio.Play(); }

        [Serializable] private sealed class EventRow { public string timestamp; public string kind; public string condition; public string detail; public float shiftSeconds; }
        private void Log(string kind, string id, string detail)
        {
            if (logPath == null) return;
            try { File.AppendAllText(logPath, JsonUtility.ToJson(new EventRow { timestamp = DateTime.UtcNow.ToString("O"), kind = kind, condition = id, detail = detail, shiftSeconds = Session?.Clock ?? 0 }) + "\n"); }
            catch (IOException e) { Debug.LogWarning("Jobsite log unavailable: " + e.GetType().Name); }
        }
    }
}
