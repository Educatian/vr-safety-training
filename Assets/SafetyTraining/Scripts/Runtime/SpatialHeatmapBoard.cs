using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// In-world movement heatmap display: renders the most recently visited site's
    /// occupancy grid (accumulated by SpatialHeatmapAccumulator, which stays alive
    /// through site isolation) as a single-hue light-to-dark cell map on a lobby
    /// board. Refreshes whenever new samples arrived while the lobby is visible.
    /// </summary>
    public sealed class SpatialHeatmapBoard : MonoBehaviour
    {
        [SerializeField] TextMesh titleDisplay;
        [SerializeField, Min(1f)] float refreshSeconds = 2f;

        const float BoardWidth = 4.1f;
        const float BoardHeight = 1.75f;
        // Site-local walkable extents (annex included).
        const float SiteMinX = -15f, SiteMaxX = 15f, SiteMinZ = -13f, SiteMaxZ = 22f;

        static readonly Color[] Ramp =
        {
            new(0.10f, 0.22f, 0.22f),
            new(0.12f, 0.35f, 0.33f),
            new(0.15f, 0.50f, 0.45f),
            new(0.18f, 0.66f, 0.58f),
            new(0.22f, 0.84f, 0.75f),
            new(0.55f, 0.96f, 0.88f)
        };

        Transform cellRoot;
        float nextRefreshAt;
        int renderedRevision = -1;

        public void Configure(TextMesh title)
        {
            titleDisplay = title;
        }

        void Update()
        {
            var accumulator = SpatialHeatmapAccumulator.Instance;
            if (accumulator == null || Time.unscaledTime < nextRefreshAt)
                return;
            nextRefreshAt = Time.unscaledTime + refreshSeconds;
            if (accumulator.Revision == renderedRevision)
                return;
            renderedRevision = accumulator.Revision;
            Render(accumulator);
        }

        void Render(SpatialHeatmapAccumulator accumulator)
        {
            if (!accumulator.LastSampledSite.HasValue)
                return;
            var site = accumulator.LastSampledSite.Value;
            var grid = accumulator.GridFor(site);
            if (grid == null || grid.Count == 0)
                return;
            if (titleDisplay != null)
                titleDisplay.text = $"MOVEMENT HEATMAP - {site.ToString().ToUpperInvariant()}";
            if (cellRoot != null)
                Destroy(cellRoot.gameObject);
            cellRoot = new GameObject("Heatmap Cells").transform;
            cellRoot.SetParent(transform, false);
            cellRoot.localPosition = new Vector3(0f, -0.15f, -0.065f);

            var cellSize = accumulator.CellSizeMeters;
            var maxCount = 1;
            foreach (var value in grid.Values)
                if (value > maxCount)
                    maxCount = value;
            var cellsX = Mathf.CeilToInt((SiteMaxX - SiteMinX) / cellSize);
            var cellsZ = Mathf.CeilToInt((SiteMaxZ - SiteMinZ) / cellSize);
            var quadWidth = BoardWidth / cellsX;
            var quadHeight = BoardHeight / cellsZ;
            foreach (var pair in grid)
            {
                var normalizedX = (pair.Key.x * cellSize - SiteMinX) / (SiteMaxX - SiteMinX);
                var normalizedZ = (pair.Key.y * cellSize - SiteMinZ) / (SiteMaxZ - SiteMinZ);
                if (normalizedX < 0f || normalizedX > 1f || normalizedZ < 0f || normalizedZ > 1f)
                    continue;
                var intensity = Mathf.Log(1f + pair.Value) / Mathf.Log(1f + maxCount);
                var bucket = Mathf.Min(Ramp.Length - 1, Mathf.FloorToInt(intensity * Ramp.Length));
                var quad = GameObject.CreatePrimitive(PrimitiveType.Cube);
                quad.name = "Heat Cell";
                Destroy(quad.GetComponent<Collider>());
                quad.transform.SetParent(cellRoot, false);
                quad.transform.localPosition = new Vector3(
                    (normalizedX - 0.5f) * BoardWidth,
                    (normalizedZ - 0.5f) * BoardHeight,
                    0f);
                quad.transform.localScale = new Vector3(quadWidth * 0.94f, quadHeight * 0.94f, 0.015f);
                quad.GetComponent<Renderer>().material.color = Ramp[bucket];
            }
        }
    }
}
