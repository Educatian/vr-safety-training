using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    public enum ReportOutcome { Reported, ReReportedAfterLapse, AlreadyReported, FalseReport, NotReportable, Unknown }
    public enum ControlOutcome { Installing, Assigned, NotFeasible, InvalidState, Unknown, Eliminated }
    public enum StopOutcome { Justified, Repeated, Unjustified, InvalidState, Unknown }
    public enum ConfirmOutcome { Confirmed, DismissedHazard, AlreadyJudged, Unknown }
    public enum DayEventKind { Lapsed, NearMiss, Recordable, StopLifted }

    public readonly struct DayEvent
    {
        public DayEvent(DayEventKind kind, string hazardId, float atSeconds)
        {
            Kind = kind;
            HazardId = hazardId;
            AtSeconds = atSeconds;
        }

        public DayEventKind Kind { get; }
        public string HazardId { get; }
        public float AtSeconds { get; }
    }

    // One shift. Owns hazard state, outcome meters, and CP evidence (GDD §5).
    // Stop-work never lowers any rating; look-alike reports cost only precision evidence.
    public sealed class DaySession
    {
        // Starting values (GDD §5.2) — tune via playtest, not guesswork.
        public static float CompetentThreshold => EvidenceModel.Current.competentThreshold;   // ecd.json
        // A stop holds this long; then the foreman restarts the crew unless a control is on the way.
        public const float StopHoldSeconds = 120f;
        // Crew trust at which a worker starts self-reporting a hazard over the radio (GDD §5.2 feedback loop).
        public const int SelfReportTrust = 3;
        // Schedule slip that still earns the "On Schedule" commendation (GDD §5.2: lateness costs only this).
        public const float OnScheduleSlipMinutes = 3f;

        sealed class Entry
        {
            public HazardSpec Spec;
            public HazardState State;
            public float LapseAt = float.PositiveInfinity;
            public float StopUntil;
            public int FalseReports;
            public bool Confirmed;
            public ControlLevel? PendingControl;   // engineered control chosen but not installed yet
            public readonly HazardEvidence Evidence = new HazardEvidence();
        }

        readonly Dictionary<string, Entry> entries = new Dictionary<string, Entry>(StringComparer.Ordinal);

        public DaySession(IEnumerable<HazardSpec> specs)
        {
            foreach (var spec in specs)
            {
                if (entries.ContainsKey(spec.Id))
                    throw new ArgumentException($"Duplicate hazard identifier: {spec.Id}", nameof(specs));
                entries.Add(spec.Id, new Entry { Spec = spec, State = HazardState.Latent });
            }

            if (!entries.Values.Any(e => e.Spec.IsHazard))
                throw new ArgumentException("A day needs at least one real hazard.", nameof(specs));
        }

        public float Clock { get; private set; }
        public bool Paused { get; set; }
        public int CrewTrust { get; private set; }
        // Continuous, bounded affect of the crew and the foreman (presentation; see CrewAffect).
        public CrewAffect Affect { get; } = new CrewAffect();
        public int NearMisses { get; private set; }
        public int Recordables { get; private set; }
        public int TrueReports { get; private set; }
        public int FalseReports { get; private set; }
        public float StoppedSeconds { get; private set; }
        public int StreakBonuses { get; private set; }
        public int ConfirmedCompliant { get; private set; }
        // Schedule meter: crew minutes lost to stops (fixes are quick; stops are what slip the plan).
        public float ScheduleSlipMinutes => StoppedSeconds / 60f;
        private int streak;

        public HazardState GetState(string id) => entries[id].State;
        public HazardEvidence GetEvidence(string id) => entries[id].Evidence;

        // Has the learner made a call on this condition yet (report, false report, or confirmed compliant)?
        // Until then the tablet shows only its neutral name.
        public bool Judged(string id) => entries.TryGetValue(id, out var e) &&
            (e.Evidence.Detected || e.FalseReports > 0 || e.Confirmed || e.Evidence.DismissedAsCompliant);

        // The learner logged it as compliant (right or wrong: the tablet treats both the same until the debrief).
        public bool LoggedCompliant(string id) => entries.TryGetValue(id, out var e) &&
            (e.Confirmed || e.Evidence.DismissedAsCompliant);

        // The condition's real title may be shown: only after a REPORT, which is when feedback is given. Logging a
        // condition compliant gives no feedback until the debrief, so it can't be used to probe for hazards.
        public bool Revealed(string id) => entries.TryGetValue(id, out var e) &&
            (e.Evidence.Detected || e.FalseReports > 0);

        public float HazardIdentificationIndex
        {
            get
            {
                var real = entries.Values.Where(e => e.Spec.IsHazard).ToList();
                return (float)real.Count(e => e.Evidence.Detected) / real.Count;
            }
        }

        public float ReportPrecision =>
            TrueReports + FalseReports == 0 ? 1f : (float)TrueReports / (TrueReports + FalseReports);

        public ReportOutcome Report(string id, EnergySource tag, int probability, int severity)
        {
            if (!entries.TryGetValue(id, out var e))
                return ReportOutcome.Unknown;

            if (!e.Spec.IsHazard)
            {
                FalseReports++;
                streak = 0;
                if (++e.FalseReports == 2)
                { CrewTrust--; Affect.Nudge(CrewAffect.Crew, -0.25f, 0.1f); } // only a repeated false alarm on the same object costs trust
                return ReportOutcome.FalseReport;
            }

            switch (e.State)
            {
                case HazardState.Latent:
                    TrueReports++;
                    e.State = HazardState.Reported;
                    e.Evidence.Detected = true;
                    e.Evidence.DetectedAtSeconds = Clock;
                    e.Evidence.TagCorrect = tag == e.Spec.Energy;
                    e.Evidence.RiskDeviation = Math.Abs(probability - e.Spec.Probability) +
                                               Math.Abs(severity - e.Spec.Severity);
                    e.Evidence.Hinted = e.Evidence.HintTier > 0;
                    if (++streak % XpRules.StreakLength == 0) StreakBonuses++;
                    return ReportOutcome.Reported;
                case HazardState.Lapsed:
                    e.State = HazardState.Reported;
                    return ReportOutcome.ReReportedAfterLapse;
                case HazardState.Incident:
                    return ReportOutcome.NotReportable;
                default:
                    return ReportOutcome.AlreadyReported;
            }
        }

        // Escalating hint for an unfound hazard; returns the tier given (1-3), or 0 if none applies.
        // The token spend lives in HintBank; this only records the evidence consequence.
        public int UseHint(string id)
        {
            if (!entries.TryGetValue(id, out var e) || !e.Spec.IsHazard || e.State != HazardState.Latent)
                return 0;
            e.Evidence.HintTier = Math.Min(3, e.Evidence.HintTier + 1);
            return e.Evidence.HintTier;
        }

        // An in-world cue pointed the learner at this hazard before they found it (GDD pillar 1: the find is
        // scaffolded, so its recognition evidence counts half). No effect once found.
        public void MarkCued(string id)
        {
            if (entries.TryGetValue(id, out var e) && e.Spec.IsHazard && e.State == HazardState.Latent)
                e.Evidence.Cued = true;
        }

        // "I checked it and it is compliant" (the CP documents compliant work too). Correct on a look-alike is
        // positive discrimination evidence; on a real hazard it is recorded as a miss and the hazard stays live.
        public ConfirmOutcome ConfirmCompliant(string id)
        {
            if (!entries.TryGetValue(id, out var e))
                return ConfirmOutcome.Unknown;
            if (Judged(id))
                return ConfirmOutcome.AlreadyJudged;
            if (!e.Spec.IsHazard)
            {
                e.Confirmed = true;
                ConfirmedCompliant++;
                return ConfirmOutcome.Confirmed;
            }
            if (e.State != HazardState.Latent)
                return ConfirmOutcome.AlreadyJudged;
            e.Evidence.DismissedAsCompliant = true;
            streak = 0;
            return ConfirmOutcome.DismissedHazard;
        }

        // Shift time spent on an action (photo, measurement, a false alarm the crew has to explain). Runs the clock
        // even while the tablet is open, so timers and incidents fire exactly as they would in real time.
        public IReadOnlyList<DayEvent> Spend(float seconds)
        {
            var wasPaused = Paused;
            Paused = false;
            var events = Advance(seconds);
            Paused = wasPaused;
            return events;
        }

        public ControlOutcome ChooseControl(string id, ControlLevel level)
        {
            if (!entries.TryGetValue(id, out var e) || !e.Spec.IsHazard)
                return ControlOutcome.Unknown;
            if (e.State != HazardState.Reported && e.State != HazardState.Stopped)
                return ControlOutcome.InvalidState;
            if (level < e.Spec.BestFeasibleControl)
                return ControlOutcome.NotFeasible;

            if (level == ControlLevel.Elimination)
            {
                // Remove it from service: done on the spot (tag out, pull it), nothing to carry, nothing to lapse.
                e.Evidence.AppliedControl = level;
                e.State = HazardState.Controlled;
                e.LapseAt = float.PositiveInfinity;
                e.PendingControl = null;
                CrewTrust++; Affect.Nudge(CrewAffect.Crew, 0.2f, -0.1f);
                return ControlOutcome.Eliminated;
            }

            if (level <= ControlLevel.Engineering)
            {
                // Credit only once the control is actually in place (CompleteInstall), not when it is chosen.
                e.PendingControl = level;
                e.State = HazardState.Installing;
                return ControlOutcome.Installing;
            }

            e.Evidence.AppliedControl = level;

            // Assign (admin / PPE): holds only for a while unless it is the best feasible control.
            e.State = HazardState.Controlled;
            e.LapseAt = level == e.Spec.BestFeasibleControl ? float.PositiveInfinity : Clock + e.Spec.LapseAfterSeconds;
            return ControlOutcome.Assigned;
        }

        public bool CompleteInstall(string id, bool success)
        {
            // Installing, or stopped mid-install with the kit still on the way.
            if (!entries.TryGetValue(id, out var e) ||
                !(e.State == HazardState.Installing || (e.State == HazardState.Stopped && e.PendingControl != null)))
                return false;

            e.Evidence.InstallAttempts++;
            if (!success)
                return false;

            e.State = HazardState.Controlled;
            e.LapseAt = float.PositiveInfinity;
            e.Evidence.AppliedControl = e.PendingControl ?? ControlLevel.Engineering;
            e.PendingControl = null;
            CrewTrust++; Affect.Nudge(CrewAffect.Crew, 0.2f, -0.1f);
            return true;
        }

        // How long an assigned (administrative / PPE) control holds, scaled from now: coaching that sticks makes it
        // permanent (+inf), a lecture halves what's left. Controls that never lapse are unaffected.
        public float LapseTime(string id) => entries.TryGetValue(id, out var e) ? e.LapseAt : float.NaN;

        public void ScaleLapse(string id, float factor)
        {
            if (!entries.TryGetValue(id, out var e) || e.State != HazardState.Controlled || float.IsPositiveInfinity(e.LapseAt)) return;
            e.LapseAt = float.IsPositiveInfinity(factor) ? float.PositiveInfinity : Clock + Math.Max(1f, (e.LapseAt - Clock) * factor);
        }

        public void CancelInstall(string id)
        {
            if (entries.TryGetValue(id, out var e) && e.State == HazardState.Installing)
            {
                e.State = HazardState.Reported;
                e.PendingControl = null;
            }
        }

        public StopOutcome StopWork(string id)
        {
            if (!entries.TryGetValue(id, out var e))
                return StopOutcome.Unknown;
            if (!e.Spec.IsHazard)
                return StopOutcome.Unjustified; // costs schedule only, never rating
            if (e.State != HazardState.Reported && e.State != HazardState.Installing && e.State != HazardState.Lapsed)
                return StopOutcome.InvalidState;

            e.State = HazardState.Stopped;
            e.StopUntil = Clock + StopHoldSeconds;
            // A second stop on the same hazard holds the crew again (safety first) but earns nothing new:
            // the evidence was the first call; re-stopping instead of fixing only burns schedule.
            if (e.Evidence.StopWorkCalled)
                return StopOutcome.Repeated;
            e.Evidence.StopWorkCalled = true;
            CrewTrust++; Affect.Nudge(CrewAffect.Crew, 0.15f, 0.1f); Affect.Nudge(CrewAffect.Foreman, -0.05f, 0.35f);   // the foreman feels the schedule
            return StopOutcome.Justified;
        }

        // The foreman pushes back on a stop (GDD N4). Passive gives the crew back to the hazard; aggressive holds
        // the stop but costs trust; assertive-respectful holds it and keeps the foreman on side.
        public bool SpeakUp(string id, SpeakUpStyle style)
        {
            if (!entries.TryGetValue(id, out var e) || !e.Spec.IsHazard || e.Evidence.SpeakUp.HasValue)
                return false;
            e.Evidence.SpeakUp = style;
            if (style == SpeakUpStyle.Assertive) { CrewTrust++; Affect.Nudge(CrewAffect.Foreman, 0.25f, -0.15f); Affect.Nudge(CrewAffect.Crew, 0.1f, 0f); }
            else if (style == SpeakUpStyle.Aggressive) { CrewTrust--; Affect.Nudge(CrewAffect.Foreman, -0.35f, 0.3f); Affect.Nudge(CrewAffect.Crew, -0.1f, 0.1f); }
            else { LiftStop(id); Affect.Nudge(CrewAffect.Foreman, 0.05f, -0.2f); Affect.Nudge(CrewAffect.Crew, -0.15f, 0.05f); }
            return true;
        }

        public void LiftStop(string id)
        {
            if (entries.TryGetValue(id, out var e) && e.State == HazardState.Stopped)
                e.State = e.PendingControl != null ? HazardState.Installing : HazardState.Reported;
        }

        public IReadOnlyList<DayEvent> Advance(float deltaSeconds)
        {
            var events = new List<DayEvent>();
            if (Paused || deltaSeconds <= 0f)
                return events;

            Clock += deltaSeconds;
            Affect.Tick(deltaSeconds);
            foreach (var e in entries.Values)
            {
                if (!e.Spec.IsHazard)
                    continue;

                if (e.State == HazardState.Stopped)
                {
                    // Stopped work cannot hurt anyone, but it costs schedule and it does not last.
                    StoppedSeconds += Math.Min(deltaSeconds, Math.Max(0f, e.StopUntil - (Clock - deltaSeconds)));
                    if (Clock < e.StopUntil) continue;
                    e.State = e.PendingControl != null ? HazardState.Installing : HazardState.Reported;
                    events.Add(new DayEvent(DayEventKind.StopLifted, e.Spec.Id, Clock));
                }

                if (e.State == HazardState.Controlled && Clock >= e.LapseAt)
                {
                    e.State = HazardState.Lapsed;
                    e.LapseAt = float.PositiveInfinity;
                    e.Evidence.Lapses++;
                    CrewTrust--; Affect.Nudge(CrewAffect.Crew, -0.15f, 0.1f);
                    events.Add(new DayEvent(DayEventKind.Lapsed, e.Spec.Id, Clock));
                }

                var exposed = e.State == HazardState.Latent || e.State == HazardState.Reported ||
                              e.State == HazardState.Installing || e.State == HazardState.Lapsed;
                if (exposed && Clock >= e.Spec.TriggerAtSeconds)
                {
                    e.State = HazardState.Incident;
                    e.Evidence.BecameIncident = true;
                    var kind = e.Spec.IsHighSeverity ? DayEventKind.Recordable : DayEventKind.NearMiss;
                    if (kind == DayEventKind.Recordable) Recordables++; else NearMisses++;
                    Affect.Nudge(CrewAffect.Crew, -0.1f, kind == DayEventKind.Recordable ? 0.35f : 0.25f); Affect.Nudge(CrewAffect.Foreman, 0f, 0.2f);
                    events.Add(new DayEvent(kind, e.Spec.Id, Clock));
                }
            }

            return events;
        }

        // Composite 0..1 per real hazard; weights and factors come from the ECD model (EvidenceModel / ecd.json).
        public static float HazardScore(HazardSpec spec, HazardEvidence ev)
        {
            if (!ev.Detected)
                return 0f;

            var m = EvidenceModel.Current;
            var score = m.wDetect;
            if (ev.TagCorrect) score += m.wTag;
            score += m.wRisk * (1f - Math.Min(1f, ev.RiskDeviation / m.riskDeviationSpan));
            score += m.wControl * ControlQuality(spec, ev);
            // Escalation counts only if the stop was called and held (backing down to the foreman forfeits it).
            if (!spec.RequiresStopWork || (ev.StopWorkCalled && ev.SpeakUp != SpeakUpStyle.Passive)) score += m.wEscalation;
            return ev.Hinted || ev.Cued ? score * m.cuedOrHintedFactor : score;
        }

        static float ControlQuality(HazardSpec spec, HazardEvidence ev)
        {
            if (ev.AppliedControl == null || ev.BecameIncident)
                return 0f;
            var gap = (int)ev.AppliedControl.Value - (int)spec.BestFeasibleControl;
            var q = EvidenceModel.Current.controlGapQuality;
            var quality = gap <= 0 ? q[0] : gap == 1 ? q[1] : q[2];
            return ev.Lapses > 0 ? quality * 0.5f : quality;
        }

        public IReadOnlyDictionary<CpArea, float> Mastery()
        {
            return entries.Values
                .Where(e => e.Spec.IsHazard)
                .GroupBy(e => e.Spec.Area)
                .ToDictionary(g => g.Key, g => g.Average(e => HazardScore(e.Spec, e.Evidence)));
        }
    }

    public static class HazardPoolSampler
    {
        // Deterministic per seed so a replay can reproduce, and a re-roll changes, the answer key.
        public static List<HazardSpec> Sample(IReadOnlyList<HazardSpec> pool, int realCount, int lookAlikeCount, int seed)
        {
            var rng = new Random(seed);
            var real = Shuffle(pool.Where(h => h.IsHazard).ToList(), rng).Take(realCount);
            var fakes = Shuffle(pool.Where(h => !h.IsHazard).ToList(), rng).Take(lookAlikeCount);
            return real.Concat(fakes).ToList();
        }

        static List<T> Shuffle<T>(List<T> items, Random rng)
        {
            for (var i = items.Count - 1; i > 0; i--)
            {
                var j = rng.Next(i + 1);
                (items[i], items[j]) = (items[j], items[i]);
            }
            return items;
        }
    }
}
