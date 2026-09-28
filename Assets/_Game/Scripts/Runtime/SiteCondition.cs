using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    public sealed class SiteCondition : MonoBehaviour
    {
        [SerializeField] private string conditionId;
        [SerializeField] private string displayName;
        [SerializeField] private string explanation;
        [SerializeField] private bool isHazard;
        [SerializeField] private EnergySource energy;
        [SerializeField] private CpArea area;
        [SerializeField] private int probability = 3;
        [SerializeField] private int severity = 3;
        [SerializeField] private GameObject unresolved;
        [SerializeField] private GameObject resolved;
        public string Id => conditionId;
        public string DisplayName => displayName;
        public string Explanation => explanation;
        public bool IsHazard => isHazard;
        public Bounds PhotoBounds => GetComponent<Collider>().bounds;
        public HazardSpec Spec => new HazardSpec(conditionId, isHazard, energy,
            area == CpArea.FallProtection ? FocusFour.Falls : FocusFour.None,
            area, probability, severity, ControlLevel.Engineering,
            triggerAtSeconds: 510, lapseAfterSeconds: 90);

        public void Configure(string id, string title, string why, bool hazard,
            EnergySource source, CpArea category, int p, int s, GameObject before, GameObject after)
        {
            conditionId = id; displayName = title; explanation = why; isHazard = hazard;
            energy = source; area = category; probability = p; severity = s;
            unresolved = before; resolved = after;
            if (resolved != null) resolved.SetActive(false);
        }

        public void ShowControl()
        {
            if (unresolved != null) unresolved.SetActive(false);
            if (resolved != null) resolved.SetActive(true);
        }
    }
}
