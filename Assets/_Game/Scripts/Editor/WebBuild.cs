using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace Jobsite.Editor
{
    // Web target (TechSpec platform decision 2026-09-28): Unity WebGL, desktop browsers first.
    // Batch: Unity.exe -batchmode -quit -executeMethod Jobsite.Editor.WebBuild.Build
    public static class WebBuild
    {
        const string Out = "Builds/WebGL";

        [MenuItem("Jobsite/Build/WebGL")]
        public static void Build()
        {
            EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.WebGL, BuildTarget.WebGL);

            PlayerSettings.WebGL.compressionFormat = WebGLCompressionFormat.Brotli;
            PlayerSettings.WebGL.decompressionFallback = true;    // works on plain static hosting without Content-Encoding headers
            PlayerSettings.WebGL.dataCaching = true;
            PlayerSettings.WebGL.exceptionSupport = WebGLExceptionSupport.ExplicitlyThrownExceptionsOnly;
            PlayerSettings.WebGL.memorySize = 512;
            PlayerSettings.SetManagedStrippingLevel(UnityEditor.Build.NamedBuildTarget.WebGL, ManagedStrippingLevel.High);
            PlayerSettings.SetIl2CppCodeGeneration(UnityEditor.Build.NamedBuildTarget.WebGL, UnityEditor.Build.Il2CppCodeGeneration.OptimizeSize);
            PlayerSettings.productName = "Competent Person";
            PlayerSettings.companyName = "ADIE Lab";

            // Web rendering budget: no MSAA, shorter shadows (TechSpec web budgets).
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Game/Settings/URP_PC.asset");
            if (urp != null)
            {
                urp.msaaSampleCount = 1;
                urp.shadowDistance = 40f;
                EditorUtility.SetDirty(urp);
                AssetDatabase.SaveAssets();
            }

            var options = new BuildPlayerOptions
            {
                scenes = new[] { "Assets/_Game/Scenes/Jobsite.unity" },
                locationPathName = Out,
                target = BuildTarget.WebGL,
                options = BuildOptions.None,
            };
            var report = BuildPipeline.BuildPlayer(options);
            var bytes = Directory.Exists(Out) ? Directory.GetFiles(Out, "*", SearchOption.AllDirectories).Sum(f => new FileInfo(f).Length) : 0;
            Debug.Log($"[WebBuild] {report.summary.result} total={bytes / 1e6:F1} MB time={report.summary.totalTime}");
            foreach (var f in Directory.Exists(Out + "/Build") ? Directory.GetFiles(Out + "/Build") : new string[0])
                Debug.Log($"[WebBuild] {Path.GetFileName(f)} {new FileInfo(f).Length / 1e6:F1} MB");
            if (report.summary.result != BuildResult.Succeeded) EditorApplication.Exit(1);
        }
    }
}
