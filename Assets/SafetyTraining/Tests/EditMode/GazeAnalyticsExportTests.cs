using System.IO;
using NUnit.Framework;
using SafetyTraining.Editor;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class GazeAnalyticsExportTests
    {
        [Test]
        public void ExportDirectory_PreservesGazeModeTargetAndHitCoordinates()
        {
            var root = Path.Combine(Path.GetTempPath(), "vr-safety-gaze-export",
                TestContext.CurrentContext.Test.ID);
            var logs = Path.Combine(root, "logs");
            var output = Path.Combine(root, "output");
            Directory.CreateDirectory(logs);
            File.WriteAllText(Path.Combine(logs, "session_gaze001.jsonl"),
                "{\"timestampUtc\":\"2026-07-20T20:00:00Z\",\"sessionId\":\"gaze001\"," +
                "\"eventType\":\"gaze_target_dwell\",\"site\":\"Construction\"," +
                "\"subjectId\":\"evidence:guardrail-gap\",\"outcome\":\"eye_gaze\"," +
                "\"worldX\":1,\"worldY\":1.7,\"worldZ\":2,\"durationOrDistance\":1.25," +
                "\"metricKind\":\"duration_seconds\",\"gazeMode\":\"eye_gaze\"," +
                "\"targetKind\":\"evidence\",\"hitWorldX\":4.5,\"hitWorldY\":2.2," +
                "\"hitWorldZ\":8.1,\"hitSiteX\":4.5,\"hitSiteY\":2.2,\"hitSiteZ\":-111.9}\n");
            try
            {
                var result = AnalyticsLogExporter.ExportDirectory(logs, output);
                var csv = File.ReadAllText(result.SpatialPath);

                Assert.That(result.SpatialRowCount, Is.EqualTo(1));
                Assert.That(csv, Does.Contain("gazeMode,targetKind,hitWorldX,hitWorldY,hitWorldZ"));
                Assert.That(csv, Does.Contain("eye_gaze,evidence,4.5,2.2,8.1"));
            }
            finally
            {
                if (Directory.Exists(root))
                    Directory.Delete(root, true);
            }
        }
    }
}
