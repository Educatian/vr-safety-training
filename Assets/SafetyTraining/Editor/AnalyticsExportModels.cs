using System;

namespace SafetyTraining.Editor
{
    internal sealed class AnalyticsEntry
    {
        public string Source = string.Empty;
        public DateTime TimestampUtc;
        public string SessionId = string.Empty;
        public string EventType = string.Empty;
        public string Site = string.Empty;
        public string ZoneId = string.Empty;
        public string ZoneName = string.Empty;
        public string SubjectId = string.Empty;
        public string Outcome = string.Empty;
        public string Phase = string.Empty;
        public string HazardType = string.Empty;
        public string Detail = string.Empty;
        public int EvidenceCount;
        public int CollectedItemCount;
        public int DistractorCount;
        public bool IsDistractor;
        public float WorldX;
        public float WorldY;
        public float WorldZ;
        public float SiteX;
        public float SiteY;
        public float SiteZ;
        public float DurationOrDistance;
        public string MetricKind = string.Empty;

        public bool HasCoordinates =>
            Math.Abs(WorldX) > float.Epsilon ||
            Math.Abs(WorldY) > float.Epsilon ||
            Math.Abs(WorldZ) > float.Epsilon ||
            Math.Abs(SiteX) > float.Epsilon ||
            Math.Abs(SiteY) > float.Epsilon ||
            Math.Abs(SiteZ) > float.Epsilon ||
            EventType == "site_enter" ||
            EventType == "site_exit" ||
            EventType == "zone_enter" ||
            EventType == "zone_exit" ||
            EventType == "spatial_sample";
    }

    internal sealed class DwellEntry
    {
        public string SessionId = string.Empty;
        public string Site = string.Empty;
        public string ZoneId = string.Empty;
        public string ZoneName = string.Empty;
        public DateTime FirstSeenUtc;
        public DateTime LastSeenUtc;
        public int SampleCount;
        public double DwellSeconds;
    }

    internal sealed class RouteSummaryEntry
    {
        public string SessionId = string.Empty;
        public string Site = string.Empty;
        public DateTime FirstSeenUtc;
        public DateTime LastSeenUtc;
        public double VisitSeconds;
        public double ZoneDwellSeconds;
        public double PathDistanceMeters;
        public int ZonesVisited;
        public int SpatialSamples;
        public int Inspections;
        public int EvidenceCollected;
        public int DistractorSelections;
        public int HypothesesSelected;
        public int ReportBlocks;
        public int FinalReports;
        public int PlacementAttempts;
        public int PlacementSuccesses;
        public int CoachTurns;
    }

    public readonly struct AnalyticsExportResult
    {
        public AnalyticsExportResult(string spatialPath, string inquiryPath, string dwellPath,
            string routeSummaryPath, int spatialRowCount, int inquiryRowCount, int dwellRowCount,
            int routeSummaryRowCount)
        {
            SpatialPath = spatialPath;
            InquiryPath = inquiryPath;
            DwellPath = dwellPath;
            RouteSummaryPath = routeSummaryPath;
            SpatialRowCount = spatialRowCount;
            InquiryRowCount = inquiryRowCount;
            DwellRowCount = dwellRowCount;
            RouteSummaryRowCount = routeSummaryRowCount;
        }

        public string SpatialPath { get; }
        public string InquiryPath { get; }
        public string DwellPath { get; }
        public string RouteSummaryPath { get; }
        public int SpatialRowCount { get; }
        public int InquiryRowCount { get; }
        public int DwellRowCount { get; }
        public int RouteSummaryRowCount { get; }
    }
}
