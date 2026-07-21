using System.Text;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// In-world live debrief dashboard: surfaces this session's assessment state
    /// (score, hit/false-alarm counts, evidence precision, engineering first-attempt
    /// rate, coach engagement, guided progress) on a lobby wall board. Read-only:
    /// it renders existing deterministic state and never mutates it.
    /// </summary>
    public sealed class SessionDebriefBoard : MonoBehaviour
    {
        [SerializeField] TextMesh statsDisplay;

        float nextRefreshAt;
        string lastRendered = string.Empty;
        string lastChartSignature = string.Empty;
        Transform chartRoot;
        readonly System.Collections.Generic.List<System.Collections.Generic.KeyValuePair<string, float>>
            masteryBuffer = new();

        static readonly Color BarColor = new(0.216f, 0.839f, 0.753f);
        static readonly Color BarBackColor = new(0.075f, 0.115f, 0.14f);
        static readonly Color AxisColor = new(0.3f, 0.42f, 0.48f);
        const float ChartBaselineY = -1.02f;
        const float BarMaxHeight = 0.58f;

        public void Configure(TextMesh display)
        {
            statsDisplay = display;
        }

        void Update()
        {
            if (statsDisplay == null || Time.unscaledTime < nextRefreshAt)
                return;
            nextRefreshAt = Time.unscaledTime + 1f;
            var text = Compose();
            if (text != lastRendered)
            {
                lastRendered = text;
                statsDisplay.text = text;
            }
            RefreshMasteryChart();
        }

        // Per-objective BKT posterior as a bar chart on the board: one teal bar per
        // observed objective on a dim track, objective id below and P(L) above.
        void RefreshMasteryChart()
        {
            var outcomes = LearningOutcomeTracker.Instance;
            if (outcomes == null)
                return;
            outcomes.CollectMasteryEstimates(masteryBuffer);
            var signature = new StringBuilder();
            foreach (var pair in masteryBuffer)
                signature.Append(pair.Key).Append(':').Append(Mathf.RoundToInt(pair.Value * 100f)).Append('|');
            if (signature.ToString() == lastChartSignature)
                return;
            lastChartSignature = signature.ToString();

            if (chartRoot != null)
                Destroy(chartRoot.gameObject);
            if (masteryBuffer.Count == 0)
                return;
            chartRoot = new GameObject("Mastery Chart").transform;
            chartRoot.SetParent(statsDisplay.transform.parent, false);
            chartRoot.localPosition = new Vector3(0f, ChartBaselineY, -0.065f);

            var slotWidth = Mathf.Min(0.58f, 4.1f / masteryBuffer.Count);
            var barWidth = slotWidth * 0.5f;
            var chartWidth = slotWidth * masteryBuffer.Count;
            var startX = -slotWidth * (masteryBuffer.Count - 1) / 2f;

            var axis = GameObject.CreatePrimitive(PrimitiveType.Cube);
            axis.name = "Chart Baseline";
            Destroy(axis.GetComponent<Collider>());
            axis.transform.SetParent(chartRoot, false);
            axis.transform.localPosition = new Vector3(0f, -0.005f, -0.012f);
            axis.transform.localScale = new Vector3(chartWidth + 0.2f, 0.012f, 0.02f);
            axis.GetComponent<Renderer>().material.color = AxisColor;

            for (var index = 0; index < masteryBuffer.Count; index++)
            {
                var x = startX + index * slotWidth;
                var mastery = Mathf.Clamp01(masteryBuffer[index].Value);
                CreateBar("Track", x, BarMaxHeight, barWidth, BarBackColor, 0f);
                CreateBar("Bar", x, BarMaxHeight * mastery, barWidth, BarColor, -0.012f);
                CreateBarLabel($"{Mathf.RoundToInt(mastery * 100f)}",
                    new Vector3(x, BarMaxHeight * mastery + 0.07f, -0.03f), 0.045f, BarColor);
                CreateBarLabel(masteryBuffer[index].Key,
                    new Vector3(x, -0.09f, -0.03f), 0.038f, new Color(0.62f, 0.74f, 0.8f));
            }
        }

        void CreateBar(string name, float x, float height, float width, Color color, float zOffset)
        {
            var bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = name;
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(chartRoot, false);
            bar.transform.localPosition = new Vector3(x, Mathf.Max(0.01f, height) / 2f, zOffset);
            bar.transform.localScale = new Vector3(width, Mathf.Max(0.01f, height), 0.02f);
            bar.GetComponent<Renderer>().material.color = color;
        }

        void CreateBarLabel(string text, Vector3 localPosition, float size, Color color)
        {
            var labelObject = new GameObject("Chart Label");
            labelObject.transform.SetParent(chartRoot, false);
            labelObject.transform.localPosition = localPosition;
            var label = labelObject.AddComponent<TextMesh>();
            label.text = text;
            label.characterSize = size;
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.color = color;
        }

        static string Compose()
        {
            var coordinator = TrainingCoordinator.Instance;
            if (coordinator == null)
                return "SESSION DEBRIEF\nINITIALIZING";
            var inquiry = InquirySessionController.Instance;
            var outcomes = LearningOutcomeTracker.Instance;

            var relevant = 0;
            var lookAlikes = 0;
            if (inquiry != null)
            {
                foreach (TrainingSiteId site in System.Enum.GetValues(typeof(TrainingSiteId)))
                {
                    relevant += inquiry.EvidenceCount(site);
                    lookAlikes += inquiry.DistractorCount(site);
                }
            }
            var evidenceTotal = relevant + lookAlikes;
            var precision = evidenceTotal > 0
                ? $"{Mathf.RoundToInt(100f * relevant / evidenceTotal)}%" : "--";

            var firstAttempt = outcomes != null
                ? outcomes.EarnedCriteriaMatching(":first_attempt") : 0;
            var decisionsAttempted = outcomes != null
                ? outcomes.AttemptedCriteriaMatching("decision:") : 0;

            var builder = new StringBuilder();
            builder.AppendLine($"SCORE {coordinator.OverallScore:0000}");
            builder.AppendLine(
                $"HAZARDS {coordinator.IdentifiedHazardCount}/{coordinator.TotalHazardCount}   " +
                $"FALSE POSITIVES {coordinator.TotalFalsePositives}");
            builder.AppendLine(
                $"EVIDENCE {relevant} RELEVANT / {lookAlikes} LOOK-ALIKE   PRECISION {precision}");
            builder.AppendLine(
                $"FIRST-ATTEMPT MARKS {firstAttempt}   DECISION STATIONS {decisionsAttempted}");
            builder.AppendLine(
                $"COACH TURNS {coordinator.CompletedCoachTurns}   " +
                $"PROGRESS {Mathf.RoundToInt(coordinator.GuidedProgress01 * 100f)}%");
            var objectiveCount = 0;
            var mastery = outcomes != null ? outcomes.MeanMastery(out objectiveCount) : -1f;
            builder.Append(mastery < 0f
                ? "BKT MASTERY --"
                : $"BKT MASTERY {Mathf.RoundToInt(mastery * 100f)}%  ({objectiveCount} OBJECTIVES)");
            return builder.ToString();
        }
    }
}
