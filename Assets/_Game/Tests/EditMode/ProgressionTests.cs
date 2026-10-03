using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    public sealed class ProgressionTests
    {
        static readonly HazardSpec Edge = new HazardSpec("edge", true, EnergySource.Gravity, FocusFour.Falls,
            CpArea.FallProtection, 4, 5, ControlLevel.Engineering);

        [Test]
        public void FullCredit_ForFindTagRiskAndEngineeredFix()
        {
            var ev = new HazardEvidence { Detected = true, TagCorrect = true, RiskDeviation = 1, AppliedControl = ControlLevel.Engineering };
            Assert.That(XpRules.HazardXp(Edge, ev), Is.EqualTo(100 + 25 + 25 + 40 + 20));
        }

        [Test]
        public void HintedFind_EarnsHalfDetectXp()
        {
            var ev = new HazardEvidence { Detected = true, Hinted = true };
            Assert.That(XpRules.HazardXp(Edge, ev), Is.EqualTo(50 + 25)); // half detect + risk within range (dev 0)
        }

        [Test]
        public void StopWork_OnlyEverAddsXp()
        {
            var without = new HazardEvidence { Detected = true, AppliedControl = ControlLevel.Ppe };
            var with = new HazardEvidence { Detected = true, AppliedControl = ControlLevel.Ppe, StopWorkCalled = true };
            Assert.That(XpRules.HazardXp(Edge, with) - XpRules.HazardXp(Edge, without), Is.EqualTo(XpRules.JustifiedStop));
        }

        [Test]
        public void WeakOrLapsedControl_GetsNoControlXp()
        {
            var ppe = new HazardEvidence { Detected = true, RiskDeviation = 8, AppliedControl = ControlLevel.Ppe };
            var lapsed = new HazardEvidence { Detected = true, RiskDeviation = 8, AppliedControl = ControlLevel.Engineering, Lapses = 1 };
            Assert.That(XpRules.HazardXp(Edge, ppe), Is.EqualTo(100));
            Assert.That(XpRules.HazardXp(Edge, lapsed), Is.EqualTo(100));
        }

        [Test]
        public void Levels_FollowThresholds()
        {
            Assert.That(XpRules.Level(0), Is.EqualTo(1));
            Assert.That(XpRules.Level(399), Is.EqualTo(1));
            Assert.That(XpRules.Level(400), Is.EqualTo(2));
            Assert.That(XpRules.Level(2200), Is.EqualTo(4));
            Assert.That(XpRules.Level(99999), Is.EqualTo(4));
        }

        [Test]
        public void HintBank_CannotGoNegative()
        {
            var bank = new HintBank(1);
            Assert.That(bank.TrySpend(), Is.True);
            Assert.That(bank.TrySpend(), Is.False);
            bank.Earn();
            Assert.That(bank.Tokens, Is.EqualTo(1));
        }

        [Test]
        public void HintedHazard_CountsHalfTowardMastery()
        {
            var clean = new HazardEvidence { Detected = true, TagCorrect = true, AppliedControl = ControlLevel.Engineering };
            var hinted = new HazardEvidence { Detected = true, TagCorrect = true, AppliedControl = ControlLevel.Engineering, Hinted = true };
            Assert.That(DaySession.HazardScore(Edge, hinted), Is.EqualTo(DaySession.HazardScore(Edge, clean) * 0.5f).Within(1e-5));
        }

        [Test]
        public void Streak_AwardsBonusEveryThreeCleanReports_AndResetsOnLookAlike()
        {
            var day = new DaySession(new[]
            {
                new HazardSpec("a", true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 3, 3, ControlLevel.Engineering),
                new HazardSpec("b", true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 3, 3, ControlLevel.Engineering),
                new HazardSpec("c", true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 3, 3, ControlLevel.Engineering),
                new HazardSpec("x", false, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 1, 1, ControlLevel.Engineering),
            });
            day.Report("x", EnergySource.Gravity, 1, 1);
            day.Report("a", EnergySource.Gravity, 3, 3);
            day.Report("b", EnergySource.Gravity, 3, 3);
            Assert.That(day.StreakBonuses, Is.Zero);
            day.Report("c", EnergySource.Gravity, 3, 3);
            Assert.That(day.StreakBonuses, Is.EqualTo(1));
        }

        [Test]
        public void ZeroRecordablesBadge_NeedsTheFullShift()
        {
            var early = new DaySession(new[] { Edge });
            early.Advance(30f);                         // ended at minute 0.5, before any incident window
            Assert.That(Badges.Earned(early, new[] { Edge }, 600f), Has.No.Member(Badge.ZeroRecordablesDay));
            var full = new DaySession(new[] { Edge });
            full.Report("edge", EnergySource.Gravity, 4, 5);
            full.ChooseControl("edge", ControlLevel.Engineering);
            full.CompleteInstall("edge", true);
            full.Advance(600f);
            Assert.That(Badges.Earned(full, new[] { Edge }, 600f), Does.Contain(Badge.ZeroRecordablesDay));
        }

        [Test]
        public void NeutralNames_HideTheDiagnosis_AndReadingsGiveNoVerdict()
        {
            Assert.That(ConditionNames.NeutralName("wed-open-hole"), Is.EqualTo(ConditionNames.NeutralName("wed-covered-hole")));
            Assert.That(ConditionNames.NeutralName("fri-rebar-impalement"), Is.EqualTo(ConditionNames.NeutralName("fri-rebar-capped")));
            Assert.That(ConditionNames.NeutralName("unknown-id"), Is.EqualTo(ConditionNames.Fallback));
            foreach (var r in InstrumentTable.All)
                foreach (var verdict in new[] { "needs", "PEL", "Type A", "Type B", "Type C", "violation", "unsafe" })
                    Assert.That(r.Value, Does.Not.Contain(verdict), $"{r.Key}: a reading states a verdict");
            Assert.That(InstrumentTable.Get("wed-missing-midrail", GearId.LaserMeasure, true), Does.Contain("midrail"),
                "a condition showing its control reads its compliant value");
        }

        [Test]
        public void UseHint_MarksTheNextFindAsHinted()
        {
            var day = new DaySession(new[] { Edge });
            Assert.That(day.UseHint("edge"), Is.EqualTo(1));
            Assert.That(day.UseHint("edge"), Is.EqualTo(2));
            day.Report("edge", EnergySource.Gravity, 4, 5);
            Assert.That(day.GetEvidence("edge").Hinted, Is.True);
            Assert.That(day.UseHint("edge"), Is.Zero); // already found: no hint to give
        }
    }
}
