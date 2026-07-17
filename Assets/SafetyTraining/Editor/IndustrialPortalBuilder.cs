using SafetyTraining.Core;
using SafetyTraining.Runtime;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Editor
{
    internal static class IndustrialPortalBuilder
    {
        const string EnvironmentModels = "Assets/ThirdParty/PolyHaven/Environment/Models/";
        const string PropModels = "Assets/ThirdParty/PolyHaven/Models/";

        public static void CreateModePortal(Transform parent, string number, string title, string subtitle,
            TrainingSiteId site, Vector3 position, Vector3 destination, Color accent, string propAsset,
            Vector3 propRotation)
        {
            var root = new GameObject($"Portal - {title}");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 1.15f, 0f);
            collider.size = new Vector3(2.05f, 2.3f, 0.72f);
            root.AddComponent<XRSimpleInteractable>();
            root.AddComponent<SitePortal>().Configure(destination, site);

            AddFittedModel(root.transform, EnvironmentModels + "utility_box_01_1k.fbx", "Industrial Access Kiosk",
                Vector3.zero, Vector3.zero, 2.05f);
            AddFittedModel(root.transform, PropModels + propAsset, $"Mode Prop - {title}",
                new Vector3(-0.73f, 0.06f, -0.45f), propRotation, 0.62f);
            BuildDisplay(root.transform, number, title, subtitle, accent, false);
        }

        public static void CreateReturnPortal(Transform parent, TrainingSiteId site, Vector3 position,
            Vector3 destination, Color accent)
        {
            var root = new GameObject("Return Portal - TRAINING HUB");
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.center = new Vector3(0f, 0.74f, 0f);
            collider.size = new Vector3(1.4f, 1.48f, 0.65f);
            root.AddComponent<XRSimpleInteractable>();
            root.AddComponent<SitePortal>().ConfigureReturn(destination, site);

            AddFittedModel(root.transform, EnvironmentModels + "utility_box_01_1k.fbx", "Industrial Return Kiosk",
                Vector3.zero, Vector3.zero, 1.3f);
            BuildDisplay(root.transform, "HUB", "RETURN", "TRAINING CAMPUS", accent, true);
        }

        static void BuildDisplay(Transform parent, string number, string title, string subtitle, Color accent,
            bool compact)
        {
            var scale = compact ? 0.66f : 1f;
            var centerY = compact ? 0.82f : 1.34f;
            var panel = Visual(PrimitiveType.Cube, "Recessed Safety Display", parent,
                new Vector3(0f, centerY, -0.42f), new Vector3(1.72f * scale, 1.25f * scale, 0.07f),
                new Color(0.025f, 0.045f, 0.06f));
            var frame = Visual(PrimitiveType.Cube, "PBR Metal Display Frame", parent,
                new Vector3(0f, centerY, -0.36f), new Vector3(1.96f * scale, 1.47f * scale, 0.06f), Color.white);
            frame.GetComponent<Renderer>().sharedMaterial = RealEnvironmentMaterials.MetalSheet;
            frame.transform.SetSiblingIndex(panel.transform.GetSiblingIndex());

            Visual(PrimitiveType.Cube, "Illuminated Header", parent,
                new Vector3(0f, centerY + 0.48f * scale, -0.47f),
                new Vector3(1.62f * scale, 0.11f * scale, 0.035f), accent);
            Visual(PrimitiveType.Cube, "Status Rail Left", parent,
                new Vector3(-0.77f * scale, centerY - 0.02f, -0.47f),
                new Vector3(0.055f * scale, 0.83f * scale, 0.035f), accent * 0.72f);
            Visual(PrimitiveType.Cube, "Status Rail Right", parent,
                new Vector3(0.77f * scale, centerY - 0.02f, -0.47f),
                new Vector3(0.055f * scale, 0.83f * scale, 0.035f), accent * 0.72f);

            Label(number, parent, new Vector3(-0.62f * scale, centerY + 0.3f * scale, -0.515f),
                0.095f * scale, accent, TextAnchor.MiddleLeft);
            Label(title, parent, new Vector3(0f, centerY + 0.08f * scale, -0.515f),
                (title.Length > 12 ? 0.08f : 0.095f) * scale, Color.white, TextAnchor.MiddleCenter);
            Label(subtitle, parent, new Vector3(0f, centerY - 0.18f * scale, -0.515f),
                0.066f * scale, new Color(0.78f, 0.88f, 0.92f), TextAnchor.MiddleCenter);
            Label(compact ? "SELECT TO RETURN" : "CLICK / SELECT TO ENTER", parent,
                new Vector3(0f, centerY - 0.39f * scale, -0.515f), 0.06f * scale,
                Color.Lerp(accent, Color.white, 0.25f),
                TextAnchor.MiddleCenter);

            var beacon = new GameObject("Portal Interaction Beacon");
            beacon.transform.SetParent(parent, false);
            beacon.transform.localPosition = new Vector3(0f, centerY + 0.78f * scale, -0.35f);
            var light = beacon.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = accent;
            light.intensity = 0.9f;
            light.range = compact ? 1.6f : 2.3f;
            light.shadows = LightShadows.None;
        }

        static GameObject Visual(PrimitiveType type, string name, Transform parent, Vector3 position,
            Vector3 scale, Color color)
        {
            var item = SafetyScenePrimitives.Primitive(type, name, parent, position, scale, color);
            Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }

        static void Label(string text, Transform parent, Vector3 position, float size, Color color,
            TextAnchor anchor)
        {
            var label = SafetyScenePrimitives.Label(text, parent, position, size);
            label.anchor = anchor;
            label.color = color;
            label.fontStyle = FontStyle.Bold;
        }

        static void AddFittedModel(Transform parent, string assetPath, string name, Vector3 localPosition,
            Vector3 rotation, float targetSize)
        {
            var asset = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);
            if (asset == null)
                return;

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(asset, parent);
            instance.name = name;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(rotation);
            instance.transform.localScale = Vector3.one;
            foreach (var child in instance.GetComponentsInChildren<Transform>(true))
                child.gameObject.SetActive(true);
            foreach (var collider in instance.GetComponentsInChildren<Collider>(true))
                Object.DestroyImmediate(collider);

            var renderers = instance.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled).ToArray();
            if (renderers.Length == 0)
                return;
            var bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            var largest = Mathf.Max(bounds.size.x, bounds.size.y, bounds.size.z);
            instance.transform.localScale = Vector3.one * (targetSize / Mathf.Max(largest, 0.001f));

            bounds = renderers[0].bounds;
            foreach (var renderer in renderers.Skip(1))
                bounds.Encapsulate(renderer.bounds);
            instance.transform.position += Vector3.up * (parent.position.y + localPosition.y - bounds.min.y);
        }
    }
}
