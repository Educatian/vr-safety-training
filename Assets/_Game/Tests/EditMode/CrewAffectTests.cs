using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Bounded affect (area 6): values stay in range under any event sequence, steps saturate near the bounds,
    // state relaxes over time, and the session's events move the right people the right way.
    public sealed class CrewAffectTests
    {
        [Test]
        public void StaysBounded_AndSaturates()
        {
            var a = new CrewAffect();
            for (var i = 0; i < 100; i++) a.Nudge("crew", 0.35f, 0.35f);
            Assert.That(a.Trust("crew"), Is.LessThanOrEqualTo(1f)); Assert.That(a.Stress("crew"), Is.LessThanOrEqualTo(1f));
            var before = a.Trust("crew"); a.Nudge("crew", 0.35f, 0f);
            Assert.That(a.Trust("crew") - before, Is.LessThan(0.01f), "near the bound a step barely moves");
            for (var i = 0; i < 100; i++) a.Nudge("crew", -5f, -5f);
            Assert.That(a.Trust("crew"), Is.GreaterThanOrEqualTo(-1f)); Assert.That(a.Stress("crew"), Is.GreaterThanOrEqualTo(0f));
        }

        [Test]
        public void SingleEvent_IsCapped()
        {
            var a = new CrewAffect();
            a.Nudge("crew", 10f, 10f);
            Assert.That(a.Trust("crew"), Is.LessThanOrEqualTo(CrewAffect.MaxStep + 1e-5f));
            Assert.That(a.Stress("crew"), Is.LessThanOrEqualTo(CrewAffect.MaxStep + 1e-5f));
        }

        [Test]
        public void RelaxesTowardBaseline()
        {
            var a = new CrewAffect();
            a.Nudge("ray", 0f, 0.35f); var s0 = a.Stress("ray");
            a.Tick(10f);
            Assert.That(a.Stress("ray"), Is.LessThan(s0));
            a.Tick(1000f);
            Assert.That(a.Stress("ray"), Is.EqualTo(0f).Within(1e-5)); Assert.That(a.Trust("ray"), Is.EqualTo(-0.1f).Within(1e-5));
        }

        [Test]
        public void AggressiveSpeakUp_MakesTheForemanHostile_AssertiveKeepsHimOnSide()
        {
            DaySession Run(SpeakUpStyle style)
            {
                var d = new DaySession(new[] { new HazardSpec("swing", true, EnergySource.Motion, FocusFour.StruckBy, CpArea.StruckBy, 4, 5, ControlLevel.Engineering, requiresStopWork: true) });
                d.Report("swing", EnergySource.Motion, 4, 5); d.StopWork("swing"); d.SpeakUp("swing", style);
                return d;
            }
            var aggressive = Run(SpeakUpStyle.Aggressive).Affect; var assertive = Run(SpeakUpStyle.Assertive).Affect;
            Assert.That(aggressive.Trust(CrewAffect.Foreman), Is.LessThan(assertive.Trust(CrewAffect.Foreman)));
            Assert.That(aggressive.BandOf(CrewAffect.Foreman), Is.EqualTo(CrewAffect.Band.Hostile));
            Assert.That(assertive.BandOf(CrewAffect.Foreman) != CrewAffect.Band.Hostile, "assertive keeps the foreman on side");
        }
    }
}
