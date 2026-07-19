using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    [RequireComponent(typeof(Collider), typeof(XRSimpleInteractable))]
    public sealed class EngineeringDecisionOption : MonoBehaviour
    {
        [SerializeField] string optionId;
        [SerializeField] string label;
        [SerializeField] bool correct;
        [SerializeField, TextArea] string feedback;
        [SerializeField] EngineeringDecisionStation station;
        XRSimpleInteractable interactable;
        InteractiveHoverFeedback hover;

        public string OptionId => optionId;
        public string Label => label;
        public bool IsCorrect => correct;
        public string Feedback => feedback;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            hover = GetComponent<InteractiveHoverFeedback>() ?? gameObject.AddComponent<InteractiveHoverFeedback>();
        }
        void OnEnable()
        {
            if (interactable == null) interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }
        void OnDisable()
        {
            if (interactable == null) return;
            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }
        void OnMouseEnter() => hover?.SetHovered(true);
        void OnMouseExit() => hover?.SetHovered(false);
        void OnMouseDown()
        {
            if (DesktopPointerInputGate.CanUseWorldPointer) Select();
        }
        public void Configure(string id, string displayLabel, bool isCorrect, string diagnosticFeedback,
            EngineeringDecisionStation owner)
        {
            optionId = id; label = displayLabel; correct = isCorrect; feedback = diagnosticFeedback; station = owner;
        }
        public void Select() => station?.Submit(this);
        void OnSelected(SelectEnterEventArgs _) => Select();
        void OnHoverEntered(HoverEnterEventArgs _) => hover?.SetHovered(true);
        void OnHoverExited(HoverExitEventArgs _) => hover?.SetHovered(false);
    }
}
