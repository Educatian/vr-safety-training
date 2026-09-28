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
        [SerializeField] private FocusFour focusFour;
        [SerializeField] private ControlLevel bestControl = ControlLevel.Engineering;
        [SerializeField] private float triggerAtSeconds = 510;
        [SerializeField] private string cfr;
        [SerializeField] private string requirementPlain;
        [SerializeField] private string threshold;
        [SerializeField] private bool cfrVerified;
        [SerializeField] private GameObject unresolved;
        [SerializeField] private GameObject resolved;
        public string Id => conditionId;
        public string DisplayName => displayName;
        public string Explanation => explanation;
        public bool IsHazard => isHazard;
        public Bounds PhotoBounds => GetComponent<Collider>().bounds;
        public HazardSpec Spec => new HazardSpec(conditionId, isHazard, energy, focusFour,
            area, probability, severity, bestControl,
            triggerAtSeconds: triggerAtSeconds, lapseAfterSeconds: 90)
            .WithStandard(cfr, requirementPlain, threshold, cfrVerified);
        public string Cfr => cfr;
        public string RequirementPlain => requirementPlain;
        public string Threshold => threshold;

        public void SetStandard(string citation, string requirement, string limit, bool verified)
        {
            cfr = citation; requirementPlain = requirement; threshold = limit; cfrVerified = verified;
        }

        public void Configure(string id, string title, string why, bool hazard,
            EnergySource source, CpArea category, int p, int s, GameObject before, GameObject after)
        {
            conditionId = id; displayName = title; explanation = why; isHazard = hazard;
            energy = source; area = category; probability = p; severity = s;
            unresolved = before; resolved = after;
            focusFour = area == CpArea.FallProtection || area == CpArea.Scaffold ? FocusFour.Falls
                : area == CpArea.Excavation ? FocusFour.CaughtIn
                : area == CpArea.StruckBy ? FocusFour.StruckBy
                : area == CpArea.Electrical ? FocusFour.Electrocution : FocusFour.None;
            if (resolved != null) resolved.SetActive(false);
        }

        public void SetControlKey(ControlLevel best, float triggerAt)
        {
            bestControl = best;
            triggerAtSeconds = triggerAt;
        }

        public void ShowControl()
        {
            if (unresolved != null) unresolved.SetActive(false);
            if (resolved != null) resolved.SetActive(true);
        }
    }
}
