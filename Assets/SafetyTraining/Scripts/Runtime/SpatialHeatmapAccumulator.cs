using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// Always-active accumulator for the movement heatmap: lives on the training
    /// coordinator (which survives site isolation) and folds every 1 Hz spatial
    /// sample into per-site occupancy grids. Display components (the lobby board)
    /// read from here whenever they are visible.
    /// </summary>
    public sealed class SpatialHeatmapAccumulator : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] float cellSizeMeters = 2.5f;

        readonly Dictionary<TrainingSiteId, Dictionary<Vector2Int, int>> counts = new();
        TrainingEventLogger logger;

        public static SpatialHeatmapAccumulator Instance { get; private set; }

        public float CellSizeMeters => cellSizeMeters;
        public TrainingSiteId? LastSampledSite { get; private set; }
        public int Revision { get; private set; }

        public IReadOnlyDictionary<Vector2Int, int> GridFor(TrainingSiteId site) =>
            counts.TryGetValue(site, out var grid) ? grid : null;

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
            if (logger != null)
                logger.SpatialEventRecorded -= OnSpatialEvent;
        }

        void Update()
        {
            if (logger != null)
                return;
            logger = GetComponent<TrainingEventLogger>();
            if (logger != null)
                logger.SpatialEventRecorded += OnSpatialEvent;
        }

        void OnSpatialEvent(TrainingEventLogger.SpatialAnalyticsEvent spatialEvent)
        {
            if (spatialEvent.eventType != "spatial_sample")
                return;
            if (!System.Enum.TryParse<TrainingSiteId>(spatialEvent.site, out var site))
                return;
            if (!counts.TryGetValue(site, out var grid))
            {
                grid = new Dictionary<Vector2Int, int>();
                counts.Add(site, grid);
            }
            var cell = new Vector2Int(
                Mathf.FloorToInt(spatialEvent.siteX / cellSizeMeters),
                Mathf.FloorToInt(spatialEvent.siteZ / cellSizeMeters));
            grid.TryGetValue(cell, out var current);
            grid[cell] = current + 1;
            LastSampledSite = site;
            Revision++;
        }
    }
}
