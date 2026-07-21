using NUnit.Framework;
using SafetyTraining.Core;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class BktModelTests
    {
        [Test]
        public void Observe_CorrectRaisesMastery_IncorrectLowersIt()
        {
            var rising = new BktModel();
            var falling = new BktModel();

            var afterCorrect = rising.Observe(true);
            var afterWrong = falling.Observe(false);

            Assert.That(afterCorrect, Is.GreaterThan(0.30));
            Assert.That(afterWrong, Is.LessThan(afterCorrect));
        }

        [Test]
        public void Observe_ConsistentSuccessConvergesTowardMastery()
        {
            var model = new BktModel();
            for (var index = 0; index < 6; index++)
                model.Observe(true);

            Assert.That(model.Mastery, Is.GreaterThan(0.9));
            Assert.That(model.ObservationCount, Is.EqualTo(6));
        }

        [Test]
        public void Observe_StaysWithinProbabilityBounds()
        {
            var model = new BktModel();
            for (var index = 0; index < 12; index++)
                model.Observe(false);

            Assert.That(model.Mastery, Is.InRange(0.001, 0.999));
        }
    }
}
