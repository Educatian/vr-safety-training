using System.Collections.Generic;
using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Conversation as evidence: interview clues, own-words briefing, coaching, inspection log (rules, not a model).
    public sealed class ConversationTests
    {
        [Test]
        public void Clues_PointAtRealConditions_HeldByTalkablePeople()
        {
            var prefix = new Dictionary<int, string> { [1] = "mon-", [2] = "tue-", [3] = "wed-", [4] = "thu-", [5] = "fri-" };
            foreach (var c in CrewInterview.All)
            {
                Assert.That(ConditionNames.Has(c.HazardId), Is.True, c.Id + " -> " + c.HazardId);
                StringAssert.StartsWith(prefix[c.Episode], c.HazardId, c.Id);
                Assert.That(c.Npc == "Dolores" || c.Npc == "Ray", Is.True, c.Id + " belongs to a talkable person");
                StringAssert.StartsWith(c.Npc + ":", c.Line); StringAssert.StartsWith(c.Npc + ":", c.AllClear);
            }
            Assert.That(CrewInterview.All.Select(c => c.Id).Distinct().Count(), Is.EqualTo(CrewInterview.All.Count));
            foreach (var ep in Enumerable.Range(1, 5)) Assert.That(CrewInterview.For(ep).Count(), Is.GreaterThanOrEqualTo(3), "episode " + ep);
        }

        [Test]
        public void EachQuestionChip_FindsItsOwnClue_AndDecoysFindNone()
        {
            foreach (var c in CrewInterview.All)
                Assert.That(CrewInterview.Match(c.Episode, c.Npc, c.Prompt, new HashSet<string>())?.Id, Is.EqualTo(c.Id), c.Prompt);
            foreach (var ep in Enumerable.Range(1, 5))
                foreach (var npc in new[] { "Dolores", "Ray" })
                    foreach (var q in CrewInterview.RedHerrings)
                        Assert.That(CrewInterview.Match(ep, npc, q, new HashSet<string>()), Is.Null, q);
            Assert.That(CrewInterview.Match(2, "Ray", "who inspected it?", new HashSet<string> { "tue-clue-inspect" }), Is.Null, "asked once only");
            Assert.That(CrewInterview.Match(2, "Dolores", "who inspected it?", new HashSet<string>()), Is.Null, "Ray knows that one, not Dolores");
        }

        [Test]
        public void Briefing_CountsTheFiveParts()
        {
            var full = BriefingRubric.Score("Morning everyone. The trench walls could cave in and bury someone. Nobody goes in until the box is set; OSHA 1926.652 says 5 ft or deeper needs it. Tell me if you see a crack.", "Pipe crew in an unprotected trench", EnergySource.Gravity);
            Assert.That(full.Score, Is.EqualTo(1f));
            var thin = BriefingRubric.Score("Be safe out there today.", "Pipe crew in an unprotected trench", EnergySource.Gravity);
            Assert.That(thin.Score, Is.LessThan(0.5f));
            StringAssert.Contains("name the hazard", thin.Feedback);
        }

        [Test]
        public void Coaching_AskWhyAgree_BeatsShouting()
        {
            var s = CoachingRubric.Samples("thu-roof-edge");
            Assert.That(CoachingRubric.Score(s[0]).Score, Is.GreaterThanOrEqualTo(0.99f), "ideal sample");
            Assert.That(CoachingRubric.Score(s[1]).Score, Is.EqualTo(0.2f).Within(1e-4), "hostile sample");
            Assert.That(CoachingRubric.Score(s[2]).Score, Is.LessThan(0.4f), "vague sample");
            Assert.That(float.IsPositiveInfinity(CoachingRubric.LapseFactor(1f)), Is.True);
            Assert.That(CoachingRubric.LapseFactor(0.1f), Is.EqualTo(0.5f));
            Assert.That(CoachingRubric.Applies("thu-roof-edge"), Is.True);
            foreach (var id in new[] { "thu-roof-edge", "thu-under-load", "fri-backing-mixer", "tue-swing-radius", "thu-swing-radius" })
                Assert.That(CoachingRubric.Worker.ContainsKey(id) && ConditionNames.Has(id), Is.True, id);
        }

        [Test]
        public void InspectionLog_RewardsTheRightEntries()
        {
            var real = new List<string> { "tue-no-protective-system", "tue-spoil-at-edge" };
            var alike = new List<string> { "tue-box-ok" };
            var best = InspectionLog.Score("Type C", true, "Trench box", real, real, alike, "Moved the trench box over the crew; spoil pulled back 2 ft.");
            Assert.That(best.Score, Is.EqualTo(1f).Within(1e-4));
            var soil = InspectionLog.Score("Type A", true, "Trench box", real, real, alike, "Moved the trench box over the crew.");
            Assert.That(soil.Score, Is.EqualTo(0.7f).Within(1e-4)); StringAssert.Contains("Type C", soil.Feedback);
            var noisy = InspectionLog.Score("Type C", true, "Trench box", real.Concat(alike).ToList(), real, alike, "Moved the trench box.");
            Assert.That(noisy.Score, Is.LessThan(best.Score));
        }

        [Test]
        public void ScaleLapse_MakesCoachedRemindersStick()
        {
            var spec = new HazardSpec("h", true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 3, 4, ControlLevel.Engineering, lapseAfterSeconds: 90f);
            var s = new DaySession(new[] { spec });
            s.Report("h", EnergySource.Gravity, 3, 4);
            Assert.That(s.ChooseControl("h", ControlLevel.Administrative), Is.EqualTo(ControlOutcome.Assigned));
            Assert.That(s.LapseTime("h"), Is.EqualTo(90f).Within(1e-3));
            s.ScaleLapse("h", 0.5f);
            Assert.That(s.LapseTime("h"), Is.EqualTo(45f).Within(1e-3));
            s.ScaleLapse("h", float.PositiveInfinity);
            Assert.That(float.IsPositiveInfinity(s.LapseTime("h")), Is.True);
        }

        [Test]
        public void ArcadeLeads_AreABonusOutsideTheGrade()
        {
            var ev = new HazardEvidence { Detected = true, DetectedAtSeconds = 30f };
            var h = new[] { new ArcadeRules.Hazard(new HazardSpec("a", true, EnergySource.Gravity, FocusFour.Falls, CpArea.General, 3, 3, ControlLevel.Engineering), ev, HazardState.Reported) };
            var a = ArcadeRules.Score(h, 0, 0, 600f); var b = ArcadeRules.Score(h, 0, 0, 600f, leads: 2);
            Assert.That(b.Score - a.Score, Is.EqualTo(2 * ArcadeRules.Lead));
            Assert.That(b.Grade, Is.EqualTo(a.Grade));
        }
    
        [Test]
        public void IncidentReview_RewardsSystemsThinking_AndPenalisesBlame()
        {
            var good = IncidentReview.Score("The ladder to the trailer roof kicked out because it wasn't extended 3 ft or tied off and nobody checked it. We tie it off and extend it before anyone climbs.", "Ladder to the trailer roof", EnergySource.Gravity);
            Assert.That(good.Score, Is.EqualTo(1f).Within(1e-4), good.Feedback);
            var blame = IncidentReview.Score("He was careless and should have been more careful. Tell him to pay attention.", "Ladder to the trailer roof", EnergySource.Gravity);
            Assert.That(IncidentReview.Blamed(blame), Is.True);
            Assert.That(blame.Score, Is.LessThan(0.5f));
            StringAssert.Contains("Blaming", blame.Feedback);
            var reminder = IncidentReview.Score("The ladder slipped because it was not inspected. Remind everyone to be careful.", "Ladder to the trailer roof", EnergySource.Gravity);
            Assert.That(reminder.Score, Is.LessThan(1f), "a reminder is not a fix at the condition");
            StringAssert.Contains("level=0", reminder.Flags);
        }

        [Test]
        public void EvidenceModel_KnowsTheReviewAndReferenceObservables()
        {
            Assert.That(EvidenceModel.KsaOf("incident_rca"), Is.EqualTo(Ksa.SControl));
            Assert.That(EvidenceModel.KsaOf("reference_lookup"), Is.EqualTo(Ksa.KStandard));
        }
    }
}
