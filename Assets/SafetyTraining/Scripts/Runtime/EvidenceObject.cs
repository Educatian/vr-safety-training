using SafetyTraining.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    [RequireComponent(typeof(Collider), typeof(XRSimpleInteractable))]
    public sealed class EvidenceObject : MonoBehaviour
    {
        [SerializeField] TrainingSiteId siteId;
        [SerializeField] string evidenceId = "evidence";
        [SerializeField] string title = "Evidence";
        [SerializeField] string hazardType = "General";
        [SerializeField, TextArea] string observation = "Record this observation.";
        [SerializeField] bool distractor;
        [SerializeField] GameObject hoverLabel;

        XRSimpleInteractable interactable;
        InteractiveHoverFeedback hoverFeedback;
        bool collected;

        public TrainingSiteId SiteId => siteId;
        public string EvidenceId => evidenceId;
        public string Title => title;
        public string HazardType => hazardType;
        public string Observation => observation;
        public bool IsDistractor => distractor;
        public bool IsCollected => collected;

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

        // Labels stay hidden until the learner attends to the object, keeping the
        // scene readable and avoiding always-on floating text.
        public void SetHoverLabel(GameObject label)
        {
            hoverLabel = label;
            if (hoverLabel != null)
                hoverLabel.SetActive(false);
        }

        void SetHovered(bool hovered)
        {
            hoverFeedback?.SetHovered(hovered);
            if (hoverLabel != null)
                hoverLabel.SetActive(hovered);
        }

        void OnMouseEnter() => SetHovered(true);
        void OnMouseExit() => SetHovered(false);

        void OnMouseDown()
        {
            if (DesktopPointerInputGate.CanUseWorldPointer)
                Collect();
        }

        public void Configure(TrainingSiteId site, string id, string displayTitle,
            string type, string note, bool isDistractor)
        {
            siteId = site;
            evidenceId = id;
            title = displayTitle;
            hazardType = type;
            observation = note;
            distractor = isDistractor;
        }

        public void Collect()
        {
            if (collected)
                return;
            var controller = InquirySessionController.Instance;
            if (controller == null || !controller.CollectEvidence(this))
                return;
            collected = true;
        }

        void OnSelected(SelectEnterEventArgs _) => Collect();
        void OnHoverEntered(HoverEnterEventArgs _) => SetHovered(true);
        void OnHoverExited(HoverExitEventArgs _) => SetHovered(false);
    }
}
