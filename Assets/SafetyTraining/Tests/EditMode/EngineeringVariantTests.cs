using System.Linq;
using NUnit.Framework;
using SafetyTraining.Core;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class EngineeringVariantTests
    {
        [Test]
        public void EveryConstructionDecision_HasATransferVariant()
        {
            foreach (var decision in ConstructionEngineeringCatalog.All)
                Assert.That(ConstructionEngineeringCatalog.Variants.ContainsKey(decision.Id),
                    Is.True, decision.Id);
        }

        [Test]
        public void EveryVariant_HasExactlyOneCorrectOptionAndNoWorkedSolution()
        {
            foreach (var pair in ConstructionEngineeringCatalog.Variants)
            {
                var variant = pair.Value;
                Assert.That(variant.Options.Count(option => option.IsCorrect), Is.EqualTo(1), pair.Key);
                Assert.That(variant.Calculation, Does.Contain("No worked solution"), pair.Key);
                Assert.That(variant.LearningMaterial,
                    Is.Not.EqualTo(ConstructionEngineeringCatalog.All
                        .Single(item => item.Id == pair.Key).LearningMaterial), pair.Key);
            }
        }

        [Test]
        public void VariantCorrectAnswers_DifferFromBaseCorrectAnswers()
        {
            foreach (var decision in ConstructionEngineeringCatalog.All)
            {
                var baseCorrect = decision.Options.Single(option => option.IsCorrect).Id;
                var variantCorrect = ConstructionEngineeringCatalog.Variants[decision.Id]
                    .Options.Single(option => option.IsCorrect).Id;
                Assert.That(variantCorrect, Is.Not.EqualTo(baseCorrect), decision.Id);
            }
        }
    }
}
