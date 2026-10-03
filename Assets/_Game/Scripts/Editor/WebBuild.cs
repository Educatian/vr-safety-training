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

        // Background crew variety needs the female body in the player: referenced from Resources/CrewBodies.asset.
        public static void EnsureCrewBodies()
        {
            const string path = "Assets/_Game/Resources/CrewBodies.asset";
            var b = AssetDatabase.LoadAssetAtPath<Jobsite.Runtime.CrewBodies>(path);
            if (b == null) { b = ScriptableObject.CreateInstance<Jobsite.Runtime.CrewBodies>(); AssetDatabase.CreateAsset(b, path); }
            b.female = AssetDatabase.LoadAssetAtPath<GameObject>(Jobsite.Runtime.CrewBodies.FemalePath);
            EditorUtility.SetDirty(b);
            AssetDatabase.SaveAssets();
            Debug.Log("[WebBuild] crew bodies: female=" + (b.female != null));
        }

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
            PlayerSettings.SplashScreen.show = false;              // 2.7 MB logo texture, and a slower start
            ShrinkAssets();
            UrpSetup.EnsurePostProcessData();
            EnsureAlwaysIncludedShaders();
            EnsureCrewBodies();
            PlayerSettings.productName = "Competent Person";
            PlayerSettings.companyName = "ADIE Lab";

            // Web rendering budget: no MSAA, shorter shadows (TechSpec web budgets).
            var urp = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/_Game/Settings/URP_PC.asset");
            if (urp != null)
            {
                urp.msaaSampleCount = 1;
                urp.supportsHDR = false;                 // WebGL: render straight to the backbuffer, no HDR copy chain
                urp.supportsCameraOpaqueTexture = false;
                urp.supportsCameraDepthTexture = false;
                var data = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/_Game/Settings/URP_PC_Renderer.asset");
                if (data != null) { data.intermediateTextureMode = IntermediateTextureMode.Auto; EditorUtility.SetDirty(data); }
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

        // GraphicsSettings' always-included list had been emptied, so players shipped without UI/Default and every
        // uGUI element (menu shade included) drew as the pink error shader. Restore Unity's defaults (idempotent).
        static void EnsureAlwaysIncludedShaders()
        {
            var names = new[] { "UI/Default", "Sprites/Default", "Sprites/Mask", "Legacy Shaders/Diffuse", "Hidden/CubeBlur", "Hidden/CubeCopy",
                                "Hidden/CubeBlend", "Hidden/VideoComposite", "Hidden/VideoDecode", "Hidden/Compositing" };
            var settings = new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/GraphicsSettings.asset")[0]);
            var list = settings.FindProperty("m_AlwaysIncludedShaders");
            var have = Enumerable.Range(0, list.arraySize).Select(i => list.GetArrayElementAtIndex(i).objectReferenceValue).ToList();
            foreach (var n in names)
            {
                var s = Shader.Find(n);
                if (s == null || have.Contains(s)) continue;
                list.InsertArrayElementAtIndex(list.arraySize);
                list.GetArrayElementAtIndex(list.arraySize - 1).objectReferenceValue = s;
                have.Add(s);
            }
            settings.ApplyModifiedPropertiesWithoutUndo();
            AssetDatabase.SaveAssets();
            Debug.Log("[WebBuild] always-included shaders: " + list.arraySize);
        }

        // Web download budget (Pages serves files <= 25 MiB; students on campus Wi-Fi): cap textures at 1024 px
        // (2048 for UI/episode art), crunch them, and compress meshes. Idempotent; only touches what exceeds the budget.
        static void ShrinkAssets()
        {
            var changed = 0;
            AssetDatabase.StartAssetEditing();
            try
            {
                foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game", "Assets/ThirdParty" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is TextureImporter ti)) continue;
                    var ui = path.Contains("/Resources/UI/") || path.Contains("/Resources/Episodes/") || path.Contains("/Resources/Fonts/");
                    var cap = ui ? 2048 : 1024;
                    var dirty = false;
                    if (ti.maxTextureSize > cap) { ti.maxTextureSize = cap; dirty = true; }
                    if (!ui && !ti.crunchedCompression && ti.textureCompression != TextureImporterCompression.Uncompressed)
                    { ti.crunchedCompression = true; ti.compressionQuality = 50; dirty = true; }
                    if (dirty) { ti.SaveAndReimport(); changed++; }
                }
                foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/_Game", "Assets/ThirdParty" }))
                {
                    var path = AssetDatabase.GUIDToAssetPath(guid);
                    if (!(AssetImporter.GetAtPath(path) is ModelImporter mi)) continue;
                    var dirty = false;
                    if (mi.meshCompression < ModelImporterMeshCompression.Medium) { mi.meshCompression = ModelImporterMeshCompression.Medium; dirty = true; }
                    // Named cast (Tripo): ~1.4 MB FBX each imported to ~13 MB, mostly per-blend-shape normal and tangent
                    // deltas. Their material is a flat colour map (no normal map), so tangents and blend-shape normals
                    // buy nothing; the face shapes (blink, jaw, visemes) keep their positions.
                    if (path.Contains("/TR-3D/NPC/"))
                    {
                        if (mi.importBlendShapeNormals != ModelImporterNormals.None) { mi.importBlendShapeNormals = ModelImporterNormals.None; dirty = true; }
                        if (mi.importTangents != ModelImporterTangents.None) { mi.importTangents = ModelImporterTangents.None; dirty = true; }
                        if (mi.importAnimation) { mi.importAnimation = false; dirty = true; }
                    }
                    if (dirty) { mi.SaveAndReimport(); changed++; }
                }
            }
            finally { AssetDatabase.StopAssetEditing(); }
            Debug.Log($"[WebBuild] shrink: {changed} importers updated");
        }
    }
}
