using NUnit.Framework;
using SafetyTraining.Core;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class RetrievalRoundPlanTests
    {
        static RetrievalRoundPlan CreatePlan()
        {
            return new RetrievalRoundPlan(new[]
            {
                new RetrievalItemSpec(TrainingSiteId.Construction, "guarded-edge", false),
                new RetrievalItemSpec(TrainingSiteId.Construction, "fall-edge", true)
            });
        }

        [Test]
        public void Judge_ResolvesItemOnFirstCorrectJudgment()
        {
            var plan = CreatePlan();

            var judgment = plan.Judge("guarded-edge", judgedHazard: false);

            Assert.That(judgment.Correct, Is.True);
            Assert.That(judgment.FirstAttempt, Is.True);
            Assert.That(plan.IsResolved("guarded-edge"), Is.True);
            Assert.That(judgment.RemainingItems, Is.EqualTo(1));
        }

        [Test]
        public void Judge_WrongJudgmentLeavesItemOpenAndTracksFirstAttempt()
        {
            var plan = CreatePlan();

            var wrong = plan.Judge("guarded-edge", judgedHazard: true);
            var retry = plan.Judge("guarded-edge", judgedHazard: false);

            Assert.That(wrong.Correct, Is.False);
            Assert.That(wrong.FirstAttempt, Is.True);
            Assert.That(retry.Correct, Is.True);
            Assert.That(retry.FirstAttempt, Is.False);
        }

        [Test]
        public void Plan_CompletesOnlyWhenEveryItemIsResolved()
        {
            var plan = CreatePlan();

            plan.Judge("guarded-edge", judgedHazard: false);
            Assert.That(plan.IsComplete, Is.False);
            plan.Judge("fall-edge", judgedHazard: true);
            Assert.That(plan.IsComplete, Is.True);
        }

        [Test]
        public void Judge_IgnoresUnknownAndAlreadyResolvedItems()
        {
            var plan = CreatePlan();
            plan.Judge("guarded-edge", judgedHazard: false);

            Assert.That(plan.Judge("guarded-edge", judgedHazard: true), Is.Null);
            Assert.That(plan.Judge("unknown-item", judgedHazard: true), Is.Null);
            Assert.That(plan.IsResolved("guarded-edge"), Is.True);
        }

        [Test]
        public void TrainingSession_TracksWhichFoilsWereFalselySelected()
        {
            var session = new TrainingSession(TrainingSiteId.Construction, new[]
            {
                new InspectionTargetSpec("fall-edge", true),
                new InspectionTargetSpec("guarded-edge", false),
                new InspectionTargetSpec("stored-materials", false)
            });

            session.Inspect("guarded-edge");
            session.Inspect("fall-edge");

            Assert.That(session.FalsePositiveTargetIds, Is.EqualTo(new[] { "guarded-edge" }));
            Assert.That(session.HazardTargetIds, Is.EquivalentTo(new[] { "fall-edge" }));
        }
    }
}
