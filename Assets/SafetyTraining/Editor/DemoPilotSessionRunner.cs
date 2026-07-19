using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SafetyTraining.Editor
{
    public static class DemoPilotSessionRunner
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";
        const int SecondsPerSite = 240;
        const string SessionId = "demo-pilot-20min";

        static readonly TrainingSiteId[] SiteOrder =
        {
            TrainingSiteId.Construction,
            TrainingSiteId.Warehouse,
            TrainingSiteId.FireResponse,
            TrainingSiteId.ChemicalProcessing,
            TrainingSiteId.ElectricalMaintenance
        };

        [MenuItem("Safety Training/Analytics/Run 20 Minute Demo Pilot")]
        public static void Run()
        {
            var outputRoot = ReadCommandLineValue("-pilotOutputDir") ??
                             Environment.GetEnvironmentVariable("SAFETY_PILOT_OUTPUT_DIR") ??
                             Path.GetFullPath(Path.Combine("Captures", "pilot-session-20min"));
            Run(outputRoot);
        }

        public static DemoPilotResult Run(string outputRoot)
        {
            if (string.IsNullOrWhiteSpace(outputRoot))
                throw new ArgumentException("Pilot output directory is required.", nameof(outputRoot));

            EditorSceneManager.OpenScene(ScenePath);
            var logDirectory = Path.Combine(outputRoot, "raw-logs");
            var analyticsDirectory = Path.Combine(outputRoot, "analytics");
            Directory.CreateDirectory(logDirectory);
            Directory.CreateDirectory(analyticsDirectory);

            var spatialPath = Path.Combine(logDirectory, $"session_{SessionId}.jsonl");
            var inquiryPath = Path.Combine(logDirectory, $"inquiry_{SessionId}.jsonl");
            var startedUtc = DateTime.UtcNow;
            var spatialEventCount = 0;
            var inquiryEventCount = 0;
            using (var spatial = new StreamWriter(spatialPath, false))
            using (var inquiry = new StreamWriter(inquiryPath, false))
            {
                for (var siteIndex = 0; siteIndex < SiteOrder.Length; siteIndex++)
                {
                    SimulateSite(SiteOrder[siteIndex], siteIndex, startedUtc, spatial, inquiry,
                        ref spatialEventCount, ref inquiryEventCount);
                }
            }

            var export = AnalyticsLogExporter.ExportDirectory(logDirectory, analyticsDirectory);
            var previewPath = Path.Combine(analyticsDirectory, "analytics_map_preview.png");
            var preview = AnalyticsMapPreviewExporter.ExportPreview(analyticsDirectory, previewPath);
            var result = new DemoPilotResult
            {
                sessionId = SessionId,
                simulatedSeconds = SiteOrder.Length * SecondsPerSite,
                sitesVisited = SiteOrder.Length,
                analyticsZonesVisited = UnityEngine.Object.FindObjectsByType<SpatialAnalyticsZone>(FindObjectsSortMode.None).Length,
                inspectionTargetsVisited = UnityEngine.Object.FindObjectsByType<InspectionTarget>(FindObjectsSortMode.None).Length,
                evidenceObjectsVisited = UnityEngine.Object.FindObjectsByType<EvidenceObject>(FindObjectsSortMode.None).Length,
                practicalActionsAttempted = CountPracticalActions(),
                spatialEvents = spatialEventCount,
                inquiryEvents = inquiryEventCount,
                spatialRowsExported = export.SpatialRowCount,
                inquiryRowsExported = export.InquiryRowCount,
                dwellRowsExported = export.DwellRowCount,
                routeSummaryRowsExported = export.RouteSummaryRowCount,
                previewPoints = preview.PointCount
            };
            File.WriteAllText(Path.Combine(outputRoot, "pilot-summary.json"),
                JsonConvert.SerializeObject(result, Formatting.Indented));
            File.WriteAllText(Path.Combine(outputRoot, "pilot-complete.txt"), DateTime.UtcNow.ToString("O"));
            Debug.Log($"20 minute demo pilot complete: {JsonConvert.SerializeObject(result)} output={outputRoot}");
            return result;
        }

        static void SimulateSite(TrainingSiteId siteId, int siteIndex, DateTime startedUtc,
            TextWriter spatial, TextWriter inquiry, ref int spatialEventCount, ref int inquiryEventCount)
        {
            var site = UnityEngine.Object.FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None)
                .Single(item => item.SiteId == siteId);
            var zones = UnityEngine.Object.FindObjectsByType<SpatialAnalyticsZone>(FindObjectsSortMode.None)
                .Where(item => item.SiteId == siteId)
                .OrderBy(item => item.ZoneId)
                .ToArray();
            var siteStartedAt = siteIndex * SecondsPerSite;
            WriteSpatial(spatial, startedUtc.AddSeconds(siteStartedAt), "site_enter", siteId,
                string.Empty, string.Empty, zones[0], site, zones[0].transform.position, 0f, "instant");
            spatialEventCount++;

            var secondsPerZone = SecondsPerSite / (float)zones.Length;
            for (var zoneIndex = 0; zoneIndex < zones.Length; zoneIndex++)
            {
                var zone = zones[zoneIndex];
                var zoneStart = siteStartedAt + zoneIndex * secondsPerZone;
                WriteSpatial(spatial, startedUtc.AddSeconds(zoneStart), "zone_enter", siteId,
                    zone.ZoneId, string.Empty, zone, site, zone.transform.position, 0f, "instant");
                WriteSpatial(spatial, startedUtc.AddSeconds(zoneStart + secondsPerZone), "zone_exit", siteId,
                    zone.ZoneId, string.Empty, zone, site, zone.transform.position, secondsPerZone,
                    "duration_seconds");
                spatialEventCount += 2;
            }

            var previous = zones[0].transform.position;
            for (var second = 0; second < SecondsPerSite; second++)
            {
                var zoneIndex = Mathf.Min(zones.Length - 1,
                    Mathf.FloorToInt(second / secondsPerZone));
                var zone = zones[zoneIndex];
                var angle = second * 0.17f;
                var position = zone.transform.position +
                               new Vector3(Mathf.Sin(angle) * 1.25f, 1.7f, Mathf.Cos(angle) * 1.25f);
                var distance = Vector3.Distance(previous, position);
                WriteSpatial(spatial, startedUtc.AddSeconds(siteStartedAt + second), "spatial_sample", siteId,
                    string.Empty, string.Empty, zone, site, position, distance, "distance_meters");
                previous = position;
                spatialEventCount++;
            }

            var targets = UnityEngine.Object.FindObjectsByType<InspectionTarget>(FindObjectsSortMode.None)
                .Where(item => item.SiteId == siteId)
                .OrderBy(item => item.TargetId)
                .ToArray();
            for (var index = 0; index < targets.Length; index++)
            {
                var target = targets[index];
                var zone = NearestZone(zones, target.transform.position);
                WriteSpatial(spatial, startedUtc.AddSeconds(siteStartedAt + 24 + index * 9), "inspection", siteId,
                    target.TargetId, target.IsHazard ? "correct_hazard" : "controlled_comparison", zone, site,
                    target.transform.position, 0f, "instant");
                spatialEventCount++;
            }

            var relevantCount = 0;
            var distractorCount = 0;
            var evidence = UnityEngine.Object.FindObjectsByType<EvidenceObject>(FindObjectsSortMode.None)
                .Where(item => item.SiteId == siteId)
                .OrderBy(item => item.EvidenceId)
                .ToArray();
            for (var index = 0; index < evidence.Length; index++)
            {
                var item = evidence[index];
                if (item.IsDistractor)
                    distractorCount++;
                else
                    relevantCount++;
                var zone = NearestZone(zones, item.transform.position);
                WriteInquiry(inquiry, startedUtc.AddSeconds(siteStartedAt + 65 + index * 8), siteId,
                    item.IsDistractor ? "distractor_selected" : "evidence_collected", "evidence",
                    item.EvidenceId, item.HazardType, item.Observation, relevantCount, index + 1,
                    distractorCount, item.IsDistractor, zone, site, item.transform.position);
                inquiryEventCount++;
            }

            var actions = PracticalActions(siteId, site);
            for (var index = 0; index < actions.Count; index++)
            {
                var action = actions[index];
                var zone = NearestZone(zones, action.Position);
                if (index == 0)
                {
                    WriteSpatial(spatial, startedUtc.AddSeconds(siteStartedAt + 135), "placement_attempt", siteId,
                        action.Name, "retry", zone, site, action.Position, 1.4f,
                        "release_distance_meters");
                    spatialEventCount++;
                }
                WriteSpatial(spatial, startedUtc.AddSeconds(siteStartedAt + 145 + index * 10),
                    "placement_attempt", siteId, action.Name, "success", zone, site, action.Position,
                    0.25f, "release_distance_meters");
                spatialEventCount++;
            }

            WriteSpatial(spatial, startedUtc.AddSeconds(siteStartedAt + 205), "coach_turn", siteId,
                "safety_coach", string.Empty, zones[^1], site, zones[^1].transform.position, 0f, "instant");
            spatialEventCount++;
            WriteInquiry(inquiry, startedUtc.AddSeconds(siteStartedAt + 220), siteId,
                "hypothesis_selected", "hypothesis", "decision_station", string.Empty,
                "Evidence-linked control hypothesis selected.", relevantCount, evidence.Length,
                distractorCount, false, zones[^1], site, zones[^1].transform.position);
            WriteInquiry(inquiry, startedUtc.AddSeconds(siteStartedAt + 230), siteId,
                "final_explanation_submitted", "report", "decision_station", string.Empty,
                "Final explanation submitted after evidence and hands-on controls.", relevantCount,
                evidence.Length, distractorCount, false, zones[^1], site, zones[^1].transform.position);
            inquiryEventCount += 2;
            WriteSpatial(spatial, startedUtc.AddSeconds(siteStartedAt + SecondsPerSite), "site_exit", siteId,
                string.Empty, string.Empty, zones[^1], site, zones[^1].transform.position, SecondsPerSite,
                "duration_seconds");
            spatialEventCount++;
        }

        static void WriteSpatial(TextWriter writer, DateTime timestamp, string eventType, TrainingSiteId siteId,
            string subjectId, string outcome, SpatialAnalyticsZone zone, SiteExperienceZone site,
            Vector3 worldPosition, float metric, string metricKind)
        {
            var local = worldPosition - site.transform.position;
            writer.WriteLine(JsonConvert.SerializeObject(new
            {
                timestampUtc = timestamp.ToString("O", CultureInfo.InvariantCulture),
                sessionId = SessionId,
                eventType,
                site = siteId.ToString(),
                subjectId,
                outcome,
                zoneId = zone.ZoneId,
                zoneName = zone.DisplayName,
                worldX = worldPosition.x,
                worldY = worldPosition.y,
                worldZ = worldPosition.z,
                siteX = local.x,
                siteY = local.y,
                siteZ = local.z,
                durationOrDistance = metric,
                metricKind
            }));
        }

        static void WriteInquiry(TextWriter writer, DateTime timestamp, TrainingSiteId siteId,
            string eventType, string phase, string objectId, string hazardType, string detail,
            int evidenceCount, int collectedItemCount, int distractorCount, bool isDistractor,
            SpatialAnalyticsZone zone, SiteExperienceZone site, Vector3 worldPosition)
        {
            var local = worldPosition - site.transform.position;
            writer.WriteLine(JsonConvert.SerializeObject(new
            {
                timestampUtc = timestamp.ToString("O", CultureInfo.InvariantCulture),
                sessionId = SessionId,
                eventType,
                phase,
                site = siteId.ToString(),
                objectId,
                hazardType,
                detail,
                evidenceCount,
                collectedItemCount,
                distractorCount,
                isDistractor,
                zoneId = zone.ZoneId,
                zoneName = zone.DisplayName,
                worldX = worldPosition.x,
                worldY = worldPosition.y,
                worldZ = worldPosition.z,
                siteX = local.x,
                siteY = local.y,
                siteZ = local.z
            }));
        }

        static SpatialAnalyticsZone NearestZone(IEnumerable<SpatialAnalyticsZone> zones, Vector3 position)
        {
            return zones.OrderBy(zone => Vector3.SqrMagnitude(zone.transform.position - position)).First();
        }

        static List<PracticalActionPoint> PracticalActions(TrainingSiteId siteId, SiteExperienceZone site)
        {
            if (siteId == TrainingSiteId.Construction)
                return site.GetComponentsInChildren<ConstructionActionInteractable>(true)
                    .OrderBy(item => item.StepIndex)
                    .Select(item => new PracticalActionPoint(item.ActionName, item.transform.position))
                    .ToList();
            return site.GetComponentsInChildren<SitePracticalAction>(true)
                .OrderBy(item => item.StepIndex)
                .Select(item => new PracticalActionPoint(item.ActionName, item.transform.position))
                .ToList();
        }

        static int CountPracticalActions()
        {
            return UnityEngine.Object.FindObjectsByType<ConstructionActionInteractable>(FindObjectsSortMode.None).Length +
                   UnityEngine.Object.FindObjectsByType<SitePracticalAction>(FindObjectsSortMode.None).Length;
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

        readonly struct PracticalActionPoint
        {
            public PracticalActionPoint(string name, Vector3 position)
            {
                Name = name;
                Position = position;
            }

            public string Name { get; }
            public Vector3 Position { get; }
        }
    }

    [Serializable]
    public sealed class DemoPilotResult
    {
        public string sessionId = string.Empty;
        public int simulatedSeconds;
        public int sitesVisited;
        public int analyticsZonesVisited;
        public int inspectionTargetsVisited;
        public int evidenceObjectsVisited;
        public int practicalActionsAttempted;
        public int spatialEvents;
        public int inquiryEvents;
        public int spatialRowsExported;
        public int inquiryRowsExported;
        public int dwellRowsExported;
        public int routeSummaryRowsExported;
        public int previewPoints;
    }
}
