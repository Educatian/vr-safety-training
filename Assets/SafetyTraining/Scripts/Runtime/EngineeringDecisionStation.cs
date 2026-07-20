using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;
using UnityEngine.UI;

namespace SafetyTraining.Runtime
{
    public sealed class EngineeringDecisionStation : MonoBehaviour
    {
        [SerializeField] string decisionId;
        [SerializeField] TrainingSiteId siteId;
        [SerializeField] string objectiveId = "CON-02";
        [SerializeField] string title;
        [SerializeField] Text feedbackDisplay;
        readonly HashSet<string> attemptedOptions = new();
        bool solved;

        public string DecisionId => decisionId;
        public TrainingSiteId SiteId => siteId;
        public string ObjectiveId => objectiveId;
        public bool IsSolved => solved;

        public void Configure(string id, string displayTitle, Text feedback)
        {
            Configure(TrainingSiteId.Construction, "CON-02", id, displayTitle, feedback);
        }

        public void Configure(TrainingSiteId site, string objective, string id, string displayTitle, Text feedback)
        {
            siteId = site; objectiveId = objective; decisionId = id;
            title = displayTitle; feedbackDisplay = feedback;
            SetFeedback("SELECT A CONTROL DECISION", new Color(0.55f, 0.84f, 1f));
        }

        public void Submit(EngineeringDecisionOption option)
        {
            if (option == null || solved) return;
            var accepted = option.IsCorrect;
            var golden = siteId == TrainingSiteId.Construction
                ? ConstructionGoldenModuleController.Instance : null;
            if (golden != null && !golden.TryEngineeringDecision(decisionId, accepted))
                return;
            attemptedOptions.Add(option.OptionId);
            if (accepted) solved = true;
            LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                $"decision:{decisionId}", accepted, $"{title}: {option.Label}. {option.Feedback}", 50);
            SetFeedback((accepted ? "VERIFIED  " : "REVISE  ") + option.Feedback,
                accepted ? new Color(0.2f, 1f, 0.55f) : new Color(1f, 0.38f, 0.2f));
            TrainingCoordinator.Instance?.SetContextFeedback($"{title}\n{option.Feedback}");
            if (accepted)
            {
                TrainingCoordinator.Instance?.AddHandsOnBonus(50);
                TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(siteId,
                    $"Strong engineering judgment. {option.Feedback}");
            }
        }

        void SetFeedback(string value, Color color)
        {
            if (feedbackDisplay == null) return;
            feedbackDisplay.text = value;
            feedbackDisplay.color = color;
        }
    }
}
