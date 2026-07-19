using NUnit.Framework;
using UnityEditor;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class ImportBudgetTests
    {
        [Test]
        public void RealPropTexturesStayWithinVrBuildBudget()
        {
            var texturePaths = AssetDatabase.FindAssets("t:Texture", new[] { "Assets/ThirdParty/PolyHaven" });
            Assert.That(texturePaths, Is.Not.Empty);

            foreach (var guid in texturePaths)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.Contains("/Environment/HDRI/"))
                    continue;

                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.maxTextureSize, Is.LessThanOrEqualTo(1024), path);
                Assert.That(importer.textureCompression, Is.Not.EqualTo(TextureImporterCompression.Uncompressed), path);

                var standalone = importer.GetPlatformTextureSettings("Standalone");
                Assert.That(standalone.overridden, Is.True, path);
                Assert.That(standalone.maxTextureSize, Is.LessThanOrEqualTo(1024), path);
                Assert.That(standalone.crunchedCompression, Is.True, path);
            }
        }

        [Test]
        public void RealPropModelsUseVrFriendlyImporterSettings()
        {
            var modelPaths = AssetDatabase.FindAssets("t:Model", new[] { "Assets/ThirdParty/PolyHaven" });
            Assert.That(modelPaths, Is.Not.Empty);

            foreach (var guid in modelPaths)
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                Assert.That(importer, Is.Not.Null, path);
                Assert.That(importer.isReadable, Is.False, path);
                Assert.That(importer.meshCompression, Is.EqualTo(ModelImporterMeshCompression.Medium), path);
                Assert.That(importer.importAnimation, Is.False, path);
                Assert.That(importer.importCameras, Is.False, path);
                Assert.That(importer.importLights, Is.False, path);
            }
        }
    }
}
