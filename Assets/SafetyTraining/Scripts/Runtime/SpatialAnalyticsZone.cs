using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class SpatialAnalyticsZone : MonoBehaviour
    {
        static readonly List<SpatialAnalyticsZone> Registered = new();

        [SerializeField] TrainingSiteId siteId;
        [SerializeField] string zoneId = "unassigned";
        [SerializeField] string displayName = "Work Area";

        BoxCollider volume;

        public TrainingSiteId SiteId => siteId;
        public string ZoneId => zoneId;
        public string DisplayName => displayName;

        public static SpatialAnalyticsZone FindContaining(TrainingSiteId site, Vector3 worldPosition)
        {
            for (var index = 0; index < Registered.Count; index++)
            {
                var candidate = Registered[index];
                if (candidate != null && candidate.siteId == site && candidate.Contains(worldPosition))
                    return candidate;
            }

            var zones = FindObjectsByType<SpatialAnalyticsZone>(FindObjectsSortMode.None);
            for (var index = 0; index < zones.Length; index++)
            {
                var candidate = zones[index];
                if (candidate.siteId == site && candidate.Contains(worldPosition))
                    return candidate;
            }
            return null;
        }

        public void Configure(TrainingSiteId site, string id, string title)
        {
            siteId = site;
            zoneId = id;
            displayName = title;
            EnsureVolume();
        }

        public bool Contains(Vector3 worldPosition)
        {
            EnsureVolume();
            var local = transform.InverseTransformPoint(worldPosition) - volume.center;
            var half = volume.size * 0.5f;
            return Mathf.Abs(local.x) <= half.x &&
                   Mathf.Abs(local.y) <= half.y &&
                   Mathf.Abs(local.z) <= half.z;
        }

        void OnEnable()
        {
            if (!Registered.Contains(this))
                Registered.Add(this);
        }

        void OnDisable()
        {
            Registered.Remove(this);
        }

        void Awake()
        {
            EnsureVolume();
        }

        void EnsureVolume()
        {
            if (volume != null)
                return;
            volume = GetComponent<BoxCollider>();
            volume.isTrigger = true;
        }
    }
}
