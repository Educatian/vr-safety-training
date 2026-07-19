using System.Linq;
using NUnit.Framework;
using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;

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

        [Test]
        public void ConstructionStations_UseEyeHeightWorldSpaceHmiAndReachableControlDeck()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var stations = Object.FindObjectsByType<EngineeringDecisionStation>(FindObjectsSortMode.None);

            foreach (var station in stations)
            {
                var hmi = station.transform.Find("Engineering HMI");
                var canvas = hmi != null ? hmi.GetComponent<Canvas>() : null;
                Assert.That(canvas, Is.Not.Null, station.DecisionId);
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace), station.DecisionId);
                Assert.That(hmi.localPosition.y, Is.InRange(1.55f, 1.8f), station.DecisionId);
                Assert.That(hmi.localScale.x, Is.InRange(0.0018f, 0.0025f), station.DecisionId);

                var options = station.GetComponentsInChildren<EngineeringDecisionOption>(true);
                Assert.That(options.All(item => item.transform.localPosition.y >= 0.58f &&
                                                item.transform.localPosition.y <= 0.95f),
                    Is.True, $"{station.DecisionId}: controls must remain in the standing reach zone.");
                Assert.That(options.Max(item => item.transform.localPosition.x) -
                            options.Min(item => item.transform.localPosition.x),
                    Is.LessThanOrEqualTo(2.5f), station.DecisionId);
            }
        }

        [Test]
        public void ObjectiveBoards_SeparateGoalEvidenceAndStandardIntoWorldSpaceRegions()
        {
            EditorSceneManager.OpenScene(ScenePath);

            foreach (var board in Object.FindObjectsByType<LearningObjectiveBoard>(FindObjectsSortMode.None))
            {
                var canvas = board.transform.parent.Find("Objective UI")?.GetComponent<Canvas>();
                Assert.That(canvas, Is.Not.Null, board.SiteId.ToString());
                Assert.That(canvas.renderMode, Is.EqualTo(RenderMode.WorldSpace));
                Assert.That(canvas.transform.Find("Objective Statement"), Is.Not.Null);
                Assert.That(canvas.transform.Find("Evidence Statement"), Is.Not.Null);
                Assert.That(canvas.transform.Find("Reference"), Is.Not.Null);
            }
        }

        [Test]
        public void WorldSpaceLearningText_RemainsInsideItsCanvasAndUsesReadableBodyFont()
        {
            EditorSceneManager.OpenScene(ScenePath);

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                         .Where(item => item.name is "Objective UI" or "Engineering HMI"))
            {
                var canvasRect = (RectTransform)canvas.transform;
                var half = canvasRect.sizeDelta * 0.5f;
                foreach (var text in canvas.GetComponentsInChildren<Text>(true))
                {
                    var rect = text.rectTransform;
                    var min = rect.anchoredPosition - rect.sizeDelta * 0.5f;
                    var max = rect.anchoredPosition + rect.sizeDelta * 0.5f;
                    Assert.That(min.x, Is.GreaterThanOrEqualTo(-half.x),
                        $"{canvas.name}/{text.name} exceeds the left canvas edge.");
                    Assert.That(max.x, Is.LessThanOrEqualTo(half.x),
                        $"{canvas.name}/{text.name} exceeds the right canvas edge.");
                    Assert.That(text.font.name, Does.Contain("LiberationSans"),
                        $"{canvas.name}/{text.name} must use the readable body typeface.");
                }
            }
        }

        [Test]
        public void LearningCardLabels_DoNotOverlapTheirBodyCopy()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var pairs = new[]
            {
                ("Objective Label", "Objective Statement"),
                ("Evidence Label", "Evidence Statement"),
                ("Field Card Label", "Field Data"),
                ("Check Card Label", "Calculation")
            };

            foreach (var canvas in Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                         .Where(item => item.name is "Objective UI" or "Engineering HMI"))
            {
                foreach (var (labelName, bodyName) in pairs)
                {
                    var label = canvas.transform.Find(labelName) as RectTransform;
                    var body = canvas.transform.Find(bodyName) as RectTransform;
                    if (label == null || body == null)
                        continue;
                    var labelBottom = label.anchoredPosition.y - label.sizeDelta.y * 0.5f;
                    var bodyTop = body.anchoredPosition.y + body.sizeDelta.y * 0.5f;
                    Assert.That(labelBottom - bodyTop, Is.GreaterThanOrEqualTo(6f),
                        $"{canvas.name}: {labelName} overlaps {bodyName}.");
                }
            }
        }

        [Test]
        public void EngineeringStations_KeepStandardInsideDisplayAndOccludeAdjacentTasks()
        {
            EditorSceneManager.OpenScene(ScenePath);

            foreach (var station in Object.FindObjectsByType<EngineeringDecisionStation>(FindObjectsSortMode.None))
            {
                var hmi = station.transform.Find("Engineering HMI");
                var standard = hmi?.Find("Standard") as RectTransform;
                Assert.That(standard, Is.Not.Null, station.DecisionId);
                Assert.That(standard.anchoredPosition.y, Is.GreaterThan(-120f),
                    $"{station.DecisionId}: standard must remain above the feedback band.");
                Assert.That(standard.GetComponent<Text>().fontSize, Is.GreaterThanOrEqualTo(22),
                    $"{station.DecisionId}: standard must remain legible at the four-meter viewing distance.");

                var wings = station.transform.Cast<Transform>()
                    .Where(item => item.name == "Focus Wing").ToArray();
                Assert.That(wings, Has.Length.EqualTo(2), station.DecisionId);
                Assert.That(wings.All(item => item.localScale.z >= 2.2f), Is.True,
                    $"{station.DecisionId}: focus wings must screen adjacent decision stations.");
            }
            Assert.That(ConstructionEngineeringCatalog.All.All(item => item.Standard.Length <= 110), Is.True,
                "Engineering citations must use concise VR-readable evidence strips.");
        }
    }
}
