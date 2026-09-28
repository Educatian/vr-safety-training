using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Jobsite.Editor
{
    // T0.2: create URP assets, make them active on every quality level, and re-import models
    // so URP's FBX/glTF material preprocessors regenerate embedded materials as URP/Lit.
    // Batch: Unity.exe -batchmode -quit -executeMethod Jobsite.Editor.UrpSetup.Run
    public static class UrpSetup
    {
        const string Folder = "Assets/_Game/Settings";
        const string AssetPath = Folder + "/URP_PC.asset";
        const string RendererPath = Folder + "/URP_PC_Renderer.asset";

        [MenuItem("Jobsite/Setup/Switch To URP")]
        public static void Run()
        {
            Directory.CreateDirectory(Folder);

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(AssetPath);
            if (pipeline == null)
            {
                var rendererData = ScriptableObject.CreateInstance<UniversalRendererData>();
                AssetDatabase.CreateAsset(rendererData, RendererPath);
                pipeline = UniversalRenderPipelineAsset.Create(rendererData);
                AssetDatabase.CreateAsset(pipeline, AssetPath);
            }

            // PC VR target (TechSpec): MSAA 4x, HDR, soft shadows, 80 m shadow distance.
            pipeline.msaaSampleCount = 4;
            pipeline.supportsHDR = true;
            pipeline.shadowDistance = 80f;
            EditorUtility.SetDirty(pipeline);

            GraphicsSettings.defaultRenderPipeline = pipeline;
            var current = QualitySettings.GetQualityLevel();
            for (var i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }
            QualitySettings.SetQualityLevel(current, false);
            AssetDatabase.SaveAssets();

            var models = AssetDatabase.FindAssets("t:Model", new[] { "Assets/ThirdParty" })
                .Select(AssetDatabase.GUIDToAssetPath).ToList();
            foreach (var path in models)
                AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);

            Debug.Log($"[UrpSetup] URP active on {QualitySettings.names.Length} quality levels; re-imported {models.Count} models.");
        }
    }
}
