using System.IO;
using System.Reflection;
using NUnit.Framework;
using SafetyTraining.Editor;
using SafetyTraining.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using UnityEngine.XR.OpenXR.Features.Interactions;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class GazeAnalyticsTrackerTests
    {
        [TestCase(true, true, GazeTrackingMode.EyeGaze)]
        [TestCase(false, true, GazeTrackingMode.HeadGaze)]
        [TestCase(false, false, GazeTrackingMode.Unavailable)]
        public void ResolveMode_PrefersEyesThenFallsBackToHead(bool eyes, bool camera,
            GazeTrackingMode expected)
        {
            Assert.That(GazeAnalyticsTracker.ResolveMode(eyes, camera), Is.EqualTo(expected));
        }

        [Test]
        public void EpisodeTracker_SeparatesGlancesFromDwell()
        {
            var tracker = new GazeEpisodeTracker();
            tracker.Observe("evidence:a", "evidence", new Vector3(1f, 2f, 3f), 10f, 0.6f);

            var glance = tracker.Complete(10.3f, 0.6f);

            Assert.That(glance.HasValue, Is.True);
            Assert.That(glance.IsDwell, Is.False);
            Assert.That(glance.DurationSeconds, Is.EqualTo(0.3f).Within(0.001f));

            tracker.Observe("inspection:b", "inspection", new Vector3(4f, 5f, 6f), 20f, 0.6f);
            var dwell = tracker.Complete(20.8f, 0.6f);
            Assert.That(dwell.IsDwell, Is.True);
            Assert.That(dwell.HitPosition, Is.EqualTo(new Vector3(4f, 5f, 6f)));
        }

        [Test]
        public void EpisodeTracker_TargetSwitchCompletesOnlyPreviousTarget()
        {
            var tracker = new GazeEpisodeTracker();
            tracker.Observe("evidence:a", "evidence", Vector3.one, 2f, 0.6f);

            var completed = tracker.Observe("npc:coach", "npc", Vector3.zero, 3f, 0.6f);

            Assert.That(completed.TargetId, Is.EqualTo("evidence:a"));
            Assert.That(completed.IsDwell, Is.True);
            Assert.That(tracker.Complete(3.2f, 0.6f).TargetId, Is.EqualTo("npc:coach"));
        }

        [Test]
        public void AndroidOpenXrSettings_EnableEyeGazeAsOptionalExtension()
        {
            var settings = OpenXRSettings.GetSettingsForBuildTargetGroup(BuildTargetGroup.Android);
            var feature = settings != null ? settings.GetFeature<EyeGazeInteraction>() : null;

            Assert.That(feature, Is.Not.Null);
            Assert.That(feature.enabled, Is.True);
            Assert.That(EyeGazeInteraction.extensionString, Is.EqualTo("XR_EXT_eye_gaze_interaction"));
        }

        [Test]
        public void ManifestProcessor_KeepsPermissionButMakesHardwareOptional()
        {
            var root = Path.Combine(Path.GetTempPath(), "vr-safety-gaze-manifest",
                TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(root);
            var path = Path.Combine(root, "AndroidManifest.xml");
            File.WriteAllText(path,
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">" +
                "<uses-feature android:name=\"oculus.software.eye_tracking\" android:required=\"true\"/>" +
                "<uses-permission android:name=\"com.oculus.permission.EYE_TRACKING\"/>" +
                "<application/></manifest>");
            try
            {
                OptionalEyeTrackingManifestProcessor.MakeEyeTrackingOptional(path);
                var updated = File.ReadAllText(path);
                Assert.That(updated, Does.Contain("oculus.software.eye_tracking"));
                Assert.That(updated, Does.Contain("required=\"false\""));
                Assert.That(updated, Does.Contain("com.oculus.permission.EYE_TRACKING"));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void GradleCallback_UpdatesXrLibraryManifest()
        {
            var root = Path.Combine(Path.GetTempPath(), "vr-safety-gaze-gradle",
                TestContext.CurrentContext.Test.ID);
            var manifestDirectory = Path.Combine(root, "xrmanifest.androidlib");
            Directory.CreateDirectory(manifestDirectory);
            var path = Path.Combine(manifestDirectory, "AndroidManifest.xml");
            File.WriteAllText(path,
                "<manifest xmlns:android=\"http://schemas.android.com/apk/res/android\">" +
                "<uses-feature android:name=\"oculus.software.eye_tracking\" android:required=\"true\"/>" +
                "</manifest>");
            try
            {
                new OptionalEyeTrackingManifestProcessor().OnPostGenerateGradleAndroidProject(root);
                Assert.That(File.ReadAllText(path), Does.Contain("required=\"false\""));
            }
            finally
            {
                Directory.Delete(root, true);
            }
        }

        [Test]
        public void CoordinatorBootstrap_EnsuresGazeAnalyticsTracker()
        {
            var root = new GameObject("Gaze bootstrap test");
            try
            {
                var coordinator = root.AddComponent<TrainingCoordinator>();
                typeof(TrainingCoordinator).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    ?.Invoke(coordinator, null);
                Assert.That(root.GetComponent<GazeAnalyticsTracker>(), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
