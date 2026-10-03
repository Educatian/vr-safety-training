using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Crew LLM output guard (area 9): in character, grounded citations, no PII requests, bounded length.
    public sealed class ReplyGuardTests
    {
        const string Facts = "Trench 6 ft deep. 29 CFR 1926.652(a)(1): protective system required at 5 ft or more. Ladder within 25 ft, 1926.651(c)(2).";

        [Test] public void GroundedInCharacterReply_Passes()
        {
            var v = ReplyGuard.Check("It's six feet deep, so 1926.652 says it needs a box or shoring. Get the crew out until it's in.", Facts);
            Assert.That(v.Ok, v.Reason);
        }

        [Test] public void VendorOrAiMention_Fails()
        {
            Assert.That(ReplyGuard.Check("As an AI language model I can't see the trench.", Facts).Reason, Is.EqualTo("out_of_character"));
            Assert.That(ReplyGuard.Check("I'm Claude, running on OpenRouter.", Facts).Ok, Is.False);
        }

        [Test] public void InventedStandard_Fails()
        {
            var v = ReplyGuard.Check("Per 1926.1053(b)(1) you need a 3 ft extension.", Facts);
            Assert.That(v.Ok, Is.False); Assert.That(v.Reason.StartsWith("invented_cfr"), v.Reason);
        }

        [Test] public void AskingForPersonalInfo_OrLinks_Fails()
        {
            Assert.That(ReplyGuard.Check("Sure. What's your name and phone number?", Facts).Reason, Is.EqualTo("asks_pii"));
            Assert.That(ReplyGuard.Check("Read https://osha.gov for more.", Facts).Reason, Is.EqualTo("markup"));
        }

        [Test] public void LongReply_IsTrimmedToThreeSentences()
        {
            var v = ReplyGuard.Check("One. Two. Three. Four. Five.", Facts);
            Assert.That(v.Ok); Assert.That(v.Text, Is.EqualTo("One. Two. Three.")); Assert.That(v.Reason, Is.EqualTo("ok_trimmed"));
        }

        [Test] public void Empty_Fails() => Assert.That(ReplyGuard.Check("  ", Facts).Ok, Is.False);
    }
}
