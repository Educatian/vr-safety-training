using System.IO;
using NUnit.Framework;
using SafetyTraining.Editor;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class AnalyticsLogExporterTests
    {
        string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "vr-safety-analytics-export-tests", TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }

        [Test]
        public void ExportDirectory_JoinsSpatialAndInquiryBySessionId()
        {
            var logs = Path.Combine(tempRoot, "logs");
            var output = Path.Combine(tempRoot, "output");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "session_learner001.jsonl"),
                "{\"timestampUtc\":\"2026-07-18T16:00:00.0000000Z\",\"sessionId\":\"learner001\",\"eventType\":\"zone_enter\",\"site\":\"Construction\",\"zoneId\":\"construction_left_evidence\",\"zoneName\":\"Left Evidence Run\",\"worldX\":10,\"worldY\":1.8,\"worldZ\":2,\"siteX\":-5.2,\"siteY\":1.2,\"siteZ\":0.8,\"durationOrDistance\":0,\"metricKind\":\"instant\"}\n" +
                "{\"timestampUtc\":\"2026-07-18T16:00:03.0000000Z\",\"sessionId\":\"learner001\",\"eventType\":\"spatial_sample\",\"site\":\"Construction\",\"zoneId\":\"construction_left_evidence\",\"zoneName\":\"Left Evidence Run\",\"worldX\":10.4,\"worldY\":1.8,\"worldZ\":2.3,\"siteX\":-4.8,\"siteY\":1.2,\"siteZ\":1.1,\"durationOrDistance\":0.5,\"metricKind\":\"distance_meters\"}\n" +
                "{\"timestampUtc\":\"2026-07-18T16:00:08.0000000Z\",\"sessionId\":\"learner001\",\"eventType\":\"placement_attempt\",\"site\":\"Construction\",\"zoneId\":\"construction_left_evidence\",\"zoneName\":\"Left Evidence Run\",\"subjectId\":\"Install guardrail toe-board\",\"outcome\":\"success\",\"worldX\":10.7,\"worldY\":1.8,\"worldZ\":2.4,\"siteX\":-4.5,\"siteY\":1.2,\"siteZ\":1.2,\"durationOrDistance\":0.2,\"metricKind\":\"release_distance_meters\"}\n" +
                "{\"timestampUtc\":\"2026-07-18T16:00:10.0000000Z\",\"sessionId\":\"learner001\",\"eventType\":\"coach_turn\",\"site\":\"Construction\",\"zoneId\":\"construction_left_evidence\",\"zoneName\":\"Left Evidence Run\",\"subjectId\":\"safety_coach\",\"worldX\":10.9,\"worldY\":1.8,\"worldZ\":2.4,\"siteX\":-4.3,\"siteY\":1.2,\"siteZ\":1.2,\"durationOrDistance\":0,\"metricKind\":\"instant\"}\n" +
                "{\"timestampUtc\":\"2026-07-18T16:00:12.0000000Z\",\"sessionId\":\"learner001\",\"eventType\":\"zone_exit\",\"site\":\"Construction\",\"zoneId\":\"construction_left_evidence\",\"zoneName\":\"Left Evidence Run\",\"worldX\":11,\"worldY\":1.8,\"worldZ\":2.5,\"siteX\":-4.2,\"siteY\":1.2,\"siteZ\":1.3,\"durationOrDistance\":12,\"metricKind\":\"duration_seconds\"}\n" +
                "{\"timestampUtc\":\"2026-07-18T16:00:14.0000000Z\",\"sessionId\":\"learner001\",\"eventType\":\"site_exit\",\"site\":\"Construction\",\"zoneId\":\"construction_left_evidence\",\"zoneName\":\"Left Evidence Run\",\"worldX\":11.1,\"worldY\":1.8,\"worldZ\":2.6,\"siteX\":-4.1,\"siteY\":1.2,\"siteZ\":1.4,\"durationOrDistance\":14,\"metricKind\":\"duration_seconds\"}\n");
            File.WriteAllText(Path.Combine(logs, "inquiry_learner001.jsonl"),
                "{\"timestampUtc\":\"2026-07-18T16:00:05.0000000Z\",\"sessionId\":\"learner001\",\"eventType\":\"evidence_collected\",\"phase\":\"evidence\",\"site\":\"Construction\",\"objectId\":\"fall_edge_photo\",\"hazardType\":\"fall_protection\",\"detail\":\"Guardrail gap photographed\",\"zoneId\":\"construction_left_evidence\",\"zoneName\":\"Left Evidence Run\",\"siteX\":-5,\"siteY\":1.2,\"siteZ\":1}\n");

            var result = AnalyticsLogExporter.ExportDirectory(logs, output);

            Assert.That(result.SpatialRowCount, Is.EqualTo(7));
            Assert.That(result.InquiryRowCount, Is.EqualTo(1));
            Assert.That(result.DwellRowCount, Is.EqualTo(1));
            Assert.That(result.RouteSummaryRowCount, Is.EqualTo(1));
            var spatialCsv = File.ReadAllText(result.SpatialPath);
            Assert.That(spatialCsv, Does.Contain("learner001"));
            Assert.That(spatialCsv, Does.Contain("evidence_collected"));
            Assert.That(spatialCsv, Does.Contain("zoneName"));
            Assert.That(spatialCsv, Does.Contain("metricKind"));
            Assert.That(spatialCsv, Does.Contain("Left Evidence Run"));
            Assert.That(spatialCsv, Does.Contain("duration_seconds"));
            var inquiryCsv = File.ReadAllText(result.InquiryPath);
            Assert.That(inquiryCsv, Does.Contain("fall_edge_photo"));
            Assert.That(inquiryCsv,
                Does.Contain("evidenceCount,collectedItemCount,distractorCount,isDistractor"));
            Assert.That(File.ReadAllText(result.DwellPath), Does.Contain("construction_left_evidence"));
            Assert.That(File.ReadAllText(result.DwellPath), Does.Contain("Left Evidence Run"));
            Assert.That(File.ReadAllText(result.DwellPath), Does.Contain(",12"));
            var summaryCsv = File.ReadAllText(result.RouteSummaryPath);
            Assert.That(summaryCsv, Does.Contain("learner_route_summary").Or.Contain("pathDistanceMeters"));
            Assert.That(summaryCsv,
                Does.Contain("distractorSelections,hypothesesSelected,reportBlocks,finalReports"));
            Assert.That(summaryCsv, Does.Contain("learner001,Construction"));
            Assert.That(summaryCsv, Does.Contain(",14,12,0.5,1,1,0,1,0,0,0,0,1,1,1"));
        }

        [Test]
        public void ExportDirectory_EscapesInquiryDetailsForCsv()
        {
            var logs = Path.Combine(tempRoot, "logs");
            var output = Path.Combine(tempRoot, "output");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "inquiry_learner002.jsonl"),
                "{\"timestampUtc\":\"2026-07-18T16:01:00.0000000Z\",\"sessionId\":\"learner002\",\"eventType\":\"hypothesis_submitted\",\"phase\":\"hypothesis\",\"site\":\"Construction\",\"objectId\":\"note_board\",\"hazardType\":\"struck_by\",\"detail\":\"Crane swing path, missing spotter\",\"zoneId\":\"construction_crane\",\"zoneName\":\"Crane Bay\",\"siteX\":3,\"siteY\":1,\"siteZ\":4}\n");

            var result = AnalyticsLogExporter.ExportDirectory(logs, output);

            Assert.That(result.InquiryRowCount, Is.EqualTo(1));
            Assert.That(File.ReadAllText(result.InquiryPath), Does.Contain("\"Crane swing path, missing spotter\""));
        }

        [Test]
        public void ExportDirectory_PreservesObjectiveCriterionAndEarnedAssessmentEvidence()
        {
            var logs = Path.Combine(tempRoot, "logs");
            var output = Path.Combine(tempRoot, "output");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "inquiry_learner003.jsonl"),
                "{\"timestampUtc\":\"2026-07-18T16:02:00.0000000Z\",\"sessionId\":\"learner003\",\"eventType\":\"assessment_evidence\",\"phase\":\"learning_outcome\",\"site\":\"Construction\",\"objectId\":\"decision:crane-radius\",\"objectiveId\":\"CON-02\",\"criterionId\":\"decision:crane-radius\",\"outcome\":\"met\",\"earnedPoints\":50,\"possiblePoints\":50,\"detail\":\"Verified 50 ft radius\",\"siteX\":11.35,\"siteY\":1.2,\"siteZ\":0.2}\n");

            var result = AnalyticsLogExporter.ExportDirectory(logs, output);

            var inquiryCsv = File.ReadAllText(result.InquiryPath);
            Assert.That(inquiryCsv, Does.Contain("objectiveId,criterionId,earnedPoints,possiblePoints"));
            Assert.That(inquiryCsv, Does.Contain("CON-02,decision:crane-radius,50,50"));
            var summaryCsv = File.ReadAllText(result.RouteSummaryPath);
            Assert.That(summaryCsv, Does.Contain("assessmentAttempts,assessmentEvidenceEarned,objectivesTouched"));
            Assert.That(summaryCsv, Does.Contain("learner003,Construction"));
            Assert.That(summaryCsv, Does.EndWith(",1,50,1\r\n").Or.EndWith(",1,50,1\n"));
        }
    }
}
