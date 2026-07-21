using System.Linq;
using NUnit.Framework;
using SafetyTraining.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class GpuFirstEnvironmentTests
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";

        [SetUp]
        public void OpenTrainingScene() => EditorSceneManager.OpenScene(ScenePath);

        [Test]
        public void SceneUsesRealHdriCubemapInsteadOfProceduralSky()
        {
            Assert.That(RenderSettings.skybox, Is.Not.Null);
            Assert.That(RenderSettings.skybox.shader.name, Is.EqualTo("Skybox/Cubemap"));
            Assert.That(RenderSettings.skybox.GetTexture("_Tex"), Is.TypeOf<Cubemap>());
            Assert.That(RenderSettings.fog, Is.True);
        }

        [Test]
        public void GeneratedEnvironmentMaterialsEnableGpuInstancing()
        {
            var materials = AssetDatabase.FindAssets("t:Material",
                    new[] { "Assets/SafetyTraining/GeneratedMaterials/RealEnvironment" })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Select(AssetDatabase.LoadAssetAtPath<Material>)
                .Where(item => item != null && item.shader != null && item.shader.name == "Standard")
                .ToArray();

            Assert.That(materials, Is.Not.Empty);
            Assert.That(materials.All(item => item.enableInstancing), Is.True);
        }

        [Test]
        public void OperationalSubzoneAssetsKeepGpuFriendlyDistanceCulling()
        {
            var assets = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(item => item.name.Contains("Equipment Anchor")).ToArray();
            Assert.That(assets, Has.Length.EqualTo(27));
            Assert.That(assets.All(item => item.GetComponent<LODGroup>() != null), Is.True);
        }
    }
}