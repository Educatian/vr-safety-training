using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    public sealed class SiteExperienceZone : MonoBehaviour
    {
        [SerializeField] TrainingSiteId siteId;
        [SerializeField, Min(1f)] float influenceRadius = 11f;

        public TrainingSiteId SiteId => siteId;
        public float InfluenceRadius => influenceRadius;
        public NpcSiteCompanion Companion { get; private set; }

        public void Configure(TrainingSiteId value, float radius = 11f)
        {
            siteId = value;
            influenceRadius = Mathf.Max(1f, radius);
        }

        public void BindCompanion(NpcSiteCompanion companion)
        {
            Companion = companion;
        }

        public float HorizontalDistance(Vector3 point)
        {
            var delta = point - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }
    }
}
