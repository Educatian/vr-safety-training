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
        [SerializeField] Text fieldDataDisplay;
        [SerializeField] Text calculationDisplay;
        [SerializeField] Text questionDisplay;
        [SerializeField] EngineeringDecisionOption[] optionComponents;
        [SerializeField] Text[] optionLabels;
        readonly HashSet<string> attemptedOptions = new();
        bool solved;
        bool variantActive;

        public string DecisionId => decisionId;
        public TrainingSiteId SiteId => siteId;
        public string ObjectiveId => objectiveId;
        public bool IsSolved => solved;
        public bool IsVariantActive => variantActive;

        /// <summary>Wires the HMI texts and option plates so a transfer variant can rebind them.</summary>
        public void ConfigureVariantUi(Text fieldData, Text calculation, Text question,
            EngineeringDecisionOption[] options, Text[] labels)
        {
            fieldDataDisplay = fieldData;
            calculationDisplay = calculation;
            questionDisplay = question;
            optionComponents = options;
            optionLabels = labels;
        }

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

        public const int FirstAttemptBonus = 50;
        public const int RetryBonus = 15;

        public void Submit(EngineeringDecisionOption option)
        {
            if (option == null || solved) return;
            if (variantActive)
            {
                SubmitVariant(option);
                return;
            }
            var accepted = option.IsCorrect;
            var golden = siteId == TrainingSiteId.Construction
                ? ConstructionGoldenModuleController.Instance : null;
            if (golden != null && !golden.TryEngineeringDecision(decisionId, accepted))
                return;
            var firstAttempt = attemptedOptions.Count == 0;
            attemptedOptions.Add(option.OptionId);
            var bonus = firstAttempt ? FirstAttemptBonus : RetryBonus;
            LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                $"decision:{decisionId}", accepted,
                $"{title}: {option.Label}. attempt={attemptedOptions.Count} " +
                $"tried=[{string.Join("|", attemptedOptions)}]. {option.Feedback}", bonus);
            if (accepted && firstAttempt)
                LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                    $"decision:{decisionId}:first_attempt", true,
                    $"{title}: correct on first attempt.", 0);
            SetFeedback((accepted ? "VERIFIED  " : "REVISE  ") + option.Feedback,
                accepted ? new Color(0.2f, 1f, 0.55f) : new Color(1f, 0.38f, 0.2f));
            if (!accepted)
                ConsequenceVisual.FindFor(decisionId)?.Show();
            TrainingCoordinator.Instance?.SetContextFeedback($"{title}\n{option.Feedback}");
            if (accepted)
            {
                TrainingCoordinator.Instance?.AddHandsOnBonus(bonus);
                TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(siteId,
                    $"Strong engineering judgment. {option.Feedback}");
                if (!TryBeginVariant())
                    solved = true;
            }
        }

        // Fading stage: after the worked example is solved, re-present the station
        // as an isomorph with new numbers and NO worked solution. Telemetry-only:
        // the transfer criterion carries no bonus and never touches the golden gate.
        bool TryBeginVariant()
        {
            if (!ConstructionEngineeringCatalog.Variants.TryGetValue(decisionId, out var variant) ||
                fieldDataDisplay == null || optionComponents == null || optionLabels == null ||
                optionComponents.Length != variant.Options.Count)
                return false;
            variantActive = true;
            attemptedOptions.Clear();
            title = variant.Title;
            fieldDataDisplay.text = variant.LearningMaterial;
            if (calculationDisplay != null)
                calculationDisplay.text = variant.Calculation;
            if (questionDisplay != null)
                questionDisplay.text = variant.Question;
            for (var index = 0; index < optionComponents.Length; index++)
            {
                var optionDefinition = variant.Options[index];
                optionComponents[index].Configure(optionDefinition.Id, optionDefinition.Label,
                    optionDefinition.IsCorrect, optionDefinition.Feedback, this);
                if (index < optionLabels.Length && optionLabels[index] != null)
                    optionLabels[index].text = optionDefinition.Label.ToUpperInvariant();
            }
            SetFeedback("TRANSFER CHECK  APPLY THE SAME ENGINEERING CHECK - NO WORKED SOLUTION",
                new Color(0.95f, 0.72f, 0.29f));
            return true;
        }

        void SubmitVariant(EngineeringDecisionOption option)
        {
            var accepted = option.IsCorrect;
            var firstAttempt = attemptedOptions.Count == 0;
            attemptedOptions.Add(option.OptionId);
            LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                $"decision:{decisionId}:transfer", accepted,
                $"{title}: {option.Label}. attempt={attemptedOptions.Count}. {option.Feedback}", 0);
            if (accepted && firstAttempt)
                LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                    $"decision:{decisionId}:transfer:first_attempt", true,
                    $"{title}: transfer correct on first attempt.", 0);
            SetFeedback((accepted ? "TRANSFER VERIFIED  " : "REVISE  ") + option.Feedback,
                accepted ? new Color(0.2f, 1f, 0.55f) : new Color(1f, 0.38f, 0.2f));
            TrainingCoordinator.Instance?.SetContextFeedback($"{title}\n{option.Feedback}");
            if (accepted)
            {
                variantActive = false;
                solved = true;
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
