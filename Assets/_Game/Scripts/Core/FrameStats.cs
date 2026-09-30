using System;
using System.Globalization;
using System.Linq;

namespace Jobsite.Core
{
    // Frame-time telemetry (quality review area 11: "no frame measurement"). Keeps the last N frame times of the
    // shift in a ring buffer and summarises them for the shift-end "perf" event: median fps, 95th-percentile frame
    // time, and the share of frames slower than 30 fps. Pure data so it is unit-tested without the engine.
    public sealed class FrameStats
    {
        public const float SlowMs = 1000f / 30f;
        private readonly float[] buffer;
        private int next, count;
        public int Total { get; private set; }
        public int Slow { get; private set; }

        public FrameStats(int capacity = 4096) { buffer = new float[Math.Max(16, capacity)]; }

        public int Samples => count;

        // Frame time in milliseconds. Pauses/alt-tabs (>= 1 s) and invalid values are not frames of play.
        public void Add(float ms)
        {
            if (!(ms > 0f) || ms >= 1000f) return;
            buffer[next] = ms; next = (next + 1) % buffer.Length; if (count < buffer.Length) count++;
            Total++; if (ms > SlowMs) Slow++;
        }

        public float Percentile(float p)
        {
            if (count == 0) return 0f;
            var sorted = buffer.Take(count).OrderBy(v => v).ToArray();
            var i = (int)Math.Round(Math.Clamp(p, 0f, 1f) * (count - 1));
            return sorted[i];
        }

        public float MedianFps => count == 0 ? 0f : 1000f / Percentile(0.5f);
        public float SlowShare => Total == 0 ? 0f : (float)Slow / Total;

        public string Summary() => string.Format(CultureInfo.InvariantCulture, "fps50={0:0} p95ms={1:0.0} slow={2:0.000} n={3}",
            MedianFps, Percentile(0.95f), SlowShare, Total);
    }
}
