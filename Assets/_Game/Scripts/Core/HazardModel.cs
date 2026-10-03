using System;

namespace Jobsite.Core
{
    // Energy-source wheel (Albert, Hallowell & Kleiner, 2014).
    public enum EnergySource { Gravity, Motion, Mechanical, Electrical, Pressure, Temperature, Chemical, Biological, Radiation, Sound }

    public enum FocusFour { None, Falls, CaughtIn, StruckBy, Electrocution }

    // Lower value = higher in the hierarchy of controls.
    public enum ControlLevel { Elimination = 0, Engineering = 1, Administrative = 2, Ppe = 3 }

    public enum CpArea { Excavation, FallProtection, Scaffold, StruckBy, Electrical, General }

    public enum HazardState { Latent, Reported, Installing, Controlled, Lapsed, Stopped, Incident }

    public sealed class HazardSpec
    {
        public HazardSpec(string id, bool isHazard, EnergySource energy, FocusFour focusFour, CpArea area,
            int probability, int severity, ControlLevel bestFeasibleControl,
            float triggerAtSeconds = float.PositiveInfinity, float lapseAfterSeconds = 90f,
            bool requiresStopWork = false)
        {
            if (string.IsNullOrWhiteSpace(id))
                throw new ArgumentException("A hazard needs a stable identifier.", nameof(id));
            if (probability < 1 || probability > 5 || severity < 1 || severity > 5)
                throw new ArgumentOutOfRangeException(nameof(probability), "Risk ratings are 1-5.");

            Id = id;
            IsHazard = isHazard;
            Energy = energy;
            FocusFour = focusFour;
            Area = area;
            Probability = probability;
            Severity = severity;
            BestFeasibleControl = bestFeasibleControl;
            TriggerAtSeconds = triggerAtSeconds;
            LapseAfterSeconds = lapseAfterSeconds;
            RequiresStopWork = requiresStopWork;
        }

        public string Id { get; }
        public bool IsHazard { get; }
        public EnergySource Energy { get; }
        public FocusFour FocusFour { get; }
        public CpArea Area { get; }
        public int Probability { get; }          // expert key
        public int Severity { get; }             // expert key
        public ControlLevel BestFeasibleControl { get; }
        public float TriggerAtSeconds { get; }   // day-clock time an uncontrolled hazard becomes an incident
        public float LapseAfterSeconds { get; }  // how long a weak control holds
        public bool RequiresStopWork { get; }
        public bool IsHighSeverity => Severity >= 4;

        // OSHA-anchored feedback (GDD §15). Cfr is shown verbatim on the citation chip.
        public string Cfr { get; private set; } = "";
        public string RequirementPlain { get; private set; } = "";
        public string Threshold { get; private set; } = "";
        public bool CfrVerified { get; private set; }

        public HazardSpec WithStandard(string cfr, string requirementPlain, string threshold, bool verified)
        {
            Cfr = cfr ?? ""; RequirementPlain = requirementPlain ?? ""; Threshold = threshold ?? ""; CfrVerified = verified;
            return this;
        }
    }

    // Evidence for one real hazard, feeding CP mastery (GDD §4, C1-C5).
    public sealed class HazardEvidence
    {
        public bool Detected;
        public float DetectedAtSeconds = float.NaN;
        public bool TagCorrect;
        public int RiskDeviation;               // |p - key| + |s - key|, 0..8
        public ControlLevel? AppliedControl;
        public int InstallAttempts;
        public int Lapses;
        public bool StopWorkCalled;
        public bool BecameIncident;
        public bool Hinted;                     // found after a hint: half detect XP, 0.5 mastery (GDD §14)
        public bool Cued;                       // an in-world scaffold (mission zone, tutorial beacon) pointed here: 0.5 mastery, XP unchanged
        public bool DismissedAsCompliant;       // learner confirmed this real hazard as compliant (a discrimination miss)
        public SpeakUpStyle? SpeakUp;           // how the learner held the stop when the foreman pushed back
        public int HintTier;                    // 0 none, 1 zone, 2 energy, 3 Dolores points
    }
}
