using System.Linq;
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
        [SerializeField] private bool requiresStopWork;
        [SerializeField] private string cfr;
        [SerializeField] private string requirementPlain;
        [SerializeField] private string threshold;
        [SerializeField] private bool cfrVerified;
        [SerializeField] private GearId[] instruments = new GearId[0];   // gear that yields a reading here
        [SerializeField] private string[] readings = new string[0];
        [SerializeField] private float photoRange;                        // 0 = career default; overhead hazards set more
        [SerializeField] private GameObject unresolved;
        [SerializeField] private GameObject resolved;
        public string Id => conditionId;
        public string DisplayName => displayName;
        // What the tablet shows before the learner has judged this condition: never the diagnosis.
        public string NeutralName => ConditionNames.NeutralName(conditionId);
        public bool ShowingControl { get; private set; }
        public string Explanation => explanation;
        public bool IsHazard => isHazard;
        public Bounds PhotoBounds => GetComponent<Collider>().bounds;
        public HazardSpec Spec => new HazardSpec(conditionId, isHazard, energy, focusFour,
            area, probability, severity, ControlKits.BestFeasible(conditionId, bestControl),
            triggerAtSeconds: triggerAtSeconds, lapseAfterSeconds: 90, requiresStopWork: requiresStopWork)
            .WithStandard(cfr, requirementPlain, threshold, cfrVerified);
        public string Cfr => cfr;
        public string RequirementPlain => requirementPlain;
        public string Threshold => threshold;

        public void SetReading(GearId instrument, string text)
        {
            var i = System.Array.IndexOf(instruments, instrument);
            if (i < 0) { instruments = instruments.Append(instrument).ToArray(); readings = readings.Append(text).ToArray(); }
            else readings[i] = text;
        }

        // Raw reading for this instrument (Core InstrumentTable: values, never the verdict). Conditions the table
        // doesn't know use the serialized reading; the laser falls back to the measured size of the condition.
        public string Reading(GearId instrument)
        {
            if (InstrumentTable.Knows(conditionId))
            {
                var raw = InstrumentTable.Get(conditionId, instrument, ShowingControl);
                if (raw != null || instrument != GearId.LaserMeasure) return raw;
                var m = PhotoBounds.size * 3.281f;
                return $"Measured {Mathf.Max(m.x, m.z):F1} ft wide x {m.y:F1} ft high.";
            }
            var i = System.Array.IndexOf(instruments, instrument);
            if (i >= 0) return readings[i];
            if (instrument != GearId.LaserMeasure) return null;
            var s = PhotoBounds.size * 3.281f;
            return $"Measured {Mathf.Max(s.x, s.z):F1} ft wide x {s.y:F1} ft high.";
        }

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

        public void SetControlKey(ControlLevel best, float triggerAt, bool stopWorkRequired = false)
        {
            requiresStopWork = stopWorkRequired;
            bestControl = best;
            triggerAtSeconds = triggerAt;
        }

        public EnergySource Energy => energy;
        public float PhotoRange => photoRange;
        public void SetPhotoRange(float metres) => photoRange = metres;

        // Replay variety (GDD §6): this run the crew did it right, so the hazard shows as its compliant twin.
        public void MakeCompliant()
        {
            ShowControl();
            isHazard = false;
            displayName = ConditionNames.Compliant(conditionId);   // the hazard title would contradict what is on site
            explanation = "This one is compliant today: " + (string.IsNullOrEmpty(requirementPlain) ? "the control is in place." : requirementPlain);
        }

        public void ShiftTrigger(float seconds) => triggerAtSeconds = Mathf.Clamp(triggerAtSeconds + seconds, 240f, 580f);

        // animate: the installed control snaps in (0.28 s, GDD §7 "Satisfaction"); replay twins appear without it.
        public void ShowControl(bool animate = false)
        {
            ShowingControl = true;
            if (unresolved != null) unresolved.SetActive(false);
            if (resolved != null) resolved.SetActive(true);
            if (animate && resolved != null && isActiveAndEnabled) StartCoroutine(Snap(resolved.transform));
        }

        private static System.Collections.IEnumerator Snap(Transform t)
        {
            var baseScale = t.localScale;
            for (var e = 0f; e < 0.28f && t != null; e += Time.unscaledDeltaTime)
            {
                var k = e / 0.28f;
                // 0.85 -> 1.04 -> 1.0: a short overshoot reads as "clicked into place".
                var s = k < 0.7f ? Mathf.Lerp(0.85f, 1.04f, k / 0.7f) : Mathf.Lerp(1.04f, 1f, (k - 0.7f) / 0.3f);
                t.localScale = baseScale * s;
                yield return null;
            }
            if (t != null) t.localScale = baseScale;
        }
    }
}
