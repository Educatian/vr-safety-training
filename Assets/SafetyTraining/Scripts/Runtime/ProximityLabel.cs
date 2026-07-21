using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// Shows a world-space label only while the viewer is nearby, so dense prop
    /// areas do not stack a dozen readable texts at once. Checks are staggered
    /// across frames to stay cheap with 100+ labels in a site.
    /// </summary>
    public sealed class ProximityLabel : MonoBehaviour
    {
        [SerializeField, Min(1f)] float visibleDistance = 8f;

        static int nextOffset;
        Renderer labelRenderer;
        float nextCheckAt;

        public void Configure(float distance)
        {
            visibleDistance = distance;
        }

        void Awake()
        {
            labelRenderer = GetComponent<Renderer>();
            nextCheckAt = Time.unscaledTime + (nextOffset++ % 8) * 0.04f;
        }

        void Update()
        {
            if (labelRenderer == null || Time.unscaledTime < nextCheckAt)
                return;
            nextCheckAt = Time.unscaledTime + 0.3f;
            var viewer = Camera.main;
            if (viewer == null)
                return;
            var visible = (viewer.transform.position - transform.position).sqrMagnitude
                          <= visibleDistance * visibleDistance;
            if (labelRenderer.enabled != visible)
                labelRenderer.enabled = visible;
        }
    }
}
