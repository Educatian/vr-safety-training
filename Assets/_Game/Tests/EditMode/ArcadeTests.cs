using System;
using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Hazard Hunt scoring, daily site and the share card (docs/AwesomeAiGames_Plan.md).
    public sealed class ArcadeTests
    {
        static HazardSpec Spec(string id, ControlLevel best = ControlLevel.Engineering) =>
            new HazardSpec(id, true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 3, 4, best);

        static ArcadeRules.Hazard H(string id, Action<HazardEvidence> set, HazardState state = HazardState.Latent, ControlLevel best = ControlLevel.Engineering)
        {
            var ev = new HazardEvidence(); set(ev);
            return new ArcadeRules.Hazard(Spec(id, best), ev, state);
        }

        [Test]
        public void Score_RewardsEarlyUnaidedFinds_AndPunishesFalseAlarmsAndIncidents()
        {
            var hazards = new[]
            {
                H("d", e => { e.BecameIncident = true; }, HazardState.Incident),
                H("a", e => { e.Detected = true; e.DetectedAtSeconds = 60; e.TagCorrect = true; e.RiskDeviation = 1; e.AppliedControl = ControlLevel.Engineering; }, HazardState.Controlled),
                H("c", e => { }),
                H("b", e => { e.Detected = true; e.DetectedAtSeconds = 400; e.Hinted = true; e.RiskDeviation = 4; }, HazardState.Reported),
            };
            var r = ArcadeRules.Score(hazards, falseAlarms: 1, confirmed: 1, shiftLength: 600f);
            // a: 100 + 90 speed + 25 tag + 25 risk + 50 control + 50 best = 340; b: (100 + 33) / 2 = 66; d: -100; +50 confirm -50 false alarm.
            Assert.That(r.Score, Is.EqualTo(306));
            Assert.That(r.Found, Is.EqualTo(2)); Assert.That(r.Total, Is.EqualTo(4)); Assert.That(r.Incidents, Is.EqualTo(1));
            Assert.That(r.Cells, Is.EqualTo(new[] { ArcadeRules.Cell.Early, ArcadeRules.Cell.Late, ArcadeRules.Cell.Missed, ArcadeRules.Cell.Incident }), "ordered by hazard id");
            Assert.That(r.MaxScore, Is.EqualTo(4 * 350));
            Assert.That(r.Grade, Is.EqualTo("D"));
            Assert.That(r.ClearBonus, Is.EqualTo(0), "no time bonus unless everything was found");
        }

        [Test]
        public void ReportingEveryLookAlike_NeverPays()
        {
            var one = new[] { H("a", e => { e.Detected = true; e.DetectedAtSeconds = 30; e.TagCorrect = true; }) };
            var clean = ArcadeRules.Score(one, 0, 0, 600f).Score;
            Assert.That(ArcadeRules.Score(one, 3, 0, 600f).Score, Is.LessThan(clean));
            Assert.That(ArcadeRules.Score(one, 50, 0, 600f).Score, Is.EqualTo(0), "score floors at zero");
            Assert.That(ArcadeRules.Score(one, 0, 2, 600f).Score, Is.EqualTo(clean + 2 * ArcadeRules.Confirm), "confirming look-alikes pays");
        }

        [Test]
        public void Grade_S_NeedsACleanRound_AndClearBonusNeedsAllFound()
        {
            Assert.That(ArcadeRules.Grade(900, 1000, 0, 0), Is.EqualTo("S"));
            Assert.That(ArcadeRules.Grade(900, 1000, 1, 0), Is.EqualTo("A"));
            Assert.That(ArcadeRules.Grade(900, 1000, 0, 1), Is.EqualTo("A"));
            Assert.That(ArcadeRules.Grade(500, 1000, 0, 0), Is.EqualTo("B"));
            Assert.That(ArcadeRules.Grade(300, 1000, 0, 0), Is.EqualTo("C"));
            Assert.That(ArcadeRules.Grade(100, 1000, 0, 0), Is.EqualTo("D"));
            var all = new[] { H("a", e => { e.Detected = true; e.DetectedAtSeconds = 10; }), H("b", e => { e.Detected = true; e.DetectedAtSeconds = 20; }) };
            var r = ArcadeRules.Score(all, 0, 0, 600f, remainingRealSeconds: 40f);
            Assert.That(r.Cleared, Is.True);
            Assert.That(r.ClearBonus, Is.EqualTo(40 * ArcadeRules.ClearPerSecond));
            Assert.That(ArcadeRules.TimeScale(600f), Is.EqualTo(600f / ArcadeRules.RealSeconds).Within(1e-4));
        }

        [Test]
        public void DailySite_IsTheSameAllDay_AndRotatesThroughTheEpisodes()
        {
            var day1 = DailySite.Epoch;
            Assert.That(DailySite.Number(day1), Is.EqualTo(1));
            Assert.That(DailySite.Number(day1.AddDays(2).AddHours(23.9)), Is.EqualTo(3));
            Assert.That(DailySite.Number(day1.AddDays(-5)), Is.EqualTo(1), "never below #1");
            Assert.That(DailySite.Seed(day1.AddHours(1)), Is.EqualTo(DailySite.Seed(day1.AddHours(22))));
            Assert.That(DailySite.Seed(day1), Is.Not.EqualTo(DailySite.Seed(day1.AddDays(1))));
            var playable = new[] { 1, 2, 3, 4, 5 };
            Assert.That(Enumerable.Range(0, 5).Select(i => DailySite.Episode(day1.AddDays(i), playable)), Is.EqualTo(playable));
            Assert.That(DailySite.Episode(day1.AddDays(5), playable), Is.EqualTo(1));
            Assert.That(DailySite.Episode(day1, new int[0]), Is.EqualTo(1));
        }

        [Test]
        public void ShareCard_ShowsTheGrid_WithoutNamingAnyHazard()
        {
            var hazards = new[]
            {
                H("tue-swing-radius", e => { e.Detected = true; e.DetectedAtSeconds = 60; }),
                H("tue-cp-inspection", e => { }),
                H("tue-spoil-edge", e => { e.BecameIncident = true; }),
            };
            var r = ArcadeRules.Score(hazards, 1, 0, 600f);
            var text = ShareCard.Text(r, 3, "Trenching", 161f, "https://competent-person.pages.dev/?daily");
            StringAssert.StartsWith("Competent Person · Daily Site #3 · Trenching", text);
            Assert.That(text, Does.Contain("1/3 hazards"));
            Assert.That(text, Does.Contain("1 false alarm ·"));
            Assert.That(text, Does.Contain("2:41"));
            Assert.That(text, Does.Contain(ShareCard.Emoji(ArcadeRules.Cell.Early)));
            Assert.That(text, Does.Contain(ShareCard.Emoji(ArcadeRules.Cell.Incident)));
            Assert.That(text, Does.Not.Contain("swing"));
            Assert.That(text, Does.Not.Contain("spoil"));
            Assert.That(ShareCard.Text(r, 0, "", 10f, "", replay: false), Does.StartWith("Competent Person · Practice"));
            Assert.That(ShareCard.Text(r, 3, "Trenching", 10f, "", replay: true), Does.Contain("(replay)"));
            Assert.That(text.Split('\n').Length, Is.EqualTo(4), "title, grid, numbers, link");
        }
    }
}
