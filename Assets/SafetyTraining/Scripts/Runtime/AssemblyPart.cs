using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// A rigging component the learner can grab (XR) or drag (desktop) and snap
    /// into an AssemblySocket. While held near a free socket a translucent ghost
    /// previews the snap pose (the "magnetic" affordance). Parts snapped into a
    /// socket can be grabbed back out.
    /// </summary>
    [RequireComponent(typeof(Collider), typeof(Rigidbody), typeof(XRGrabInteractable))]
    public sealed class AssemblyPart : MonoBehaviour
    {
        [SerializeField] string partId = "part";
        [SerializeField] string category = "sling";
        [SerializeField] bool serviceable = true;
        [SerializeField] AssemblyStationController station;

        XRGrabInteractable grabInteractable;
        Rigidbody body;
        Vector3 startLocalPosition;
        Quaternion startLocalRotation;
        Plane desktopDragPlane;
        Vector3 desktopDragOffset;
        bool desktopDragging;
        bool held;

        public string PartId => partId;
        public string Category => category;
        public bool IsServiceable => serviceable;
        public AssemblySocket CurrentSocket { get; private set; }

        public void Configure(AssemblyStationController owner, string id, string partCategory,
            bool isServiceable)
        {
            station = owner;
            partId = id;
            category = partCategory;
            serviceable = isServiceable;
        }

        void Awake()
        {
            grabInteractable = GetComponent<XRGrabInteractable>();
            body = GetComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            grabInteractable.movementType = XRBaseInteractable.MovementType.Kinematic;
            grabInteractable.throwOnDetach = false;
            startLocalPosition = transform.localPosition;
            startLocalRotation = transform.localRotation;
        }

        void OnEnable()
        {
            grabInteractable ??= GetComponent<XRGrabInteractable>();
            grabInteractable.selectEntered.AddListener(OnGrabbed);
            grabInteractable.selectExited.AddListener(OnReleased);
        }

        void OnDisable()
        {
            if (grabInteractable == null)
                return;
            grabInteractable.selectEntered.RemoveListener(OnGrabbed);
            grabInteractable.selectExited.RemoveListener(OnReleased);
        }

        void Update()
        {
            if (held || desktopDragging)
                station?.UpdateGhost(this);
        }

        void OnGrabbed(SelectEnterEventArgs _)
        {
            BeginManipulation();
        }

        void OnReleased(SelectExitEventArgs args)
        {
            held = false;
            if (!args.isCanceled)
                SubmitRelease("XRGrab");
            else
                ReturnToStart();
        }

        void OnMouseDown()
        {
            if (!DesktopPointerInputGate.CanUseWorldPointer)
                return;
            var pointerRay = Camera.main != null
                ? Camera.main.ScreenPointToRay(Input.mousePosition) : default;
            desktopDragPlane = new Plane(Vector3.up, transform.position);
            if (!desktopDragPlane.Raycast(pointerRay, out var enter))
                return;
            desktopDragOffset = transform.position - pointerRay.GetPoint(enter);
            desktopDragging = true;
            BeginManipulation();
        }

        void OnMouseDrag()
        {
            if (!desktopDragging || Camera.main == null)
                return;
            var pointerRay = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (desktopDragPlane.Raycast(pointerRay, out var enter))
                transform.position = pointerRay.GetPoint(enter) + desktopDragOffset;
        }

        void OnMouseUp()
        {
            if (!desktopDragging)
                return;
            desktopDragging = false;
            SubmitRelease("DesktopDrag");
        }

        void BeginManipulation()
        {
            held = true;
            if (CurrentSocket != null)
            {
                station?.NotifyRemoved(this, CurrentSocket);
                CurrentSocket = null;
            }
        }

        void SubmitRelease(string inputMode)
        {
            station?.HideGhost();
            if (station != null && station.TrySnap(this, inputMode, out var socket))
            {
                CurrentSocket = socket;
                transform.SetPositionAndRotation(socket.AttachPosition, socket.AttachRotation);
                return;
            }
            ReturnToStart();
        }

        void ReturnToStart()
        {
            transform.SetLocalPositionAndRotation(startLocalPosition, startLocalRotation);
        }
    }
}
