using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Jobsite.Core
{
    // Hazard Hunt (arcade mode for the public build, docs/AwesomeAiGames_Plan.md): one site, three real minutes, a
    // score you can share. The round runs on the same DaySession as a course shift; only the clock is compressed
    // (the 10-minute shift passes in RealSeconds), so incidents, crew requests and the speed bonus keep their meaning.
    // Points come from the same evidence the ECD scores (find, energy tag, risk rating, control), so the way to a high
    // score is the competent way: reporting every look-alike costs points, and so does letting an exposure turn into an
    // incident. All values are starting values for playtest tuning.
    public static class ArcadeRules
    {
        public const float RealSeconds = 180f;
        public const int Find = 100, SpeedMax = 100, Tag = 25, Risk = 25, Control = 50, BestControl = 50, Hold = 25;
        public const int Confirm = 50, FalseAlarm = -50, Incident = -100, ClearPerSecond = 5;
        public const int RiskTolerance = 2;          // |P - P*| + |S - S*| at or under this earns the rating points

        // How much faster than real time the shift clock runs in a round.
        public static float TimeScale(float shiftSeconds) => shiftSeconds / RealSeconds;

        public enum Cell { Early, Late, Incident, Missed }

        public sealed class Result
        {
            public int Score, Found, Total, FalseAlarms, Confirmed, Incidents, ClearBonus, MaxScore;
            public Cell[] Cells = new Cell[0];
            public string Grade = "D";
            public bool Cleared => Total > 0 && Found == Total;
        }

        public readonly struct Hazard
        {
            public readonly HazardSpec Spec; public readonly HazardEvidence Evidence; public readonly HazardState State;
            public Hazard(HazardSpec spec, HazardEvidence evidence, HazardState state) { Spec = spec; Evidence = evidence; State = state; }
        }

        // Real hazards only (look-alikes score through confirms and false alarms). Cells are ordered by hazard id so
        // every player of the same daily site gets the same grid layout without the grid naming anything.
        public static Result Score(IEnumerable<Hazard> hazards, int falseAlarms, int confirmed, float shiftLength, float remainingRealSeconds = 0f)
        {
            var list = hazards.Where(h => h.Spec != null && h.Spec.IsHazard).OrderBy(h => h.Spec.Id, StringComparer.Ordinal).ToList();
            var r = new Result { Total = list.Count, FalseAlarms = Math.Max(0, falseAlarms), Confirmed = Math.Max(0, confirmed) };
            var cells = new List<Cell>();
            var points = 0;
            foreach (var h in list)
            {
                var ev = h.Evidence ?? new HazardEvidence();
                if (ev.BecameIncident) { r.Incidents++; points += Incident; }
                if (ev.Detected)
                {
                    r.Found++;
                    var t = float.IsNaN(ev.DetectedAtSeconds) ? shiftLength : ev.DetectedAtSeconds;
                    var speed = (int)Math.Round(SpeedMax * Clamp01(1f - t / Math.Max(1f, shiftLength)));
                    var findPoints = Find + speed;
                    if (ev.Hinted || ev.Cued) findPoints /= 2;
                    points += findPoints;
                    if (ev.TagCorrect) points += Tag;
                    if (ev.RiskDeviation <= RiskTolerance) points += Risk;
                    if (h.State == HazardState.Controlled && ev.AppliedControl.HasValue)
                    {
                        points += Control;
                        if (ev.AppliedControl.Value <= h.Spec.BestFeasibleControl) points += BestControl;
                    }
                    else if (h.State == HazardState.Stopped) points += Hold;
                }
                cells.Add(ev.BecameIncident ? Cell.Incident
                    : !ev.Detected ? Cell.Missed
                    : !(ev.Hinted || ev.Cued) && ev.DetectedAtSeconds <= shiftLength / 2f ? Cell.Early
                    : Cell.Late);
            }
            points += r.Confirmed * Confirm + r.FalseAlarms * FalseAlarm;
            r.MaxScore = list.Count * (Find + SpeedMax + Tag + Risk + Control + BestControl);
            r.ClearBonus = r.Cleared ? (int)Math.Round(Math.Max(0f, remainingRealSeconds) * ClearPerSecond) : 0;
            r.Score = Math.Max(0, points) + r.ClearBonus;
            r.Cells = cells.ToArray();
            r.Grade = Grade(Math.Max(0, points), r.MaxScore, r.FalseAlarms, r.Incidents);
            return r;
        }

        // S needs a clean round (no false alarms, no incidents); the rest is the share of the possible points.
        public static string Grade(int points, int max, int falseAlarms, int incidents)
        {
            if (max <= 0) return "D";
            var ratio = (float)points / max;
            if (ratio >= 0.8f && falseAlarms == 0 && incidents == 0) return "S";
            return ratio >= 0.65f ? "A" : ratio >= 0.45f ? "B" : ratio >= 0.25f ? "C" : "D";
        }

        static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    // The daily site: the same episode and the same seed (which look-alike swaps in, when incidents fire) for everyone
    // on the same UTC day, numbered from the first public day.
    public static class DailySite
    {
        public static readonly DateTime Epoch = new DateTime(2026, 10, 1, 0, 0, 0, DateTimeKind.Utc);

        public static int Number(DateTime utc) => Math.Max(1, (int)Math.Floor((utc.Date - Epoch).TotalDays) + 1);

        public static int Seed(DateTime utc)
        {
            unchecked
            {
                var h = 2166136261u;
                foreach (var ch in utc.ToString("yyyyMMdd", CultureInfo.InvariantCulture)) { h ^= ch; h *= 16777619u; }
                return (int)(h & 0x7fffffff);
            }
        }

        public static int Episode(DateTime utc, IReadOnlyList<int> playable)
        {
            if (playable == null || playable.Count == 0) return 1;
            return playable[(Number(utc) - 1) % playable.Count];
        }
    }

    // Wordle-style result: a grid that shows how the round went without naming a single hazard (no spoilers).
    public static class ShareCard
    {
        public static string Emoji(ArcadeRules.Cell c) => c switch
        {
            ArcadeRules.Cell.Early => "\U0001F7E9",      // green square: found early, unaided
            ArcadeRules.Cell.Late => "\U0001F7E8",       // yellow: found late or with a hint
            ArcadeRules.Cell.Incident => "\U0001F7E5",   // red: it turned into a near miss / injury
            _ => "⬛",                               // black: missed
        };

        public static string Time(float realSeconds)
        {
            var s = (int)Math.Round(Math.Max(0f, realSeconds));
            return $"{s / 60}:{s % 60:00}";
        }

        // daily = 0 for a practice round; replay = the daily was already played once (the first attempt is the record).
        public static string Text(ArcadeRules.Result r, int daily, string topic, float realSecondsUsed, string url, bool replay = false)
        {
            var sb = new StringBuilder();
            sb.Append("Competent Person · ").Append(daily > 0 ? "Daily Site #" + daily : "Practice");
            if (!string.IsNullOrEmpty(topic)) sb.Append(" · ").Append(topic);
            if (replay) sb.Append(" (replay)");
            sb.Append('\n');
            foreach (var c in r.Cells) sb.Append(Emoji(c));
            sb.Append("  ").Append(r.Found).Append('/').Append(r.Total).Append(" hazards · ").Append(r.Grade).Append('\n');
            sb.Append(r.Score.ToString("N0", CultureInfo.InvariantCulture)).Append(" pts · ")
              .Append(r.FalseAlarms).Append(r.FalseAlarms == 1 ? " false alarm" : " false alarms")
              .Append(" · ").Append(Time(realSecondsUsed)).Append('\n');
            if (!string.IsNullOrEmpty(url)) sb.Append(url);
            return sb.ToString().TrimEnd('\n');
        }
    }
}
