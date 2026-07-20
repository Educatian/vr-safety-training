using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    public sealed class ConstructionHandsOnController : MonoBehaviour
    {
        readonly List<ConstructionActionInteractable> actions = new();
        int nextStep;
        bool complete;
        bool active;

        public int CompletedSteps => nextStep;
        public int TotalSteps => actions.Count;
        public bool IsComplete => complete;

        void Awake()
        {
            actions.AddRange(GetComponentsInChildren<ConstructionActionInteractable>(true)
                .OrderBy(item => item.StepIndex));
        }

        public void Begin()
        {
            ConstructionGoldenModuleController.Instance?.Begin();
            active = true;
            RefreshCurrentStep();
            Publish("Construction practical: pick up the marked PPE kit to begin.");
        }

        public bool CanManipulate(ConstructionActionInteractable action)
        {
            var golden = ConstructionGoldenModuleController.Instance;
            var stageAllowsStep = golden == null || golden.CanPerformPracticalStep(action.StepIndex);
            return active && !complete && action.StepIndex == nextStep && stageAllowsStep;
        }

        public void TryPerform(ConstructionActionInteractable action, float releaseDistance,
            bool insideTarget, string inputMode)
        {
            if (!active || complete)
                return;
            var golden = ConstructionGoldenModuleController.Instance;
            if (golden != null && !golden.CanPerformPracticalStep(action.StepIndex))
            {
                golden.PresentGateMessage(
                    "Physical controls are locked until PPE, field evidence, and engineering decisions are verified.");
                return;
            }
            if (action.StepIndex != nextStep)
            {
                var expected = actions.ElementAtOrDefault(nextStep);
                var sequenceMessage = $"Sequence check: complete {expected?.ActionName ?? "the remaining control"} first.";
                Publish(sequenceMessage);
                TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(
                    SafetyTraining.Core.TrainingSiteId.Construction, sequenceMessage);
                return;
            }
            if (!insideTarget)
            {
                TrainingCoordinator.Instance?.RecordPlacementAttempt(
                    SafetyTraining.Core.TrainingSiteId.Construction, action.StepIndex, actions.Count,
                    action.ActionName, action.Instruction, releaseDistance, false, inputMode);
                var retryMessage = $"Placement check: move {action.ActionName} into the highlighted DROP HERE zone. " +
                                   $"{action.Instruction}";
                Publish(retryMessage);
                TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(
                    SafetyTraining.Core.TrainingSiteId.Construction, retryMessage);
                return;
            }

            action.MarkComplete();
            ConstructionGoldenModuleController.Instance?.NotifyPracticalStep(action.StepIndex, action.ActionName);
            nextStep++;
            RefreshCurrentStep();
            TrainingCoordinator.Instance?.RecordPlacementAttempt(
                SafetyTraining.Core.TrainingSiteId.Construction, action.StepIndex, actions.Count,
                action.ActionName, action.Instruction, releaseDistance, true, inputMode);
            TrainingCoordinator.Instance?.AddHandsOnBonus(20);
            var coach = FindObjectsByType<NpcSiteCompanion>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.SiteId == SafetyTraining.Core.TrainingSiteId.Construction);
            coach?.Encourage(nextStep >= actions.Count);
            if (nextStep >= actions.Count)
            {
                complete = true;
                var completionMessage =
                    "Construction practical complete: controls installed and verified. Debrief with the coach.";
                Publish(completionMessage);
                TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(
                    SafetyTraining.Core.TrainingSiteId.Construction, completionMessage);
                return;
            }

            var next = actions[nextStep];
            var successMessage = $"Step {nextStep}/{actions.Count} complete. Next: {next.ActionName}. {next.Instruction}";
            Publish(successMessage);
            TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(
                SafetyTraining.Core.TrainingSiteId.Construction, successMessage);
        }

        void Publish(string message)
        {
            TrainingCoordinator.Instance?.SetHandsOnFeedback(message);
        }

        public void RefreshAvailability()
        {
            RefreshCurrentStep();
        }

        void RefreshCurrentStep()
        {
            var golden = ConstructionGoldenModuleController.Instance;
            foreach (var action in actions)
            {
                var stageAllowsStep = golden == null || golden.CanPerformPracticalStep(action.StepIndex);
                action.SetCurrentStep(active && !complete && action.StepIndex == nextStep && stageAllowsStep);
            }
        }
    }
}
