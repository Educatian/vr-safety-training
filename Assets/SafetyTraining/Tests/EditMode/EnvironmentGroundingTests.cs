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
                    Gap = item.bounds.Value.min.y - item.site.transform.position.y -
                          ExpectedBaseHeight(item.child.name)
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

        [Test]
        public void CappedRebarBundle_HasSymmetricCapsAtBothEnds()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var bundle = GameObject.Find("Construction Site").transform
                .Find("RealAsset - US_Capped_Rebar_Bundle");
            Assert.That(bundle, Is.Not.Null);

            var capCenters = bundle.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.name.ToLowerInvariant().Contains("impalement cap"))
                .Select(renderer => bundle.InverseTransformPoint(renderer.bounds.center).x)
                .ToArray();
            Assert.That(capCenters.Length, Is.GreaterThanOrEqualTo(24));
            var negative = capCenters.Where(value => value < -0.25f).OrderBy(value => Mathf.Abs(value)).ToArray();
            var positive = capCenters.Where(value => value > 0.25f).OrderBy(value => Mathf.Abs(value)).ToArray();
            Assert.That(negative.Length, Is.EqualTo(positive.Length),
                "Every near-end OSHA cap needs a matching far-end cap.");
            for (var index = 0; index < negative.Length; index++)
                Assert.That(Mathf.Abs(negative[index]), Is.EqualTo(Mathf.Abs(positive[index])).Within(0.08f),
                    $"Cap pair {index} must terminate the same rebar at opposite ends.");

            var capWorldCenters = bundle.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.name.ToLowerInvariant().Contains("impalement cap"))
                .Select(renderer => renderer.bounds.center)
                .ToArray();
            var rods = bundle.GetComponentsInChildren<MeshFilter>(true)
                .Where(filter => filter.sharedMesh != null &&
                    filter.name.ToLowerInvariant().Contains("reinforcing bar"))
                .ToArray();
            Assert.That(rods, Has.Length.EqualTo(12));
            foreach (var rod in rods)
            {
                var meshBounds = rod.sharedMesh.bounds;
                var localAxis = meshBounds.size.x >= meshBounds.size.y &&
                                meshBounds.size.x >= meshBounds.size.z
                    ? Vector3.right
                    : meshBounds.size.y >= meshBounds.size.z ? Vector3.up : Vector3.forward;
                var halfLength = Vector3.Scale(meshBounds.extents, localAxis).magnitude;
                var endpointA = rod.transform.TransformPoint(meshBounds.center + localAxis * halfLength);
                var endpointB = rod.transform.TransformPoint(meshBounds.center - localAxis * halfLength);
                Assert.That(capWorldCenters.Min(center => Vector3.Distance(center, endpointA)),
                    Is.LessThan(0.16f), $"{rod.name} endpoint A needs an attached cap.");
                Assert.That(capWorldCenters.Min(center => Vector3.Distance(center, endpointB)),
                    Is.LessThan(0.16f), $"{rod.name} endpoint B needs an attached cap.");
            }
        }

        [Test]
        public void CustomSafetyProps_MatchHumanRelativeRealWorldHeights()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var expected = new (string Name, float MinHeight, float MaxHeight)[]
            {
                ("US_Modular_Formwork_Panel", 2.7f, 3.0f),
                ("US_Capped_Rebar_Bundle", 0.6f, 0.9f),
                ("US_Adjustable_Shoring_Rack", 2.7f, 3.1f),
                ("US_Electric_Warehouse_Forklift", 2.1f, 2.6f),
                ("US_Selective_Pallet_Rack_Bay", 3.0f, 3.7f),
                ("US_Loading_Dock_Leveler", 0.45f, 0.65f),
                ("US_ABC_Fire_Extinguisher", 0.5f, 0.75f),
                ("US_Recessed_Fire_Hose_Cabinet", 0.9f, 1.2f),
                ("US_Commercial_Emergency_Exit_Door", 2.4f, 2.9f),
                ("US_275_Gallon_IBC_Tote", 1.1f, 1.6f),
                ("US_Emergency_Eyewash_Shower", 2.2f, 2.7f),
                ("US_Flammable_Liquid_Cabinet", 1.6f, 2.0f),
                ("US_NEMA_Electrical_Panel", 1.8f, 2.3f),
                ("US_Lockout_Tagout_Station", 0.55f, 0.85f),
                ("US_Safety_Disconnect_Switch", 0.65f, 1.0f)
            };
            var transforms = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None);

            foreach (var item in expected)
            {
                var instances = transforms.Where(transform =>
                    transform.name == $"RealAsset - {item.Name}").ToArray();
                Assert.That(instances, Is.Not.Empty, item.Name);
                foreach (var instance in instances)
                {
                    var bounds = CombinedBounds(instance);
                    Assert.That(bounds.HasValue, Is.True, item.Name);
                    Assert.That(bounds.Value.size.y,
                        Is.InRange(item.MinHeight, item.MaxHeight),
                        $"{item.Name} must remain credible beside a 1.75 m learner/NPC.");
                }
            }
        }
        [Test]
        public void DistinctCustomProps_HaveIndependentFootprintsAndAccessClearance()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var siteNames = new[]
            {
                "Construction Site", "Warehouse", "Fire Response",
                "Chemical Processing", "Electrical Maintenance"
            };

            foreach (var siteName in siteNames)
            {
                var site = GameObject.Find(siteName);
                var props = site.transform.Cast<Transform>()
                    .Where(child => child.name.StartsWith("RealAsset - US_"))
                    .Select(child => (child, bounds: CombinedBounds(child)))
                    .Where(item => item.bounds.HasValue)
                    .ToArray();

                for (var first = 0; first < props.Length; first++)
                for (var second = first + 1; second < props.Length; second++)
                {
                    var a = props[first].bounds.Value;
                    var b = props[second].bounds.Value;
                    var gapX = Mathf.Max(0f, Mathf.Max(a.min.x - b.max.x, b.min.x - a.max.x));
                    var gapZ = Mathf.Max(0f, Mathf.Max(a.min.z - b.max.z, b.min.z - a.max.z));
                    var planarGap = Mathf.Sqrt(gapX * gapX + gapZ * gapZ);
                    Assert.That(planarGap, Is.GreaterThanOrEqualTo(0.35f),
                        $"{siteName}: {props[first].child.name} and {props[second].child.name} " +
                        $"need separate footprints and an approach gap; actual {planarGap:F2} m.");
                }
            }
        }

        [Test]
        public void CustomProps_StayInsideOpaqueHoardingWithoutNpcOverlap()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var props = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(item => item.name.StartsWith("RealAsset - US_")).ToArray();

            foreach (var prop in props)
            {
                var site = SiteAncestor(prop);
                Assert.That(site, Is.Not.Null, prop.name);
                var bounds = CombinedBounds(prop);
                Assert.That(bounds.HasValue, Is.True, prop.name);
                var localMin = site.InverseTransformPoint(bounds.Value.min);
                var localMax = site.InverseTransformPoint(bounds.Value.max);
                Assert.That(localMin.x, Is.GreaterThanOrEqualTo(-4.75f), prop.name);
                Assert.That(localMax.x, Is.LessThanOrEqualTo(4.75f), prop.name);
                Assert.That(localMin.z, Is.GreaterThanOrEqualTo(-4.12f), prop.name);
                Assert.That(localMax.z, Is.LessThanOrEqualTo(4.22f), prop.name);

                var coach = site.Find("Rocketbox Safety Coach");
                var coachBounds = coach == null ? null : CombinedBounds(coach);
                if (coachBounds.HasValue)
                    Assert.That(bounds.Value.Intersects(coachBounds.Value), Is.False,
                        $"{prop.name} must not mask or intersect the NPC.");
            }

            var construction = GameObject.Find("Construction Site");
            Assert.That(construction.GetComponentsInChildren<Transform>(true)
                .Any(item => item.name.Contains("Gantry Lift Frame")), Is.False,
                "A second overhead crane at the evidence point would create mesh-on-mesh masking.");

            var warehouse = GameObject.Find("Warehouse");
            Assert.That(warehouse.transform.Find("RealEnvironment - Warehouse Separation Fence"), Is.Null,
                "The redundant fence would mask the dedicated pallet-rack access face.");

            var chemical = GameObject.Find("Chemical Processing");
            var flammableCabinet = chemical.transform.Find("RealAsset - US_Flammable_Liquid_Cabinet");
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(flammableCabinet.localEulerAngles.y, 180f)),
                Is.LessThan(0.5f), "The flammable cabinet must expose its doors and hazard label.");

            var fire = GameObject.Find("Fire Response");
            Assert.That(fire.transform.Find("RealEnvironment - Industrial Roller Door"), Is.Null,
                "The redundant roller door would mask the dedicated emergency exit.");
            var electrical = GameObject.Find("Electrical Maintenance");
            Assert.That(electrical.transform.Find("RealEnvironment - Utility Cabinet A"), Is.Null);
            Assert.That(electrical.transform.Find("RealEnvironment - Utility Cabinet B"), Is.Null);
        }

        [Test]
        public void FireExtinguisher_HasReadableEnglishLabelAndPressureGaugeFace()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var extinguisher = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.name == "RealAsset - US_ABC_Fire_Extinguisher");
            Assert.That(extinguisher, Is.Not.Null);
            var renderers = extinguisher.GetComponentsInChildren<Renderer>(true);
            Assert.That(renderers.Any(renderer =>
                renderer.name.ToLowerInvariant().Contains("english label abc")), Is.True);
            Assert.That(renderers.Any(renderer => renderer.name == "Pressure Gauge Face"), Is.True);
            Assert.That(renderers.Any(renderer => renderer.name == "Pressure Gauge Needle"), Is.True);
        }

        [Test]
        public void WallMountedSafetyEquipment_UsesErgonomicBaseHeights()
        {
            EditorSceneManager.OpenScene(ScenePath);
            var expected = new (string Name, float BaseHeight)[]
            {
                ("US_Recessed_Fire_Hose_Cabinet", 0.75f),
                ("US_NEMA_Electrical_Panel", 0.35f),
                ("US_Lockout_Tagout_Station", 1.0f),
                ("US_Safety_Disconnect_Switch", 0.85f)
            };
            foreach (var item in expected)
            {
                var prop = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                    .Single(transform => transform.name == $"RealAsset - {item.Name}");
                var site = SiteAncestor(prop);
                var bounds = CombinedBounds(prop);
                Assert.That(bounds.Value.min.y - site.position.y,
                    Is.EqualTo(item.BaseHeight).Within(0.04f), item.Name);
                if (item.Name is "US_Recessed_Fire_Hose_Cabinet" or
                    "US_Lockout_Tagout_Station" or "US_Safety_Disconnect_Switch")
                    Assert.That(Mathf.Abs(Mathf.DeltaAngle(prop.localEulerAngles.y, 180f)),
                        Is.LessThan(0.5f), $"{item.Name} must expose its detailed front to the learner.");
            }
        }

        static float ExpectedBaseHeight(string name)
        {
            return name switch
            {
                "RealAsset - US_Recessed_Fire_Hose_Cabinet" => 0.75f,
                "RealAsset - US_NEMA_Electrical_Panel" => 0.35f,
                "RealAsset - US_Lockout_Tagout_Station" => 1.0f,
                "RealAsset - US_Safety_Disconnect_Switch" => 0.85f,
                _ => 0.012f
            };
        }

        static Transform SiteAncestor(Transform item)
        {
            while (item != null)
            {
                if (item.name is "Construction Site" or "Warehouse" or "Fire Response" or
                    "Chemical Processing" or "Electrical Maintenance")
                    return item;
                item = item.parent;
            }
            return null;
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
