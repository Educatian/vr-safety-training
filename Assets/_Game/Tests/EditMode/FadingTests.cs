using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Guidance fades from the learner model, not XP (area 3).
    public sealed class FadingTests
    {
        [Test]
        public void MasteredAreas_FadeOneStep_StrugglingAreas_AddOne()
        {
            Assert.That(Fading.Adjust(2, new float?[] { 0.8f, 0.75f }, out var why), Is.EqualTo(1)); Assert.That(why, Is.EqualTo("mastered"));
            Assert.That(Fading.Adjust(0, new float?[] { 0.9f, 0.2f }, out why), Is.EqualTo(1)); Assert.That(why, Is.EqualTo("struggling"));
            Assert.That(Fading.Adjust(1, new float?[] { 0.6f }, out why), Is.EqualTo(1)); Assert.That(why, Is.EqualTo("career"));
        }

        [Test]
        public void UnknownAreas_DontMoveIt_AndLevelsStayInRange()
        {
            Assert.That(Fading.Adjust(2, new float?[] { null, null }, out _), Is.EqualTo(2));
            Assert.That(Fading.Adjust(0, new float?[] { 0.95f }, out _), Is.EqualTo(0));
            Assert.That(Fading.Adjust(2, new float?[] { 0.1f }, out _), Is.EqualTo(2));
        }
    }
}
