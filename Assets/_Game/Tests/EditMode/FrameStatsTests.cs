using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Frame-time telemetry summary (area 11).
    public sealed class FrameStatsTests
    {
        [Test]
        public void Summary_ReportsMedianFps_P95_AndSlowShare()
        {
            var f = new FrameStats(64);
            for (var i = 0; i < 90; i++) f.Add(16.7f);
            for (var i = 0; i < 10; i++) f.Add(50f);
            Assert.That(f.Total, Is.EqualTo(100));
            Assert.That(f.Samples, Is.EqualTo(64), "ring buffer keeps the most recent frames");
            Assert.That(f.SlowShare, Is.EqualTo(0.1f).Within(1e-4));
            Assert.That(f.Percentile(0.95f), Is.EqualTo(50f));
            var g = new FrameStats();
            for (var i = 0; i < 100; i++) g.Add(i < 95 ? 16.7f : 40f);
            Assert.That(g.MedianFps, Is.EqualTo(60f).Within(0.5));
            StringAssert.StartsWith("fps50=60 p95ms=", g.Summary());
        }

        [Test]
        public void PausesAndInvalidTimes_AreNotFrames()
        {
            var f = new FrameStats();
            f.Add(0f); f.Add(-3f); f.Add(float.NaN); f.Add(2500f);
            Assert.That(f.Total, Is.EqualTo(0));
            Assert.That(f.MedianFps, Is.EqualTo(0f));
            Assert.That(f.Summary(), Does.Contain("n=0"));
        }
    }
}
