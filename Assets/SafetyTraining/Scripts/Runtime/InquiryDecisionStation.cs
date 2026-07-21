using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    [RequireComponent(typeof(Collider), typeof(XRSimpleInteractable))]
    public sealed class InquiryDecisionStation : MonoBehaviour
    {
        [SerializeField] TrainingSiteId siteId;
        [SerializeField] string hypothesis = "Primary safety issue identified from collected evidence.";
        [SerializeField] string finalExplanation = "The selected controls match the evidence collected onsite.";
        [SerializeField] int minimumEvidenceRequired = InquirySessionController.DefaultMinimumEvidenceForReport;

        XRSimpleInteractable interactable;
        InteractiveHoverFeedback hoverFeedback;
        bool submitted;
        HypothesisOption chosenHypothesis;
        readonly HashSet<string> attemptedHypotheses = new();

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            hoverFeedback = GetComponent<InteractiveHoverFeedback>();
            if (hoverFeedback == null)
                hoverFeedback = gameObject.AddComponent<InteractiveHoverFeedback>();
        }

        void OnEnable()
        {
            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }

        void OnDisable()
        {
            if (interactable == null)
                return;
            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        void OnMouseEnter() => hoverFeedback?.SetHovered(true);
        void OnMouseExit() => hoverFeedback?.SetHovered(false);

        void OnMouseDown()
        {
            if (DesktopPointerInputGate.CanUseWorldPointer)
                Submit();
        }

        public bool HasSubmitted => submitted;
        public int MinimumEvidenceRequired => minimumEvidenceRequired;
        public bool HasChosenHypothesis => chosenHypothesis != null;

        /// <summary>
        /// Registers the learner's hypothesis selection. Each distinct choice is
        /// logged; the hypothesis criterion is met only by the evidence-consistent
        /// option, and a first-attempt marker is recorded when it is chosen first.
        /// </summary>
        public void ChooseHypothesis(HypothesisOption option)
        {
            if (option == null || submitted)
                return;
            var firstAttempt = attemptedHypotheses.Count == 0;
            if (!attemptedHypotheses.Add(option.OptionId) && chosenHypothesis == option)
                return;
            chosenHypothesis = option;
            var controller = InquirySessionController.Instance;
            controller?.SelectHypothesis(siteId, option.HypothesisText, learnerAuthored: true);
            var objectives = LearningObjectiveCatalog.ForSite(siteId);
            LearningOutcomeTracker.Instance?.Record(siteId, objectives[objectives.Count - 1].Id,
                "hypothesis_choice", option.IsEvidenceConsistent,
                $"option={option.OptionId} attempt={attemptedHypotheses.Count}");
            if (option.IsEvidenceConsistent && firstAttempt)
                LearningOutcomeTracker.Instance?.Record(siteId, objectives[objectives.Count - 1].Id,
                    "hypothesis_choice:first_attempt", true, $"option={option.OptionId}", 0);
            TrainingCoordinator.Instance?.SetContextFeedback(option.IsEvidenceConsistent
                ? $"Hypothesis selected\n{option.HypothesisText}\nSubmit the report when your evidence supports it."
                : $"Hypothesis selected\n{option.HypothesisText}\nCompare this against the evidence you collected before submitting.");
        }

        public void Configure(TrainingSiteId site, string selectedHypothesis, string explanation,
            int requiredEvidence = InquirySessionController.DefaultMinimumEvidenceForReport)
        {
            siteId = site;
            hypothesis = selectedHypothesis;
            finalExplanation = explanation;
            minimumEvidenceRequired = Mathf.Max(0, requiredEvidence);
        }

        public void Submit()
        {
            if (submitted)
                return;
            var golden = siteId == TrainingSiteId.Construction
                ? ConstructionGoldenModuleController.Instance
                : null;
            if (golden != null && !golden.CanSubmitFinalReport)
            {
                golden.PresentGateMessage(
                    "Final report locked: complete engineering controls and the evidence-based coach debrief first.");
                return;
            }
            var controller = InquirySessionController.Instance;
            if (controller != null && !controller.CanSubmitReport(siteId, minimumEvidenceRequired))
            {
                controller.BlockReportSubmission(siteId, minimumEvidenceRequired, SubmittedHypothesis());
                return;
            }
            if (chosenHypothesis == null && HasHypothesisOptions())
            {
                TrainingCoordinator.Instance?.SetContextFeedback(
                    "Report blocked\nSelect one hypothesis plate before submitting the report.");
                return;
            }
            submitted = true;
            if (chosenHypothesis == null)
                controller?.SelectHypothesis(siteId, hypothesis, learnerAuthored: false);
            controller?.SubmitFinalExplanation(siteId, finalExplanation, learnerAuthored: false);
            golden?.TrySubmitFinalReport();
            TrainingCoordinator.Instance?.SetContextFeedback(
                $"Inquiry report submitted\nHypothesis: {SubmittedHypothesis()}");
        }

        string SubmittedHypothesis() =>
            chosenHypothesis != null ? chosenHypothesis.HypothesisText : hypothesis;

        bool HasHypothesisOptions()
        {
            foreach (var option in FindObjectsByType<HypothesisOption>(FindObjectsSortMode.None))
            {
                if (option != null && option.gameObject.activeInHierarchy &&
                    ReferenceEquals(option.Station, this))
                    return true;
            }
            return false;
        }

        void OnSelected(SelectEnterEventArgs _) => Submit();
        void OnHoverEntered(HoverEnterEventArgs _) => hoverFeedback?.SetHovered(true);
        void OnHoverExited(HoverExitEventArgs _) => hoverFeedback?.SetHovered(false);
    }
}
