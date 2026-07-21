using System;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// Assessment-relevant inquiry event stripped of free-text fields, raised for
    /// cloud upload. Detail and hypothesis text stay local-only by policy.
    /// </summary>
    public sealed class AssessmentAnalyticsEvent
    {
        public string SessionId = string.Empty;
        public int Sequence;
        public string TimestampUtc = string.Empty;
        public string EventType = string.Empty;
        public string Phase = string.Empty;
        public string Site = string.Empty;
        public string ObjectId = string.Empty;
        public string ObjectiveId = string.Empty;
        public string CriterionId = string.Empty;
        public string Outcome = string.Empty;
        public int EarnedPoints;
        public int PossiblePoints;
        public bool IsDistractor;
        public string ZoneId = string.Empty;
        public string ZoneName = string.Empty;
        public float SiteX;
        public float SiteY;
        public float SiteZ;
    }

    public sealed class InquiryEventLogger : MonoBehaviour
    {
        string logPath;
        string sessionId;
        int sequence;
        bool writeFailureLogged;

        public static event Action<AssessmentAnalyticsEvent> AssessmentRecorded;

        void Awake()
        {
            EnsureLogPath();
        }

        void SafeAppendLine(string serialized)
        {
            try
            {
                File.AppendAllText(logPath, serialized + Environment.NewLine);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                if (writeFailureLogged)
                    return;
                writeFailureLogged = true;
                Debug.LogWarning($"Inquiry telemetry write failed; further failures suppressed: {e.Message}");
            }
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
                sequence = sequence++,
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
                objectiveId = telemetry.ObjectiveId,
                criterionId = telemetry.CriterionId,
                outcome = telemetry.Outcome,
                earnedPoints = telemetry.EarnedPoints,
                possiblePoints = telemetry.PossiblePoints,
                zoneId = analyticsZone != null ? analyticsZone.ZoneId : string.Empty,
                zoneName = analyticsZone != null ? analyticsZone.DisplayName : string.Empty,
                worldX = position.x,
                worldY = position.y,
                worldZ = position.z,
                siteX = localPosition.x,
                siteY = localPosition.y,
                siteZ = localPosition.z
            };
            SafeAppendLine(JsonConvert.SerializeObject(entry));
            AssessmentRecorded?.Invoke(new AssessmentAnalyticsEvent
            {
                SessionId = entry.sessionId,
                Sequence = entry.sequence,
                TimestampUtc = entry.timestampUtc,
                EventType = entry.eventType,
                Phase = entry.phase,
                Site = entry.site,
                ObjectId = entry.objectId,
                ObjectiveId = entry.objectiveId,
                CriterionId = entry.criterionId,
                Outcome = entry.outcome,
                EarnedPoints = entry.earnedPoints,
                PossiblePoints = entry.possiblePoints,
                IsDistractor = entry.isDistractor,
                ZoneId = entry.zoneId,
                ZoneName = entry.zoneName,
                SiteX = entry.siteX,
                SiteY = entry.siteY,
                SiteZ = entry.siteZ
            });
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
            public int schemaVersion = TelemetrySchema.Version;
            public string timestampUtc = string.Empty;
            public string sessionId = string.Empty;
            public int sequence;
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
            public string objectiveId = string.Empty;
            public string criterionId = string.Empty;
            public string outcome = string.Empty;
            public int earnedPoints;
            public int possiblePoints;
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
