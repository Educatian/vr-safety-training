using System;
using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    public sealed class DaySessionTests
    {
        // edge: fall, best = engineering (guardrail), high severity, triggers at 300 s
        // harness: tie-off where PPE is the correct control
        // swing: struck-by requiring stop-work
        // rail-ok: compliant look-alike
        static DaySession NewDay() => new DaySession(new[]
        {
            new HazardSpec("edge", true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 4, 5,
                ControlLevel.Engineering, triggerAtSeconds: 300f, lapseAfterSeconds: 90f),
            new HazardSpec("harness", true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 3, 5,
                ControlLevel.Ppe, triggerAtSeconds: 400f),
            new HazardSpec("swing", true, EnergySource.Motion, FocusFour.StruckBy, CpArea.StruckBy, 3, 3,
                ControlLevel.Engineering, triggerAtSeconds: 200f, requiresStopWork: true),
            new HazardSpec("rail-ok", false, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 1, 1,
                ControlLevel.Engineering),
        });

        [Test]
        public void Report_RecordsDetectionTagAndRiskDeviation()
        {
            var day = NewDay();
            day.Advance(12f);

            var outcome = day.Report("edge", EnergySource.Gravity, 3, 5);

            var ev = day.GetEvidence("edge");
            Assert.That(outcome, Is.EqualTo(ReportOutcome.Reported));
            Assert.That(ev.Detected && ev.TagCorrect, Is.True);
            Assert.That(ev.RiskDeviation, Is.EqualTo(1));
            Assert.That(ev.DetectedAtSeconds, Is.EqualTo(12f));
            Assert.That(day.HazardIdentificationIndex, Is.EqualTo(1f / 3f).Within(1e-5));
        }

        [Test]
        public void EngineeredControl_PersistsAndPreventsIncident()
        {
            var day = NewDay();
            day.Report("edge", EnergySource.Gravity, 4, 5);

            Assert.That(day.ChooseControl("edge", ControlLevel.Engineering), Is.EqualTo(ControlOutcome.Installing));
            Assert.That(day.CompleteInstall("edge", false), Is.False);
            Assert.That(day.CompleteInstall("edge", true), Is.True);
            day.Advance(1000f);

            Assert.That(day.GetState("edge"), Is.EqualTo(HazardState.Controlled));
            Assert.That(day.GetEvidence("edge").InstallAttempts, Is.EqualTo(2));
            Assert.That(day.GetEvidence("edge").BecameIncident, Is.False);
        }

        [Test]
        public void WeakControl_LapsesThenBecomesIncidentIfNotReReported()
        {
            var day = NewDay();
            day.Report("edge", EnergySource.Gravity, 4, 5);
            Assert.That(day.ChooseControl("edge", ControlLevel.Ppe), Is.EqualTo(ControlOutcome.Assigned));

            var lapse = day.Advance(100f);
            Assert.That(lapse.Single(e => e.HazardId == "edge").Kind, Is.EqualTo(DayEventKind.Lapsed));
            Assert.That(day.GetState("edge"), Is.EqualTo(HazardState.Lapsed));

            var later = day.Advance(250f);
            Assert.That(later.Any(e => e.HazardId == "edge" && e.Kind == DayEventKind.Recordable), Is.True);
            Assert.That(day.Recordables, Is.EqualTo(1));
        }

        [Test]
        public void PpeWhenItIsTheBestControl_DoesNotLapse()
        {
            var day = NewDay();
            day.Report("harness", EnergySource.Gravity, 3, 5);
            day.ChooseControl("harness", ControlLevel.Ppe);

            day.Advance(1000f);

            Assert.That(day.GetState("harness"), Is.EqualTo(HazardState.Controlled));
            Assert.That(DaySession.HazardScore(
                new HazardSpec("harness", true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 3, 5, ControlLevel.Ppe),
                day.GetEvidence("harness")), Is.EqualTo(1f).Within(1e-5));
        }

        [Test]
        public void ControlAboveFeasibleLevel_IsRejected()
        {
            var day = NewDay();
            day.Report("harness", EnergySource.Gravity, 3, 5);

            Assert.That(day.ChooseControl("harness", ControlLevel.Engineering), Is.EqualTo(ControlOutcome.NotFeasible));
        }

        [Test]
        public void ControlBeforeReport_IsInvalid()
        {
            Assert.That(NewDay().ChooseControl("edge", ControlLevel.Engineering), Is.EqualTo(ControlOutcome.InvalidState));
        }

        [Test]
        public void StopWork_PreventsIncidentAndRaisesTrust()
        {
            var day = NewDay();
            day.Report("swing", EnergySource.Motion, 3, 3);

            Assert.That(day.StopWork("swing"), Is.EqualTo(StopOutcome.Justified));
            day.Advance(100f);

            Assert.That(day.GetState("swing"), Is.EqualTo(HazardState.Stopped));
            Assert.That(day.NearMisses, Is.Zero);
            Assert.That(day.CrewTrust, Is.EqualTo(1));

            // The hold runs out: the crew restarts, exposure resumes until a real control goes in.
            var events = day.Advance(40f);
            Assert.That(events.Any(e => e.Kind == DayEventKind.StopLifted), Is.True);
            Assert.That(day.GetState("swing"), Is.EqualTo(HazardState.Reported));
            Assert.That(day.StoppedSeconds, Is.EqualTo(DaySession.StopHoldSeconds).Within(0.01f));
        }

        [Test]
        public void StopWork_RequiresANamedReport_AndNeverLowersRating()
        {
            var day = NewDay();
            Assert.That(day.StopWork("swing"), Is.EqualTo(StopOutcome.InvalidState));

            var trustBefore = day.CrewTrust;
            Assert.That(day.StopWork("rail-ok"), Is.EqualTo(StopOutcome.Unjustified));
            Assert.That(day.CrewTrust, Is.EqualTo(trustBefore));
        }

        [Test]
        public void MissingRequiredStopWork_CostsEscalationEvidenceOnly()
        {
            var spec = new HazardSpec("swing", true, EnergySource.Motion, FocusFour.StruckBy, CpArea.StruckBy, 3, 3,
                ControlLevel.Engineering, requiresStopWork: true);
            var withoutStop = new HazardEvidence { Detected = true, TagCorrect = true, AppliedControl = ControlLevel.Engineering };
            var withStop = new HazardEvidence { Detected = true, TagCorrect = true, AppliedControl = ControlLevel.Engineering, StopWorkCalled = true };

            Assert.That(DaySession.HazardScore(spec, withStop) - DaySession.HazardScore(spec, withoutStop),
                Is.EqualTo(0.10f).Within(1e-5));
        }

        [Test]
        public void LookAlike_CostsPrecisionAndTrustOnlyOnRepeat()
        {
            var day = NewDay();

            Assert.That(day.Report("rail-ok", EnergySource.Gravity, 2, 2), Is.EqualTo(ReportOutcome.FalseReport));
            Assert.That(day.CrewTrust, Is.Zero);
            day.Report("rail-ok", EnergySource.Gravity, 2, 2);

            Assert.That(day.CrewTrust, Is.EqualTo(-1));
            Assert.That(day.ReportPrecision, Is.Zero);
            Assert.That(day.HazardIdentificationIndex, Is.Zero);
        }

        [Test]
        public void UnreportedHazard_BecomesIncidentAtTriggerTime()
        {
            var day = NewDay();

            var events = day.Advance(210f);

            Assert.That(events.Single().Kind, Is.EqualTo(DayEventKind.NearMiss)); // swing: severity 3
            Assert.That(day.Report("swing", EnergySource.Motion, 3, 3), Is.EqualTo(ReportOutcome.NotReportable));
        }

        [Test]
        public void PausedClock_FreezesTimers()
        {
            var day = NewDay();
            day.Paused = true;

            day.Advance(10_000f);

            Assert.That(day.Clock, Is.Zero);
            Assert.That(day.NearMisses + day.Recordables, Is.Zero);
        }

        [Test]
        public void Mastery_ReachesCompetentOnlyWithGoodControl()
        {
            var day = NewDay();
            day.Report("edge", EnergySource.Gravity, 4, 5);
            day.ChooseControl("edge", ControlLevel.Engineering);
            day.CompleteInstall("edge", true);
            day.Report("harness", EnergySource.Gravity, 3, 5);
            day.ChooseControl("harness", ControlLevel.Ppe);

            Assert.That(day.Mastery()[CpArea.FallProtection], Is.GreaterThanOrEqualTo(DaySession.CompetentThreshold));
            Assert.That(day.Mastery()[CpArea.StruckBy], Is.Zero);
        }

        [Test]
        public void Sampler_IsDeterministicPerSeed_AndRespectsCounts()
        {
            var pool = Enumerable.Range(0, 6).Select(i => new HazardSpec($"h{i}", true, EnergySource.Gravity,
                    FocusFour.Falls, CpArea.FallProtection, 3, 3, ControlLevel.Engineering))
                .Concat(Enumerable.Range(0, 4).Select(i => new HazardSpec($"l{i}", false, EnergySource.Gravity,
                    FocusFour.Falls, CpArea.FallProtection, 1, 1, ControlLevel.Engineering)))
                .ToList();

            var a = HazardPoolSampler.Sample(pool, 3, 2, seed: 7).Select(h => h.Id).ToList();
            var b = HazardPoolSampler.Sample(pool, 3, 2, seed: 7).Select(h => h.Id).ToList();

            Assert.That(a, Is.EqualTo(b));
            Assert.That(a.Count(id => id.StartsWith("h")), Is.EqualTo(3));
            Assert.That(a.Count(id => id.StartsWith("l")), Is.EqualTo(2));
        }

        [Test]
        public void SecondStopOnSameHazard_HoldsTheCrew_ButEarnsNothingNew()
        {
            var day = NewDay();
            day.Report("edge", EnergySource.Gravity, 4, 5);
            Assert.That(day.StopWork("edge"), Is.EqualTo(StopOutcome.Justified));
            Assert.That(day.CrewTrust, Is.EqualTo(1));
            day.Advance(DaySession.StopHoldSeconds + 1f);   // Ray restarts the crew
            Assert.That(day.GetState("edge"), Is.EqualTo(HazardState.Reported));
            Assert.That(day.StopWork("edge"), Is.EqualTo(StopOutcome.Repeated));
            Assert.That(day.GetState("edge"), Is.EqualTo(HazardState.Stopped), "a repeat stop still holds the crew");
            Assert.That(day.CrewTrust, Is.EqualTo(1), "re-stopping earns no trust");
            Assert.That(day.StoppedSeconds, Is.GreaterThan(DaySession.StopHoldSeconds - 1f), "stops cost schedule");
        }

        [Test]
        public void EngineeredControl_CountsOnlyOnceInstalled()
        {
            var day = NewDay();
            day.Report("edge", EnergySource.Gravity, 4, 5);
            day.ChooseControl("edge", ControlLevel.Engineering);
            Assert.That(day.GetEvidence("edge").AppliedControl, Is.Null, "choosing a fix is not the fix");
            var chosenOnly = day.Mastery()[CpArea.FallProtection];
            day.CompleteInstall("edge", true);
            Assert.That(day.GetEvidence("edge").AppliedControl, Is.EqualTo(ControlLevel.Engineering));
            Assert.That(day.Mastery()[CpArea.FallProtection], Is.GreaterThan(chosenOnly));
        }

        [Test]
        public void StopMidInstall_ThenTheKitArrives_StillInstalls()
        {
            var day = NewDay();
            day.Report("edge", EnergySource.Gravity, 4, 5);
            day.ChooseControl("edge", ControlLevel.Engineering);
            Assert.That(day.StopWork("edge"), Is.EqualTo(StopOutcome.Justified));
            Assert.That(day.CompleteInstall("edge", true), Is.True);
            Assert.That(day.GetState("edge"), Is.EqualTo(HazardState.Controlled));

            var d2 = NewDay();
            d2.Report("edge", EnergySource.Gravity, 4, 5);
            d2.ChooseControl("edge", ControlLevel.Engineering);
            d2.StopWork("edge");
            d2.Advance(DaySession.StopHoldSeconds + 1f);
            Assert.That(d2.GetState("edge"), Is.EqualTo(HazardState.Installing), "stop lifts back to the install in progress");
        }

        [Test]
        public void CuedFind_CountsHalfForMastery()
        {
            var clean = NewDay(); var cued = NewDay();
            cued.MarkCued("edge");
            foreach (var d in new[] { clean, cued }) { d.Report("edge", EnergySource.Gravity, 4, 5); d.ChooseControl("edge", ControlLevel.Engineering); d.CompleteInstall("edge", true); }
            Assert.That(cued.GetEvidence("edge").Cued, Is.True);
            Assert.That(DaySession.HazardScore(NewSpecEdge(), cued.GetEvidence("edge")),
                Is.EqualTo(DaySession.HazardScore(NewSpecEdge(), clean.GetEvidence("edge")) * 0.5f).Within(1e-5));
            Assert.That(XpRules.HazardXp(NewSpecEdge(), cued.GetEvidence("edge")), Is.EqualTo(XpRules.HazardXp(NewSpecEdge(), clean.GetEvidence("edge"))), "cues don't cost XP");
            clean.MarkCued("edge");
            Assert.That(clean.GetEvidence("edge").Cued, Is.False, "no effect once found");
        }

        static HazardSpec NewSpecEdge() => new HazardSpec("edge", true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 4, 5, ControlLevel.Engineering);

        [Test]
        public void ConfirmCompliant_OnLookAlike_IsPositiveEvidence_OnHazard_ItStaysLive()
        {
            var day = NewDay();
            Assert.That(day.ConfirmCompliant("rail-ok"), Is.EqualTo(ConfirmOutcome.Confirmed));
            Assert.That(day.ConfirmCompliant("rail-ok"), Is.EqualTo(ConfirmOutcome.AlreadyJudged));
            Assert.That(day.ConfirmedCompliant, Is.EqualTo(1));
            Assert.That(day.LoggedCompliant("rail-ok"), Is.True);
            Assert.That(day.Revealed("rail-ok"), Is.False, "logging compliant gives no feedback");

            Assert.That(day.ConfirmCompliant("edge"), Is.EqualTo(ConfirmOutcome.DismissedHazard));
            Assert.That(day.GetState("edge"), Is.EqualTo(HazardState.Latent));
            Assert.That(day.Revealed("edge"), Is.False, "a wrong confirm must not reveal the hazard");
            Assert.That(day.GetEvidence("edge").DismissedAsCompliant, Is.True);
            Assert.That(day.Report("edge", EnergySource.Gravity, 4, 5), Is.EqualTo(ReportOutcome.Reported), "learner may change the call");
            Assert.That(day.FalseReports, Is.Zero);
        }

        [Test]
        public void Spend_RunsTheClockEvenWhilePaused()
        {
            var day = NewDay();
            day.Paused = true;
            Assert.That(day.Advance(50f), Is.Empty);
            Assert.That(day.Clock, Is.Zero);
            var events = day.Spend(250f);
            Assert.That(day.Clock, Is.EqualTo(250f).Within(1e-4));
            Assert.That(day.Paused, Is.True, "pause state restored");
            Assert.That(events.Any(e => e.HazardId == "swing"), Is.True, "incident timers fire during spent time");
        }

        [Test]
        public void Constructor_RejectsDuplicatesAndHazardFreeDays()
        {
            var lookAlike = new HazardSpec("x", false, EnergySource.Gravity, FocusFour.None, CpArea.General, 1, 1, ControlLevel.Engineering);
            Assert.Throws<ArgumentException>(() => new DaySession(new[] { lookAlike }));
            Assert.Throws<ArgumentException>(() => new DaySession(new[] { lookAlike, lookAlike }));
        }
    }
}
