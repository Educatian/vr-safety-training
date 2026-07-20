using System.IO;
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
        public void LegacyXrSettings_IsExcludedFromAndroidPlayerCompilation()
        {
            const string tourSourcePath = "Assets/SafetyTraining/Scripts/Runtime/VisualCaptureTour.cs";
            var source = File.ReadAllText(tourSourcePath).Replace("\r\n", "\n");

            StringAssert.DoesNotContain(
                "UnityEngine.XR.XRSettings",
                source,
                "Legacy XRSettings requires the removed VR module and must not compile into Android or Unity 6000.4.");
            StringAssert.Contains(
                "UnityEngine.XR.Management.XRGeneralSettings.Instance?.Manager",
                source,
                "The screenshot tour must stop XR through the supported XR Management API.");
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
