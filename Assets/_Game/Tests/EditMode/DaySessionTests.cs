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
        public void Constructor_RejectsDuplicatesAndHazardFreeDays()
        {
            var lookAlike = new HazardSpec("x", false, EnergySource.Gravity, FocusFour.None, CpArea.General, 1, 1, ControlLevel.Engineering);
            Assert.Throws<ArgumentException>(() => new DaySession(new[] { lookAlike }));
            Assert.Throws<ArgumentException>(() => new DaySession(new[] { lookAlike, lookAlike }));
        }
    }
}
