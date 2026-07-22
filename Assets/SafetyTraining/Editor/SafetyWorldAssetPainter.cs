using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SafetyTraining.Editor
{
    internal static class SafetyWorldAssetPainter
    {
        const string ModelRoot = "Assets/ThirdParty/PolyHaven/Models/";
        const string EnvironmentRoot = "Assets/ThirdParty/PolyHaven/Environment/Models/";

        public static void AddModel(Transform parent, string name, string asset, Vector3 position, float size,
            Vector3 rotation)
        {
            var instance = InstantiateModel(parent, name, ModelRoot + asset, position, size, rotation);
            if (instance == null)
                InstantiateModel(parent, name, EnvironmentRoot + asset, position, size, rotation);
        }

        public static void AddAmbientDust(Transform parent, string name, Vector3 position, Color color, int maxParticles)
        {
            var effect = new GameObject($"WorldExpansion - {name}");
            effect.transform.SetParent(parent, false);
            effect.transform.localPosition = position;
            var particles = effect.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.duration = 4f;
            main.startLifetime = 2.4f;
            main.startSpeed = 0.16f;
            main.startSize = 0.18f;
            main.maxParticles = Mathf.Min(maxParticles, 80);
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            var emission = particles.emission;
            emission.rateOverTime = Mathf.Min(maxParticles, 80) * 0.18f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(2.8f, 0.4f, 2.2f);
            var colorModule = particles.colorOverLifetime;
            colorModule.enabled = true;
            colorModule.color = new ParticleSystem.MinMaxGradient(color * 0.85f, color);
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = SharedParticleMaterial(color);
        }

        public static void AddSparkCue(Transform parent, Vector3 position)
        {
            var effect = new GameObject("WorldExpansion - Contained Electrical Spark Cue");
            effect.transform.SetParent(parent, false);
            effect.transform.localPosition = position;
            var particles = effect.AddComponent<ParticleSystem>();
            var main = particles.main;
            main.loop = true;
            main.duration = 2.8f;
            main.startLifetime = 0.18f;
            main.startSpeed = 1.4f;
            main.startSize = 0.035f;
            main.maxParticles = 28;
            main.simulationSpace = ParticleSystemSimulationSpace.Local;
            main.cullingMode = ParticleSystemCullingMode.Automatic;
            var emission = particles.emission;
            emission.rateOverTime = 6f;
            var shape = particles.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 18f;
            shape.radius = 0.08f;
            var renderer = particles.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.material = SharedParticleMaterial(new Color(1f, 0.78f, 0.25f, 0.72f));
        }

        public static void MarkStaticEnvironment(Transform parent)
        {
            foreach (var renderer in parent.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.GetComponent<TextMesh>() != null ||
                    renderer.GetComponentInParent<LODGroup>() != null ||
                    renderer.GetComponentInParent<ParticleSystem>() != null ||
                    renderer.GetComponentInParent<SafetyTraining.Runtime.InspectionTarget>() != null ||
                    renderer.GetComponentInParent<SafetyTraining.Runtime.EvidenceObject>() != null ||
                    renderer.GetComponentInParent<SafetyTraining.Runtime.InquiryDecisionStation>() != null)
                    continue;

                GameObjectUtility.SetStaticEditorFlags(renderer.gameObject,
                    StaticEditorFlags.BatchingStatic | StaticEditorFlags.OccludeeStatic |
                    StaticEditorFlags.ReflectionProbeStatic);
            }
        }

        static GameObject InstantiateModel(Transform parent, string name, string assetPath, Vector3 position,
            float size, Vector3 rotation)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
                return null;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = $"WorldExpansion - RealAsset - {name}";
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(rotation);
            instance.transform.localScale = Vector3.one;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);
            FitLargestDimension(instance, parent, size);
            AddOptimizedSolidCollider(instance, name);
            ConfigureLod(instance);
            return instance;
        }

        static void AddOptimizedSolidCollider(GameObject instance, string displayName)
        {
            // Low-cost box proxies keep large jobsite props physically credible
            // without restoring expensive imported mesh colliders. Open-frame
            // structures remain collider-free so intended circulation stays open.
            var solidKeywords = new[]
            {
                "barrier", "cement", "crate", "pallet", "toolbox", "cart",
                "generator", "drum", "utility box", "ladder", "formwork",
                "material", "chainlink", "perimeter", "gate", "door",
                "cabinet", "hand truck", "delivery stack", "panel row"
            };
            if (!solidKeywords.Any(keyword =>
                    displayName.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0))
                return;

            var renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.GetComponent<TextMesh>() == null).ToArray();
            if (renderers.Length == 0)
                return;
            var worldBounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                worldBounds.Encapsulate(renderer.bounds);

            var localBounds = new Bounds(instance.transform.InverseTransformPoint(worldBounds.center), Vector3.zero);
            foreach (var x in new[] { worldBounds.min.x, worldBounds.max.x })
            foreach (var y in new[] { worldBounds.min.y, worldBounds.max.y })
            foreach (var z in new[] { worldBounds.min.z, worldBounds.max.z })
                localBounds.Encapsulate(instance.transform.InverseTransformPoint(new Vector3(x, y, z)));

            var collider = instance.AddComponent<BoxCollider>();
            collider.center = localBounds.center;
            collider.size = localBounds.size + Vector3.one * 0.04f;
            collider.isTrigger = false;
        }

        static void ConfigureLod(GameObject instance)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.GetComponent<TextMesh>() == null).ToArray();
            if (renderers.Length == 0)
                return;

            var group = instance.GetComponent<LODGroup>();
            if (group == null)
                group = instance.AddComponent<LODGroup>();
            group.SetLODs(new[] { new LOD(0.006f, renderers) });
            group.RecalculateBounds();
        }

        static void FitLargestDimension(GameObject instance, Transform parent, float size)
        {
            var renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0)
                return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            instance.transform.localScale = Vector3.one * (size / Mathf.Max(largest, 0.001f));
            bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            instance.transform.position += Vector3.up * (parent.position.y - bounds.min.y);
        }

        static Material SharedParticleMaterial(Color tint)
        {
            SafetyScenePrimitives.EnsureFolder("Assets/SafetyTraining/GeneratedMaterials/WorldExpansion");
            var key = ColorUtility.ToHtmlStringRGBA(tint);
            var path = $"Assets/SafetyTraining/GeneratedMaterials/WorldExpansion/Particle_{key}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
                return existing;

            var shader = Shader.Find("Particles/Standard Unlit") ?? Shader.Find("Sprites/Default");
            var material = new Material(shader)
            {
                name = $"World Particle {key}",
                color = tint
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
