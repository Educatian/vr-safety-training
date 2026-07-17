using SafetyTraining.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SafetyTraining.Editor
{
    static class PlacementTargetFactory
    {
        const string MarkerMaterialPath = "Assets/SafetyTraining/GeneratedMaterials/2EC7A8.mat";
        const int SegmentCount = 40;

        public static GameObject Create(Transform parent, string actionObjectName, Vector3 localPosition,
            string actionTitle, float acceptanceRadius)
        {
            var target = new GameObject($"Placement Target - {actionObjectName}");
            target.transform.SetParent(parent, false);
            target.transform.localPosition = new Vector3(localPosition.x, 0.035f, localPosition.z);

            var ring = target.AddComponent<LineRenderer>();
            ring.useWorldSpace = false;
            ring.loop = true;
            ring.positionCount = SegmentCount;
            ring.startWidth = 0.045f;
            ring.endWidth = 0.045f;
            ring.shadowCastingMode = ShadowCastingMode.Off;
            ring.receiveShadows = false;
            ring.sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>(MarkerMaterialPath);
            var visualRadius = Mathf.Clamp(acceptanceRadius * 0.72f, 0.5f, 0.82f);
            for (var index = 0; index < SegmentCount; index++)
            {
                var angle = index * Mathf.PI * 2f / SegmentCount;
                ring.SetPosition(index, new Vector3(Mathf.Cos(angle) * visualRadius, 0f,
                    Mathf.Sin(angle) * visualRadius));
            }

            var labelAnchor = new GameObject("Placement Target Label");
            labelAnchor.transform.SetParent(target.transform, false);
            labelAnchor.transform.localPosition = new Vector3(0f, 0.22f, 0f);
            labelAnchor.AddComponent<BillboardLabel>();
            var label = SafetyScenePrimitives.Label($"DROP HERE\n{actionTitle.ToUpperInvariant()}",
                labelAnchor.transform, Vector3.zero, 0.038f);
            label.color = new Color(0.216f, 0.839f, 0.753f);
            target.SetActive(false);
            return target;
        }
    }
}
