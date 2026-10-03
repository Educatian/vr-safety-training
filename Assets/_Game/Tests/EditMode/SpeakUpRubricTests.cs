using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Free-text speak-up: the scored stance is a deterministic rubric, consistent with the three set answers.
    public sealed class SpeakUpRubricTests
    {
        [Test]
        public void TheSetAnswers_ClassifyAsTheirOwnStyle()
        {
            foreach (var o in SpeakUp.Options(1))
                Assert.That(SpeakUpRubric.Classify(o.Text).Style, Is.EqualTo(o.Style), o.Text);
        }

        [Test]
        public void Classify_HoldsHostilityAndYields()
        {
            Assert.That(SpeakUpRubric.Classify("No. It stays stopped until the shoring is in.").Style, Is.EqualTo(SpeakUpStyle.Assertive));
            Assert.That(SpeakUpRubric.Classify("Okay, but nobody goes back in until the box is set.").Style, Is.EqualTo(SpeakUpStyle.Assertive), "a hold after 'okay' still holds");
            Assert.That(SpeakUpRubric.Classify("STOP EVERYTHING RIGHT NOW!!!").Style, Is.EqualTo(SpeakUpStyle.Aggressive), "shouting");
            Assert.That(SpeakUpRubric.Classify("Shut up Ray, it's stopped.").Style, Is.EqualTo(SpeakUpStyle.Aggressive));
            Assert.That(SpeakUpRubric.Classify("Don't stop them, it's fine, we'll look after lunch.").Style, Is.EqualTo(SpeakUpStyle.Passive), "'don't stop' is a yield");
            Assert.That(SpeakUpRubric.Classify("").Style, Is.EqualTo(SpeakUpStyle.Passive));
            Assert.That(SpeakUpRubric.Classify("hmm let me think").Style, Is.EqualTo(SpeakUpStyle.Passive), "no hold, no stop");
        }

        [Test]
        public void QualityNotes_DriveTheTip()
        {
            var best = SpeakUpRubric.Classify("Ray, those walls could cave in on Marcus. It stays stopped until the box is in. I'll help set it, twenty minutes.", "Unprotected trench wall");
            Assert.That(best.Style, Is.EqualTo(SpeakUpStyle.Assertive));
            Assert.That(best.Reason && best.Offer && best.Respect, Is.True, best.Flags);
            StringAssert.Contains("competent-person answer", best.Tip);
            var bare = SpeakUpRubric.Classify("No. It stays stopped.");
            Assert.That(bare.Reason, Is.False);
            StringAssert.Contains("name the hazard", bare.Tip);
            Assert.That(SpeakUpRubric.Classify("The trench is not safe, no one goes in until it's fixed.", "Trench").Reason, Is.True, "hazard name counts as a reason");
            Assert.That(best.Flags, Does.Contain("hold=1"));
        }
    }
}
