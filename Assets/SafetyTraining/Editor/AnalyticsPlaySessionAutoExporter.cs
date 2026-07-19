using System;
using System.Globalization;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace SafetyTraining.Editor
{
    [InitializeOnLoad]
    public static class AnalyticsPlaySessionAutoExporter
    {
        const string AutoExportPrefsKey = "SafetyTraining.Analytics.AutoExportAfterPlay";
        const string AutoExportMenuPath = "Safety Training/Analytics/Auto Export After Play Mode";
        static DateTime playStartedUtc;

        static AnalyticsPlaySessionAutoExporter()
        {
            EditorApplication.playModeStateChanged -= OnPlayModeStateChanged;
            EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
        }

        public static bool AutoExportEnabled
        {
            get => EditorPrefs.GetBool(AutoExportPrefsKey, true);
            set => EditorPrefs.SetBool(AutoExportPrefsKey, value);
        }

        public static AnalyticsPlaySessionExportResult ExportLogsAndPreview(string logDirectory, string outputDirectory)
        {
            if (string.IsNullOrWhiteSpace(logDirectory))
                throw new ArgumentException("Log directory is required.", nameof(logDirectory));
            if (string.IsNullOrWhiteSpace(outputDirectory))
                throw new ArgumentException("Output directory is required.", nameof(outputDirectory));
            if (!Directory.Exists(logDirectory))
                return AnalyticsPlaySessionExportResult.Skipped(outputDirectory, "No SafetyTrainingLogs directory exists yet.");

            Directory.CreateDirectory(outputDirectory);
            var exportResult = AnalyticsLogExporter.ExportDirectory(logDirectory, outputDirectory);
            var previewPath = Path.Combine(outputDirectory, "analytics_map_preview.png");
            var previewResult = AnalyticsMapPreviewExporter.ExportPreview(outputDirectory, previewPath);
            return AnalyticsPlaySessionExportResult.Exported(
                outputDirectory,
                previewPath,
                exportResult.SpatialRowCount,
                exportResult.InquiryRowCount,
                exportResult.DwellRowCount,
                previewResult.PointCount,
                previewResult.InquiryPointCount,
                previewResult.DwellZoneCount);
        }

        [MenuItem(AutoExportMenuPath)]
        public static void ToggleAutoExportAfterPlay()
        {
            AutoExportEnabled = !AutoExportEnabled;
            Menu.SetChecked(AutoExportMenuPath, AutoExportEnabled);
            Debug.Log($"Safety analytics auto export after Play Mode: {(AutoExportEnabled ? "enabled" : "disabled")}");
        }

        [MenuItem(AutoExportMenuPath, true)]
        static bool ToggleAutoExportAfterPlayValidate()
        {
            Menu.SetChecked(AutoExportMenuPath, AutoExportEnabled);
            return true;
        }

        [MenuItem("Safety Training/Analytics/Export Current Logs And Preview")]
        public static void ExportCurrentLogsAndPreview()
        {
            var result = ExportLogsAndPreview(DefaultLogDirectory(), TimestampedOutputDirectory());
            LogResult(result, reveal: true);
        }

        public static void ExportFromEnvironment()
        {
            var logDirectory = ReadCommandLineValue("-safetyAnalyticsLogDir") ??
                               Environment.GetEnvironmentVariable("SAFETY_ANALYTICS_LOG_DIR") ??
                               DefaultLogDirectory();
            var outputDirectory = ReadCommandLineValue("-safetyAnalyticsOutputDir") ??
                                  Environment.GetEnvironmentVariable("SAFETY_ANALYTICS_OUTPUT_DIR") ??
                                  TimestampedOutputDirectory();
            var result = ExportLogsAndPreview(logDirectory, outputDirectory);
            LogResult(result, reveal: false);
        }

        static void OnPlayModeStateChanged(PlayModeStateChange state)
        {
            if (Application.isBatchMode)
                return;
            if (state == PlayModeStateChange.EnteredPlayMode)
            {
                playStartedUtc = DateTime.UtcNow;
                return;
            }
            if (state != PlayModeStateChange.EnteredEditMode || !AutoExportEnabled)
                return;
            var outputDirectory = TimestampedOutputDirectory(playStartedUtc == default ? DateTime.UtcNow : playStartedUtc);
            var result = ExportLogsAndPreview(DefaultLogDirectory(), outputDirectory);
            LogResult(result, reveal: false);
        }

        static string DefaultLogDirectory()
        {
            return Path.Combine(Application.persistentDataPath, "SafetyTrainingLogs");
        }

        static string TimestampedOutputDirectory()
        {
            return TimestampedOutputDirectory(DateTime.UtcNow);
        }

        static string TimestampedOutputDirectory(DateTime timestampUtc)
        {
            var stamp = timestampUtc.ToUniversalTime().ToString("yyyyMMdd_HHmmss", CultureInfo.InvariantCulture);
            return Path.Combine(Application.persistentDataPath, "SafetyTrainingAnalytics", $"session_{stamp}");
        }

        static void LogResult(AnalyticsPlaySessionExportResult result, bool reveal)
        {
            if (!result.DidExport)
            {
                Debug.LogWarning($"Safety analytics export skipped: {result.Message} output={result.OutputDirectory}");
                return;
            }
            Debug.Log($"Safety analytics post-play export complete: spatial={result.SpatialRowCount}, " +
                      $"inquiry={result.InquiryRowCount}, dwell={result.DwellRowCount}, " +
                      $"previewPoints={result.PreviewPointCount}, output={result.OutputDirectory}");
            if (reveal)
                EditorUtility.RevealInFinder(result.PreviewPath);
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

    public readonly struct AnalyticsPlaySessionExportResult
    {
        AnalyticsPlaySessionExportResult(bool didExport, string outputDirectory, string previewPath,
            int spatialRowCount, int inquiryRowCount, int dwellRowCount, int previewPointCount,
            int previewInquiryPointCount, int previewDwellZoneCount, string message)
        {
            DidExport = didExport;
            OutputDirectory = outputDirectory;
            PreviewPath = previewPath;
            SpatialRowCount = spatialRowCount;
            InquiryRowCount = inquiryRowCount;
            DwellRowCount = dwellRowCount;
            PreviewPointCount = previewPointCount;
            PreviewInquiryPointCount = previewInquiryPointCount;
            PreviewDwellZoneCount = previewDwellZoneCount;
            Message = message;
        }

        public bool DidExport { get; }
        public string OutputDirectory { get; }
        public string PreviewPath { get; }
        public int SpatialRowCount { get; }
        public int InquiryRowCount { get; }
        public int DwellRowCount { get; }
        public int PreviewPointCount { get; }
        public int PreviewInquiryPointCount { get; }
        public int PreviewDwellZoneCount { get; }
        public string Message { get; }

        public static AnalyticsPlaySessionExportResult Skipped(string outputDirectory, string message)
        {
            return new AnalyticsPlaySessionExportResult(false, outputDirectory, string.Empty, 0, 0, 0, 0, 0, 0, message);
        }

        public static AnalyticsPlaySessionExportResult Exported(string outputDirectory, string previewPath,
            int spatialRowCount, int inquiryRowCount, int dwellRowCount, int previewPointCount,
            int previewInquiryPointCount, int previewDwellZoneCount)
        {
            return new AnalyticsPlaySessionExportResult(true, outputDirectory, previewPath, spatialRowCount,
                inquiryRowCount, dwellRowCount, previewPointCount, previewInquiryPointCount, previewDwellZoneCount,
                "Export complete.");
        }
    }
}
