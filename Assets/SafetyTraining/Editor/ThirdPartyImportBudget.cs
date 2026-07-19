using UnityEditor;

namespace SafetyTraining.Editor
{
    public sealed class ThirdPartyImportBudget : AssetPostprocessor
    {
        const int DefaultTextureBudget = 1024;
        const int HdriTextureBudget = 512;
        const int UserInterfaceTextureBudget = 512;

        static bool IsThirdPartyAsset(string path)
        {
            return path.StartsWith("Assets/ThirdParty/PolyHaven/") ||
                   path.StartsWith("Assets/ThirdParty/Kenney/") ||
                   path.StartsWith("Assets/ThirdParty/MicrosoftRocketbox/");
        }

        static bool IsRocketboxAsset(string path)
        {
            return path.StartsWith("Assets/ThirdParty/MicrosoftRocketbox/");
        }

        static bool IsAnimatedCharacterAsset(string path)
        {
            return IsRocketboxAsset(path) ||
                   path.StartsWith("Assets/ThirdParty/UnityPeopleSansPeople/Animations/");
        }

        public static void ReimportBudgetedAssets()
        {
            AssetDatabase.ImportAsset("Assets/ThirdParty/PolyHaven", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/ThirdParty/Kenney", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.ImportAsset("Assets/ThirdParty/MicrosoftRocketbox", ImportAssetOptions.ImportRecursive | ImportAssetOptions.ForceUpdate);
            AssetDatabase.SaveAssets();
        }

        void OnPreprocessTexture()
        {
            if (!IsThirdPartyAsset(assetPath))
                return;

            var importer = (TextureImporter)assetImporter;
            var lowerPath = assetPath.ToLowerInvariant();
            var isHdri = lowerPath.EndsWith(".hdr") || lowerPath.Contains("/hdri/");
            var isUi = lowerPath.Contains("/ui-pack-");
            var maxSize = isHdri ? HdriTextureBudget : isUi ? UserInterfaceTextureBudget : DefaultTextureBudget;

            importer.maxTextureSize = maxSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.compressionQuality = 50;
            importer.mipmapEnabled = !isUi;
            importer.isReadable = false;

            if (!isHdri)
            {
                var standalone = importer.GetPlatformTextureSettings("Standalone");
                standalone.overridden = true;
                standalone.maxTextureSize = maxSize;
                standalone.textureCompression = TextureImporterCompression.Compressed;
                standalone.compressionQuality = 50;
                standalone.crunchedCompression = true;
                importer.SetPlatformTextureSettings(standalone);
            }

            if (lowerPath.Contains("_nor") || lowerPath.Contains("normal"))
            {
                importer.textureType = TextureImporterType.NormalMap;
                importer.convertToNormalmap = false;
            }
        }

        void OnPreprocessModel()
        {
            if (!IsThirdPartyAsset(assetPath) && !IsAnimatedCharacterAsset(assetPath))
                return;

            var importer = (ModelImporter)assetImporter;
            importer.isReadable = false;
            importer.meshCompression = ModelImporterMeshCompression.Medium;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;

            if (!IsAnimatedCharacterAsset(assetPath))
            {
                importer.importAnimation = false;
                importer.importBlendShapes = false;
            }
        }
    }
}
