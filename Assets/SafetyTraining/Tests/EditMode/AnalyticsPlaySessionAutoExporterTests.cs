using System.IO;
using NUnit.Framework;
using SafetyTraining.Editor;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class AnalyticsPlaySessionAutoExporterTests
    {
        string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "vr-safety-analytics-auto-export-tests",
                TestContext.CurrentContext.Test.ID);
            Directory.CreateDirectory(tempRoot);
        }

        [TearDown]
        public void TearDown()
        {
            if (Directory.Exists(tempRoot))
                Directory.Delete(tempRoot, true);
        }

        [Test]
        public void ExportLogsAndPreview_CreatesCsvAndPreviewArtifacts()
        {
            var logs = Path.Combine(tempRoot, "logs");
            var output = Path.Combine(tempRoot, "session_output");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "session_demo001.jsonl"),
                "{\"timestampUtc\":\"2026-07-18T19:00:00.0000000Z\",\"sessionId\":\"demo001\",\"eventType\":\"zone_enter\",\"site\":\"Construction\",\"zoneId\":\"construction_crane_bay\",\"worldX\":3,\"worldY\":1.7,\"worldZ\":8,\"siteX\":3,\"siteY\":1.7,\"siteZ\":8,\"durationOrDistance\":0}\n" +
                "{\"timestampUtc\":\"2026-07-18T19:00:18.0000000Z\",\"sessionId\":\"demo001\",\"eventType\":\"zone_exit\",\"site\":\"Construction\",\"zoneId\":\"construction_crane_bay\",\"worldX\":6,\"worldY\":1.7,\"worldZ\":9,\"siteX\":6,\"siteY\":1.7,\"siteZ\":9,\"durationOrDistance\":18}\n");
            File.WriteAllText(Path.Combine(logs, "inquiry_demo001.jsonl"),
                "{\"timestampUtc\":\"2026-07-18T19:00:07.0000000Z\",\"sessionId\":\"demo001\",\"eventType\":\"evidence_collected\",\"phase\":\"evidence\",\"site\":\"Construction\",\"objectId\":\"crane_swing_path_photo\",\"hazardType\":\"struck_by\",\"detail\":\"Missing barricade under swing radius\",\"zoneId\":\"construction_crane_bay\",\"zoneName\":\"Crane Bay\",\"siteX\":4,\"siteY\":1.7,\"siteZ\":8.5}\n");

            var result = AnalyticsPlaySessionAutoExporter.ExportLogsAndPreview(logs, output);

            Assert.That(result.DidExport, Is.True);
            Assert.That(result.SpatialRowCount, Is.EqualTo(3));
            Assert.That(result.InquiryRowCount, Is.EqualTo(1));
            Assert.That(result.DwellRowCount, Is.EqualTo(1));
            Assert.That(result.PreviewPointCount, Is.EqualTo(3));
            Assert.That(result.PreviewInquiryPointCount, Is.EqualTo(1));
            Assert.That(result.PreviewDwellZoneCount, Is.EqualTo(1));
            Assert.That(File.Exists(Path.Combine(output, "spatial_samples.csv")), Is.True);
            Assert.That(File.Exists(Path.Combine(output, "inquiry_events.csv")), Is.True);
            Assert.That(File.Exists(Path.Combine(output, "zone_dwell.csv")), Is.True);
            Assert.That(File.Exists(result.PreviewPath), Is.True);
            Assert.That(new FileInfo(result.PreviewPath).Length, Is.GreaterThan(1024));
        }

        [Test]
        public void ExportLogsAndPreview_SkipsWhenNoPlayLogsExist()
        {
            var output = Path.Combine(tempRoot, "session_output");

            var result = AnalyticsPlaySessionAutoExporter.ExportLogsAndPreview(Path.Combine(tempRoot, "missing_logs"), output);

            Assert.That(result.DidExport, Is.False);
            Assert.That(result.OutputDirectory, Is.EqualTo(output));
            Assert.That(result.Message, Does.Contain("No SafetyTrainingLogs"));
        }
    }
}
