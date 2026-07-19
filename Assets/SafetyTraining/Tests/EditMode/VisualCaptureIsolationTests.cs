using NUnit.Framework;
using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class VisualCaptureIsolationTests
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";

        [SetUp]
        public void OpenTrainingScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [Test]
        public void PrepareSiteCapture_ShowsOnlyRequestedTrainingSite()
        {
            VisualCaptureTour.PrepareSiteCapture(TrainingSiteId.Construction);

            var isolation = Object.FindFirstObjectByType<SiteIsolationController>();
            Assert.That(isolation.HubVisible, Is.False);
            Assert.That(isolation.VisibleSiteCount, Is.EqualTo(1));
            Assert.That(isolation.IsSiteVisible(TrainingSiteId.Construction), Is.True);
        }
    }
}
