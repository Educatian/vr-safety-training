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
            if (text == lastRendered)
                return;
            lastRendered = text;
            statsDisplay.text = text;
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
            builder.Append(
                $"COACH TURNS {coordinator.CompletedCoachTurns}   " +
                $"PROGRESS {Mathf.RoundToInt(coordinator.GuidedProgress01 * 100f)}%");
            return builder.ToString();
        }
    }
}
