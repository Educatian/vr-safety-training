using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace Jobsite.Editor
{
    // Tripo FBX (via Blender) embed their PBR textures but Unity left the materials white:
    // (1) Blender wrote the glTF images without a file extension, so extracted files were not textures;
    // (2) nothing bound them to the imported materials. Fix: extract, restore extensions from the file
    // header, and build one URP material per model from Tools/tripo/texture_map.json (glTF material wiring).
    public static class TripoImport
    {
        const string Root = "Assets/_Game/Art/Models/TR-3D";
        const string MatDir = "Assets/_Game/Art/Materials/TR";
        static JObject map;

        [MenuItem("Jobsite/Setup/Extract Tripo Textures")]
        public static void Run()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { Root }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var dir = TexDir(Path.GetFileNameWithoutExtension(path));
                if (!AssetDatabase.IsValidFolder(dir))
                {
                    Directory.CreateDirectory(dir);
                    AssetDatabase.Refresh();
                    ((ModelImporter)AssetImporter.GetAtPath(path)).ExtractTextures(dir);
                }
                FixExtensions(dir);
                CapTextures(dir, IsVehicle(Path.GetFileNameWithoutExtension(path)) ? 1024 : 512);
            }
            AssetDatabase.Refresh();
        }

        // Web download budget (TechSpec): vehicles 1024, props 512; crunch-compressed in the build.
        static bool IsVehicle(string model) => new[] { "Truck", "Crane", "Excavator", "Loader", "Lift", "Roller", "Steer", "Pickup", "Telehandler" }
            .Any(model.Contains);

        static void CapTextures(string dir, int size)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { dir }))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (imp.maxTextureSize == size && imp.crunchedCompression) continue;
                imp.maxTextureSize = size;
                imp.crunchedCompression = true;
                imp.compressionQuality = 50;
                imp.SaveAndReimport();
            }
        }

        static string TexDir(string model) => $"{Root}/Textures/{model}";

        static void FixExtensions(string dir)
        {
            foreach (var file in Directory.GetFiles(dir).Where(f => Path.GetExtension(f) == ""))
            {
                var head = new byte[4];
                using (var s = File.OpenRead(file)) s.Read(head, 0, 4);
                var ext = head[0] == 0xFF && head[1] == 0xD8 ? ".jpg" : head[0] == 0x89 && head[1] == 0x50 ? ".png" : null;
                if (ext == null) continue;
                File.Move(file, file + ext);
                if (File.Exists(file + ".meta")) File.Delete(file + ".meta");
            }
        }

        // One URP/Lit material per Tripo model: BaseMap + Normal from the glTF wiring.
        public static Material ForModel(string smName)
        {
            smName = smName.Replace("_Rig", "");
            Directory.CreateDirectory(MatDir);
            var matPath = $"{MatDir}/M_TR_{smName}.mat";
            var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
            if (mat != null) return mat;
            map ??= JObject.Parse(File.ReadAllText("Tools/tripo/texture_map.json"));
            var entry = map[smName];
            if (entry == null) return null;
            mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            var baseTex = Find(smName, (string)entry["base"]);
            if (baseTex != null) mat.SetTexture("_BaseMap", baseTex);
            var normal = Find(smName, (string)entry["normal"]);
            if (normal != null)
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
                if (imp.textureType != TextureImporterType.NormalMap) { imp.textureType = TextureImporterType.NormalMap; imp.SaveAndReimport(); }
                mat.SetTexture("_BumpMap", normal);
                mat.EnableKeyword("_NORMALMAP");
            }
            mat.SetFloat("_Smoothness", 0.3f);
            AssetDatabase.CreateAsset(mat, matPath);
            return mat;
        }

        static Texture2D Find(string smName, string imageName)
        {
            if (string.IsNullOrEmpty(imageName)) return null;
            foreach (var dir in new[] { TexDir(smName), TexDir(smName + "_Rig") })
                foreach (var ext in new[] { ".jpg", ".png" })
                {
                    var t = AssetDatabase.LoadAssetAtPath<Texture2D>($"{dir}/{imageName}{ext}");
                    if (t != null) return t;
                }
            return null;
        }

        // Replace every material on an instantiated Tripo model (keeps interior parts that have their own).
        public static void Apply(GameObject instance, string smName)
        {
            var mat = ForModel(smName);
            if (mat == null) return;
            foreach (var r in instance.GetComponentsInChildren<Renderer>(true))
            {
                if (r.name.StartsWith("Interior_") || r is ParticleSystemRenderer || r is LineRenderer) continue;
                r.sharedMaterials = Enumerable.Repeat(mat, r.sharedMaterials.Length).ToArray();
            }
        }
    }
}
