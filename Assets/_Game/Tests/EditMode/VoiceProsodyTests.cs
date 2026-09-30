using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Crew voices follow the affect state (area 7) and only crew lines are voiced.
    public sealed class VoiceProsodyTests
    {
        [Test]
        public void Stress_RaisesPitchRateAndVolume_Distrust_LowersPitch()
        {
            var calm = VoiceProsody.For("ray", 0f, 0f);
            var tense = VoiceProsody.For("ray", 0f, 0.9f);
            Assert.That(tense.Pitch, Is.GreaterThan(calm.Pitch));
            Assert.That(tense.Rate, Is.GreaterThan(calm.Rate));
            Assert.That(tense.Volume, Is.GreaterThan(calm.Volume));
            var wary = VoiceProsody.For("ray", -0.9f, 0f);
            Assert.That(wary.Pitch, Is.LessThan(calm.Pitch));
            var warm = VoiceProsody.For("marcus", 0.9f, 0f);
            Assert.That(warm.Rate, Is.LessThan(VoiceProsody.For("marcus", 0f, 0f).Rate));
        }

        [Test]
        public void Prosody_StaysInRange_ForEveryCastMemberAndExtremeState()
        {
            foreach (var c in Cast.All)
            {
                Assert.That(VoiceProsody.Knows(c.Id), Is.True, c.Id + " has a base voice");
                foreach (var t in new[] { -5f, -1f, 0f, 1f, 5f })
                    foreach (var s in new[] { -1f, 0f, 1f, 3f })
                    {
                        var p = VoiceProsody.For(c.Id, t, s);
                        Assert.That(p.Pitch >= VoiceProsody.MinPitch && p.Pitch <= VoiceProsody.MaxPitch, Is.True, c.Id + " pitch " + p);
                        Assert.That(p.Rate >= VoiceProsody.MinRate && p.Rate <= VoiceProsody.MaxRate, Is.True, c.Id + " rate " + p);
                        Assert.That(p.Volume >= 0f && p.Volume <= 1f, Is.True, c.Id + " volume " + p);
                    }
            }
            Assert.That(VoiceProsody.For("dolores", 0, 0).Female, Is.True);
            Assert.That(VoiceProsody.For("earl", 0, 0).Female, Is.False);
        }

        [Test]
        public void ForemanUsesHisOwnState_EveryoneElseTheCrews()
        {
            var a = new CrewAffect();
            a.Nudge(CrewAffect.Foreman, -0.35f, 0.35f);
            Assert.That(VoiceProsody.AffectPerson("ray"), Is.EqualTo(CrewAffect.Foreman));
            Assert.That(VoiceProsody.AffectPerson("luis"), Is.EqualTo(CrewAffect.Crew));
            Assert.That(VoiceProsody.For("ray", a).Rate, Is.GreaterThan(VoiceProsody.For("ray", 0f, 0f).Rate));
        }

        [Test]
        public void OnlyCrewLinesAreVoiced()
        {
            Assert.That(VoiceProsody.TrySplit("Ray: We can't sit all day.", out var id, out var text), Is.True);
            Assert.That(id, Is.EqualTo("ray")); Assert.That(text, Is.EqualTo("We can't sit all day."));
            Assert.That(VoiceProsody.TrySplit("Crew (radio): Hey, check the ladder.", out id, out _), Is.True);
            Assert.That(id, Is.EqualTo("crew"));
            Assert.That(VoiceProsody.TrySplit("Dolores (radio): Good call.", out id, out _), Is.True);
            Assert.That(id, Is.EqualTo("dolores"));
            foreach (var notice in new[] { "Walk the site with WASD, drag the mouse to look.", "Checked in: PPE, Tools. 2 to go.",
                         "Near miss: the crew. All stop.", "Here's the rule: 1926.651", "Correct. Explanation (1926.21)", "", null })
                Assert.That(VoiceProsody.TrySplit(notice, out _, out _), Is.False, notice ?? "null");
        }

        [Test]
        public void EveryCrewRequestLine_IsVoicedByItsOwnSpeaker()
        {
            foreach (var r in CrewRequests.All)
                foreach (var line in new[] { r.Line, r.DoneLine })
                {
                    Assert.That(VoiceProsody.TrySplit(line, out var id, out _), Is.True, line);
                    Assert.That(Cast.Get(id).Name.Split(' ')[0], Is.EqualTo(line.Split(':')[0]), line);
                }
        }
    }
}
