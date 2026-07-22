using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build.Reporting;

namespace SafetyTraining.Editor
{
    public static class StandaloneBuildRunner
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";

        [MenuItem("Safety Training/Build Windows Training App")]
        public static void BuildWindows()
        {
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.StandaloneWindows64 &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(
                    BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new InvalidOperationException("Unity could not switch to the Windows build target.");
            var outputDirectory = Path.GetFullPath(Path.Combine("Builds", "Windows"));
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "VR-Safety-Training.exe");

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.CleanBuildCache
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Windows build failed: {report.summary.result}, errors={report.summary.totalErrors}");

            UnityEngine.Debug.Log(
                $"Windows build succeeded: {outputPath} ({new FileInfo(outputPath).Length} executable bytes, " +
                $"{report.summary.totalSize} total build bytes)");
        }

        [MenuItem("Safety Training/Build Meta Quest APK")]
        public static void BuildMetaQuest()
        {
            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException(
                    "Meta Quest build requires Unity Android Build Support, Android SDK/NDK Tools, and OpenJDK.");
            // Meta Quest validation rules query Android-specific OpenXR state. Running them while
            // Standalone is still the active target can throw inside the OpenXR package before the
            // build pipeline gets a chance to switch targets.
            if (EditorUserBuildSettings.activeBuildTarget != BuildTarget.Android &&
                !EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new InvalidOperationException("Unity could not switch to the Android build target.");
            if (!OpenXrProjectConfigurator.ConfigureAndroid())
                throw new InvalidOperationException("Meta Quest OpenXR configuration failed.");

            var outputDirectory = Path.GetFullPath(Path.Combine("Builds", "MetaQuest"));
            Directory.CreateDirectory(outputDirectory);
            var outputPath = Path.Combine(outputDirectory, "VR-Safety-Training-Quest.apk");
            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { ScenePath },
                locationPathName = outputPath,
                target = BuildTarget.Android,
                options = BuildOptions.CleanBuildCache
            });

            if (report.summary.result != BuildResult.Succeeded)
                throw new InvalidOperationException(
                    $"Meta Quest build failed: {report.summary.result}, errors={report.summary.totalErrors}");
            UnityEngine.Debug.Log(
                $"Meta Quest APK succeeded: {outputPath} ({new FileInfo(outputPath).Length} APK bytes, " +
                $"{report.summary.totalSize} total build bytes)");
        }
    }
}
