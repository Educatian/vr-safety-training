using System;
using System.IO;
using Newtonsoft.Json;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    public sealed class TrainingEventLogger : MonoBehaviour
    {
        string logPath;
        string sessionId;
        float nextSpatialSampleAt;
        TrainingSiteId? activeSite;
        float activeSiteStartedAt;
        string activeZoneId = string.Empty;
        string activeZoneName = string.Empty;
        float activeZoneStartedAt;
        Vector3 lastSpatialSamplePosition;
        bool hasSpatialSamplePosition;

        void Update()
        {
            if (IsVisualCaptureRun() || Time.unscaledTime < nextSpatialSampleAt)
                return;

            nextSpatialSampleAt = Time.unscaledTime + 1f;
            var coordinator = FindFirstObjectByType<TrainingCoordinator>() ?? TrainingCoordinator.Instance;
            var currentSite = coordinator?.ActiveSite;
            if (currentSite != activeSite)
                RecordSiteTransition(currentSite);
            if (!currentSite.HasValue)
                return;

            RecordAnalyticsZoneTransition(currentSite.Value);
            var viewer = Camera.main;
            var sampleDistance = 0f;
            if (viewer != null)
            {
                if (hasSpatialSamplePosition)
                    sampleDistance = Vector3.Distance(viewer.transform.position, lastSpatialSamplePosition);
                lastSpatialSamplePosition = viewer.transform.position;
                hasSpatialSamplePosition = true;
            }
            AppendSpatialEvent("spatial_sample", currentSite.Value, null, null, sampleDistance,
                "distance_meters");
        }

        void Awake()
        {
            EnsureLogPath();
        }

        void EnsureLogPath()
        {
            if (!string.IsNullOrEmpty(logPath))
                return;
            sessionId = SafetyTrainingSession.SessionId;
            var directory = Path.Combine(Application.persistentDataPath, "SafetyTrainingLogs");
            Directory.CreateDirectory(directory);
            logPath = Path.Combine(directory, $"session_{sessionId}.jsonl");
        }

        public void Record(InspectionTarget target, InspectionResult result, int overallScore)
        {
            if (IsVisualCaptureRun())
                return;
            EnsureLogPath();
            var entry = new TrainingEvent
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                sessionId = sessionId,
                site = target.SiteId.ToString(),
                targetId = target.TargetId,
                isHazard = target.IsHazard,
                outcome = result.Outcome.ToString(),
                scoreDelta = result.ScoreDelta,
                siteScore = result.Score,
                overallScore = overallScore,
                hazardsFound = result.HazardsFound,
                hazardsRequired = result.HazardsRequired,
                siteComplete = result.IsComplete
            };
            File.AppendAllText(logPath, JsonConvert.SerializeObject(entry) + Environment.NewLine);
            AppendSpatialEvent("inspection", target.SiteId, target.TargetId, result.Outcome.ToString(), 0f,
                "instant");
        }

        public void RecordPlacement(TrainingSiteId siteId, int stepIndex, int totalSteps, string actionName,
            string instruction, float releaseDistance, bool success, string inputMode)
        {
            if (IsVisualCaptureRun())
                return;
            EnsureLogPath();
            var entry = new PlacementEvent
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                sessionId = sessionId,
                eventType = "placement_attempt",
                site = siteId.ToString(),
                stepIndex = stepIndex,
                totalSteps = totalSteps,
                actionName = actionName,
                instruction = instruction,
                releaseDistance = releaseDistance,
                success = success,
                inputMode = inputMode
            };
            File.AppendAllText(logPath, JsonConvert.SerializeObject(entry) + Environment.NewLine);
            AppendSpatialEvent("placement_attempt", siteId, actionName,
                success ? "success" : "retry", releaseDistance, "release_distance_meters");
        }

        public void RecordCoachTurn(TrainingSiteId siteId)
        {
            if (!IsVisualCaptureRun())
                AppendSpatialEvent("coach_turn", siteId, "safety_coach", null, 0f, "instant");
        }

        void RecordSiteTransition(TrainingSiteId? currentSite)
        {
            if (activeSite.HasValue)
            {
                RecordAnalyticsZoneTransition(null);
                AppendSpatialEvent("site_exit", activeSite.Value, null, null,
                    Time.unscaledTime - activeSiteStartedAt, "duration_seconds");
            }
            activeSite = currentSite;
            activeSiteStartedAt = Time.unscaledTime;
            hasSpatialSamplePosition = false;
            if (activeSite.HasValue)
                AppendSpatialEvent("site_enter", activeSite.Value, null, null, 0f, "instant");
        }

        void RecordAnalyticsZoneTransition(TrainingSiteId? currentSite)
        {
            var viewer = Camera.main;
            var nextZone = currentSite.HasValue && viewer != null
                ? SpatialAnalyticsZone.FindContaining(currentSite.Value, viewer.transform.position)
                : null;
            var nextZoneId = nextZone != null ? nextZone.ZoneId : string.Empty;
            var nextZoneName = nextZone != null ? nextZone.DisplayName : string.Empty;
            if (nextZoneId == activeZoneId)
                return;

            if (!string.IsNullOrEmpty(activeZoneId) && activeSite.HasValue)
                AppendSpatialEvent("zone_exit", activeSite.Value, activeZoneId, null,
                    Time.unscaledTime - activeZoneStartedAt, "duration_seconds");

            activeZoneId = nextZoneId;
            activeZoneName = nextZoneName;
            activeZoneStartedAt = Time.unscaledTime;
            if (currentSite.HasValue && !string.IsNullOrEmpty(activeZoneId))
                AppendSpatialEvent("zone_enter", currentSite.Value, activeZoneId, null, 0f, "instant");
        }

        void AppendSpatialEvent(string eventType, TrainingSiteId siteId, string subjectId,
            string outcome, float durationOrDistance, string metricKind)
        {
            EnsureLogPath();
            var viewer = Camera.main;
            if (viewer == null)
                return;
            var siteZone = SiteExperienceZoneFor(siteId);
            var analyticsZone = SpatialAnalyticsZone.FindContaining(siteId, viewer.transform.position);
            var localPosition = viewer.transform.position - (siteZone != null
                ? siteZone.transform.position : Vector3.zero);
            var entry = new SpatialEvent
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                sessionId = sessionId,
                eventType = eventType,
                site = siteId.ToString(),
                subjectId = subjectId ?? string.Empty,
                outcome = outcome ?? string.Empty,
                zoneId = analyticsZone != null ? analyticsZone.ZoneId : activeZoneId,
                zoneName = analyticsZone != null ? analyticsZone.DisplayName : activeZoneName,
                worldX = viewer.transform.position.x,
                worldY = viewer.transform.position.y,
                worldZ = viewer.transform.position.z,
                siteX = localPosition.x,
                siteY = localPosition.y,
                siteZ = localPosition.z,
                durationOrDistance = durationOrDistance,
                metricKind = metricKind
            };
            File.AppendAllText(logPath, JsonConvert.SerializeObject(entry) + Environment.NewLine);
        }

        static SiteExperienceZone SiteExperienceZoneFor(TrainingSiteId siteId)
        {
            var zones = FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None);
            for (var index = 0; index < zones.Length; index++)
            {
                if (zones[index].SiteId == siteId)
                    return zones[index];
            }
            return null;
        }

        static bool IsVisualCaptureRun()
        {
            return string.Equals(Environment.GetEnvironmentVariable("SAFETY_CAPTURE_TOUR"), "1",
                StringComparison.Ordinal);
        }

        [Serializable]
        sealed class TrainingEvent
        {
            public string timestampUtc = string.Empty;
            public string sessionId = string.Empty;
            public string site = string.Empty;
            public string targetId = string.Empty;
            public bool isHazard;
            public string outcome = string.Empty;
            public int scoreDelta;
            public int siteScore;
            public int overallScore;
            public int hazardsFound;
            public int hazardsRequired;
            public bool siteComplete;
        }

        [Serializable]
        sealed class PlacementEvent
        {
            public string timestampUtc = string.Empty;
            public string sessionId = string.Empty;
            public string eventType = string.Empty;
            public string site = string.Empty;
            public int stepIndex;
            public int totalSteps;
            public string actionName = string.Empty;
            public string instruction = string.Empty;
            public float releaseDistance;
            public bool success;
            public string inputMode = string.Empty;
        }

        [Serializable]
        sealed class SpatialEvent
        {
            public string timestampUtc = string.Empty;
            public string sessionId = string.Empty;
            public string eventType = string.Empty;
            public string site = string.Empty;
            public string subjectId = string.Empty;
            public string outcome = string.Empty;
            public string zoneId = string.Empty;
            public string zoneName = string.Empty;
            public float worldX;
            public float worldY;
            public float worldZ;
            public float siteX;
            public float siteY;
            public float siteZ;
            public float durationOrDistance;
            public string metricKind = string.Empty;
        }
    }
}
