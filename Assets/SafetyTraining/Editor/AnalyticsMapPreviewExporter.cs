using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace SafetyTraining.Editor
{
    public static class AnalyticsMapPreviewExporter
    {
        const int DefaultImageSize = 1024;

        public static AnalyticsMapPreviewResult ExportPreview(string csvDirectory, string outputPath)
        {
            if (string.IsNullOrWhiteSpace(csvDirectory))
                throw new ArgumentException("CSV directory is required.", nameof(csvDirectory));
            if (!Directory.Exists(csvDirectory))
                throw new DirectoryNotFoundException(csvDirectory);
            if (string.IsNullOrWhiteSpace(outputPath))
                throw new ArgumentException("Output path is required.", nameof(outputPath));

            var spatialPath = Path.Combine(csvDirectory, "spatial_samples.csv");
            var dwellPath = Path.Combine(csvDirectory, "zone_dwell.csv");
            if (!File.Exists(spatialPath))
                throw new FileNotFoundException("spatial_samples.csv is required.", spatialPath);

            var points = ReadSpatialPoints(spatialPath).ToList();
            var dwellByZone = File.Exists(dwellPath)
                ? ReadDwellSeconds(dwellPath)
                : new Dictionary<string, float>();
            Directory.CreateDirectory(Path.GetDirectoryName(outputPath) ?? csvDirectory);
            Render(points, dwellByZone, outputPath);
            return new AnalyticsMapPreviewResult(outputPath, points.Count,
                points.Count(point => point.Source == "inquiry"), dwellByZone.Count);
        }

        static void Render(IReadOnlyList<SpatialPoint> points, IReadOnlyDictionary<string, float> dwellByZone,
            string outputPath)
        {
            var texture = new Texture2D(DefaultImageSize, DefaultImageSize, TextureFormat.RGBA32, false);
            Fill(texture, new Color32(11, 18, 26, 255));
            DrawGrid(texture);

            if (points.Count > 0)
            {
                var bounds = CoordinateBounds.From(points);
                var zones = points
                    .Where(point => !string.IsNullOrEmpty(point.ZoneId))
                    .GroupBy(point => point.ZoneId)
                    .Select(group => new ZoneAnchor(group.Key, group.Average(point => point.SiteX),
                        group.Average(point => point.SiteZ)))
                    .ToList();
                var maxDwell = dwellByZone.Count == 0 ? 0f : dwellByZone.Values.Max();
                foreach (var zone in zones)
                {
                    var dwellSeconds = dwellByZone.TryGetValue(zone.ZoneId, out var value) ? value : 0f;
                    if (dwellSeconds <= 0f)
                        continue;
                    var pixel = bounds.ToPixel(zone.SiteX, zone.SiteZ);
                    var radius = Mathf.RoundToInt(Mathf.Lerp(18f, 70f, maxDwell <= 0f ? 0f : dwellSeconds / maxDwell));
                    DrawCircle(texture, pixel.x, pixel.y, radius, new Color32(255, 184, 64, 74));
                    DrawCircleOutline(texture, pixel.x, pixel.y, radius, new Color32(255, 203, 91, 190));
                }

                var routes = points
                    .Where(point => point.Source != "inquiry")
                    .GroupBy(point => point.SessionId);
                foreach (var route in routes)
                {
                    var ordered = route.OrderBy(point => point.TimestampUtc).ToList();
                    for (var index = 1; index < ordered.Count; index++)
                    {
                        var start = bounds.ToPixel(ordered[index - 1].SiteX, ordered[index - 1].SiteZ);
                        var end = bounds.ToPixel(ordered[index].SiteX, ordered[index].SiteZ);
                        DrawLine(texture, start.x, start.y, end.x, end.y, new Color32(63, 206, 255, 255), 4);
                    }
                }

                foreach (var point in points)
                {
                    var pixel = bounds.ToPixel(point.SiteX, point.SiteZ);
                    if (point.Source == "inquiry")
                        DrawSquare(texture, pixel.x, pixel.y, 16, new Color32(255, 86, 86, 255));
                    else
                        DrawCircle(texture, pixel.x, pixel.y, 7, new Color32(150, 232, 255, 255));
                }
            }

            DrawLegend(texture);
            texture.Apply(false, false);
            File.WriteAllBytes(outputPath, texture.EncodeToPNG());
            UnityEngine.Object.DestroyImmediate(texture);
        }

        static IEnumerable<SpatialPoint> ReadSpatialPoints(string path)
        {
            using var reader = new StreamReader(path);
            var header = ReadCsvLine(reader.ReadLine()).ToArray();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var values = ReadCsvLine(line).ToArray();
                yield return new SpatialPoint
                {
                    TimestampUtc = ReadDate(header, values, "timestampUtc"),
                    SessionId = ReadString(header, values, "sessionId"),
                    Source = ReadString(header, values, "source"),
                    Site = ReadString(header, values, "site"),
                    ZoneId = ReadString(header, values, "zoneId"),
                    SiteX = ReadFloat(header, values, "siteX"),
                    SiteZ = ReadFloat(header, values, "siteZ")
                };
            }
        }

        static Dictionary<string, float> ReadDwellSeconds(string path)
        {
            var result = new Dictionary<string, float>();
            using var reader = new StreamReader(path);
            var header = ReadCsvLine(reader.ReadLine()).ToArray();
            string line;
            while ((line = reader.ReadLine()) != null)
            {
                if (string.IsNullOrWhiteSpace(line))
                    continue;
                var values = ReadCsvLine(line).ToArray();
                var zoneId = ReadString(header, values, "zoneId");
                if (string.IsNullOrEmpty(zoneId))
                    continue;
                result[zoneId] = ReadFloat(header, values, "dwellSeconds");
            }
            return result;
        }

        static IEnumerable<string> ReadCsvLine(string line)
        {
            if (line == null)
                yield break;
            var value = string.Empty;
            var inQuotes = false;
            for (var index = 0; index < line.Length; index++)
            {
                var current = line[index];
                if (current == '"')
                {
                    if (inQuotes && index + 1 < line.Length && line[index + 1] == '"')
                    {
                        value += '"';
                        index++;
                    }
                    else
                    {
                        inQuotes = !inQuotes;
                    }
                }
                else if (current == ',' && !inQuotes)
                {
                    yield return value;
                    value = string.Empty;
                }
                else
                {
                    value += current;
                }
            }
            yield return value;
        }

        static string ReadString(IReadOnlyList<string> header, IReadOnlyList<string> values, string key)
        {
            var index = IndexOf(header, key);
            return index < 0 || index >= values.Count ? string.Empty : values[index];
        }

        static float ReadFloat(IReadOnlyList<string> header, IReadOnlyList<string> values, string key)
        {
            var raw = ReadString(header, values, key);
            return float.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0f;
        }

        static DateTime ReadDate(IReadOnlyList<string> header, IReadOnlyList<string> values, string key)
        {
            var raw = ReadString(header, values, key);
            return DateTime.TryParse(raw, null, DateTimeStyles.RoundtripKind, out var value)
                ? value : DateTime.MinValue;
        }

        static int IndexOf(IReadOnlyList<string> header, string key)
        {
            for (var index = 0; index < header.Count; index++)
            {
                if (string.Equals(header[index], key, StringComparison.Ordinal))
                    return index;
            }
            return -1;
        }

        static void Fill(Texture2D texture, Color32 color)
        {
            var pixels = Enumerable.Repeat(color, texture.width * texture.height).ToArray();
            texture.SetPixels32(pixels);
        }

        static void DrawGrid(Texture2D texture)
        {
            var color = new Color32(31, 47, 62, 255);
            for (var value = 64; value < texture.width; value += 64)
            {
                DrawLine(texture, value, 0, value, texture.height - 1, color, 1);
                DrawLine(texture, 0, value, texture.width - 1, value, color, 1);
            }
        }

        static void DrawLegend(Texture2D texture)
        {
            DrawSquare(texture, 50, 54, 18, new Color32(255, 86, 86, 255));
            DrawCircle(texture, 50, 92, 9, new Color32(150, 232, 255, 255));
            DrawCircle(texture, 50, 132, 24, new Color32(255, 184, 64, 74));
        }

        static void DrawLine(Texture2D texture, int x0, int y0, int x1, int y1, Color32 color, int thickness)
        {
            var dx = Math.Abs(x1 - x0);
            var sx = x0 < x1 ? 1 : -1;
            var dy = -Math.Abs(y1 - y0);
            var sy = y0 < y1 ? 1 : -1;
            var error = dx + dy;
            while (true)
            {
                DrawSquare(texture, x0, y0, thickness, color);
                if (x0 == x1 && y0 == y1)
                    break;
                var doubledError = 2 * error;
                if (doubledError >= dy)
                {
                    error += dy;
                    x0 += sx;
                }
                if (doubledError <= dx)
                {
                    error += dx;
                    y0 += sy;
                }
            }
        }

        static void DrawSquare(Texture2D texture, int centerX, int centerY, int size, Color32 color)
        {
            var radius = Math.Max(1, size / 2);
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                for (var x = centerX - radius; x <= centerX + radius; x++)
                    SetPixel(texture, x, y, color);
            }
        }

        static void DrawCircle(Texture2D texture, int centerX, int centerY, int radius, Color32 color)
        {
            var squared = radius * radius;
            for (var y = centerY - radius; y <= centerY + radius; y++)
            {
                for (var x = centerX - radius; x <= centerX + radius; x++)
                {
                    var dx = x - centerX;
                    var dy = y - centerY;
                    if (dx * dx + dy * dy <= squared)
                        BlendPixel(texture, x, y, color);
                }
            }
        }

        static void DrawCircleOutline(Texture2D texture, int centerX, int centerY, int radius, Color32 color)
        {
            for (var angle = 0; angle < 360; angle++)
            {
                var radians = angle * Mathf.Deg2Rad;
                var x = centerX + Mathf.RoundToInt(Mathf.Cos(radians) * radius);
                var y = centerY + Mathf.RoundToInt(Mathf.Sin(radians) * radius);
                DrawSquare(texture, x, y, 3, color);
            }
        }

        static void SetPixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                return;
            texture.SetPixel(x, y, color);
        }

        static void BlendPixel(Texture2D texture, int x, int y, Color32 color)
        {
            if (x < 0 || y < 0 || x >= texture.width || y >= texture.height)
                return;
            var existing = texture.GetPixel(x, y);
            var alpha = color.a / 255f;
            var blended = Color.Lerp(existing, color, alpha);
            blended.a = 1f;
            texture.SetPixel(x, y, blended);
        }

        struct SpatialPoint
        {
            public DateTime TimestampUtc { get; set; }
            public string SessionId { get; set; }
            public string Source { get; set; }
            public string Site { get; set; }
            public string ZoneId { get; set; }
            public float SiteX { get; set; }
            public float SiteZ { get; set; }
        }

        readonly struct ZoneAnchor
        {
            public ZoneAnchor(string zoneId, double siteX, double siteZ)
            {
                ZoneId = zoneId;
                SiteX = (float)siteX;
                SiteZ = (float)siteZ;
            }

            public string ZoneId { get; }
            public float SiteX { get; }
            public float SiteZ { get; }
        }

        readonly struct CoordinateBounds
        {
            readonly float minX;
            readonly float maxX;
            readonly float minZ;
            readonly float maxZ;

            CoordinateBounds(float minX, float maxX, float minZ, float maxZ)
            {
                this.minX = minX;
                this.maxX = maxX;
                this.minZ = minZ;
                this.maxZ = maxZ;
            }

            public static CoordinateBounds From(IReadOnlyList<SpatialPoint> points)
            {
                var minX = points.Min(point => point.SiteX);
                var maxX = points.Max(point => point.SiteX);
                var minZ = points.Min(point => point.SiteZ);
                var maxZ = points.Max(point => point.SiteZ);
                if (Math.Abs(maxX - minX) < 0.01f)
                {
                    minX -= 1f;
                    maxX += 1f;
                }
                if (Math.Abs(maxZ - minZ) < 0.01f)
                {
                    minZ -= 1f;
                    maxZ += 1f;
                }
                return new CoordinateBounds(minX, maxX, minZ, maxZ);
            }

            public Vector2Int ToPixel(float siteX, float siteZ)
            {
                const float padding = 90f;
                var x = Mathf.InverseLerp(minX, maxX, siteX);
                var y = Mathf.InverseLerp(minZ, maxZ, siteZ);
                return new Vector2Int(
                    Mathf.RoundToInt(Mathf.Lerp(padding, DefaultImageSize - padding, x)),
                    Mathf.RoundToInt(Mathf.Lerp(padding, DefaultImageSize - padding, y)));
            }
        }
    }

    public readonly struct AnalyticsMapPreviewResult
    {
        public AnalyticsMapPreviewResult(string previewPath, int pointCount, int inquiryPointCount, int dwellZoneCount)
        {
            PreviewPath = previewPath;
            PointCount = pointCount;
            InquiryPointCount = inquiryPointCount;
            DwellZoneCount = dwellZoneCount;
        }

        public string PreviewPath { get; }
        public int PointCount { get; }
        public int InquiryPointCount { get; }
        public int DwellZoneCount { get; }
    }

    public static class AnalyticsMapPreviewExporterMenu
    {
        [MenuItem("Safety Training/Analytics/Render Analytics Map Preview")]
        public static void RenderPersistentAnalyticsPreview()
        {
            var csvDirectory = Path.Combine(Application.persistentDataPath, "SafetyTrainingAnalytics");
            var outputPath = Path.Combine(csvDirectory, "analytics_map_preview.png");
            var result = AnalyticsMapPreviewExporter.ExportPreview(csvDirectory, outputPath);
            Debug.Log($"Safety analytics map preview complete: points={result.PointCount}, " +
                      $"inquiry={result.InquiryPointCount}, dwellZones={result.DwellZoneCount}, output={outputPath}");
            EditorUtility.RevealInFinder(outputPath);
        }

        public static void RenderFromEnvironment()
        {
            var csvDirectory = ReadCommandLineValue("-safetyAnalyticsCsvDir") ??
                               Environment.GetEnvironmentVariable("SAFETY_ANALYTICS_CSV_DIR");
            var outputPath = ReadCommandLineValue("-safetyAnalyticsPreviewPath") ??
                             Environment.GetEnvironmentVariable("SAFETY_ANALYTICS_PREVIEW_PATH");
            if (string.IsNullOrWhiteSpace(csvDirectory) || string.IsNullOrWhiteSpace(outputPath))
                throw new InvalidOperationException("Set SAFETY_ANALYTICS_CSV_DIR and SAFETY_ANALYTICS_PREVIEW_PATH, " +
                                                    "or pass -safetyAnalyticsCsvDir and -safetyAnalyticsPreviewPath.");

            var result = AnalyticsMapPreviewExporter.ExportPreview(csvDirectory, outputPath);
            Debug.Log($"Safety analytics map preview complete: points={result.PointCount}, " +
                      $"inquiry={result.InquiryPointCount}, dwellZones={result.DwellZoneCount}, output={outputPath}");
        }

        static string ReadCommandLineValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (var index = 0; index < args.Length - 1; index++)
            {
                if (string.Equals(args[index], key, StringComparison.Ordinal))
                    return args[index + 1];
            }
            return null;
        }
    }
}
