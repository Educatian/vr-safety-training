using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    [RequireComponent(typeof(InquiryEventLogger))]
    public sealed class ConstructionGoldenModuleController : MonoBehaviour
    {
        ConstructionGoldenModuleProgress progress = new();
        InquiryEventLogger eventLogger;
        TextMesh statusText;

        public static ConstructionGoldenModuleController Instance { get; private set; }
        public ConstructionGoldenStage Stage => progress.Stage;
        public bool IsComplete => progress.Stage == ConstructionGoldenStage.Complete;
        public bool CanCollectEvidence => Stage == ConstructionGoldenStage.EvidenceInvestigation;
        public bool CanUseEngineeringStations => Stage == ConstructionGoldenStage.EngineeringDecisions;
        public bool CanInstallControls => Stage == ConstructionGoldenStage.ControlInstallation;
        public bool CanDebriefCoach => Stage == ConstructionGoldenStage.CoachDebrief;
        public bool CanSubmitFinalReport => Stage == ConstructionGoldenStage.FinalReport;

        void Awake()
        {
            Instance = this;
            eventLogger = GetComponent<InquiryEventLogger>();
            RefreshStatus();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Configure(TextMesh missionStatus)
        {
            statusText = missionStatus;
            RefreshStatus();
        }

        public void Begin()
        {
            if (Stage != ConstructionGoldenStage.NotStarted) return;
            Apply(progress.Begin(), "mission_started", "CON-01", "entry");
        }

        public bool CanPerformPracticalStep(int stepIndex)
        {
            if (stepIndex == 0)
                return Stage == ConstructionGoldenStage.PpeEntry;
            return CanInstallControls;
        }

        public void NotifyPracticalStep(int stepIndex, string actionName)
        {
            var update = stepIndex == 0
                ? progress.RecordPpePlacement()
                : progress.RecordControlStep(stepIndex);
            Apply(update, stepIndex == 0 ? "ppe_verified" : "control_installed",
                stepIndex == 0 ? "CON-01" : "CON-03", $"practical:{stepIndex}:{actionName}");
        }

        public bool TryCollectEvidence(string evidenceId, bool relevant)
        {
            var update = progress.RecordEvidence(evidenceId, relevant);
            Apply(update, relevant ? "golden_evidence_recorded" : "golden_comparison_recorded",
                "CON-01", $"evidence:{evidenceId}");
            return update.Accepted;
        }

        public bool TryEngineeringDecision(string decisionId, bool accepted)
        {
            var update = progress.RecordEngineeringDecision(decisionId, accepted);
            Apply(update, accepted ? "golden_engineering_verified" : "golden_engineering_revision",
                "CON-02", $"decision:{decisionId}");
            return update.Accepted;
        }

        public bool TryCoachExplanation(string explanation)
        {
            var update = progress.RecordCoachExplanation(explanation);
            Apply(update, update.Accepted ? "coach_explanation_accepted" : "coach_explanation_revise",
                "CON-04", "coach:explanation");
            return update.Accepted;
        }

        public bool TrySubmitFinalReport()
        {
            var update = progress.RecordFinalReport();
            Apply(update, update.Accepted ? "golden_module_completed" : "golden_report_blocked",
                "CON-04", "report:final");
            return update.Accepted;
        }

        public void PresentGateMessage(string message)
        {
            TrainingCoordinator.Instance?.SetContextFeedback(message);
            TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(TrainingSiteId.Construction, message);
        }

        void Apply(ConstructionProgressUpdate update, string eventType, string objectiveId, string criterionId)
        {
            eventLogger ??= GetComponent<InquiryEventLogger>();
            eventLogger?.Record(new InquiryTelemetryEvent
            {
                SiteId = TrainingSiteId.Construction,
                EventType = update.Accepted ? eventType : "golden_attempt_blocked",
                Phase = Stage.ToString(),
                ObjectId = criterionId,
                ObjectiveId = objectiveId,
                CriterionId = criterionId,
                Outcome = update.Accepted ? "accepted" : "blocked",
                EarnedPoints = update.Accepted ? 1 : 0,
                PossiblePoints = 1,
                EvidenceCount = progress.RelevantEvidenceCount,
                Detail = update.Message
            });
            var earnsCriterion = update.Accepted &&
                                 eventType != "golden_comparison_recorded" &&
                                 eventType != "golden_engineering_revision";
            if (earnsCriterion)
            {
                LearningOutcomeTracker.Instance?.Record(TrainingSiteId.Construction, objectiveId,
                    criterionId, true, update.Message);
            }
            RefreshStatus();
            if (update.StageChanged)
                GetComponent<ConstructionHandsOnController>()?.RefreshAvailability();
            PresentGateMessage(update.Message);
        }

        void RefreshStatus()
        {
            if (statusText == null) return;
            statusText.text =
                $"MISSION STATUS  {StageLabel(Stage)}\n" +
                $"Evidence {progress.RelevantEvidenceCount}/{ConstructionGoldenModuleProgress.RequiredRelevantEvidence}   " +
                $"Engineering {progress.EngineeringDecisionCount}/{ConstructionGoldenModuleProgress.RequiredEngineeringDecisions}\n" +
                $"Controls {progress.ControlStepCount}/{ConstructionGoldenModuleProgress.RequiredControlSteps}   " +
                $"Debrief {(Stage >= ConstructionGoldenStage.FinalReport ? "VERIFIED" : "PENDING")}";
        }

        static string StageLabel(ConstructionGoldenStage stage)
        {
            return stage switch
            {
                ConstructionGoldenStage.NotStarted => "READY",
                ConstructionGoldenStage.PpeEntry => "1/6 PPE ENTRY",
                ConstructionGoldenStage.EvidenceInvestigation => "2/6 FIELD INQUIRY",
                ConstructionGoldenStage.EngineeringDecisions => "3/6 ENGINEERING",
                ConstructionGoldenStage.ControlInstallation => "4/6 CONTROLS",
                ConstructionGoldenStage.CoachDebrief => "5/6 COACH DEBRIEF",
                ConstructionGoldenStage.FinalReport => "6/6 FINAL REPORT",
                ConstructionGoldenStage.Complete => "COMPLETE",
                _ => stage.ToString().ToUpperInvariant()
            };
        }
    }
}
