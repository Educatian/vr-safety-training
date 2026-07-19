using System.IO;
using NUnit.Framework;
using SafetyTraining.Editor;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class AnalyticsMapPreviewExporterTests
    {
        string tempRoot;

        [SetUp]
        public void SetUp()
        {
            tempRoot = Path.Combine(Path.GetTempPath(), "vr-safety-analytics-preview-tests",
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
        public void ExportPreview_RendersRouteInquiryPointAndDwellOverlay()
        {
            File.WriteAllText(Path.Combine(tempRoot, "spatial_samples.csv"),
                "timestampUtc,sessionId,source,eventType,site,zoneId,subjectId,outcome,worldX,worldY,worldZ,siteX,siteY,siteZ,durationOrDistance\n" +
                "2026-07-18T18:00:00.0000000Z,demo001,spatial,zone_enter,Construction,construction_crane_bay,,,4,1.7,8,4,1.7,8,0\n" +
                "2026-07-18T18:00:08.0000000Z,demo001,inquiry,evidence_collected,Construction,construction_crane_bay,crane_swing_path_photo,,5,1.7,8.5,5,1.7,8.5,0\n" +
                "2026-07-18T18:00:20.0000000Z,demo001,spatial,zone_exit,Construction,construction_crane_bay,,,6,1.7,9,6,1.7,9,20\n");
            File.WriteAllText(Path.Combine(tempRoot, "zone_dwell.csv"),
                "sessionId,site,zoneId,firstSeenUtc,lastSeenUtc,sampleCount,dwellSeconds\n" +
                "demo001,Construction,construction_crane_bay,2026-07-18T18:00:00.0000000Z,2026-07-18T18:00:20.0000000Z,2,20\n");
            var outputPath = Path.Combine(tempRoot, "preview.png");

            var result = AnalyticsMapPreviewExporter.ExportPreview(tempRoot, outputPath);

            Assert.That(result.PointCount, Is.EqualTo(3));
            Assert.That(result.InquiryPointCount, Is.EqualTo(1));
            Assert.That(result.DwellZoneCount, Is.EqualTo(1));
            Assert.That(File.Exists(outputPath), Is.True);
            Assert.That(new FileInfo(outputPath).Length, Is.GreaterThan(1024));
            var bytes = File.ReadAllBytes(outputPath);
            var texture = new Texture2D(2, 2);
            try
            {
                Assert.That(ImageConversion.LoadImage(texture, bytes), Is.True);
                Assert.That(texture.width, Is.EqualTo(1024));
                Assert.That(texture.height, Is.EqualTo(1024));
            }
            finally
            {
                Object.DestroyImmediate(texture);
            }
        }
    }
}
