using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    public sealed class EngineeringDecisionStation : MonoBehaviour
    {
        [SerializeField] string decisionId;
        [SerializeField] string title;
        [SerializeField] TextMesh feedbackDisplay;
        readonly HashSet<string> attemptedOptions = new();
        bool solved;

        public string DecisionId => decisionId;
        public bool IsSolved => solved;

        public void Configure(string id, string displayTitle, TextMesh feedback)
        {
            decisionId = id; title = displayTitle; feedbackDisplay = feedback;
            SetFeedback("SELECT A CONTROL DECISION", new Color(0.55f, 0.84f, 1f));
        }

        public void Submit(EngineeringDecisionOption option)
        {
            if (option == null || solved) return;
            attemptedOptions.Add(option.OptionId);
            var accepted = option.IsCorrect;
            if (accepted) solved = true;
            LearningOutcomeTracker.Instance?.Record(TrainingSiteId.Construction, "CON-02",
                $"decision:{decisionId}", accepted, $"{title}: {option.Label}. {option.Feedback}", 50);
            SetFeedback((accepted ? "VERIFIED  " : "REVISE  ") + option.Feedback,
                accepted ? new Color(0.2f, 1f, 0.55f) : new Color(1f, 0.38f, 0.2f));
            TrainingCoordinator.Instance?.SetContextFeedback($"{title}\n{option.Feedback}");
            if (accepted)
            {
                TrainingCoordinator.Instance?.AddHandsOnBonus(50);
                TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(TrainingSiteId.Construction,
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
