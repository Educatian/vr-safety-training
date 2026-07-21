using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.XR;

namespace SafetyTraining.Runtime
{
    public sealed class DesktopExplorerController : MonoBehaviour
    {
        [SerializeField, Min(0.1f)] float moveSpeed = 5f;
        [SerializeField, Min(1f)] float lookSpeed = 100f;

        Transform locomotionRoot;
        CharacterController characterController;
        bool desktopMode;
        bool startupStabilityReported;
        float pitch;
        float startupStabilityDeadline;
        float maximumRootDrift;
        float maximumCameraLocalDrift;
        float maximumCameraAngularDrift;
        Vector3 stableRootPosition;
        InteractiveHoverFeedback hoveredTarget;
        HandsOnPlacementInteractable draggedPlacement;
        readonly List<MonoBehaviour> disabledXrWriters = new();
        bool modeExternallySelected;

        public bool DesktopMode => desktopMode;

        void Start()
        {
            EnsureInitialized();
            if (!modeExternallySelected)
                ApplyDesktopMode(!HasRunningXrDisplay());
        }

        public void SetDesktopMode(bool value)
        {
            modeExternallySelected = true;
            EnsureInitialized();
            ApplyDesktopMode(value);
        }

        void ApplyDesktopMode(bool value)
        {
            desktopMode = value;
            enabled = value;
            if (!value)
            {
                RestoreXrPoseAndLocomotionWriters();
                return;
            }

            DisableXrPoseAndLocomotionWriters();
            transform.SetLocalPositionAndRotation(Vector3.zero, Quaternion.identity);
            pitch = 0f;
            stableRootPosition = locomotionRoot.position;
            startupStabilityDeadline = Time.unscaledTime + 2f;
            Debug.Log("Desktop preview mode: XR pose and locomotion writers disabled; camera is controlled through the rig root.");
        }

        void EnsureInitialized()
        {
            if (locomotionRoot != null)
                return;
            locomotionRoot = transform.root;
            characterController = locomotionRoot.GetComponent<CharacterController>();
        }

        void Update()
        {
            if (!desktopMode)
                return;

            var chatVisible = NpcChatPanel.Instance?.IsVisible ?? false;
            var textEntryFocused = EventSystem.current?.currentSelectedGameObject?.GetComponent<InputField>()?.isFocused ?? false;
            var locomotionBlocked = ShouldBlockLocomotion(chatVisible, textEntryFocused);
            var movement = locomotionBlocked
                ? Vector3.zero
                : new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
            MonitorStartupStability(movement);
            HandlePointer();
            if (locomotionBlocked)
                return;
            var planarForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            var planarRight = Vector3.ProjectOnPlane(transform.right, Vector3.up).normalized;
            var worldMovement = (planarRight * movement.x + planarForward * movement.z).normalized;
            var displacement = worldMovement * (moveSpeed * Time.deltaTime);
            if (characterController != null && characterController.enabled)
                characterController.Move(displacement);
            else
                locomotionRoot.position += displacement;

            if (!Input.GetMouseButton(1))
                return;

            var yaw = Input.GetAxis("Mouse X") * lookSpeed * Time.deltaTime;
            pitch = Mathf.Clamp(pitch - Input.GetAxis("Mouse Y") * lookSpeed * Time.deltaTime, -80f, 80f);
            locomotionRoot.Rotate(0f, yaw, 0f, Space.World);
            transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
        }

        void HandlePointer()
        {
            var ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            if (draggedPlacement != null)
            {
                HandleActiveDesktopDrag(ray, Input.GetMouseButton(0));
                return;
            }

            var chatVisible = NpcChatPanel.Instance?.IsVisible ?? false;
            var pointerOverUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (!DesktopPointerInputGate.CanUseWorldPointer || ShouldBlockWorldPointer(chatVisible, pointerOverUi))
            {
                hoveredTarget?.SetHovered(false);
                hoveredTarget = null;
                return;
            }

            var hits = Physics.RaycastAll(ray, 100f,
                Physics.DefaultRaycastLayers, QueryTriggerInteraction.Collide);
            Array.Sort(hits, (left, right) => left.distance.CompareTo(right.distance));
            Transform target = null;
            InteractiveHoverFeedback nextHoveredTarget = null;
            foreach (var hit in hits)
            {
                var candidate = hit.collider.transform;
                var feedback = candidate.GetComponentInParent<InteractiveHoverFeedback>();
                if (feedback == null && candidate.GetComponentInParent<SitePortal>() == null &&
                    candidate.GetComponentInParent<ExperienceModeToggle>() == null &&
                    candidate.GetComponentInParent<InspectionTarget>() == null &&
                    candidate.GetComponentInParent<ConstructionActionInteractable>() == null &&
                    candidate.GetComponentInParent<SitePracticalAction>() == null &&
                    candidate.GetComponentInParent<NpcTalkInteractable>() == null)
                    continue;
                target = candidate;
                nextHoveredTarget = feedback;
                break;
            }
            if (nextHoveredTarget != hoveredTarget)
            {
                hoveredTarget?.SetHovered(false);
                hoveredTarget = nextHoveredTarget;
                hoveredTarget?.SetHovered(true);
                Debug.Log($"Desktop hover target: {(hoveredTarget != null ? hoveredTarget.name : "none")}");
            }

            if (!Input.GetMouseButtonDown(0) || target == null)
                return;

            var placement = target.GetComponentInParent<HandsOnPlacementInteractable>();
            if (placement != null && placement.BeginDesktopDrag(ray))
            {
                draggedPlacement = placement;
                return;
            }

            ActivateTarget(target);
        }

        static bool ShouldBlockWorldPointer(bool chatVisible, bool pointerOverUi)
        {
            return chatVisible || pointerOverUi || PauseMenuController.VisibleNow;
        }

        static bool ShouldBlockLocomotion(bool chatVisible, bool textEntryFocused)
        {
            return chatVisible || textEntryFocused || PauseMenuController.VisibleNow;
        }

        void HandleActiveDesktopDrag(Ray pointerRay, bool buttonHeld)
        {
            if (draggedPlacement == null)
                return;

            if (buttonHeld)
            {
                draggedPlacement.UpdateDesktopDrag(pointerRay);
                return;
            }

            draggedPlacement.EndDesktopDrag();
            draggedPlacement = null;
        }

        void ActivateTarget(Transform target)
        {
            var portal = target.GetComponentInParent<SitePortal>();
            if (portal != null)
            {
                portal.Activate();
                return;
            }

            var modeToggle = target.GetComponentInParent<ExperienceModeToggle>();
            if (modeToggle != null)
            {
                modeToggle.Toggle();
                return;
            }

            var npc = target.GetComponentInParent<NpcTalkInteractable>();
            if (npc != null)
            {
                npc.Ask();
                return;
            }

            var inspection = target.GetComponentInParent<InspectionTarget>();
            if (inspection != null)
            {
                inspection.Inspect();
                return;
            }

        }

        void OnDisable()
        {
            hoveredTarget?.SetHovered(false);
            hoveredTarget = null;
            draggedPlacement?.CancelDesktopDrag();
            draggedPlacement = null;
        }

        void OnApplicationFocus(bool hasFocus)
        {
            if (hasFocus || draggedPlacement == null)
                return;

            draggedPlacement.CancelDesktopDrag();
            draggedPlacement = null;
        }

        void MonitorStartupStability(Vector3 movement)
        {
            if (startupStabilityReported || movement.sqrMagnitude > 0f || Input.GetMouseButton(1))
                return;

            maximumRootDrift = Mathf.Max(maximumRootDrift, Vector3.Distance(stableRootPosition, locomotionRoot.position));
            maximumCameraLocalDrift = Mathf.Max(maximumCameraLocalDrift, transform.localPosition.magnitude);
            maximumCameraAngularDrift = Mathf.Max(maximumCameraAngularDrift, Quaternion.Angle(Quaternion.identity, transform.localRotation));
            if (Time.unscaledTime < startupStabilityDeadline)
                return;

            startupStabilityReported = true;
            Debug.Log($"Desktop startup stability PASS: root drift {maximumRootDrift:F4} m, camera local drift {maximumCameraLocalDrift:F4} m, camera angular drift {maximumCameraAngularDrift:F3} deg over 2.0 s.");
        }

        static bool HasRunningXrDisplay()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            return displays.Exists(display => display != null && display.running);
        }

        void DisableXrPoseAndLocomotionWriters()
        {
            foreach (var behaviour in locomotionRoot.GetComponentsInChildren<MonoBehaviour>(true))
            {
                if (behaviour == this)
                    continue;

                var type = behaviour.GetType();
                var typeName = type.Name;
                var typeNamespace = type.Namespace ?? string.Empty;
                if (typeName.Contains("TrackedPoseDriver") ||
                    typeNamespace.StartsWith("UnityEngine.XR.Interaction.Toolkit.Locomotion"))
                {
                    if (behaviour.enabled && !disabledXrWriters.Contains(behaviour))
                        disabledXrWriters.Add(behaviour);
                    behaviour.enabled = false;
                }
            }
        }

        void RestoreXrPoseAndLocomotionWriters()
        {
            foreach (var behaviour in disabledXrWriters)
                if (behaviour != null)
                    behaviour.enabled = true;
            disabledXrWriters.Clear();
        }
    }
}
