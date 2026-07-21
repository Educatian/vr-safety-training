using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// First-run onboarding board in the lobby: the training flow plus a controls
    /// card that switches between desktop and VR bindings depending on whether an
    /// XR device is active.
    /// </summary>
    public sealed class OnboardingBoard : MonoBehaviour
    {
        [SerializeField] TextMesh controlsDisplay;

        public const string DesktopControls =
            "DESKTOP CONTROLS\n" +
            "WASD  MOVE      RIGHT MOUSE  LOOK\n" +
            "LEFT CLICK  INSPECT / SELECT / TALK\n" +
            "CLICK + DRAG  CARRY AND PLACE PARTS\n" +
            "H  FOLD HUD      ESC  CLOSE CHAT";

        const string VrControls =
            "QUEST CONTROLS\n" +
            "LEFT STICK  MOVE      RIGHT STICK  TURN\n" +
            "TRIGGER  POINT + SELECT / INSPECT / TALK\n" +
            "GRIP  GRAB PARTS - RELEASE NEAR THE GLOW TO SNAP\n" +
            "POINT AT PORTALS TO TRAVEL BETWEEN SITES";

        float nextRefreshAt;
        bool? lastVrActive;

        public void Configure(TextMesh controls)
        {
            controlsDisplay = controls;
        }

        void Update()
        {
            if (controlsDisplay == null || Time.unscaledTime < nextRefreshAt)
                return;
            nextRefreshAt = Time.unscaledTime + 2f;
            var vrActive = HasRunningXrDisplay();
            if (lastVrActive == vrActive)
                return;
            lastVrActive = vrActive;
            controlsDisplay.text = vrActive ? VrControls : DesktopControls;
        }

        static bool HasRunningXrDisplay()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            return displays.Exists(display => display != null && display.running);
        }
    }
}
