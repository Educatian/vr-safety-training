using System.Collections.Generic;
using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Toolbox talk, speak-up, debrief grouping, mastery gate, story verdict, control kits (design review 2026-09-29).
    public sealed class ReflectionTests
    {
        static HazardSpec H(string id, int p, int s, ControlLevel best = ControlLevel.Engineering, bool stop = false, float trigger = float.PositiveInfinity) =>
            new HazardSpec(id, true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, p, s, best, triggerAtSeconds: trigger, requiresStopWork: stop);

        [Test]
        public void ToolboxTalk_RewardsPickingAndLeadingWithTheHighestRisk()
        {
            var hole = H("hole", 4, 5); var rail = H("rail", 3, 5); var ladder = H("ladder", 3, 4); var cord = H("cord", 2, 2);
            var found = new List<HazardSpec> { hole, rail, ladder, cord };
            var best = new List<HazardSpec> { hole, rail, ladder };
            Assert.That(ToolboxTalk.SelectionScore(best, found), Is.EqualTo(1f));
            Assert.That(ToolboxTalk.OrderScore(best), Is.EqualTo(1f));
            Assert.That(ToolboxTalk.LeadsWithTopRisk(best, found), Is.True);
            Assert.That(ToolboxTalk.Score(best, found, true), Is.EqualTo(1f).Within(1e-5));

            var weak = new List<HazardSpec> { cord, ladder, hole };
            Assert.That(ToolboxTalk.LeadsWithTopRisk(weak, found), Is.False);
            Assert.That(ToolboxTalk.OrderScore(weak), Is.EqualTo(0f));
            Assert.That(ToolboxTalk.SelectionScore(weak, found), Is.EqualTo(2f / 3f).Within(1e-5));
            Assert.That(ToolboxTalk.Score(weak, found, false), Is.LessThan(ToolboxTalk.GoodTalk));
        }

        [Test]
        public void ToolboxWhy_ExistsForEveryEpisode_AndIsNotTheSameAnswerEachTime()
        {
            var items = Enumerable.Range(1, 5).Select(ToolboxTalk.Why).ToList();
            Assert.That(items.Select(i => i.Id).Distinct().Count(), Is.EqualTo(5));
            Assert.That(items.Select(i => i.Options[i.Correct]).Distinct().Count(), Is.EqualTo(5));
            Assert.That(items.Select(i => i.Correct).Distinct().Count(), Is.GreaterThan(1), "authored key position varies");
        }

        [Test]
        public void SpeakUp_PassiveGivesTheCrewBack_AssertiveHoldsAndBuildsTrust()
        {
            var specs = new[] { H("swing", 4, 5, stop: true, trigger: 400f) };
            var passive = new DaySession(specs); passive.Report("swing", EnergySource.Gravity, 4, 5); passive.StopWork("swing");
            Assert.That(passive.SpeakUp("swing", SpeakUpStyle.Passive), Is.True);
            Assert.That(passive.GetState("swing"), Is.EqualTo(HazardState.Reported), "backing down restarts the crew");
            Assert.That(passive.SpeakUp("swing", SpeakUpStyle.Assertive), Is.False, "one exchange per hazard");

            var assertive = new DaySession(specs); assertive.Report("swing", EnergySource.Gravity, 4, 5); assertive.StopWork("swing");
            var trust = assertive.CrewTrust;
            assertive.SpeakUp("swing", SpeakUpStyle.Assertive);
            Assert.That(assertive.GetState("swing"), Is.EqualTo(HazardState.Stopped));
            Assert.That(assertive.CrewTrust, Is.EqualTo(trust + 1));

            var aggressive = new DaySession(specs); aggressive.Report("swing", EnergySource.Gravity, 4, 5); aggressive.StopWork("swing");
            aggressive.SpeakUp("swing", SpeakUpStyle.Aggressive);
            Assert.That(aggressive.GetState("swing"), Is.EqualTo(HazardState.Stopped));
            Assert.That(aggressive.CrewTrust, Is.EqualTo(trust - 1));

            Assert.That(DaySession.HazardScore(specs[0], passive.GetEvidence("swing")),
                Is.LessThan(DaySession.HazardScore(specs[0], assertive.GetEvidence("swing"))), "backing down forfeits escalation evidence");
            Assert.That(SpeakUp.Options(7).Select(o => o.Style).Distinct().Count(), Is.EqualTo(3));
        }

        [Test]
        public void Elimination_IsImmediate_AndOnlyWhereFeasible()
        {
            var cord = new HazardSpec("mon-damaged-cord", true, EnergySource.Electrical, FocusFour.Electrocution, CpArea.Electrical, 3, 4,
                ControlKits.BestFeasible("mon-damaged-cord", ControlLevel.Engineering));
            Assert.That(cord.BestFeasibleControl, Is.EqualTo(ControlLevel.Elimination));
            var day = new DaySession(new[] { cord, H("rail", 3, 5) });
            day.Report("mon-damaged-cord", EnergySource.Electrical, 3, 4);
            Assert.That(day.ChooseControl("mon-damaged-cord", ControlLevel.Elimination), Is.EqualTo(ControlOutcome.Eliminated));
            Assert.That(day.GetState("mon-damaged-cord"), Is.EqualTo(HazardState.Controlled));
            Assert.That(day.GetEvidence("mon-damaged-cord").AppliedControl, Is.EqualTo(ControlLevel.Elimination));
            day.Report("rail", EnergySource.Gravity, 3, 5);
            Assert.That(day.ChooseControl("rail", ControlLevel.Elimination), Is.EqualTo(ControlOutcome.NotFeasible));
        }

        [Test]
        public void ControlKits_ShuffleButKeepTheKey()
        {
            var a = ControlKits.Options("wed-open-hole", 3, out var ka);
            var b = ControlKits.Options("wed-open-hole", 11, out var kb);
            Assert.That(a.Count, Is.EqualTo(3));
            Assert.That(a[ka], Is.EqualTo(ControlKits.Get("wed-open-hole").Correct));
            Assert.That(b[kb], Is.EqualTo(ControlKits.Get("wed-open-hole").Correct));
            Assert.That(ControlKits.Options("no-such-id", 1, out var none).Count, Is.Zero);
            Assert.That(none, Is.EqualTo(-1));
        }

        [Test]
        public void Debrief_ComparesChosenAndBestControl()
        {
            var edge = H("edge", 4, 5);
            var day = new DaySession(new[] { edge, H("hole", 4, 5), H("gap", 3, 3) });
            day.Report("edge", EnergySource.Gravity, 4, 5); day.ChooseControl("edge", ControlLevel.Engineering); day.CompleteInstall("edge", true);
            day.Report("hole", EnergySource.Gravity, 4, 5); day.ChooseControl("hole", ControlLevel.Ppe);
            Assert.That(Debrief.Group(edge, day.GetEvidence("edge"), day.GetState("edge")), Is.EqualTo(DebriefGroup.ControlledAtBest));
            Assert.That(Debrief.Group(H("hole", 4, 5), day.GetEvidence("hole"), day.GetState("hole")), Is.EqualTo(DebriefGroup.ControlledBelowBest));
            Assert.That(Debrief.Group(H("gap", 3, 3), day.GetEvidence("gap"), day.GetState("gap")), Is.EqualTo(DebriefGroup.Missed));
            Assert.That(Debrief.WhatAlmostHappened("wed-open-hole"), Does.Not.Contain("blood"));
        }

        [Test]
        public void MasteryGate_KeepsBestPerArea_AndListsWhatIsMissing()
        {
            var best = new Dictionary<CpArea, float> { { CpArea.Electrical, 0.9f }, { CpArea.Excavation, 0.4f } };
            var merged = MasteryGate.Merge(best, new Dictionary<CpArea, float> { { CpArea.Excavation, 0.8f }, { CpArea.Electrical, 0.2f } });
            Assert.That(merged[CpArea.Electrical], Is.EqualTo(0.9f).Within(1e-5), "a weak replay never lowers the best");
            Assert.That(merged[CpArea.Excavation], Is.EqualTo(0.8f).Within(1e-5));
            var missing = MasteryGate.Missing(merged);
            Assert.That(missing, Does.Contain(CpArea.FallProtection));
            Assert.That(missing, Does.Contain(CpArea.StruckBy));
            Assert.That(missing, Has.No.Member(CpArea.Electrical));
        }

        [Test]
        public void ShiftVerdict_CapstoneEndingFollowsPlay()
        {
            var specs = new[] { H("boom", 4, 5, stop: true, trigger: 330f), H("rebar", 3, 4) };
            var clean = new DaySession(specs);
            clean.Report("boom", EnergySource.Electrical, 4, 5); clean.StopWork("boom"); clean.SpeakUp("boom", SpeakUpStyle.Assertive);
            clean.Report("rebar", EnergySource.Gravity, 3, 4);
            Assert.That(ShiftVerdict.Clean(clean, specs), Is.True);

            var caved = new DaySession(specs);
            caved.Report("boom", EnergySource.Electrical, 4, 5); caved.StopWork("boom"); caved.SpeakUp("boom", SpeakUpStyle.Passive);
            caved.Report("rebar", EnergySource.Gravity, 3, 4);
            Assert.That(ShiftVerdict.Clean(caved, specs), Is.False);

            var ep5 = Episodes.Get(5).Epilogue;
            Assert.That(ep5.Any(l => l.Gate == LineGate.CleanShift) && ep5.Any(l => l.Gate == LineGate.RoughShift), Is.True);
            Assert.That(ShiftVerdict.Plays(LineGate.RoughShift, false), Is.True);
            Assert.That(ShiftVerdict.Plays(LineGate.CleanShift, false), Is.False);
        }

        [Test]
        public void OnScheduleBadge_ForfeitedByLongStops_NeverTheRating()
        {
            var edge = H("edge", 4, 5);
            var quick = new DaySession(new[] { edge });
            quick.Advance(600f);
            Assert.That(Badges.Earned(quick, new[] { edge }, 600f), Does.Contain(Badge.OnSchedule));

            var stalled = new DaySession(new[] { edge });
            stalled.Report("edge", EnergySource.Gravity, 4, 5);
            stalled.StopWork("edge"); stalled.Advance(121f);
            stalled.StopWork("edge"); stalled.Advance(121f);
            stalled.Advance(400f);
            Assert.That(stalled.ScheduleSlipMinutes, Is.GreaterThan(DaySession.OnScheduleSlipMinutes));
            Assert.That(Badges.Earned(stalled, new[] { edge }, 600f), Has.No.Member(Badge.OnSchedule));
        }
    }
}
