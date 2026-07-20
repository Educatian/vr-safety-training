using System.IO;
using System.Linq;
using System.Reflection;
using NUnit.Framework;
using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class ConstructionGoldenModuleSceneTests
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";

        [SetUp]
        public void OpenTrainingScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [TearDown]
        public void ResetSceneSingletons()
        {
            InvokeOnDestroy(Object.FindFirstObjectByType<ConstructionGoldenModuleController>());
            InvokeOnDestroy(Object.FindFirstObjectByType<InquirySessionController>());
            InvokeOnDestroy(Object.FindFirstObjectByType<LearningOutcomeTracker>());
            InvokeOnDestroy(Object.FindFirstObjectByType<TrainingCoordinator>());
        }

        [Test]
        public void ConstructionScene_HasGoldenMissionCoordinatorAndReadableStatusBoard()
        {
            var construction = GameObject.Find("Construction Site");
            var golden = construction.GetComponent<ConstructionGoldenModuleController>();
            var board = construction.transform.Find("Construction Mission Status Board");
            var text = construction.GetComponentsInChildren<TextMesh>(true)
                .SingleOrDefault(item => item.text.Contains("MISSION STATUS"));

            Assert.That(golden, Is.Not.Null);
            Assert.That(board, Is.Not.Null);
            Assert.That(text, Is.Not.Null);
            Assert.That(text.text, Does.Contain("Evidence 0/4"));
            Assert.That(text.text, Does.Contain("Engineering 0/3"));
            Assert.That(text.text, Does.Contain("Controls 0/4"));
        }

        [Test]
        public void ConstructionPractical_UsesFieldControlOrderAfterPpe()
        {
            var actions = Object.FindObjectsByType<ConstructionActionInteractable>(FindObjectsSortMode.None)
                .OrderBy(item => item.StepIndex).ToArray();

            Assert.That(actions.Select(item => item.ActionName), Is.EqualTo(new[]
            {
                "PPE check",
                "Move material cart",
                "Install guardrail kit",
                "Set exclusion barricade",
                "Complete final walkdown"
            }));
        }

        [Test]
        public void ConstructionReport_RequiresFourEvidenceAndMissionDebrief()
        {
            var report = Object.FindObjectsByType<InquiryDecisionStation>(FindObjectsSortMode.None)
                .Single(item => item.transform.IsChildOf(GameObject.Find("Construction Site").transform));

            Assert.That(report.MinimumEvidenceRequired,
                Is.EqualTo(ConstructionGoldenModuleProgress.RequiredRelevantEvidence));
            Assert.That(Object.FindFirstObjectByType<ConstructionGoldenModuleController>().CanSubmitFinalReport,
                Is.False);
        }

        [Test]
        public void ConstructionEngineeringStations_HaveClearFrontalInteractionCorridors()
        {
            Physics.SyncTransforms();
            var stations = Object.FindObjectsByType<EngineeringDecisionStation>(FindObjectsSortMode.None);
            Assert.That(stations, Has.Length.EqualTo(3));

            foreach (var station in stations)
            {
                foreach (var option in station.GetComponentsInChildren<EngineeringDecisionOption>(true))
                {
                    var target = option.GetComponent<Collider>().bounds.center;
                    var origin = target - station.transform.forward * 4f;
                    Assert.That(Physics.Raycast(origin, (target - origin).normalized, out var hit, 5f), Is.True,
                        $"{station.DecisionId}/{option.name} has no reachable frontal surface.");
                    Assert.That(hit.transform.IsChildOf(station.transform), Is.True,
                        $"{station.DecisionId}/{option.name} is blocked by {hit.transform.name}.");
                }
            }
        }
        [Test]
        public void FullSceneMission_CompletesThroughRealInteractionsAndWritesTelemetry()
        {
            var coordinator = Object.FindFirstObjectByType<TrainingCoordinator>();
            var inquiry = Object.FindFirstObjectByType<InquirySessionController>();
            var outcomes = Object.FindFirstObjectByType<LearningOutcomeTracker>();
            var golden = Object.FindFirstObjectByType<ConstructionGoldenModuleController>();
            var practical = Object.FindFirstObjectByType<ConstructionHandsOnController>();
            InvokeAwake(outcomes);
            InvokeAwake(coordinator);
            InvokeAwake(inquiry);
            InvokeAwake(golden);
            InvokeAwake(practical);
            foreach (var action in practical.GetComponentsInChildren<ConstructionActionInteractable>(true))
                InvokeAwake(action);

            coordinator.EnterSite(TrainingSiteId.Construction);
            var actions = practical.GetComponentsInChildren<ConstructionActionInteractable>(true)
                .OrderBy(item => item.StepIndex).ToArray();
            actions[0].transform.localPosition = actions[0].TargetLocalPosition;
            actions[0].TryPerform("DesktopDrag");

            var evidence = GameObject.Find("Construction Site").GetComponentsInChildren<EvidenceObject>(true)
                .Where(item => !item.IsDistractor).Take(4).ToArray();
            foreach (var item in evidence)
                inquiry.CollectEvidence(item);

            foreach (var station in Object.FindObjectsByType<EngineeringDecisionStation>(FindObjectsSortMode.None))
                station.GetComponentsInChildren<EngineeringDecisionOption>(true)
                    .Single(item => item.IsCorrect).Select();

            foreach (var action in actions.Skip(1))
            {
                action.transform.localPosition = action.TargetLocalPosition;
                action.TryPerform("DesktopDrag");
            }

            Assert.That(golden.TryCoachExplanation(
                "The crane load and fall risk require barricade isolation and guardrail protection."), Is.True);
            var report = Object.FindObjectsByType<InquiryDecisionStation>(FindObjectsSortMode.None)
                .Single(item => item.transform.IsChildOf(GameObject.Find("Construction Site").transform));
            report.Submit();

            Assert.That(report.HasSubmitted, Is.True);
            Assert.That(golden.IsComplete, Is.True);
            var logger = golden.GetComponent<InquiryEventLogger>();
            var logPath = (string)typeof(InquiryEventLogger)
                .GetField("logPath", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(logger);
            Assert.That(File.Exists(logPath), Is.True);
            Assert.That(File.ReadAllText(logPath), Does.Contain("golden_module_completed"));
        }

        static void InvokeAwake(object instance)
        {
            instance.GetType().GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(instance, null);
        }

        static void InvokeOnDestroy(object instance)
        {
            if (instance == null) return;
            instance.GetType().GetMethod("OnDestroy", BindingFlags.Instance | BindingFlags.NonPublic)
                ?.Invoke(instance, null);
        }
    }
}
