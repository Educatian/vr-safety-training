using System;

namespace SafetyTraining.Runtime
{
    public static class CloudAnalyticsSettings
    {
        public const string Endpoint = "https://teacher-training-collector.jewoong-moon.workers.dev";
        public const string ClientId = "teacher-training-quest";
        public const string ScenarioId = "vr-safety-training";

        public static bool IsAllowedEndpoint(string value)
        {
            if (!Uri.TryCreate(value, UriKind.Absolute, out var uri) ||
                uri.Scheme != Uri.UriSchemeHttps)
                return false;
            var host = uri.Host.ToLowerInvariant();
            return host != "api.cloudflare.com" &&
                   !host.EndsWith(".r2.cloudflarestorage.com", StringComparison.Ordinal);
        }
    }

    [Serializable]
    public sealed class CloudQuestTokenRequest
    {
        public int schemaVersion = 1;
        public string clientId = CloudAnalyticsSettings.ClientId;
        public string installationId = string.Empty;
        public string buildVersion = string.Empty;
        public string deviceModel = string.Empty;
    }

    [Serializable]
    public sealed class CloudQuestTokenResponse
    {
        public int schemaVersion;
        public string token = string.Empty;
        public string participantCode = string.Empty;
        public string expiresAtUtc = string.Empty;
    }

    [Serializable]
    public sealed class CloudSessionStart
    {
        public int schemaVersion = 1;
        public string sessionId = string.Empty;
        public string participantCode = string.Empty;
        public string scenarioId = CloudAnalyticsSettings.ScenarioId;
        public string startedAtUtc = string.Empty;
        public string deviceModel = string.Empty;
        public string buildVersion = string.Empty;
        public bool rawGazeConsent;
    }

    [Serializable]
    public sealed class SafetyCloudEvent
    {
        public const int GazeModeKind = 100;
        public const int GazeGlanceKind = 101;
        public const int GazeDwellKind = 102;
        public const int SpatialSampleKind = 103;
        public const int InteractionKind = 199;
        public const int AssessmentKind = 200;

        // Assessment events reuse the spatial queue; offsetting their sequence keeps
        // queue file names and event ids from colliding with spatial events.
        public const int AssessmentSequenceOffset = 1000000;

        public int schemaVersion = 1;
        public string eventId = string.Empty;
        public string sessionId = string.Empty;
        public int sequence;
        public string timestampUtc = string.Empty;
        public string scenarioId = CloudAnalyticsSettings.ScenarioId;
        public int beatIndex;
        public int kind;
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
        public string gazeMode = string.Empty;
        public string targetKind = string.Empty;
        public float hitWorldX;
        public float hitWorldY;
        public float hitWorldZ;
        public float hitSiteX;
        public float hitSiteY;
        public float hitSiteZ;

        public static SafetyCloudEvent FromSpatial(TrainingEventLogger.SpatialAnalyticsEvent source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            return new SafetyCloudEvent
            {
                eventId = source.sessionId + "-" + source.sequence,
                sessionId = source.sessionId,
                sequence = source.sequence,
                timestampUtc = source.timestampUtc,
                kind = KindFor(source.eventType),
                eventType = source.eventType,
                site = source.site,
                subjectId = source.subjectId,
                outcome = source.outcome,
                zoneId = source.zoneId,
                zoneName = source.zoneName,
                worldX = source.worldX,
                worldY = source.worldY,
                worldZ = source.worldZ,
                siteX = source.siteX,
                siteY = source.siteY,
                siteZ = source.siteZ,
                durationOrDistance = source.durationOrDistance,
                metricKind = source.metricKind,
                gazeMode = source.gazeMode,
                targetKind = source.targetKind,
                hitWorldX = source.hitWorldX,
                hitWorldY = source.hitWorldY,
                hitWorldZ = source.hitWorldZ,
                hitSiteX = source.hitSiteX,
                hitSiteY = source.hitSiteY,
                hitSiteZ = source.hitSiteZ
            };
        }

        // Maps assessment evidence into the existing event columns so the collector
        // schema stays unchanged: subjectId=criterionId (objectId fallback),
        // targetKind=objectiveId, durationOrDistance=earnedPoints,
        // metricKind=assessment_points. Free text is intentionally absent.
        public static SafetyCloudEvent FromAssessment(AssessmentAnalyticsEvent source)
        {
            if (source == null)
                throw new ArgumentNullException(nameof(source));
            var sequence = AssessmentSequenceOffset + source.Sequence;
            return new SafetyCloudEvent
            {
                eventId = source.SessionId + "-" + sequence,
                sessionId = source.SessionId,
                sequence = sequence,
                timestampUtc = source.TimestampUtc,
                kind = AssessmentKind,
                eventType = source.EventType,
                site = source.Site,
                subjectId = string.IsNullOrEmpty(source.CriterionId)
                    ? source.ObjectId : source.CriterionId,
                outcome = string.IsNullOrEmpty(source.Outcome) && source.IsDistractor
                    ? "distractor" : source.Outcome,
                zoneId = source.ZoneId,
                zoneName = source.ZoneName,
                siteX = source.SiteX,
                siteY = source.SiteY,
                siteZ = source.SiteZ,
                durationOrDistance = source.EarnedPoints,
                metricKind = "assessment_points",
                targetKind = source.ObjectiveId
            };
        }

        static int KindFor(string eventType)
        {
            return eventType switch
            {
                "gaze_mode_changed" => GazeModeKind,
                "gaze_target_glance" => GazeGlanceKind,
                "gaze_target_dwell" => GazeDwellKind,
                "spatial_sample" => SpatialSampleKind,
                _ => InteractionKind
            };
        }
    }

    [Serializable]
    public sealed class CloudEventBatch
    {
        public int schemaVersion = 1;
        public string requestId = string.Empty;
        public SafetyCloudEvent[] events = Array.Empty<SafetyCloudEvent>();
    }
}
