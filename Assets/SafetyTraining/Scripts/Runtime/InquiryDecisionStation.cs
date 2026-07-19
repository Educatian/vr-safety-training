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
            var controller = InquirySessionController.Instance;
            if (controller != null && !controller.CanSubmitReport(siteId, minimumEvidenceRequired))
            {
                controller.BlockReportSubmission(siteId, minimumEvidenceRequired, hypothesis);
                return;
            }
            submitted = true;
            controller?.SelectHypothesis(siteId, hypothesis);
            controller?.SubmitFinalExplanation(siteId, finalExplanation);
            TrainingCoordinator.Instance?.SetContextFeedback(
                $"Inquiry report submitted\nHypothesis: {hypothesis}");
        }

        void OnSelected(SelectEnterEventArgs _) => Submit();
        void OnHoverEntered(HoverEnterEventArgs _) => hoverFeedback?.SetHovered(true);
        void OnHoverExited(HoverExitEventArgs _) => hoverFeedback?.SetHovered(false);
    }
}
