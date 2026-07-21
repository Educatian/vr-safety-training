using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;
using UnityEngine.XR;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    public enum GazeTrackingMode
    {
        Unavailable,
        HeadGaze,
        EyeGaze
    }

    public readonly struct GazeEpisode
    {
        public GazeEpisode(string targetId, string targetKind, float durationSeconds,
            Vector3 hitPosition, bool isDwell)
        {
            TargetId = targetId;
            TargetKind = targetKind;
            DurationSeconds = durationSeconds;
            HitPosition = hitPosition;
            IsDwell = isDwell;
        }

        public string TargetId { get; }
        public string TargetKind { get; }
        public float DurationSeconds { get; }
        public Vector3 HitPosition { get; }
        public bool IsDwell { get; }
        public bool HasValue => !string.IsNullOrEmpty(TargetId);
    }

    public sealed class GazeEpisodeTracker
    {
        string targetId = string.Empty;
        string targetKind = string.Empty;
        float startedAt;
        Vector3 lastHitPosition;

        public GazeEpisode Observe(string nextTargetId, string nextTargetKind, Vector3 hitPosition,
            float now, float dwellThresholdSeconds)
        {
            nextTargetId ??= string.Empty;
            if (nextTargetId == targetId)
            {
                lastHitPosition = hitPosition;
                return default;
            }

            var completed = Complete(now, dwellThresholdSeconds);
            if (!string.IsNullOrEmpty(nextTargetId))
            {
                targetId = nextTargetId;
                targetKind = nextTargetKind ?? string.Empty;
                startedAt = now;
                lastHitPosition = hitPosition;
            }
            return completed;
        }

        public GazeEpisode Complete(float now, float dwellThresholdSeconds)
        {
            if (string.IsNullOrEmpty(targetId))
                return default;

            var duration = Mathf.Max(0f, now - startedAt);
            var completed = new GazeEpisode(targetId, targetKind, duration, lastHitPosition,
                duration >= Mathf.Max(0f, dwellThresholdSeconds));
            targetId = string.Empty;
            targetKind = string.Empty;
            startedAt = 0f;
            lastHitPosition = default;
            return completed;
        }
    }

    [DefaultExecutionOrder(-800)]
    public sealed class GazeAnalyticsTracker : MonoBehaviour
    {
        static readonly InputFeatureUsage<Vector3> GazePosition =
            new InputFeatureUsage<Vector3>("gazePosition");
        static readonly InputFeatureUsage<Quaternion> GazeRotation =
            new InputFeatureUsage<Quaternion>("gazeRotation");

        [SerializeField, Range(0.05f, 1f)] float sampleIntervalSeconds = 0.1f;
        [SerializeField, Range(0.1f, 5f)] float dwellThresholdSeconds = 0.6f;
        [SerializeField, Range(2f, 100f)] float maximumDistanceMeters = 35f;
        [SerializeField] LayerMask raycastLayers = ~0;

        readonly List<InputDevice> eyeDevices = new();
        readonly GazeEpisodeTracker episodeTracker = new();
        TrainingCoordinator coordinator;
        TrainingEventLogger eventLogger;
        Camera viewer;
        float nextSampleAt;
        float nextDeviceRefreshAt;
        GazeTrackingMode activeMode = GazeTrackingMode.Unavailable;
        TrainingSiteId? episodeSite;
        TrainingSiteId? modeLoggedForSite;

        public GazeTrackingMode ActiveMode => activeMode;

        void Awake()
        {
            coordinator = GetComponent<TrainingCoordinator>() ?? TrainingCoordinator.Instance;
            eventLogger = GetComponent<TrainingEventLogger>();
            RefreshEyeDevices();
        }

        void OnEnable()
        {
            InputDevices.deviceConnected += OnInputDeviceChanged;
            InputDevices.deviceDisconnected += OnInputDeviceChanged;
        }

        void OnDisable()
        {
            InputDevices.deviceConnected -= OnInputDeviceChanged;
            InputDevices.deviceDisconnected -= OnInputDeviceChanged;
            FlushEpisode(Time.unscaledTime);
        }

        void Update()
        {
            var now = Time.unscaledTime;
            if (now < nextSampleAt)
                return;
            nextSampleAt = now + Mathf.Max(0.05f, sampleIntervalSeconds);

            coordinator ??= TrainingCoordinator.Instance;
            eventLogger ??= GetComponent<TrainingEventLogger>();
            viewer = Camera.main != null ? Camera.main : viewer;
            if (now >= nextDeviceRefreshAt)
            {
                RefreshEyeDevices();
                nextDeviceRefreshAt = now + 1f;
            }

            var currentSite = coordinator?.ActiveSite;
            if (currentSite != episodeSite)
            {
                FlushEpisode(now);
                episodeSite = currentSite;
                modeLoggedForSite = null;
            }

            var nextMode = ResolveMode(HasValidEyeDevice(), viewer != null);
            if (nextMode != activeMode)
            {
                FlushEpisode(now);
                activeMode = nextMode;
                modeLoggedForSite = null;
            }

            if (!currentSite.HasValue)
                return;
            if (modeLoggedForSite != currentSite)
            {
                eventLogger?.RecordGazeMode(currentSite.Value, ModeToken(activeMode), EyeDeviceName());
                modeLoggedForSite = currentSite;
            }

            if (!TryGetGazeRay(activeMode, out var ray))
            {
                Publish(episodeTracker.Complete(now, dwellThresholdSeconds), currentSite.Value);
                return;
            }

            if (Physics.Raycast(ray, out var hit, maximumDistanceMeters, raycastLayers,
                    QueryTriggerInteraction.Collide) &&
                TryResolveMeaningfulTarget(hit.collider, out var targetId, out var targetKind))
            {
                Publish(episodeTracker.Observe(targetId, targetKind, hit.point, now,
                    dwellThresholdSeconds), currentSite.Value);
            }
            else
            {
                Publish(episodeTracker.Complete(now, dwellThresholdSeconds), currentSite.Value);
            }
        }

        public static GazeTrackingMode ResolveMode(bool eyeTrackingDeviceAvailable, bool cameraAvailable)
        {
            if (eyeTrackingDeviceAvailable)
                return GazeTrackingMode.EyeGaze;
            return cameraAvailable ? GazeTrackingMode.HeadGaze : GazeTrackingMode.Unavailable;
        }

        void RefreshEyeDevices()
        {
            eyeDevices.Clear();
            InputDevices.GetDevicesWithCharacteristics(InputDeviceCharacteristics.EyeTracking, eyeDevices);
        }

        void OnInputDeviceChanged(InputDevice _)
        {
            RefreshEyeDevices();
            nextDeviceRefreshAt = Time.unscaledTime + 1f;
        }

        bool HasValidEyeDevice()
        {
            for (var index = 0; index < eyeDevices.Count; index++)
                if (eyeDevices[index].isValid)
                    return true;
            return false;
        }

        string EyeDeviceName()
        {
            if (activeMode != GazeTrackingMode.EyeGaze)
                return activeMode == GazeTrackingMode.HeadGaze ? "head_pose_fallback" : "unavailable";
            for (var index = 0; index < eyeDevices.Count; index++)
                if (eyeDevices[index].isValid)
                    return string.IsNullOrWhiteSpace(eyeDevices[index].name)
                        ? "openxr_eye_gaze" : eyeDevices[index].name;
            return "openxr_eye_gaze";
        }

        bool TryGetGazeRay(GazeTrackingMode mode, out Ray ray)
        {
            if (mode == GazeTrackingMode.EyeGaze)
                return TryGetEyeGazeRay(out ray);
            if (mode == GazeTrackingMode.HeadGaze && viewer != null)
            {
                ray = new Ray(viewer.transform.position, viewer.transform.forward);
                return true;
            }
            ray = default;
            return false;
        }

        bool TryGetEyeGazeRay(out Ray ray)
        {
            for (var index = 0; index < eyeDevices.Count; index++)
            {
                var device = eyeDevices[index];
                if (!device.isValid ||
                    !device.TryGetFeatureValue(CommonUsages.isTracked, out var tracked) || !tracked ||
                    !device.TryGetFeatureValue(GazePosition, out var localPosition) ||
                    !device.TryGetFeatureValue(GazeRotation, out var localRotation))
                    continue;

                var trackingSpace = viewer != null ? viewer.transform.parent : null;
                var origin = trackingSpace != null
                    ? trackingSpace.TransformPoint(localPosition) : localPosition;
                var direction = trackingSpace != null
                    ? trackingSpace.TransformDirection(localRotation * Vector3.forward)
                    : localRotation * Vector3.forward;
                ray = new Ray(origin, direction.normalized);
                return true;
            }
            ray = default;
            return false;
        }

        static bool TryResolveMeaningfulTarget(Collider hitCollider, out string targetId,
            out string targetKind)
        {
            var inspection = hitCollider.GetComponentInParent<InspectionTarget>();
            if (inspection != null)
                return Resolved("inspection", inspection.TargetId, out targetId, out targetKind);
            var evidence = hitCollider.GetComponentInParent<EvidenceObject>();
            if (evidence != null)
                return Resolved("evidence", evidence.EvidenceId, out targetId, out targetKind);
            var constructionAction = hitCollider.GetComponentInParent<ConstructionActionInteractable>();
            if (constructionAction != null)
                return Resolved("construction_practical", constructionAction.ActionName,
                    out targetId, out targetKind);
            var siteAction = hitCollider.GetComponentInParent<SitePracticalAction>();
            if (siteAction != null)
                return Resolved("site_practical", siteAction.ActionName, out targetId, out targetKind);
            var npc = hitCollider.GetComponentInParent<NpcConversationAgent>();
            if (npc != null)
                return Resolved("npc", npc.gameObject.name, out targetId, out targetKind);
            var interactable = hitCollider.GetComponentInParent<XRSimpleInteractable>();
            if (interactable != null)
                return Resolved("interactable", interactable.gameObject.name, out targetId, out targetKind);
            targetId = string.Empty;
            targetKind = string.Empty;
            return false;
        }

        static bool Resolved(string kind, string id, out string targetId, out string targetKind)
        {
            targetKind = kind;
            targetId = $"{kind}:{id}";
            return true;
        }

        void Publish(GazeEpisode episode, TrainingSiteId site)
        {
            if (!episode.HasValue)
                return;
            eventLogger?.RecordGazeEpisode(site, episode.TargetId, episode.TargetKind,
                ModeToken(activeMode), episode.DurationSeconds, episode.HitPosition, episode.IsDwell);
        }

        void FlushEpisode(float now)
        {
            var completed = episodeTracker.Complete(now, dwellThresholdSeconds);
            if (completed.HasValue && episodeSite.HasValue)
                Publish(completed, episodeSite.Value);
        }

        static string ModeToken(GazeTrackingMode mode)
        {
            return mode switch
            {
                GazeTrackingMode.EyeGaze => "eye_gaze",
                GazeTrackingMode.HeadGaze => "head_gaze_fallback",
                _ => "unavailable"
            };
        }
    }
}
