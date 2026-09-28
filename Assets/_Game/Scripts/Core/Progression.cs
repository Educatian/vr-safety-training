using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    public enum Badge { StoppedTheLine, ZeroRecordablesDay, HierarchyHawk }

    // GDD §14. XP comes only from DaySession evidence; stop-work never costs XP.
    // All numbers are starting values (playtest-tuned), kept here so tests pin them.
    public static class XpRules
    {
        public const int Detect = 100, Tag = 25, RiskClose = 25, BestControl = 40, EngineeredBonus = 20,
            JustifiedStop = 50, StreakBonus = 50, StreakLength = 3;

        public static readonly int[] LevelThresholds = { 0, 400, 1100, 2200 }; // L1 Trainee .. L4 Competent Person

        public static int HazardXp(HazardSpec spec, HazardEvidence ev)
        {
            if (!ev.Detected || !spec.IsHazard) return 0;
            var detect = ev.Hinted ? Detect / 2 : Detect;
            var xp = detect;
            if (ev.TagCorrect) xp += Tag;
            if (ev.RiskDeviation <= 2) xp += RiskClose; // within +/-1 on each axis
            if (ev.AppliedControl.HasValue && !ev.BecameIncident && ev.Lapses == 0 &&
                ev.AppliedControl.Value <= spec.BestFeasibleControl)
            {
                xp += BestControl;
                if (ev.AppliedControl.Value <= ControlLevel.Engineering) xp += EngineeredBonus;
            }
            if (ev.StopWorkCalled) xp += JustifiedStop;
            return xp;
        }

        public static int Level(int totalXp)
        {
            var level = 1;
            for (var i = 0; i < LevelThresholds.Length; i++)
                if (totalXp >= LevelThresholds[i]) level = i + 1;
            return level;
        }
    }

    // Hint economy (GDD §14): earn tokens from streaks / explain-back, spend on 3 tiers per hazard.
    public sealed class HintBank
    {
        public HintBank(int startingTokens = 0) => Tokens = Math.Max(0, startingTokens);

        public int Tokens { get; private set; }
        public void Earn(int n = 1) => Tokens += Math.Max(0, n);

        public bool TrySpend()
        {
            if (Tokens <= 0) return false;
            Tokens--;
            return true;
        }
    }

    public static class Badges
    {
        public static IReadOnlyList<Badge> Earned(DaySession day, IEnumerable<HazardSpec> specs)
        {
            var list = new List<Badge>();
            var real = specs.Where(s => s.IsHazard).ToList();
            if (real.Any(s => day.GetEvidence(s.Id).StopWorkCalled)) list.Add(Badge.StoppedTheLine);
            if (day.Recordables == 0 && day.Clock > 0) list.Add(Badge.ZeroRecordablesDay);
            var engineered = real.Count(s =>
            {
                var ev = day.GetEvidence(s.Id);
                return ev.AppliedControl.HasValue && ev.AppliedControl.Value <= ControlLevel.Engineering &&
                       day.GetState(s.Id) == HazardState.Controlled;
            });
            if (engineered >= 5) list.Add(Badge.HierarchyHawk);
            return list;
        }
    }
}
