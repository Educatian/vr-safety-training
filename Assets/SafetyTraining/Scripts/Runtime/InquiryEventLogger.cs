using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    public sealed class InquiryEventLogger : MonoBehaviour
    {
        string logPath;
        string sessionId;

        void Awake()
        {
            EnsureLogPath();
        }

        public void Record(InquiryTelemetryEvent telemetry)
        {
            if (IsVisualCaptureRun())
                return;
            EnsureLogPath();
            var camera = Camera.main;
            var position = camera != null ? camera.transform.position : Vector3.zero;
            var siteZone = SiteExperienceZoneFor(telemetry.SiteId);
            var analyticsZone = camera != null
                ? SpatialAnalyticsZone.FindContaining(telemetry.SiteId, position)
                : null;
            var localPosition = position - (siteZone != null ? siteZone.transform.position : Vector3.zero);
            var entry = new InquiryLogEntry
            {
                timestampUtc = DateTime.UtcNow.ToString("O"),
                sessionId = sessionId,
                eventType = telemetry.EventType,
                phase = telemetry.Phase,
                site = telemetry.SiteId.ToString(),
                objectId = telemetry.ObjectId,
                hazardType = telemetry.HazardType,
                detail = telemetry.Detail,
                hypothesis = telemetry.Hypothesis,
                evidenceCount = telemetry.EvidenceCount,
                collectedItemCount = telemetry.CollectedItemCount,
                distractorCount = telemetry.DistractorCount,
                isDistractor = telemetry.IsDistractor,
                zoneId = analyticsZone != null ? analyticsZone.ZoneId : string.Empty,
                zoneName = analyticsZone != null ? analyticsZone.DisplayName : string.Empty,
                worldX = position.x,
                worldY = position.y,
                worldZ = position.z,
                siteX = localPosition.x,
                siteY = localPosition.y,
                siteZ = localPosition.z
            };
            File.AppendAllText(logPath, JsonConvert.SerializeObject(entry) + Environment.NewLine);
        }

        void EnsureLogPath()
        {
            if (!string.IsNullOrEmpty(logPath))
                return;
            sessionId = SafetyTrainingSession.SessionId;
            var directory = Path.Combine(Application.persistentDataPath, "SafetyTrainingLogs");
            Directory.CreateDirectory(directory);
            logPath = Path.Combine(directory, $"inquiry_{sessionId}.jsonl");
        }

        static bool IsVisualCaptureRun()
        {
            return string.Equals(Environment.GetEnvironmentVariable("SAFETY_CAPTURE_TOUR"), "1",
                StringComparison.Ordinal);
        }

        static SiteExperienceZone SiteExperienceZoneFor(SafetyTraining.Core.TrainingSiteId siteId)
        {
            var zones = FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None);
            for (var index = 0; index < zones.Length; index++)
            {
                if (zones[index].SiteId == siteId)
                    return zones[index];
            }
            return null;
        }

        [Serializable]
        sealed class InquiryLogEntry
        {
            public string timestampUtc = string.Empty;
            public string sessionId = string.Empty;
            public string eventType = string.Empty;
            public string phase = string.Empty;
            public string site = string.Empty;
            public string objectId = string.Empty;
            public string hazardType = string.Empty;
            public string detail = string.Empty;
            public string hypothesis = string.Empty;
            public int evidenceCount;
            public int collectedItemCount;
            public int distractorCount;
            public bool isDistractor;
            public string zoneId = string.Empty;
            public string zoneName = string.Empty;
            public float worldX;
            public float worldY;
            public float worldZ;
            public float siteX;
            public float siteY;
            public float siteZ;
        }
    }
}
