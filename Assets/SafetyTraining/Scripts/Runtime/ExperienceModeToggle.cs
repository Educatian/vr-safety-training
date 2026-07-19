using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    [RequireComponent(typeof(Collider), typeof(XRSimpleInteractable))]
    public sealed class ExperienceModeToggle : MonoBehaviour
    {
        [SerializeField] TextMesh label;

        XRSimpleInteractable interactable;
        Renderer surface;
        ExperienceModeController controller;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            surface = GetComponent<Renderer>();
        }

        void Start()
        {
            BindController();
            Refresh();
        }

        void OnEnable()
        {
            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);
        }

        void OnDisable()
        {
            if (interactable != null)
                interactable.selectEntered.RemoveListener(OnSelected);
            if (controller != null)
                controller.ModeChanged -= OnModeChanged;
        }

        void OnMouseDown()
        {
            if (DesktopPointerInputGate.CanUseWorldPointer)
                Toggle();
        }

        public void Configure(TextMesh statusLabel)
        {
            label = statusLabel;
        }

        public void Toggle()
        {
            BindController();
            controller?.ToggleMode();
        }

        void OnSelected(SelectEnterEventArgs _) => Toggle();

        void BindController()
        {
            var candidate = ExperienceModeController.Instance ??
                            Object.FindFirstObjectByType<ExperienceModeController>();
            if (candidate == controller)
                return;
            if (controller != null)
                controller.ModeChanged -= OnModeChanged;
            controller = candidate;
            if (controller != null)
                controller.ModeChanged += OnModeChanged;
        }

        void OnModeChanged(TrainingExperienceMode _, string __) => Refresh();

        void Refresh()
        {
            if (controller == null)
            {
                if (label != null)
                    label.text = "EXPERIENCE: AUTO\nCLICK FOR IVR";
                return;
            }

            var ivr = controller.ActiveMode == TrainingExperienceMode.ImmersiveVr;
            if (label != null)
                label.text = ivr
                    ? "EXPERIENCE: IVR ACTIVE\nQUEST / OPENXR"
                    : "EXPERIENCE: DESKTOP\nCLICK TO TRY IVR";
            if (surface != null)
                surface.material.color = ivr
                    ? new Color(0.08f, 0.58f, 0.48f)
                    : new Color(0.08f, 0.24f, 0.42f);
        }
    }
}
