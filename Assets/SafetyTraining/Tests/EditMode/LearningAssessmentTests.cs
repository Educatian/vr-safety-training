using System.Linq;
using NUnit.Framework;
using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class LearningAssessmentTests
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";

        [Test]
        public void EveryModule_HasExplicitObjectivesStandardsAndAssessmentEvidence()
        {
            foreach (var site in (TrainingSiteId[])System.Enum.GetValues(typeof(TrainingSiteId)))
            {
                var objectives = LearningObjectiveCatalog.ForSite(site);
                Assert.That(objectives.Count, Is.GreaterThanOrEqualTo(3), site.ToString());
                Assert.That(objectives.Select(item => item.Id).Distinct().Count(), Is.EqualTo(objectives.Count));
                Assert.That(objectives.All(item => !string.IsNullOrWhiteSpace(item.Statement)), Is.True);
                Assert.That(objectives.All(item => !string.IsNullOrWhiteSpace(item.AssessmentEvidence)), Is.True);
                Assert.That(objectives.All(item => !string.IsNullOrWhiteSpace(item.Standards)), Is.True);
                Assert.That(objectives.All(item => item.RequiredCriteria > 0), Is.True);
            }
        }

        [Test]
        public void ConstructionModule_ContainsThreeCalculationBasedEngineeringDecisions()
        {
            Assert.That(LearningObjectiveCatalog.ForSite(TrainingSiteId.Construction).Count, Is.EqualTo(4));
            Assert.That(ConstructionEngineeringCatalog.All.Count, Is.EqualTo(3));
            Assert.That(ConstructionEngineeringCatalog.All.All(item =>
                item.Options.Count(option => option.IsCorrect) == 1), Is.True);
            Assert.That(ConstructionEngineeringCatalog.All.All(item =>
                item.Calculation.Any(char.IsDigit) && item.Standard.Contains("OSHA")), Is.True);
            Assert.That(ConstructionEngineeringCatalog.All.Select(item => item.Id),
                Is.EquivalentTo(new[] { "formwork-capacity", "crane-radius", "trench-system" }));
        }

        [Test]
        public void GeneratedScene_ContainsObjectiveBoardsAndEngineeringDecisionStations()
        {
            EditorSceneManager.OpenScene(ScenePath);

            Assert.That(Object.FindObjectsByType<LearningObjectiveBoard>(FindObjectsSortMode.None),
                Has.Length.EqualTo(5));
            Assert.That(Object.FindObjectsByType<EngineeringDecisionStation>(FindObjectsSortMode.None),
                Has.Length.EqualTo(3));
            Assert.That(Object.FindObjectsByType<EngineeringDecisionOption>(FindObjectsSortMode.None),
                Has.Length.EqualTo(9));
            Assert.That(Object.FindFirstObjectByType<LearningOutcomeTracker>(), Is.Not.Null);
            Assert.That(GameObject.Find("Learning Evidence Shell"), Is.Not.Null);
        }

        [Test]
        public void ConstructionStations_HaveUniqueIdsAndOneCorrectOptionEach()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var stations = Object.FindObjectsByType<EngineeringDecisionStation>(FindObjectsSortMode.None);

            Assert.That(stations.Select(item => item.DecisionId).Distinct().Count(), Is.EqualTo(3));
            foreach (var station in stations)
            {
                var options = station.GetComponentsInChildren<EngineeringDecisionOption>(true);
                Assert.That(options, Has.Length.EqualTo(3), station.DecisionId);
                Assert.That(options.Count(item => item.IsCorrect), Is.EqualTo(1), station.DecisionId);
            }
        }
    }
}
