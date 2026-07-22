using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Management;

namespace SafetyTraining.Runtime
{
    public enum TrainingExperienceMode
    {
        Auto,
        Desktop,
        ImmersiveVr
    }

    [DefaultExecutionOrder(-1100)]
    public sealed class ExperienceModeController : MonoBehaviour
    {
        [SerializeField] DesktopExplorerController desktopController;

        Coroutine transition;

        public static ExperienceModeController Instance { get; private set; }
        public TrainingExperienceMode ActiveMode { get; private set; } = TrainingExperienceMode.Auto;
        public bool Transitioning => transition != null;
        public string StatusMessage { get; private set; } = "Detecting experience mode";

        public event System.Action<TrainingExperienceMode, string> ModeChanged;

        void Awake()
        {
            Instance = this;
            if (desktopController == null)
                desktopController = GetComponentInChildren<DesktopExplorerController>(true);
        }

        void Start()
        {
            var requested = ReadCommandLineMode();
            var resolved = ResolveStartupMode(requested, Application.platform, HasRunningXrDisplay());
            RequestMode(resolved);
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public void Configure(DesktopExplorerController controller)
        {
            desktopController = controller;
        }

        public void ToggleMode()
        {
            if (Transitioning)
                return;

            RequestMode(ActiveMode == TrainingExperienceMode.ImmersiveVr
                ? TrainingExperienceMode.Desktop
                : TrainingExperienceMode.ImmersiveVr);
        }

        public void RequestMode(TrainingExperienceMode mode)
        {
            NpcChatPanel.Instance?.Close();
            if (transition != null)
                StopCoroutine(transition);
            transition = StartCoroutine(ApplyMode(mode));
        }

        IEnumerator ApplyMode(TrainingExperienceMode mode)
        {
            if (mode == TrainingExperienceMode.Desktop)
            {
                if (Application.platform == RuntimePlatform.Android)
                {
                    Publish(TrainingExperienceMode.ImmersiveVr,
                        "Quest standalone stays in IVR. Use the Windows build for desktop mode.");
                    transition = null;
                    yield break;
                }

                StopXr();
                desktopController?.SetDesktopMode(true);
                Publish(TrainingExperienceMode.Desktop, "Desktop exploration active: WASD, mouse look, click and drag.");
                transition = null;
                yield break;
            }

            desktopController?.SetDesktopMode(false);
            if (Application.platform != RuntimePlatform.Android && !HasRunningXrDisplay())
            {
                RevertToDesktop("IVR runtime is not already active. Start Meta Quest Link/OpenXR first, or use the Quest APK.");
                yield break;
            }

            var manager = XRGeneralSettings.Instance?.Manager;
            if (manager == null)
            {
                RevertToDesktop("IVR unavailable: XR Management is not configured for this build.");
                yield break;
            }

            if (manager.activeLoader == null)
                manager.InitializeLoaderSync();
            if (manager.activeLoader == null)
            {
                RevertToDesktop("IVR unavailable: start Meta Quest Link/OpenXR or install the Quest APK.");
                yield break;
            }

            manager.StartSubsystems();
            for (var frame = 0; frame < 30 && !HasRunningXrDisplay(); frame++)
                yield return null;

            if (!HasRunningXrDisplay())
            {
                manager.StopSubsystems();
                manager.DeinitializeLoader();
                RevertToDesktop("IVR runtime did not start. Check Meta Quest Link and set it as OpenXR runtime.");
                yield break;
            }

            Publish(TrainingExperienceMode.ImmersiveVr,
                Application.platform == RuntimePlatform.Android
                    ? "Meta Quest standalone IVR active."
                    : "Meta Quest / OpenXR IVR active.");
            transition = null;
        }

        void RevertToDesktop(string reason)
        {
            desktopController?.SetDesktopMode(true);
            Publish(TrainingExperienceMode.Desktop, reason);
            transition = null;
        }

        void StopXr()
        {
            var manager = XRGeneralSettings.Instance?.Manager;
            if (manager == null || manager.activeLoader == null)
                return;
            manager.StopSubsystems();
            manager.DeinitializeLoader();
        }

        void Publish(TrainingExperienceMode mode, string message)
        {
            ActiveMode = mode;
            StatusMessage = message;
            TrainingCoordinator.Instance?.SetContextFeedback(message);
            ModeChanged?.Invoke(mode, message);
            Debug.Log($"Experience mode: {mode}. {message}");
        }

        public static TrainingExperienceMode ResolveStartupMode(
            TrainingExperienceMode requested,
            RuntimePlatform platform,
            bool xrDisplayRunning)
        {
            if (platform == RuntimePlatform.Android)
                return TrainingExperienceMode.ImmersiveVr;
            if (requested != TrainingExperienceMode.Auto)
                return requested;
            return xrDisplayRunning ? TrainingExperienceMode.ImmersiveVr : TrainingExperienceMode.Desktop;
        }

        static TrainingExperienceMode ReadCommandLineMode()
        {
            var args = System.Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (!string.Equals(args[index], "-experienceMode", System.StringComparison.OrdinalIgnoreCase))
                    continue;
                if (string.Equals(args[index + 1], "ivr", System.StringComparison.OrdinalIgnoreCase) ||
                    string.Equals(args[index + 1], "quest", System.StringComparison.OrdinalIgnoreCase))
                    return TrainingExperienceMode.ImmersiveVr;
                if (string.Equals(args[index + 1], "desktop", System.StringComparison.OrdinalIgnoreCase))
                    return TrainingExperienceMode.Desktop;
            }
            return TrainingExperienceMode.Auto;
        }

        static bool HasRunningXrDisplay()
        {
            var displays = new List<XRDisplaySubsystem>();
            SubsystemManager.GetSubsystems(displays);
            return displays.Exists(display => display != null && display.running);
        }
    }
}
