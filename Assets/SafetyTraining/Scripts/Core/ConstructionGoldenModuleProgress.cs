using System;
using System.Collections.Generic;

namespace SafetyTraining.Core
{
    public enum ConstructionGoldenStage
    {
        NotStarted,
        PpeEntry,
        EvidenceInvestigation,
        EngineeringDecisions,
        ControlInstallation,
        CoachDebrief,
        FinalReport,
        Complete
    }

    public readonly struct ConstructionProgressUpdate
    {
        public ConstructionProgressUpdate(bool accepted, bool stageChanged, ConstructionGoldenStage stage,
            string message)
        {
            Accepted = accepted;
            StageChanged = stageChanged;
            Stage = stage;
            Message = message;
        }

        public bool Accepted { get; }
        public bool StageChanged { get; }
        public ConstructionGoldenStage Stage { get; }
        public string Message { get; }
    }

    public sealed class ConstructionGoldenModuleProgress
    {
        public const int RequiredRelevantEvidence = 4;
        public const int RequiredEngineeringDecisions = 3;
        public const int RequiredControlSteps = 4;

        static readonly HashSet<string> ValidEngineeringIds = new(StringComparer.Ordinal)
        {
            "formwork-capacity", "crane-radius", "trench-system"
        };
        readonly HashSet<string> evidenceIds = new(StringComparer.Ordinal);
        readonly HashSet<string> engineeringIds = new(StringComparer.Ordinal);
        readonly HashSet<int> controlSteps = new();

        public ConstructionGoldenStage Stage { get; private set; } = ConstructionGoldenStage.NotStarted;
        public int RelevantEvidenceCount => evidenceIds.Count;
        public int EngineeringDecisionCount => engineeringIds.Count;
        public int ControlStepCount => controlSteps.Count;
        public string CoachExplanation { get; private set; } = string.Empty;

        public ConstructionProgressUpdate Begin()
        {
            if (Stage != ConstructionGoldenStage.NotStarted)
                return Reject("The construction mission is already active.");
            Stage = ConstructionGoldenStage.PpeEntry;
            return Accept(true, "Stage PPE at the entry checkpoint before entering the work area.");
        }

        public ConstructionProgressUpdate RecordPpePlacement()
        {
            if (Stage != ConstructionGoldenStage.PpeEntry)
                return Reject("Complete the current mission stage before changing PPE.");
            Stage = ConstructionGoldenStage.EvidenceInvestigation;
            return Accept(true, "PPE verified. Collect four relevant field observations before calculating controls.");
        }

        public ConstructionProgressUpdate RecordEvidence(string evidenceId, bool relevant)
        {
            if (Stage != ConstructionGoldenStage.EvidenceInvestigation)
                return Reject("Evidence collection unlocks after PPE and closes when the engineering review begins.");
            if (!relevant)
                return Accept(false, "Comparison sample recorded; it does not count toward the four relevant observations.");
            if (string.IsNullOrWhiteSpace(evidenceId) || !evidenceIds.Add(evidenceId))
                return Reject("This observation is already in the field record.");
            if (evidenceIds.Count < RequiredRelevantEvidence)
                return Accept(false, $"Relevant evidence {evidenceIds.Count}/{RequiredRelevantEvidence}. Keep investigating distinct conditions.");
            Stage = ConstructionGoldenStage.EngineeringDecisions;
            return Accept(true, "Evidence threshold met. Solve the formwork, crane-radius, and trench decisions.");
        }

        public ConstructionProgressUpdate RecordEngineeringDecision(string decisionId, bool accepted)
        {
            if (Stage != ConstructionGoldenStage.EngineeringDecisions)
                return Reject("Engineering stations unlock after four relevant field observations.");
            if (!accepted)
                return Accept(false, "Decision recorded as a revision attempt. Recheck demand, capacity, geometry, and the cited standard.");
            if (!ValidEngineeringIds.Contains(decisionId))
                return Reject("This is not a recognized construction engineering decision.");
            if (!engineeringIds.Add(decisionId))
                return Reject("This engineering decision is already verified.");
            if (engineeringIds.Count < RequiredEngineeringDecisions)
                return Accept(false, $"Engineering decisions {engineeringIds.Count}/{RequiredEngineeringDecisions}. Continue to the next station.");
            Stage = ConstructionGoldenStage.ControlInstallation;
            return Accept(true, "Calculations verified. Move materials, install guardrail and barricade controls, then complete the walkdown.");
        }

        public ConstructionProgressUpdate RecordControlStep(int stepIndex)
        {
            if (Stage != ConstructionGoldenStage.ControlInstallation)
                return Reject("Physical controls unlock after all three engineering decisions are verified.");
            if (stepIndex < 1 || stepIndex > RequiredControlSteps || !controlSteps.Add(stepIndex))
                return Reject("This control step is invalid or already complete.");
            if (controlSteps.Count < RequiredControlSteps)
                return Accept(false, $"Control installation {controlSteps.Count}/{RequiredControlSteps}. Verify the next marked control.");
            Stage = ConstructionGoldenStage.CoachDebrief;
            return Accept(true, "Controls verified. Tell the safety coach what evidence drove your control plan.");
        }

        public ConstructionProgressUpdate RecordCoachExplanation(string explanation)
        {
            if (Stage != ConstructionGoldenStage.CoachDebrief)
                return Reject("The evidence-based coach debrief follows physical control verification.");
            if (!IsSubstantiveExplanation(explanation))
                return Reject("Use at least eight words and connect a site risk to a specific control.");
            CoachExplanation = explanation.Trim();
            Stage = ConstructionGoldenStage.FinalReport;
            return Accept(true, "Coach debrief accepted. Submit the evidence-gated final report at the field station.");
        }

        public ConstructionProgressUpdate RecordFinalReport()
        {
            if (Stage != ConstructionGoldenStage.FinalReport)
                return Reject("Complete the coach debrief before final report submission.");
            Stage = ConstructionGoldenStage.Complete;
            return Accept(true, "Construction golden module complete. Review your evidence and analytics record.");
        }

        public static bool IsSubstantiveExplanation(string explanation)
        {
            if (string.IsNullOrWhiteSpace(explanation)) return false;
            var words = explanation.Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
            if (words.Length < 8) return false;
            var lower = explanation.ToLowerInvariant();
            var hasRisk = lower.Contains("fall") || lower.Contains("crane") || lower.Contains("trench") ||
                          lower.Contains("formwork") || lower.Contains("access") || lower.Contains("load") ||
                          lower.Contains("hazard") || lower.Contains("risk");
            var hasControl = lower.Contains("guardrail") || lower.Contains("barricade") || lower.Contains("isolate") ||
                             lower.Contains("control") || lower.Contains("shore") || lower.Contains("remove") ||
                             lower.Contains("staging") || lower.Contains("protect");
            return hasRisk && hasControl;
        }

        ConstructionProgressUpdate Accept(bool stageChanged, string message) =>
            new(true, stageChanged, Stage, message);

        ConstructionProgressUpdate Reject(string message) =>
            new(false, false, Stage, message);
    }
}
