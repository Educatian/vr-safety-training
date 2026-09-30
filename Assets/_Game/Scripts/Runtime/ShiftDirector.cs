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
        public string PendingInstall => pendingInstall;
        public SiteCondition[] Conditions => conditions;
        public IReadOnlyCollection<CheckInStation.Kind> CheckedIn => checkedIn;
        public bool CheckInComplete => Enum.GetValues(typeof(CheckInStation.Kind)).Cast<CheckInStation.Kind>().All(checkedIn.Contains);
        public QuizSession Quiz { get; private set; }
        public int HierarchyScore { get; private set; } = -1;
        public CrewMember TalkingTo { get; private set; }
        public bool EpisodeComplete { get; private set; }
        public Career Career { get; private set; } = new Career();
        public HintBank Hints { get; private set; } = new HintBank();
        public int PointsAwarded { get; private set; }
        public IReadOnlyList<Badge> BadgesEarned { get; private set; } = new Badge[0];
        public int Xp { get; private set; }
        public Episode Episode => EpisodeDirector.Selected ?? Episodes.Get(1);
        public static bool SampleHazards = true;           // tests switch this off for a fixed answer key
        public int Seed { get; private set; }
        public string LastFeedback { get; private set; } = "";
        public string CompletionCode { get; private set; }
        public Telemetry Telemetry { get; private set; }
        public WeatherEvent PendingWeather { get; private set; }
        public readonly List<(WeatherEvent ev, int quality)> WeatherCalls = new List<(WeatherEvent, int)>();
        public WeatherState Weather => FindFirstObjectByType<WeatherDirector>()?.Current;
        public KsaLedger Competence { get; } = new KsaLedger();
        public MissionRun Mission { get; private set; }
        public string LastKsa { get; private set; } = "";        // one-line KSA feedback after the last scored action
        // Instruments usable this shift: owned gear plus what the mission loans from the gang box.
        public IEnumerable<GearId> Instruments => Career.Owned.Union(Mission?.Mission.Issued ?? new GearId[0]);
        public const float ShiftLength = 600f;
        // Shift-time costs (GDD §6 starting values): looking is cheap, but not free, so "photograph everything"
        // and "measure everything" cost clock time; a false alarm costs the time it takes the crew to explain it.
        public const float PhotoSeconds = 5f, MeasureSeconds = 10f, FalseAlarmSeconds = 15f;
        private readonly HashSet<(string, GearId)> measured = new HashSet<(string, GearId)>();
        public bool Measured(SiteCondition c, GearId gear) => c != null && measured.Contains((c.Id, gear));
        public bool EndedEarly { get; private set; }

        // Speak-up under pressure (GDD N4): Ray pushes back right after a justified stop.
        public string PendingSpeakUp { get; private set; }
        public IReadOnlyList<SpeakUpOption> SpeakUpOptions { get; private set; }
        // Near-miss stop-down card (GDD N10, §8): what almost happened, then back to work.
        public string PendingIncident { get; private set; }
        public readonly List<string> Incidents = new List<string>();
        // Engineered control: which kit (the right one or a plausible wrong one) goes in.
        public IReadOnlyList<string> KitOptions { get; private set; }
        public int KitCorrect { get; private set; } = -1;
        public int ChosenKit { get; private set; } = -1;
        // Tomorrow's toolbox talk (GDD N7): up to 3 of today's findings in briefing order + one "why".
        private readonly List<string> talkPicks = new List<string>();
        public IReadOnlyList<string> TalkPicks => talkPicks;
        public QuizSession TalkWhy { get; private set; }
        public float TalkScore { get; private set; } = -1f;
        public string TalkFeedback { get; private set; } = "";
        public IReadOnlyDictionary<CpArea, float> MasteryToday { get; private set; }
        public IReadOnlyDictionary<CpArea, float> MasteryBest { get; private set; }
        private bool selfReported;
        const string BankedHintsKey = "banked_hints";

        public void Configure(SiteCondition[] targets, SitePlayer explorer, FieldTablet ui, AudioSource speaker)
        { conditions = targets; player = explorer; tablet = ui; radio = speaker; }

        private void Start()
        {
            // The day's hazards are whatever SitePhaseController left active (Mon, Tue, Wed ...).
            conditions = FindObjectsByType<SiteCondition>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
            Seed = Environment.TickCount & 0x7fffffff;
            if (SampleHazards) Sample(new System.Random(Seed));
            EcdLoader.LoadOnce();   // scoring weights / thresholds / observable map from Resources/ecd.json
            Session = new DaySession(conditions.Select(c => c.Spec));
            Telemetry = GetComponent<Telemetry>() ?? gameObject.AddComponent<Telemetry>();
            Telemetry.Episode = Episode.Number; Telemetry.Seed = SampleHazards ? Seed : 0;
            Career = CareerStore.Load();
            Mission = new MissionRun(Missions.For(Episode.Number));
            if (GetComponent<ScaffoldCues>() == null) gameObject.AddComponent<ScaffoldCues>();
            if (FindFirstObjectByType<SitePolish>() == null) gameObject.AddComponent<SitePolish>();
            TrenchLighting.Apply();   // below-grade crew: ambient + sky bounce instead of black faces
            // A good toolbox talk last shift banks a hint token for this one (GDD §14: earn by explain-back).
            var banked = PlayerPrefs.GetInt(BankedHintsKey, 0);
            Hints = new HintBank(Career.StartingHints + banked);
            if (banked > 0) { PlayerPrefs.SetInt(BankedHintsKey, 0); PlayerPrefs.Save(); }
            logPath = Path.Combine(Application.persistentDataPath, "jobsite-" + Guid.NewGuid().ToString("N") + ".jsonl");
            Log("session_start", "ep" + Episode.Number, "conditions=" + conditions.Length + " seed=" + (SampleHazards ? Seed : 0) +
                " level=" + Career.Level + " guidance=" + ScaffoldCues.Level(Career.Level, TodaysAreas, out var fadeWhy) + ":" + fadeWhy + " mission=" + Mission.Mission.Title +
                " consent=" + GameSettings.ResearchConsent + ":" + GameSettings.ConsentVersion + " ecd=" + EvidenceModel.Current.version);
            tablet.Refresh();
        }

        // One real hazard shows its compliant twin each run and incident timers shift, so a replay is not a memory test.
        private void Sample(System.Random rng)
        {
            var hazards = conditions.Where(c => c.IsHazard).ToList();
            if (hazards.Count > 2)
            {
                var keep = HazardPoolSampler.Sample(hazards.Select(c => c.Spec).ToList(), hazards.Count - 1, 0, rng.Next()).Select(s => s.Id).ToHashSet();
                foreach (var c in hazards.Where(c => !keep.Contains(c.Id))) c.MakeCompliant();
            }
            foreach (var c in conditions.Where(c => c.IsHazard)) c.ShiftTrigger(rng.Next(-45, 46));
        }

        // Hazard areas active today (drives the mastery-based guidance fading).
        public IEnumerable<CpArea> TodaysAreas => conditions == null ? Enumerable.Empty<CpArea>() : conditions.Where(c => c != null && c.IsHazard).Select(c => c.Spec.Area).Distinct();

        private void Update()
        {
            if (Session == null || Current != Phase.Shift) return;
            Session.Paused = MenuOpen;
            foreach (var ev in Session.Advance(Time.deltaTime)) Handle(ev);
            if (!selfReported && Session.CrewTrust >= DaySession.SelfReportTrust && Session.Clock >= 90f) CrewSelfReport();
            if (Time.time >= nextAffect) { nextAffect = Time.time + 0.5f; ApplyAffect(); }
            if (Session.Clock >= ShiftLength) EndShift();
        }

        // High crew trust pays off (GDD §5.2): a worker radios in one hazard you have not found yet. It is a cue,
        // so the find is flagged like any other scaffold.
        // Faces follow the bounded affect state: the foreman reads Ray's trust/stress, everyone else the crew's.
        private float nextAffect;
        private NpcFace[] faces;
        private void ApplyAffect()
        {
            faces ??= FindObjectsByType<NpcFace>(FindObjectsSortMode.None);
            foreach (var f in faces)
            {
                if (f == null || !f.HasFace) continue;
                var who = f.name.Contains("Ray") ? CrewAffect.Foreman : CrewAffect.Crew;
                f.Ambient = Session.Affect.BandOf(who) switch
                {
                    CrewAffect.Band.Hostile => NpcFace.Mood.Angry,
                    CrewAffect.Band.Tense => NpcFace.Mood.Frown,
                    CrewAffect.Band.Warm => NpcFace.Mood.Smile,
                    _ => NpcFace.Mood.Neutral,
                };
            }
        }

        public static string AffectWord(CrewAffect.Band b) => b switch
        {
            CrewAffect.Band.Hostile => "hostile", CrewAffect.Band.Tense => "tense", CrewAffect.Band.Warm => "on your side", _ => "neutral",
        };

        private void CrewSelfReport()
        {
            selfReported = true;
            var target = conditions.Where(c => c.IsHazard && Session.GetState(c.Id) == HazardState.Latent && !Session.LoggedCompliant(c.Id))
                .OrderBy(c => Vector3.Distance(c.transform.position, player.transform.position)).FirstOrDefault();
            if (target == null) return;
            MarkCued(target.Id);
            Say($"Crew (radio): Hey, you've got our backs, so... check the {target.NeutralName.ToLowerInvariant()}, {Where(target.transform.position - player.transform.position)}. Doesn't look right.");
            Log("crew_self_report", target.Id, "trust=" + Session.CrewTrust, Ecd("crew_self_report"), 1f);
        }

        // Spend shift time on an action; timers and incidents fire as they would in real time.
        private void SpendShiftTime(float seconds)
        {
            if (Session == null || Current != Phase.Shift) return;
            foreach (var ev in Session.Spend(seconds)) Handle(ev);
        }

        private void Handle(DayEvent ev)
        {
            var name = conditions.FirstOrDefault(c => c.Id == ev.HazardId)?.DisplayName ?? "the crew";
            Say(ev.Kind == DayEventKind.Lapsed ? "Temporary control lapsed. Revisit your report."
                : ev.Kind == DayEventKind.StopLifted ? $"Ray: We can't sit all day. Crew's back at it: {name}. Get a real fix in."
                : "Near-miss reported. Secure the area; review follows.");
            var at = conditions.FirstOrDefault(c => c.Id == ev.HazardId);
            if (ev.Kind == DayEventKind.NearMiss || ev.Kind == DayEventKind.Recordable)
            {
                // Stop-down: the tablet opens the "what almost happened" card and the clock waits (GDD N10, §8).
                Incidents.Add(ev.HazardId);
                PendingIncident = ev.HazardId;
                if (TalkingTo != null) EndTalk();
                if (Current == Phase.Shift) { MenuOpen = true; tablet.Refresh(); }
                Say((ev.Kind == DayEventKind.Recordable ? "Injury on site: " : "Near miss: ") + (at != null ? at.DisplayName : "the crew") + ". All stop. Secure the area.");
                AudioDirector.Play("alarm");
                if (at != null) CrewGestures.ReactNear(at.transform.position, 14f, CrewGestures.Situation.NearMiss, at.PhotoBounds.center);
                Log(ev.Kind.ToString(), ev.HazardId, "", Ecd("incident"), 0f);
            }
            else if (ev.Kind == DayEventKind.StopLifted) { CrewGestures.Named("Ray")?.React(CrewGestures.Situation.BackToWork); Log(ev.Kind.ToString(), ev.HazardId, ""); }
            else Log(ev.Kind.ToString(), ev.HazardId, "", Ecd("incident"), 0.5f);
        }

        public void AcknowledgeIncident()
        {
            if (PendingIncident == null) return;
            Log("incident_review", PendingIncident, "acknowledged");
            PendingIncident = null;
            MenuOpen = PendingWeather != null || PendingSpeakUp != null || Current != Phase.Shift;
            tablet.Refresh();
        }

        // Scaffolds that point at a hazard (tutorial beacon, mission zone) flag its find as cued.
        public void MarkCued(string id) => Session?.MarkCued(id);

        // ---------- check-in and briefing ----------
        public void CheckIn(CheckInStation.Kind kind)
        {
            if (Current != Phase.CheckIn || !checkedIn.Add(kind)) return;
            Log("checkin", kind.ToString(), "");
            if (CheckInComplete)
            {
                Current = Phase.Briefing; MenuOpen = true;
                Quiz = new QuizSession(Episode.ToolboxQuiz(), Seed);
                Say("Checked in. Dolores: order the controls, then three quick questions.");
            }
            else Say($"Checked in: {string.Join(", ", checkedIn)}. {Enum.GetValues(typeof(CheckInStation.Kind)).Length - checkedIn.Count} to go.");
            tablet.Refresh();
        }

        public void SubmitHierarchy(IReadOnlyList<string> order)
        {
            HierarchyScore = HierarchyOrdering.Score(order);
            Log("hierarchy_order", "gate", HierarchyScore + "/5:" + string.Join(">", order), Ecd("hierarchy_order"), HierarchyScore / 5f, "1926.20(b)");
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
            Log("quiz", item.Id, (ok ? "correct:" : "wrong:") + option, Ecd("quiz"), ok ? 1f : 0f, string.IsNullOrEmpty(item.Cfr) ? "1926.21(b)(2)" : item.Cfr);
            Say((ok ? "Correct. " : "Not quite. ") + item.Explanation + (string.IsNullOrEmpty(item.Cfr) ? "" : " (" + item.Cfr + ")"));
            tablet.Refresh();
        }

        public void Begin()
        {
            Current = Phase.Shift; MenuOpen = false;
            Say("Walk the site with WASD, drag the mouse to look. Photograph conditions with E.");
            tablet.Refresh(); Log("shift_begin", "day", "");
            GetComponent<ScaffoldCues>()?.Refresh();
        }

        // An alert card (weather, incident, speak-up) must be answered: Tab/Esc no longer dismiss it and leave the decision
        // pending with the clock running.
        public bool Blocking => MenuOpen && (PendingWeather != null || PendingIncident != null || PendingSpeakUp != null);

        public void ToggleTablet()
        {
            if (TalkingTo != null) { EndTalk(); return; }
            if (Current != Phase.Shift) return;
            if (Blocking) { Say("Answer the alert on your tablet first."); return; }
            MenuOpen = !MenuOpen; tablet.Refresh();
        }

        // ---------- interaction ----------
        public void Interact()
        {
            if (MenuOpen || Finished) return;
            var ray = player.View.ViewportPointToRay(new Vector3(.5f, .5f));
            if (!Physics.Raycast(ray, out var hit, 16f, ~0, QueryTriggerInteraction.Collide) || !InReach(hit)) { Say("Move closer. Center it in your view."); return; }

            var station = hit.collider.GetComponentInParent<CheckInStation>();
            if (station != null) { station.Use(this); return; }
            var crew = hit.collider.GetComponentInParent<CrewMember>();
            if (crew != null) { StartTalk(crew); return; }
            if (Current != Phase.Shift) { Say("Finish check-in at the gate first."); return; }
            var access = hit.collider.GetComponentInParent<AccessPoint>();
            if (access != null) { access.Use(player); Log("access", access.name, ""); return; }

            var vehicle = hit.collider.GetComponentInParent<VehicleController>();
            if (vehicle != null) { vehicle.Interact(player); Log("vehicle_enter", vehicle.name, ""); return; }
            if (hit.collider.GetComponentInParent<ControlSupply>() != null) { Collect(); return; }

            var target = hit.collider.GetComponentInParent<SiteCondition>();
            if (Carrying) { SetKitDown(target, hit.point); return; }
            // Anything can be photographed. An empty or badly framed shot gets the same answer either way, so the
            // camera never tells the learner which objects are conditions (GDD pillar 1).
            if (target == null || !PhotoValid(target))
            {
                AudioDirector.Play("shutter"); tablet.Flash();
                Log("photo", target != null ? target.Id : "none", target != null ? "invalid-frame" : "empty");
                SpendShiftTime(PhotoSeconds);
                Say("Nothing reportable fully in frame. Frame the whole condition.");
                return;
            }
            Photograph(target);
        }

        // A valid photo opens the condition on the tablet; instruments you carry take their readings (field inspection).
        public void Photograph(SiteCondition target)
        {
            // A scaffold was pointing here: the find counts, but its recognition evidence is flagged (0.5 mastery).
            if (GetComponent<ScaffoldCues>()?.Cueing(target.Id) == true) MarkCued(target.Id);
            SpendShiftTime(PhotoSeconds);
            selected = target; MenuOpen = true; LastFeedback = ""; LastKsa = "";
            AudioDirector.Play("shutter"); tablet.Flash();
            Log("photo", target.Id, "valid-frame");
            tablet.Refresh(); Ping();
        }

        // Field inspection is a deliberate act: pick the instrument, spend the time, read a raw value. Measuring
        // before making the call is the inspection skill (SInspect); measuring after the call is only a log entry.
        public string Measure(GearId gear)
        {
            if (selected == null || Finished) return null;
            if (!Instruments.Contains(gear)) { Say("You don't have that instrument on this shift."); return null; }
            var reading = selected.Reading(gear);
            SpendShiftTime(MeasureSeconds);
            measured.Add((selected.Id, gear));
            if (reading == null) { Say(GearCatalog.Get(gear).Name + ": no useful reading here."); Log("measure_none", selected.Id, gear.ToString()); }
            else if (!Session.Judged(selected.Id)) Log("measure", selected.Id, gear.ToString(), Ecd("measure"), 1f);
            // Picking an instrument that reads the hazard's energy (GFCI tester on power, gas meter in a trench) is
            // evidence of energy knowledge; an irrelevant instrument before the call is not.
            if (!Session.Judged(selected.Id)) Log("instrument_match", selected.Id, gear.ToString(), Ecd("instrument_match"), reading != null ? 1f : 0f);
            else Log("measure", selected.Id, gear.ToString() + " after-call");
            tablet.Refresh();
            return reading;
        }

        // Thoroughness: when an instrument on this shift can read the condition, did the learner measure before the call?
        private void LogMeasuredFirst(SiteCondition c)
        {
            if (c == null || Session.Judged(c.Id)) return;
            var readable = Instruments.Where(g => c.Reading(g) != null).ToList();
            if (readable.Count == 0) return;
            var did = readable.Any(g => measured.Contains((c.Id, g)));
            Log("measured_first", c.Id, did ? "measured" : "no-measurement", Ecd("measured_first"), did ? 1f : 0f);
        }

        // "Checked, compliant." Positive evidence on a look-alike; a miss (hazard left live) on a real hazard.
        public void ConfirmCompliant()
        {
            if (selected == null || Finished) return;
            LogMeasuredFirst(selected);
            var result = Session.ConfirmCompliant(selected.Id);
            // Right and wrong look identical on the tablet (same line, no XP tick, no crew reaction): feedback waits
            // for the debrief, otherwise "log it compliant" becomes a free probe for which objects are hazards.
            // XP for correct confirmations is paid at the end of the shift.
            if (result == ConfirmOutcome.Confirmed || result == ConfirmOutcome.DismissedHazard)
            {
                var ok = result == ConfirmOutcome.Confirmed;
                Log("confirm_compliant", selected.Id, ok ? "correct" : "hazard-dismissed", Ecd("confirm_compliant"), ok ? 1f : 0f);
                LastKsa = "";
                Say("Logged as compliant.");
            }
            else Say("You already made the call on this one.");
            tablet.Refresh();
        }

        private void Collect() => PickUpKit();
        public void PickUpKit()
        {
            if (pendingInstall == null) { Say("Choose an engineered fix on the tablet first."); return; }
            if (Carrying) return;
            if (KitOptions != null && ChosenKit < 0) { Say("Choose which control goes in on the tablet first."); return; }
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
        public void SetKitDown(SiteCondition target, Vector3 point)
        {
            var goal = conditions.FirstOrDefault(c => c.Id == pendingInstall);
            if (goal == null) return;
            var error = Vector3.Distance(point, goal.PhotoBounds.ClosestPoint(point));
            var ok = target == goal || error <= placeTolerance;
            Log("placement_attempt", goal.Id, $"error={error:F2}m ok={ok}");
            if (!ok)
            {
                Log("ksa", goal.Id, "misplaced", Ecd("install_misplaced"), 0f);
                Say($"Wrong spot — {error:F1} m off. Set it at the hazard itself."); return;
            }
            // Right place, wrong control: it fails at the hazard, counts as an install attempt, and goes back.
            if (KitOptions != null && ChosenKit != KitCorrect)
            {
                Session.CompleteInstall(goal.Id, false);
                var kit = ControlKits.Get(goal.Id);
                Log("install_attempt", goal.Id, "wrong-kit:" + KitOptions[ChosenKit], Ecd("install_wrong_kit"), 0f);
                Say("That won't pass. " + (kit != null ? kit.Why : "") + " Pick the right control on the tablet.");
                LastKsa = Feedback(Jobsite.Core.Ksa.SControl, goal.Cfr, "\"" + KitOptions[ChosenKit] + "\" doesn't meet the standard here.");
                AudioDirector.Play("click");
                Destroy(carriedVisual); carriedVisual = null; ChosenKit = -1;
                CrewGestures.ReactNear(goal.transform.position, 10f, CrewGestures.Situation.Puzzled, goal.PhotoBounds.center);
                return;
            }
            if (Session.CompleteInstall(goal.Id, true))
            {
                goal.ShowControl(true);
                Destroy(carriedVisual); carriedVisual = null; pendingInstall = null;
                KitOptions = null; KitCorrect = -1; ChosenKit = -1;
                Xp += ControlLevel.Engineering <= goal.Spec.BestFeasibleControl ? XpRules.BestControl + XpRules.EngineeredBonus : XpRules.EngineeredBonus;
                Say("Control installed. Crew can continue safely.");
                AudioDirector.Play("success");
                Log("install_success", goal.Id, "Engineering", Ecd("install_success"), 1f);
                LastKsa = Feedback(Jobsite.Core.Ksa.SInstall, goal.Cfr, "control in place at the exposure and verified.");
                CrewGestures.ReactNear(goal.transform.position, 12f, CrewGestures.Situation.ControlInstalled, goal.PhotoBounds.center);
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
            LogMeasuredFirst(selected);
            var streakBefore = Session.StreakBonuses;
            var outcome = Session.Report(selected.Id, energy, probability, severity);
            var streakPaid = Session.StreakBonuses > streakBefore;
            if (streakPaid) { Xp += XpRules.StreakBonus; Hints.Earn(); Log("streak", selected.Id, "bonus+token"); }
            if (outcome == ReportOutcome.FalseReport) SpendShiftTime(FalseAlarmSeconds);
            if (outcome == ReportOutcome.Reported) Xp += XpRules.HazardXp(selected.Spec, Session.GetEvidence(selected.Id));
            // Corrective feedback on the tag and the rating (the engine already knows both).
            var spec = selected.Spec;
            LastFeedback = outcome != ReportOutcome.Reported ? "" :
                (energy == spec.Energy ? $"Energy: {energy} - correct." : $"Energy: you tagged {energy}; the source here is {spec.Energy}.") + "\n" +
                (Math.Abs(probability - spec.Probability) + Math.Abs(severity - spec.Severity) <= 2
                    ? $"Risk P{probability} x S{severity}: close to the site assessment (P{spec.Probability} x S{spec.Severity})."
                    : $"Risk P{probability} x S{severity}; site assessment is P{spec.Probability} x S{spec.Severity}. " +
                      (severity < spec.Severity ? "Think about the worst credible outcome." : "Weigh how likely it is today."));
            if (outcome == ReportOutcome.Reported)
            {
                AudioDirector.Play("success");
                var ev = Session.GetEvidence(selected.Id);
                var detect = KsaLedger.DetectScore(ev.DetectedAtSeconds, ShiftLength, ev.Hinted || ev.Cued);
                Log("ksa", selected.Id, ev.Cued ? "recognize cued" : "recognize", Ecd("recognize"), ev.Hinted || ev.Cued ? 0.5f : 1f);
                Log("ksa", selected.Id, "energy=" + energy, Ecd("energy"), energy == spec.Energy ? 1f : 0f);
                Log("ksa", selected.Id, $"P{probability}xS{severity}", Ecd("risk"), KsaLedger.RiskScore(ev.RiskDeviation));
                Log("ksa", selected.Id, "t=" + ev.DetectedAtSeconds.ToString("F0"), Ecd("detect_time"), detect);
                LastKsa = "K  " + (string.IsNullOrEmpty(selected.Cfr) ? spec.Energy + " energy" : selected.Cfr + ": " + selected.Threshold) + "\n" +
                          "S  recognized" + (ev.Hinted ? " (with a hint)" : ev.Cued ? " (with a guide marker)" : "") + " · energy " + (energy == spec.Energy ? "✓" : "✗") +
                          " · risk " + (ev.RiskDeviation == 0 ? "on the key" : "±" + ev.RiskDeviation) + "\n" +
                          $"A  proactive: spotted at {ev.DetectedAtSeconds / 60f:0.0} min of {ShiftLength / 60f:0}" + (detect >= 1f ? " · early, before exposure grows" : " · late: scan the site sooner") +
                          (streakPaid ? $"\nStreak: three clean reports · +{XpRules.StreakBonus} XP, +1 hint token" : "");
                CrewGestures.ReactNear(selected.transform.position, 12f, CrewGestures.Situation.Acknowledge, selected.PhotoBounds.center);
            }
            else if (outcome == ReportOutcome.FalseReport)
            {
                Log("ksa", selected.Id, "look-alike reported", Ecd("lookalike_reported"), 0f);
                Log("ksa", selected.Id, "crew trust", Ecd("lookalike_trust"), 0f);
                LastKsa = Feedback(Jobsite.Core.Ksa.SDiscriminate, selected.Cfr, "this one is compliant. Confirm it, don't report it: false alarms cost crew trust.");
                CrewGestures.ReactNear(selected.transform.position, 10f, CrewGestures.Situation.Puzzled, selected.PhotoBounds.center);
            }
            Say(outcome == ReportOutcome.FalseReport ? selected.Explanation : outcome == ReportOutcome.Reported ? "Report recorded. Check the feedback, then choose your control." : "Report recorded. Choose your control.");
            Log("report", selected.Id, outcome + ":" + energy + ":" + probability + ":" + severity); tablet.Refresh();
        }

        public void Control(ControlLevel level)
        {
            if (selected == null || Finished) return;
            if (pendingInstall != null && pendingInstall != selected.Id) { Say("Finish your current installation first."); return; }
            var result = Session.ChooseControl(selected.Id, level);
            var best = selected.Spec.BestFeasibleControl;
            var cs = result == ControlOutcome.Installing || result == ControlOutcome.Assigned || result == ControlOutcome.Eliminated ? KsaLedger.ControlScore(level, best) : -1f;
            Log("control_choose", selected.Id, result + ":" + level, cs >= 0 ? Ecd("control_choose") : (Ksa?)null, cs);
            if (cs >= 0) LastKsa = Feedback(Jobsite.Core.Ksa.SControl, selected.Cfr, cs >= 1f ? $"{level} is the most effective feasible control here."
                : $"{level} leans on people. {best} is feasible here and removes the exposure.");
            if (result == ControlOutcome.Installing)
            {
                pendingInstall = selected.Id;
                KitOptions = null; KitCorrect = -1; ChosenKit = -1;
                if (ControlKits.Has(selected.Id))
                {
                    // Which control? The tablet stays open for the choice (right kit + two plausible wrong ones).
                    KitOptions = ControlKits.Options(selected.Id, Seed ^ (selected.Id.Length * 7919), out var correct);
                    KitCorrect = correct;
                    Say("Which control goes in? Pick it, then collect it at the supply rack.");
                }
                else { MenuOpen = false; Say("Pick up materials at the supply rack (E)."); }
            }
            else if (result == ControlOutcome.Eliminated)
            {
                selected.ShowControl(true);
                Xp += XpRules.BestControl + XpRules.EngineeredBonus;
                Say("Removed from service and tagged out. The hazard is gone.");
                AudioDirector.Play("success");
                Log("install_success", selected.Id, "Elimination", Ecd("install_success"), 1f);
                CrewGestures.ReactNear(selected.transform.position, 12f, CrewGestures.Situation.ControlInstalled, selected.PhotoBounds.center);
            }
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
            if (result == StopOutcome.Repeated)
            {
                // Still honoured (stopping is never wrong to try) but it earns nothing new and costs schedule.
                Say("Ray: Stopped again? Crew's idle. Get a real control in.");
                Log("stop_work", selected.Id, result.ToString());
                CrewGestures.Named("Ray")?.React(CrewGestures.Situation.ForemanPressure);
                tablet.Refresh();
                return;
            }
            Say(result == StopOutcome.Justified ? $"Work stopped for about {DaySession.StopHoldSeconds / 60:0} min. Get a control in before Ray restarts the crew." : "Report an active hazard before stopping this crew.");
            var js = result == StopOutcome.Justified ? (selected.Spec.RequiresStopWork ? 1f : 0.7f) : 0f;
            Log("stop_work", selected.Id, result.ToString(), Ecd("stop_work"), js);
            LastKsa = Feedback(Jobsite.Core.Ksa.AIntervene, selected.Cfr, result != StopOutcome.Justified ? "report the hazard first; a stop needs a named reason."
                : selected.Spec.RequiresStopWork ? "right call: this exposure can't wait for a fix." : "defensible, but a quick fix would have kept the crew working.");
            if (result == StopOutcome.Justified)
            {
                CrewGestures.ReactNear(selected.transform.position, 14f, CrewGestures.Situation.WorkStopped, selected.PhotoBounds.center);
                CrewGestures.Named("Ray")?.React(CrewGestures.Situation.ForemanPressure);
                // Production pressure: the foreman pushes back and the learner has to hold the line (GDD N4).
                PendingSpeakUp = selected.Id;
                SpeakUpOptions = SpeakUp.Options(Seed ^ (selected.Id.Length * 104729));
                Say(SpeakUp.Pushback(selected.DisplayName));
            }
            tablet.Refresh();
        }

        public void ChooseKit(int option)
        {
            if (KitOptions == null || option < 0 || option >= KitOptions.Count || pendingInstall == null) return;
            ChosenKit = option;
            Log("kit_choose", pendingInstall, KitOptions[option]);
            Say("Kit: " + KitOptions[option] + ". Collect it at the supply rack (E).");
            MenuOpen = false;
            tablet.Refresh();
        }

        // Ray's pushback after a stop: passive gives the crew back, aggressive holds but costs trust, assertive holds.
        public void ChooseSpeakUp(int option)
        {
            if (PendingSpeakUp == null || SpeakUpOptions == null || option < 0 || option >= SpeakUpOptions.Count) return;
            var id = PendingSpeakUp; var style = SpeakUpOptions[option].Style;
            PendingSpeakUp = null;
            if (Session.SpeakUp(id, style))
            {
                if (style == SpeakUpStyle.Assertive) Xp += XpRules.SpeakUpAssertive;
                Log("speakup_choice", id, style.ToString(), Ecd("speakup_choice"), SpeakUp.Score(style));
                LastKsa = Feedback(Jobsite.Core.Ksa.SCommunicate, selected != null ? selected.Cfr : "", style == SpeakUpStyle.Assertive
                    ? "firm and respectful: the stop holds and Ray stays on your side."
                    : style == SpeakUpStyle.Aggressive ? "the stop holds, but you spent the foreman's trust to do it."
                    : "you gave the crew back to the hazard. The exposure is live again.");
            }
            Say(SpeakUp.Reply(style));
            CrewGestures.Named("Ray")?.React(style == SpeakUpStyle.Passive ? CrewGestures.Situation.BackToWork : CrewGestures.Situation.Acknowledge);
            tablet.Refresh();
        }

        public void EndShift()
        {
            if (Current != Phase.Shift) return;
            EndedEarly = Session.Clock < ShiftLength;
            PendingSpeakUp = null; PendingIncident = null; PendingWeather = null;
            TalkWhy = new QuizSession(new[] { ToolboxTalk.Why(Episode.Number) }, Seed + 2);
            Xp += Session.ConfirmedCompliant * XpRules.ConfirmCompliant;
            Current = Phase.Closed; MenuOpen = true; Session.Paused = true;
            Quiz = new QuizSession(Episode.ClosingQuiz(), Seed + 1);
            Say("Shift closed. Review what your crew needed.");
            Log("shift_end", "day", "HII=" + Session.HazardIdentificationIndex.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + " xp=" + Xp +
                " early=" + EndedEarly + " stopped_s=" + Session.StoppedSeconds.ToString("F0", System.Globalization.CultureInfo.InvariantCulture) +
                " confirmed=" + Session.ConfirmedCompliant +
                " mission=" + Mission.Completed + "/" + Mission.Mission.Steps.Count);
            Log("ksa", "mission", Mission.Completed + "/" + Mission.Mission.Steps.Count, Ecd("mission"),
                (float)Mission.Completed / Mission.Mission.Steps.Count, "1926.20(b)(2)");
            foreach (var c in conditions.Where(c => c.IsHazard && !Session.GetEvidence(c.Id).Detected))
                Log("ksa", c.Id, "missed", Ecd("missed"), 0f);
            foreach (Ksa k in Enum.GetValues(typeof(Ksa)))
                if (Competence.Mean(k) is float m) Log("ksa_profile", k.ToString(), "n=" + Competence.Count(k), k, m, "");
            tablet.Refresh();
        }

        // ---------- tomorrow's toolbox talk (GDD N7, §4 Reflect) ----------
        public IEnumerable<SiteCondition> Findings => conditions.Where(c => c != null && c.IsHazard && Session.GetEvidence(c.Id).Detected);

        public void ToggleTalkPick(string id)
        {
            if (!Finished || EpisodeComplete || Findings.All(c => c.Id != id)) return;
            if (!talkPicks.Remove(id) && talkPicks.Count < ToolboxTalk.Picks) talkPicks.Add(id);
            tablet.Refresh();
        }

        public void ClearTalk() { talkPicks.Clear(); tablet.Refresh(); }

        public void SubmitToolboxTalk(int whyOption)
        {
            if (!Finished || EpisodeComplete || TalkWhy == null || TalkWhy.Done) return;
            var found = Findings.Select(c => c.Spec).ToList();
            if (found.Count > 0 && talkPicks.Count == 0) { Say("Dolores: Pick what you'll brief first. At least one finding."); return; }
            var why = TalkWhy.Current;
            var whyOk = TalkWhy.Answer(whyOption);
            var ordered = talkPicks.Select(id => conditions.First(c => c.Id == id).Spec).ToList();
            TalkScore = ToolboxTalk.Score(ordered, found, whyOk);
            var leads = ToolboxTalk.LeadsWithTopRisk(ordered, found);
            if (leads) Xp += XpRules.ToolboxLead;
            if (found.Count > 1) Log("talk_top_risk", "day", leads ? "leads-with-top-risk" : "top-risk-not-first", Ecd("talk_top_risk"), leads ? 1f : 0f);
            if (whyOk) Xp += XpRules.ToolboxWhy;
            var good = TalkScore >= ToolboxTalk.GoodTalk;
            if (good) { PlayerPrefs.SetInt(BankedHintsKey, PlayerPrefs.GetInt(BankedHintsKey, 0) + 1); PlayerPrefs.Save(); }
            var top = conditions.Where(c => found.Any(f => f.Id == c.Id)).OrderByDescending(c => ToolboxTalk.Risk(c.Spec)).FirstOrDefault();
            TalkFeedback = (found.Count == 0 ? "Dolores: No findings to brief. Walk the site first thing tomorrow."
                    : leads ? $"Dolores: Right, open with the {top?.DisplayName.ToLowerInvariant()}. Worst credible outcome goes first."
                    : $"Dolores: I'd open with the {top?.DisplayName.ToLowerInvariant()}. It had the highest risk today (P{top?.Spec.Probability} x S{top?.Spec.Severity}).")
                + "\n" + (whyOk ? "Why: right. " : "Why: not quite. ") + why.Explanation
                + (good ? "\nGood talk: +1 hint token banked for your next shift." : "");
            Log("toolbox_talk", "day", "order=" + string.Join(">", talkPicks) + " why=" + (whyOk ? "ok" : "wrong") +
                " score=" + TalkScore.ToString("F2", System.Globalization.CultureInfo.InvariantCulture), Ecd("toolbox_talk"), TalkScore, "1926.21(b)(2)");
            Log("why_choice", why.Id, whyOk ? "correct" : "wrong", Ecd("why_choice"), whyOk ? 1f : 0f, "1926.21(b)(2)");
            Say(TalkFeedback.Split('\n')[0]);
            CompleteEpisode();
        }

        // Legacy entry (demo autoplay and older tests): submits whatever is picked, or the findings in the order found.
        public void ExplainBack(int choice)
        {
            if (!Finished || EpisodeComplete) return;
            if (TalkWhy == null) TalkWhy = new QuizSession(new[] { ToolboxTalk.Why(Episode.Number) }, Seed + 2);
            if (talkPicks.Count == 0)
                talkPicks.AddRange(Findings.OrderBy(c => Session.GetEvidence(c.Id).DetectedAtSeconds).Take(ToolboxTalk.Picks).Select(c => c.Id));
            SubmitToolboxTalk(Mathf.Clamp(choice, 0, TalkWhy.Current.Options.Length - 1));
        }

        private void CompleteEpisode()
        {
            MasteryToday = Session.Mastery();
            MasteryBest = MasteryStore.Record(MasteryToday);
            Log("mastery", "ep" + Episode.Number, string.Join(" ", MasteryToday.Select(kv => kv.Key + "=" + kv.Value.ToString("F2", System.Globalization.CultureInfo.InvariantCulture))));
            if (Xp > PlayerPrefs.GetInt(EpisodeDirector.Key(Episode), -1)) { PlayerPrefs.SetInt(EpisodeDirector.Key(Episode), Xp); PlayerPrefs.Save(); }
            BadgesEarned = Badges.Earned(Session, conditions.Select(c => c.Spec), ShiftLength);
            PointsAwarded = Career.Award(Xp, BadgesEarned.Count);
            CareerStore.Save(Career);
            EpisodeComplete = true;
            Log("episode_complete", "ep" + Episode.Number, "xp=" + Xp);
            StartCoroutine(EpisodeDirector.Epilogue(this));
            Telemetry.Complete(new Telemetry.Completion
            {
                episode = Episode.Number, xp = Xp, hii = Session.HazardIdentificationIndex, precision = Session.ReportPrecision,
                incidents = Session.NearMisses + Session.Recordables, quizCorrect = Quiz?.CorrectCount ?? 0, quizTotal = Quiz?.Items.Count ?? 0,
            }, code => { CompletionCode = code ?? "offline"; tablet.Refresh(); });
            tablet.Refresh();
        }

        // ---------- weather (GDD §20): the CP makes the call when conditions change ----------
        public void WeatherChanged(WeatherEvent ev)
        {
            Say(ev.Radio);
            Log("weather", ev.Id, ev.State.Summary, null, -1f, ev.Cfr);
            if (player != null) CrewGestures.ReactNear(player.transform.position, 30f, CrewGestures.Situation.WeatherTurn, player.transform.position + Vector3.up * 30f);
            if (!ev.IsDecision || Current != Phase.Shift) return;
            PendingWeather = ev;
            if (TalkingTo != null) EndTalk();
            MenuOpen = true;                        // the tablet pops the alert; the shift clock pauses while it is open
            AudioDirector.Play("radio");
            tablet.Refresh();
        }

        public void LogHeat(string kind, float strain) => Log(kind, "player", "strain=" + strain.ToString("F2", System.Globalization.CultureInfo.InvariantCulture));

        public void ChooseWeather(int option)
        {
            var ev = PendingWeather;
            if (ev == null || option < 0 || option >= ev.Options.Count) return;
            var o = ev.Options[option];
            WeatherCalls.Add((ev, o.Quality));
            Xp += WeatherPlan.Xp(o.Quality);
            Log("weather_decision", ev.Id, "q=" + o.Quality + ":" + option, Ecd("weather_decision"), o.Quality / 2f, ev.Cfr);
            LastKsa = Feedback(Jobsite.Core.Ksa.AIntervene, ev.Cfr, o.Quality == 2 ? "you made the call the conditions demanded." : "conditions changed; the plan has to change with them.");
            CrewGestures.Named("Dolores")?.React(o.Quality == 2 ? CrewGestures.Situation.Acknowledge : CrewGestures.Situation.Puzzled);
            Say((o.Quality == 2 ? "Good call. " : "") + o.Feedback);
            AudioDirector.Play(o.Quality == 2 ? "success" : "click");
            PendingWeather = null; MenuOpen = false;
            tablet.Refresh();
        }

        // ---------- hints (GDD §14/§19): Dolores points you toward the nearest hazard you have not found ----------
        public void UseHint()
        {
            if (Current != Phase.Shift) return;
            var target = conditions.Where(c => c.IsHazard && Session.GetState(c.Id) == HazardState.Latent)
                .OrderBy(c => Vector3.Distance(c.transform.position, player.transform.position)).FirstOrDefault();
            if (target == null) { Say("Dolores: You've found everything I'd flag. Check your controls."); return; }
            if (!Hints.TrySpend()) { Say("No hint tokens left. A logbook in the gear locker adds one per shift."); return; }
            var tier = Session.UseHint(target.Id);
            var spec = target.Spec;
            Say(Career.HintText(tier, target.NeutralName, spec.Energy, spec.FocusFour, Where(target.transform.position - player.transform.position)));
            Log("hint", target.Id, "tier=" + tier);
            FindFirstObjectByType<ScaffoldCues>()?.Hint(target, tier);
            var dolores = CrewGestures.Named("Dolores");
            if (dolores != null && Vector3.Distance(dolores.transform.position, player.transform.position) < 25f)
                dolores.React(CrewGestures.Situation.Hint, target.PhotoBounds.center);
            tablet.Refresh();
        }

        // Site plan is north-up (+Z): "northeast, about 20 m".
        static string Where(Vector3 d)
        {
            var names = new[] { "north", "northeast", "east", "southeast", "south", "southwest", "west", "northwest" };
            var i = Mathf.RoundToInt(Mathf.Repeat(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, 360) / 45f) % 8;
            return $"{names[i]}, about {Mathf.Max(5, Mathf.RoundToInt(new Vector2(d.x, d.z).magnitude / 5f) * 5)} m";
        }

        // ---------- crew conversation ----------
        public void StartTalk(CrewMember crew)
        {
            TalkingTo = crew; MenuOpen = true;
            crew.BeginTalk(player.transform);
            // Opening a conversation is logged, not scored: communication evidence has to come from what is said
            // or decided (speak-up choices), not from pressing E on a person.
            Log("radio_query_open", crew.DisplayName, "", null, -1f, "1926.21(b)(2)");
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
            Log("chat_reply", crew.DisplayName, "verdict=" + crew.LastVerdict + " ms=" + crew.LastLatencyMs + " len=" + crew.LastReplyLength);
            if (TalkingTo == crew) tablet.Refresh();
        }

        public void Say(string text) { notice = text; Ping(); }
        private void Ping() => AudioDirector.Play("radio");

        // Normal reach is the career photo range; overhead hazards (boom near a line) can be photographed from farther.
        private bool InReach(RaycastHit hit)
        {
            if (hit.distance <= Career.PhotoRange) return true;
            var c = hit.collider.GetComponentInParent<SiteCondition>();
            return c != null && c.PhotoRange > 0 && hit.distance <= c.PhotoRange;
        }

        // HUD prompt for whatever the crosshair is on (same ray and range as Interact).
        public string AimPrompt(out bool actionable)
        {
            actionable = false;
            if (MenuOpen || Finished || player == null) return "";
            var ray = player.View.ViewportPointToRay(new Vector3(.5f, .5f));
            if (!Physics.Raycast(ray, out var hit, 16f, ~0, QueryTriggerInteraction.Collide) || !InReach(hit)) return "";
            actionable = true;
            var station = hit.collider.GetComponentInParent<CheckInStation>();
            if (station != null) return "E  " + (station.name == "SignInBoard" ? "Sign in" : "Take the " + station.name);
            var crew = hit.collider.GetComponentInParent<CrewMember>();
            if (crew != null) return "E  Talk to " + crew.DisplayName;
            if (Current != Phase.Shift) { actionable = false; return ""; }
            var access = hit.collider.GetComponentInParent<AccessPoint>();
            if (access != null) return "E  " + access.Label;
            if (hit.collider.GetComponentInParent<VehicleController>() != null) return "E  Get in";
            if (hit.collider.GetComponentInParent<ControlSupply>() != null) return pendingInstall != null ? "E  Pick up the kit" : "Supply rack";
            if (Carrying) return "E  Set the kit down here";
            // Same prompt on every surface in reach: the crosshair must not single out conditions.
            return "E  Photograph";
        }

        static string Feedback(Ksa k, string cfr, string text) =>
            $"{KsaInfo.Domain(k)}  {KsaInfo.Name(k)}" + (string.IsNullOrEmpty(cfr) ? "" : $" · {cfr}") + ": " + text;

        [Serializable] private sealed class EventRow
        { public string timestamp; public string kind; public string condition; public string detail; public float shiftSeconds; public string cfr; public string ksa; public float score; }

        // Every event is tied to an OSHA standard (explicit, or the condition's own citation) and, when it is a scored
        // action, to a KSA with a 0..1 performance score. Rows go to the local JSONL log, the course server and missions.
        // KSA for an observable, from the ECD model (EvidenceModel / Resources/ecd.json).
        private static Ksa Ecd(string observable) => EvidenceModel.KsaOf(observable);

        private void Log(string kind, string id, string detail, Ksa? ksa = null, float score = -1f, string cfr = null)
        {
            cfr ??= conditions?.FirstOrDefault(c => c != null && c.Id == id)?.Cfr ?? "";
            // Scored actions without their own citation fall under the CP inspection program (frequent and regular
            // inspections by competent persons); the end-of-shift profile only summarises, it isn't new evidence.
            if (ksa.HasValue && score >= 0 && cfr.Length == 0 && kind != "ksa_profile") cfr = "1926.20(b)(2)";
            if (ksa.HasValue && score >= 0 && kind != "ksa_profile") Competence.Record(ksa.Value, cfr, score, kind + ":" + id);
            var clock = Session?.Clock ?? 0;
            Telemetry?.Add(kind, id, detail, clock, cfr, ksa?.ToString() ?? "", ksa.HasValue ? score : -1f);
            if (logPath != null)
                try
                {
                    File.AppendAllText(logPath, JsonUtility.ToJson(new EventRow { timestamp = DateTime.UtcNow.ToString("O"), kind = kind, condition = id, detail = detail,
                        shiftSeconds = clock, cfr = cfr, ksa = ksa?.ToString() ?? "", score = ksa.HasValue ? score : -1f }) + "\n");
                }
                catch (IOException e) { Debug.LogWarning("Jobsite log unavailable: " + e.GetType().Name); }
            var step = Mission?.OnEvent(kind, id, detail);
            if (step != null)
            {
                Log("mission_step", id, Mission.Completed + "/" + Mission.Mission.Steps.Count + " " + step.Text, step.Ksa, 1f, step.Cfr);
                Say($"Checklist ✓ {step.Text}. " + (Mission.Complete ? "Inspection complete: sign the log." : "Next: " + Mission.Next.Text + "."));
                if (Mission.Complete) { Xp += 100; Log("mission_complete", "ep" + Episode.Number, Mission.Mission.Title); }
                FindFirstObjectByType<ScaffoldCues>()?.Refresh();
            }
        }
    }
}
