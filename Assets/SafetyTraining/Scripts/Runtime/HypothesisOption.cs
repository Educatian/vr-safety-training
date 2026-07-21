using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// One selectable hypothesis statement at an inquiry decision station.
    /// Exactly one option per station is evidence-consistent; the learner's
    /// selection (not an authored preset) becomes the submitted hypothesis.
    /// </summary>
    [RequireComponent(typeof(Collider), typeof(XRSimpleInteractable))]
    public sealed class HypothesisOption : MonoBehaviour
    {
        [SerializeField] string optionId = "hypothesis-option";
        [SerializeField, TextArea] string hypothesisText = string.Empty;
        [SerializeField] bool evidenceConsistent;
        [SerializeField] InquiryDecisionStation station;

        XRSimpleInteractable interactable;
        InteractiveHoverFeedback hoverFeedback;

        public string OptionId => optionId;
        public string HypothesisText => hypothesisText;
        public bool IsEvidenceConsistent => evidenceConsistent;
        public InquiryDecisionStation Station => station;

        public void Configure(InquiryDecisionStation owner, string id, string text, bool consistent)
        {
            station = owner;
            optionId = id;
            hypothesisText = text;
            evidenceConsistent = consistent;
        }

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
                station?.ChooseHypothesis(this);
        }

        void OnSelected(SelectEnterEventArgs _) => station?.ChooseHypothesis(this);
        void OnHoverEntered(HoverEnterEventArgs _) => hoverFeedback?.SetHovered(true);
        void OnHoverExited(HoverExitEventArgs _) => hoverFeedback?.SetHovered(false);
    }
}
