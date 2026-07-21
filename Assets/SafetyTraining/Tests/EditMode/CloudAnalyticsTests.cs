using NUnit.Framework;
using SafetyTraining.Runtime;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class CloudAnalyticsTests
    {
        [Test]
        public void EndpointPolicy_AllowsWorkerHttpsAndRejectsDirectCloudflareApi()
        {
            Assert.That(CloudAnalyticsSettings.IsAllowedEndpoint(
                "https://teacher-training-collector.example.workers.dev"), Is.True);
            Assert.That(CloudAnalyticsSettings.IsAllowedEndpoint(
                "http://teacher-training-collector.example.workers.dev"), Is.False);
            Assert.That(CloudAnalyticsSettings.IsAllowedEndpoint(
                "https://api.cloudflare.com/client/v4"), Is.False);
        }

        [Test]
        public void WireEvent_FromSpatialEventPreservesGazeEvidence()
        {
            var source = new TrainingEventLogger.SpatialAnalyticsEvent
            {
                timestampUtc = "2026-07-20T20:00:01.000Z",
                sessionId = "11111111111111111111111111111111",
                sequence = 7,
                eventType = "gaze_target_dwell",
                site = "Construction",
                subjectId = "inspection:guardrail-gap",
                gazeMode = "eye_gaze",
                targetKind = "inspection",
                durationOrDistance = 1.25f,
                hitSiteX = 2f,
                hitSiteY = 1.6f,
                hitSiteZ = 4f
            };

            var wire = SafetyCloudEvent.FromSpatial(source);

            Assert.That(wire.eventId, Is.EqualTo(source.sessionId + "-7"));
            Assert.That(wire.kind, Is.EqualTo(SafetyCloudEvent.GazeDwellKind));
            Assert.That(wire.gazeMode, Is.EqualTo("eye_gaze"));
            Assert.That(wire.hitSiteZ, Is.EqualTo(4f));
        }

[Test]
        public void WireEvent_MapsHeadGazeModeWithoutClaimingEyeTracking()
        {
            var source = new TrainingEventLogger.SpatialAnalyticsEvent
            {
                timestampUtc = "2026-07-20T20:00:01.000Z",
                sessionId = "22222222222222222222222222222222",
                sequence = 8,
                eventType = "gaze_target_glance",
                gazeMode = "head_gaze_fallback"
            };

            var wire = SafetyCloudEvent.FromSpatial(source);

            Assert.That(wire.kind, Is.EqualTo(SafetyCloudEvent.GazeGlanceKind));
            Assert.That(wire.gazeMode, Is.EqualTo("head_gaze_fallback"));
        }
    }
}
