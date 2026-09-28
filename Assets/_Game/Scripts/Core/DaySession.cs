using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    public enum ReportOutcome { Reported, ReReportedAfterLapse, AlreadyReported, FalseReport, NotReportable, Unknown }
    public enum ControlOutcome { Installing, Assigned, NotFeasible, InvalidState, Unknown }
    public enum StopOutcome { Justified, Unjustified, InvalidState, Unknown }
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
        public const float CompetentThreshold = 0.7f;
        // A stop holds this long; then the foreman restarts the crew unless a control is on the way.
        public const float StopHoldSeconds = 120f;

        sealed class Entry
        {
            public HazardSpec Spec;
            public HazardState State;
            public float LapseAt = float.PositiveInfinity;
            public float StopUntil;
            public int FalseReports;
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
        public int NearMisses { get; private set; }
        public int Recordables { get; private set; }
        public int TrueReports { get; private set; }
        public int FalseReports { get; private set; }
        public float StoppedSeconds { get; private set; }
        public int StreakBonuses { get; private set; }
        private int streak;

        public HazardState GetState(string id) => entries[id].State;
        public HazardEvidence GetEvidence(string id) => entries[id].Evidence;

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
                    CrewTrust--; // only a repeated false alarm on the same object costs trust
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

        public ControlOutcome ChooseControl(string id, ControlLevel level)
        {
            if (!entries.TryGetValue(id, out var e) || !e.Spec.IsHazard)
                return ControlOutcome.Unknown;
            if (e.State != HazardState.Reported && e.State != HazardState.Stopped)
                return ControlOutcome.InvalidState;
            if (level < e.Spec.BestFeasibleControl)
                return ControlOutcome.NotFeasible;

            e.Evidence.AppliedControl = level;
            if (level <= ControlLevel.Engineering)
            {
                e.State = HazardState.Installing;
                return ControlOutcome.Installing;
            }

            // Assign (admin / PPE): holds only for a while unless it is the best feasible control.
            e.State = HazardState.Controlled;
            e.LapseAt = level == e.Spec.BestFeasibleControl ? float.PositiveInfinity : Clock + e.Spec.LapseAfterSeconds;
            return ControlOutcome.Assigned;
        }

        public bool CompleteInstall(string id, bool success)
        {
            if (!entries.TryGetValue(id, out var e) || e.State != HazardState.Installing)
                return false;

            e.Evidence.InstallAttempts++;
            if (!success)
                return false;

            e.State = HazardState.Controlled;
            e.LapseAt = float.PositiveInfinity;
            CrewTrust++;
            return true;
        }

        public void CancelInstall(string id)
        {
            if (entries.TryGetValue(id, out var e) && e.State == HazardState.Installing)
                e.State = HazardState.Reported;
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
            e.Evidence.StopWorkCalled = true;
            CrewTrust++;
            return StopOutcome.Justified;
        }

        public void LiftStop(string id)
        {
            if (entries.TryGetValue(id, out var e) && e.State == HazardState.Stopped)
                e.State = HazardState.Reported;
        }

        public IReadOnlyList<DayEvent> Advance(float deltaSeconds)
        {
            var events = new List<DayEvent>();
            if (Paused || deltaSeconds <= 0f)
                return events;

            Clock += deltaSeconds;
            foreach (var e in entries.Values)
            {
                if (!e.Spec.IsHazard)
                    continue;

                if (e.State == HazardState.Stopped)
                {
                    // Stopped work cannot hurt anyone, but it costs schedule and it does not last.
                    StoppedSeconds += Math.Min(deltaSeconds, Math.Max(0f, e.StopUntil - (Clock - deltaSeconds)));
                    if (Clock < e.StopUntil) continue;
                    e.State = HazardState.Reported;
                    events.Add(new DayEvent(DayEventKind.StopLifted, e.Spec.Id, Clock));
                }

                if (e.State == HazardState.Controlled && Clock >= e.LapseAt)
                {
                    e.State = HazardState.Lapsed;
                    e.LapseAt = float.PositiveInfinity;
                    e.Evidence.Lapses++;
                    CrewTrust--;
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
                    events.Add(new DayEvent(kind, e.Spec.Id, Clock));
                }
            }

            return events;
        }

        // Composite 0..1 per real hazard: detect .35, tag .15, risk .15, control .25, escalation .10.
        public static float HazardScore(HazardSpec spec, HazardEvidence ev)
        {
            if (!ev.Detected)
                return 0f;

            var score = 0.35f;
            if (ev.TagCorrect) score += 0.15f;
            score += 0.15f * (1f - ev.RiskDeviation / 8f);
            score += 0.25f * ControlQuality(spec, ev);
            if (!spec.RequiresStopWork || ev.StopWorkCalled) score += 0.10f;
            return ev.Hinted ? score * 0.5f : score;
        }

        static float ControlQuality(HazardSpec spec, HazardEvidence ev)
        {
            if (ev.AppliedControl == null || ev.BecameIncident)
                return 0f;
            var gap = (int)ev.AppliedControl.Value - (int)spec.BestFeasibleControl;
            var quality = gap <= 0 ? 1f : gap == 1 ? 0.5f : 0.25f;
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
