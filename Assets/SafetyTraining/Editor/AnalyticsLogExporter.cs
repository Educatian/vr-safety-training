using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;

namespace SafetyTraining.Editor
{
    public static class AnalyticsLogExporter
    {
        public static AnalyticsExportResult ExportDirectory(string logDirectory, string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                throw new ArgumentException("Log directory is required.", nameof(logDirectory));
            if (!Directory.Exists(logDirectory))
                throw new DirectoryNotFoundException(logDirectory);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Output directory is required.", nameof(outputDirectory));

            Directory.CreateDirectory(outputDirectory);
            var entries = ReadEntries(logDirectory).ToList();
            var spatialRows = entries
                .Where(entry => entry.HasCoordinates)
                .OrderBy(entry => entry.TimestampUtc)
                .ToList();
            var inquiryRows = entries
                .Where(entry => entry.Source == "inquiry")
                .OrderBy(entry => entry.TimestampUtc)
                .ToList();
            var dwellRows = BuildDwellRows(spatialRows.Where(entry => entry.Source == "spatial").ToList());
            var summaryRows = BuildRouteSummaryRows(spatialRows);

            var spatialPath = Path.Combine(outputDirectory, "spatial_samples.csv");
            var inquiryPath = Path.Combine(outputDirectory, "inquiry_events.csv");
            var dwellPath = Path.Combine(outputDirectory, "zone_dwell.csv");
            var summaryPath = Path.Combine(outputDirectory, "learner_route_summary.csv");
            AnalyticsCsv.Write(spatialPath, SpatialHeader, spatialRows.Select(ToSpatialRow));
            AnalyticsCsv.Write(inquiryPath, InquiryHeader, inquiryRows.Select(ToInquiryRow));
            AnalyticsCsv.Write(dwellPath, DwellHeader, dwellRows.Select(ToDwellRow));
            AnalyticsCsv.Write(summaryPath, RouteSummaryHeader, summaryRows.Select(ToRouteSummaryRow));
            return new AnalyticsExportResult(spatialPath, inquiryPath, dwellPath, summaryPath,
                spatialRows.Count, inquiryRows.Count, dwellRows.Count, summaryRows.Count);
        }

        static IEnumerable<AnalyticsEntry> ReadEntries(string logDirectory)
        {
            foreach (var path in Directory.GetFiles(logDirectory, "*.jsonl"))
            {
                var source = Path.GetFileName(path).StartsWith("inquiry_", StringComparison.OrdinalIgnoreCase)
                    ? "inquiry" : "spatial";
                foreach (var line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line))
                        continue;
                    var json = JObject.Parse(line);
                    yield return new AnalyticsEntry
                    {
                        Source = source,
                        TimestampUtc = AnalyticsCsv.ReadDate(json, "timestampUtc"),
                        SessionId = AnalyticsCsv.ReadString(json, "sessionId"),
                        EventType = AnalyticsCsv.ReadString(json, "eventType", AnalyticsCsv.ReadString(json, "outcome")),
                        Site = AnalyticsCsv.ReadString(json, "site"),
                        ZoneId = AnalyticsCsv.ReadString(json, "zoneId"),
                        ZoneName = AnalyticsCsv.ReadString(json, "zoneName"),
                        SubjectId = AnalyticsCsv.ReadString(json, "subjectId",
                            AnalyticsCsv.ReadString(json, "targetId", AnalyticsCsv.ReadString(json, "objectId"))),
                        Outcome = AnalyticsCsv.ReadString(json, "outcome", AnalyticsCsv.ReadString(json, "success")),
                        Phase = AnalyticsCsv.ReadString(json, "phase"),
                        HazardType = AnalyticsCsv.ReadString(json, "hazardType"),
                        Detail = AnalyticsCsv.ReadString(json, "detail", AnalyticsCsv.ReadString(json, "hypothesis")),
                        EvidenceCount = AnalyticsCsv.ReadInteger(json, "evidenceCount"),
                        CollectedItemCount = AnalyticsCsv.ReadInteger(json, "collectedItemCount"),
                        DistractorCount = AnalyticsCsv.ReadInteger(json, "distractorCount"),
                        IsDistractor = AnalyticsCsv.ReadBoolean(json, "isDistractor"),
                        WorldX = AnalyticsCsv.ReadFloat(json, "worldX"),
                        WorldY = AnalyticsCsv.ReadFloat(json, "worldY"),
                        WorldZ = AnalyticsCsv.ReadFloat(json, "worldZ"),
                        SiteX = AnalyticsCsv.ReadFloat(json, "siteX"),
                        SiteY = AnalyticsCsv.ReadFloat(json, "siteY"),
                        SiteZ = AnalyticsCsv.ReadFloat(json, "siteZ"),
                        DurationOrDistance = AnalyticsCsv.ReadFloat(json, "durationOrDistance"),
                        MetricKind = AnalyticsCsv.ReadString(json, "metricKind")
                    };
                }
            }
        }

        static List<DwellEntry> BuildDwellRows(IReadOnlyList<AnalyticsEntry> spatialRows)
        {
            var rows = new List<DwellEntry>();
            foreach (var group in spatialRows
                         .Where(entry => !string.IsNullOrEmpty(entry.SessionId) &&
                                         !string.IsNullOrEmpty(entry.Site) &&
                                         !string.IsNullOrEmpty(entry.ZoneId))
                         .GroupBy(entry => (entry.SessionId, entry.Site, entry.ZoneId)))
            {
                var ordered = group.OrderBy(entry => entry.TimestampUtc).ToList();
                var explicitDuration = ordered
                    .Where(entry => entry.EventType == "zone_exit" && entry.DurationOrDistance > 0f)
                    .Sum(entry => entry.DurationOrDistance);
                var inferredDuration = 0d;
                for (var index = 1; index < ordered.Count; index++)
                    inferredDuration += Math.Max(0d, (ordered[index].TimestampUtc - ordered[index - 1].TimestampUtc).TotalSeconds);
                rows.Add(new DwellEntry
                {
                    SessionId = group.Key.SessionId,
                    Site = group.Key.Site,
                    ZoneId = group.Key.ZoneId,
                    ZoneName = ordered.FirstOrDefault(entry => !string.IsNullOrEmpty(entry.ZoneName))?.ZoneName ??
                               string.Empty,
                    FirstSeenUtc = ordered[0].TimestampUtc,
                    LastSeenUtc = ordered[^1].TimestampUtc,
                    SampleCount = ordered.Count,
                    DwellSeconds = explicitDuration > 0f ? explicitDuration : inferredDuration
                });
            }
            return rows.OrderBy(row => row.SessionId)
                .ThenBy(row => row.Site)
                .ThenBy(row => row.ZoneId)
                .ToList();
        }

        static List<RouteSummaryEntry> BuildRouteSummaryRows(IReadOnlyList<AnalyticsEntry> spatialRows)
        {
            var rows = new List<RouteSummaryEntry>();
            foreach (var group in spatialRows
                         .Where(entry => !string.IsNullOrEmpty(entry.SessionId) &&
                                         !string.IsNullOrEmpty(entry.Site))
                         .GroupBy(entry => (entry.SessionId, entry.Site)))
            {
                var ordered = group.OrderBy(entry => entry.TimestampUtc).ToList();
                var inferredVisitSeconds = Math.Max(0d,
                    (ordered[^1].TimestampUtc - ordered[0].TimestampUtc).TotalSeconds);
                var visitSeconds = ordered
                    .Where(entry => entry.EventType == "site_exit" && entry.MetricKind == "duration_seconds")
                    .Sum(entry => entry.DurationOrDistance);
                var zoneDwellSeconds = ordered
                    .Where(entry => entry.EventType == "zone_exit" && entry.MetricKind == "duration_seconds")
                    .Sum(entry => entry.DurationOrDistance);
                rows.Add(new RouteSummaryEntry
                {
                    SessionId = group.Key.SessionId,
                    Site = group.Key.Site,
                    FirstSeenUtc = ordered[0].TimestampUtc,
                    LastSeenUtc = ordered[^1].TimestampUtc,
                    VisitSeconds = Math.Max(visitSeconds, inferredVisitSeconds),
                    ZoneDwellSeconds = zoneDwellSeconds,
                    PathDistanceMeters = ordered
                        .Where(entry => entry.EventType == "spatial_sample" &&
                                        entry.MetricKind == "distance_meters")
                        .Sum(entry => entry.DurationOrDistance),
                    ZonesVisited = ordered.Select(entry => entry.ZoneId)
                        .Where(zoneId => !string.IsNullOrEmpty(zoneId))
                        .Distinct()
                        .Count(),
                    SpatialSamples = ordered.Count(entry => entry.EventType == "spatial_sample"),
                    Inspections = ordered.Count(entry => entry.EventType == "inspection"),
                    EvidenceCollected = ordered.Count(entry => entry.EventType == "evidence_collected"),
                    DistractorSelections = ordered.Count(entry => entry.EventType == "distractor_selected"),
                    HypothesesSelected = ordered.Count(entry => entry.EventType == "hypothesis_selected"),
                    ReportBlocks = ordered.Count(entry => entry.EventType == "report_blocked_insufficient_evidence"),
                    FinalReports = ordered.Count(entry => entry.EventType == "final_explanation_submitted"),
                    PlacementAttempts = ordered.Count(entry => entry.EventType == "placement_attempt"),
                    PlacementSuccesses = ordered.Count(entry =>
                        entry.EventType == "placement_attempt" && entry.Outcome == "success"),
                    CoachTurns = ordered.Count(entry => entry.EventType == "coach_turn")
                });
            }
            return rows.OrderBy(row => row.SessionId)
                .ThenBy(row => row.Site)
                .ToList();
        }

        static readonly string[] SpatialHeader =
        {
            "timestampUtc", "sessionId", "source", "eventType", "site", "zoneId", "zoneName", "subjectId",
            "outcome", "worldX", "worldY", "worldZ", "siteX", "siteY", "siteZ", "durationOrDistance", "metricKind"
        };

        static readonly string[] InquiryHeader =
        {
            "timestampUtc", "sessionId", "eventType", "phase", "site", "zoneId", "zoneName", "subjectId",
            "hazardType", "outcome", "detail", "evidenceCount", "collectedItemCount", "distractorCount",
            "isDistractor", "siteX", "siteY", "siteZ"
        };

        static readonly string[] DwellHeader =
        {
            "sessionId", "site", "zoneId", "zoneName", "firstSeenUtc", "lastSeenUtc", "sampleCount",
            "dwellSeconds"
        };

        static readonly string[] RouteSummaryHeader =
        {
            "sessionId", "site", "firstSeenUtc", "lastSeenUtc", "visitSeconds", "zoneDwellSeconds",
            "pathDistanceMeters", "zonesVisited", "spatialSamples", "inspections", "evidenceCollected",
            "distractorSelections", "hypothesesSelected", "reportBlocks", "finalReports", "placementAttempts",
            "placementSuccesses", "coachTurns"
        };

        static string[] ToSpatialRow(AnalyticsEntry entry)
        {
            return new[]
            {
                AnalyticsCsv.FormatDate(entry.TimestampUtc),
                entry.SessionId,
                entry.Source,
                entry.EventType,
                entry.Site,
                entry.ZoneId,
                entry.ZoneName,
                entry.SubjectId,
                entry.Outcome,
                AnalyticsCsv.FormatFloat(entry.WorldX),
                AnalyticsCsv.FormatFloat(entry.WorldY),
                AnalyticsCsv.FormatFloat(entry.WorldZ),
                AnalyticsCsv.FormatFloat(entry.SiteX),
                AnalyticsCsv.FormatFloat(entry.SiteY),
                AnalyticsCsv.FormatFloat(entry.SiteZ),
                AnalyticsCsv.FormatFloat(entry.DurationOrDistance),
                entry.MetricKind
            };
        }

        static string[] ToInquiryRow(AnalyticsEntry entry)
        {
            return new[]
            {
                AnalyticsCsv.FormatDate(entry.TimestampUtc),
                entry.SessionId,
                entry.EventType,
                entry.Phase,
                entry.Site,
                entry.ZoneId,
                entry.ZoneName,
                entry.SubjectId,
                entry.HazardType,
                entry.Outcome,
                entry.Detail,
                AnalyticsCsv.FormatInteger(entry.EvidenceCount),
                AnalyticsCsv.FormatInteger(entry.CollectedItemCount),
                AnalyticsCsv.FormatInteger(entry.DistractorCount),
                entry.IsDistractor ? "true" : "false",
                AnalyticsCsv.FormatFloat(entry.SiteX),
                AnalyticsCsv.FormatFloat(entry.SiteY),
                AnalyticsCsv.FormatFloat(entry.SiteZ)
            };
        }

        static string[] ToDwellRow(DwellEntry entry)
        {
            return new[]
            {
                entry.SessionId,
                entry.Site,
                entry.ZoneId,
                entry.ZoneName,
                AnalyticsCsv.FormatDate(entry.FirstSeenUtc),
                AnalyticsCsv.FormatDate(entry.LastSeenUtc),
                AnalyticsCsv.FormatInteger(entry.SampleCount),
                AnalyticsCsv.FormatFloat((float)entry.DwellSeconds)
            };
        }

        static string[] ToRouteSummaryRow(RouteSummaryEntry entry)
        {
            return new[]
            {
                entry.SessionId,
                entry.Site,
                AnalyticsCsv.FormatDate(entry.FirstSeenUtc),
                AnalyticsCsv.FormatDate(entry.LastSeenUtc),
                AnalyticsCsv.FormatFloat((float)entry.VisitSeconds),
                AnalyticsCsv.FormatFloat((float)entry.ZoneDwellSeconds),
                AnalyticsCsv.FormatFloat((float)entry.PathDistanceMeters),
                AnalyticsCsv.FormatInteger(entry.ZonesVisited),
                AnalyticsCsv.FormatInteger(entry.SpatialSamples),
                AnalyticsCsv.FormatInteger(entry.Inspections),
                AnalyticsCsv.FormatInteger(entry.EvidenceCollected),
                AnalyticsCsv.FormatInteger(entry.DistractorSelections),
                AnalyticsCsv.FormatInteger(entry.HypothesesSelected),
                AnalyticsCsv.FormatInteger(entry.ReportBlocks),
                AnalyticsCsv.FormatInteger(entry.FinalReports),
                AnalyticsCsv.FormatInteger(entry.PlacementAttempts),
                AnalyticsCsv.FormatInteger(entry.PlacementSuccesses),
                AnalyticsCsv.FormatInteger(entry.CoachTurns)
            };
        }
    }

    public static class AnalyticsLogExporterMenu
    {
        [MenuItem("Safety Training/Analytics/Export Persistent Logs To CSV")]
        public static void ExportPersistentLogsToCsv()
        {
            var logDirectory = Path.Combine(Application.persistentDataPath, "SafetyTrainingLogs");
            var outputDirectory = Path.Combine(Application.persistentDataPath, "SafetyTrainingAnalytics");
            var result = AnalyticsLogExporter.ExportDirectory(logDirectory, outputDirectory);
            Debug.Log($"Safety analytics CSV export complete: spatial={result.SpatialRowCount}, " +
                      $"inquiry={result.InquiryRowCount}, dwell={result.DwellRowCount}, " +
                      $"routeSummary={result.RouteSummaryRowCount}, output={outputDirectory}");
            EditorUtility.RevealInFinder(outputDirectory);
        }

        public static void ExportFromEnvironment()
        {
            var logDirectory = ReadCommandLineValue("-safetyAnalyticsLogDir") ??
                               Environment.GetEnvironmentVariable("SAFETY_ANALYTICS_LOG_DIR");
            var outputDirectory = ReadCommandLineValue("-safetyAnalyticsOutputDir") ??
                                  Environment.GetEnvironmentVariable("SAFETY_ANALYTICS_OUTPUT_DIR");
            if (string.IsNullOrWhiteSpace(logDirectory) || string.IsNullOrWhiteSpace(outputDirectory))
                throw new InvalidOperationException("Set SAFETY_ANALYTICS_LOG_DIR and SAFETY_ANALYTICS_OUTPUT_DIR, " +
                                                    "or pass -safetyAnalyticsLogDir and -safetyAnalyticsOutputDir.");

            var result = AnalyticsLogExporter.ExportDirectory(logDirectory, outputDirectory);
            Debug.Log($"Safety analytics CSV export complete: spatial={result.SpatialRowCount}, " +
                      $"inquiry={result.InquiryRowCount}, dwell={result.DwellRowCount}, " +
                      $"routeSummary={result.RouteSummaryRowCount}, output={outputDirectory}");
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
