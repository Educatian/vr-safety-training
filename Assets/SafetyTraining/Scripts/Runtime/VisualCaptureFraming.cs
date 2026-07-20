using System.Linq;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    internal static class VisualCaptureFraming
    {
        public static bool TryGetBounds(GameObject target, out Bounds bounds)
        {
            var renderers = target.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.enabled && renderer.gameObject.activeInHierarchy &&
                                   renderer.GetComponent<TextMesh>() == null &&
                                   renderer.GetComponent<BillboardLabel>() == null)
                .ToArray();
            if (renderers.Length == 0)
            {
                bounds = default;
                return false;
            }

            bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Length; index++)
                bounds.Encapsulate(renderers[index].bounds);
            return true;
        }
    }
}
