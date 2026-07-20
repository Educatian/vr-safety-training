using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Editor
{
    internal static class SafetyScenePrimitives
    {
        const string MaterialFolder = "Assets/SafetyTraining/GeneratedMaterials";

        public static GameObject Primitive(
            PrimitiveType type,
            string name,
            Transform parent,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            var item = GameObject.CreatePrimitive(type);
            item.name = name;
            item.transform.SetParent(parent, false);
            item.transform.localPosition = position;
            item.transform.localScale = scale;
            item.GetComponent<Renderer>().sharedMaterial = Material(color);
            return item;
        }

        public static TextMesh Label(string text, Transform parent, Vector3 position, float size)
        {
            var label = new GameObject($"Label - {text}");
            label.transform.SetParent(parent, false);
            label.transform.localPosition = position;
            label.transform.localRotation = Quaternion.identity;
            var mesh = label.AddComponent<TextMesh>();
            mesh.text = text;
            mesh.characterSize = size;
            mesh.anchor = TextAnchor.MiddleCenter;
            mesh.alignment = TextAlignment.Center;
            mesh.color = Color.white;
            return mesh;
        }

        public static InspectionTarget Target(
            GameObject item,
            TrainingSiteId site,
            string id,
            bool hazard,
            string title,
            string rationale,
            string action)
        {
            if (item.GetComponent<XRSimpleInteractable>() == null)
                item.AddComponent<XRSimpleInteractable>();
            if (item.GetComponent<InteractiveHoverFeedback>() == null)
                item.AddComponent<InteractiveHoverFeedback>();
            var target = item.AddComponent<InspectionTarget>();
            target.Configure(site, id, hazard, title, rationale, action);
            return target;
        }

        public static void EnsureFolder(string path)
        {
            var parts = path.Split('/');
            var current = parts[0];
            for (var index = 1; index < parts.Length; index++)
            {
                var next = $"{current}/{parts[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, parts[index]);
                current = next;
            }
        }

        static Material Material(Color color)
        {
            EnsureFolder(MaterialFolder);
            var path = $"{MaterialFolder}/{ColorUtility.ToHtmlStringRGB(color)}.mat";
            var existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null)
            {
                existing.enableInstancing = true;
                EditorUtility.SetDirty(existing);
                return existing;
            }

            var material = new Material(Shader.Find("Standard"))
            {
                color = color,
                enableInstancing = true
            };
            AssetDatabase.CreateAsset(material, path);
            return material;
        }
    }
}
