using System.Linq;
using NUnit.Framework;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class EnvironmentGroundingTests
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";

        [Test]
        public void DirectImportedEnvironmentProps_TouchTheirSiteFloor()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var siteNames = new[]
            {
                "Construction Site", "Warehouse", "Fire Response",
                "Chemical Processing", "Electrical Maintenance"
            };
            var failures = siteNames
                .Select(GameObject.Find)
                .Where(site => site != null)
                .SelectMany(site => site.transform.Cast<Transform>()
                    .Where(child => child.name.StartsWith("RealAsset - "))
                    .Select(child => (site, child, bounds: CombinedBounds(child))))
                .Where(item => item.bounds.HasValue)
                .Select(item => new
                {
                    Path = $"{item.site.name}/{item.child.name}",
                    Gap = item.bounds.Value.min.y - item.site.transform.position.y
                })
                .Where(item => Mathf.Abs(item.Gap) > 0.04f)
                .OrderByDescending(item => Mathf.Abs(item.Gap))
                .ToArray();

            Assert.That(failures, Is.Empty,
                "Imported site props must contact the site floor. Offsets: " +
                string.Join(", ", failures.Select(item => $"{item.Path}={item.Gap:F3}m")));
        }

        [Test]
        public void PortableLadders_AreStoredOnTheGroundInsteadOfUnsupportedUpright()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var ladders = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(item => item.name.StartsWith("RealAsset - ladder_sectioned") &&
                               item.parent != null &&
                               item.parent.name is "Fire Response" or "Electrical Maintenance")
                .ToArray();

            Assert.That(ladders, Has.Length.EqualTo(2));
            Assert.That(ladders.All(item =>
                    Mathf.Abs(Mathf.DeltaAngle(item.localEulerAngles.z, 90f)) <= 2f),
                Is.True, "Portable ladders need a visible support or horizontal ground storage.");
        }

        [Test]
        public void CustomConstructionProps_ArePresentStaticAndQuestScale()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var construction = GameObject.Find("Construction Site");
            var expected = new[]
            {
                "RealAsset - US_Modular_Formwork_Panel",
                "RealAsset - US_Capped_Rebar_Bundle",
                "RealAsset - US_Adjustable_Shoring_Rack"
            };

            foreach (var name in expected)
            {
                var prop = construction.transform.Find(name);
                Assert.That(prop, Is.Not.Null, name);
                Assert.That(prop.GetComponentsInChildren<Transform>(true).All(item => item.gameObject.isStatic),
                    Is.True, $"{name} must support static batching.");
                var triangles = prop.GetComponentsInChildren<MeshFilter>(true)
                    .Where(item => item.sharedMesh != null)
                    .Sum(item => item.sharedMesh.triangles.Length / 3);
                Assert.That(triangles, Is.InRange(100, 12000),
                    $"{name} must stay within the standalone Quest prop budget.");
            }
        }

        [Test]
        public void EverySiteHasItsDistinctUSSafetyPropSetWithinQuestBudget()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var expected = new[]
            {
                "US_Modular_Formwork_Panel", "US_Capped_Rebar_Bundle", "US_Adjustable_Shoring_Rack",
                "US_Electric_Warehouse_Forklift", "US_Selective_Pallet_Rack_Bay", "US_Loading_Dock_Leveler",
                "US_ABC_Fire_Extinguisher", "US_Recessed_Fire_Hose_Cabinet",
                "US_Commercial_Emergency_Exit_Door", "US_275_Gallon_IBC_Tote",
                "US_Emergency_Eyewash_Shower", "US_Flammable_Liquid_Cabinet",
                "US_NEMA_Electrical_Panel", "US_Lockout_Tagout_Station", "US_Safety_Disconnect_Switch"
            };
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

            foreach (var asset in expected)
            {
                var instances = transforms.Where(item => item.name == $"RealAsset - {asset}").ToArray();
                Assert.That(instances, Is.Not.Empty, asset);
                foreach (var instance in instances)
                {
                    var triangles = instance.GetComponentsInChildren<MeshFilter>(true)
                        .Where(item => item.sharedMesh != null)
                        .Sum(item => item.sharedMesh.triangles.Length / 3);
                    Assert.That(triangles, Is.InRange(40, 12000), asset);
                }
            }
        }

        static Bounds? CombinedBounds(Transform root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true)
                .Where(item => item.enabled && item.GetComponent<TextMesh>() == null).ToArray();
            if (renderers.Length == 0)
                return null;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            return bounds;
        }
    }
}
