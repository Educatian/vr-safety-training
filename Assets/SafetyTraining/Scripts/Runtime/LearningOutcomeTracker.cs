using System.Collections.Generic;
using System.Linq;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    [DefaultExecutionOrder(-950)]
    public sealed class LearningOutcomeTracker : MonoBehaviour
    {
        readonly Dictionary<string, HashSet<string>> earnedCriteria = new();
        readonly Dictionary<string, HashSet<string>> attemptedCriteria = new();
        InquiryEventLogger eventLogger;

        public static LearningOutcomeTracker Instance { get; private set; }

        void Awake()
        {
            Instance = this;
            eventLogger = GetComponent<InquiryEventLogger>() ?? gameObject.AddComponent<InquiryEventLogger>();
        }

        void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public void Record(TrainingSiteId site, string objectiveId, string criterionId,
            bool accepted, string detail, int possiblePoints = 1)
        {
            eventLogger ??= GetComponent<InquiryEventLogger>() ?? gameObject.AddComponent<InquiryEventLogger>();
            Get(attemptedCriteria, objectiveId).Add(criterionId);
            if (accepted) Get(earnedCriteria, objectiveId).Add(criterionId);
            eventLogger?.Record(new InquiryTelemetryEvent
            {
                SiteId = site,
                EventType = "assessment_evidence",
                Phase = "learning_outcome",
                ObjectId = criterionId,
                ObjectiveId = objectiveId,
                CriterionId = criterionId,
                Outcome = accepted ? "met" : "not_met",
                EarnedPoints = accepted ? possiblePoints : 0,
                PossiblePoints = possiblePoints,
                Detail = detail
            });
        }

        public int EarnedCount(string objectiveId) =>
            earnedCriteria.TryGetValue(objectiveId, out var values) ? values.Count : 0;

        /// <summary>Logs the session's experimental condition assignment to telemetry.</summary>
        public void RecordExperimentCondition(string flag, string value)
        {
            eventLogger ??= GetComponent<InquiryEventLogger>() ?? gameObject.AddComponent<InquiryEventLogger>();
            eventLogger?.Record(new InquiryTelemetryEvent
            {
                SiteId = TrainingSiteId.Construction,
                EventType = "experiment_condition",
                Phase = "session",
                ObjectId = flag,
                Outcome = value,
                Detail = $"{flag}={value}"
            });
        }

        public int EarnedCriteriaMatching(string fragment) => CountMatching(earnedCriteria, fragment);

        public int AttemptedCriteriaMatching(string fragment) => CountMatching(attemptedCriteria, fragment);

        static int CountMatching(Dictionary<string, HashSet<string>> source, string fragment)
        {
            var count = 0;
            foreach (var criteria in source.Values)
                foreach (var criterion in criteria)
                    if (criterion.Contains(fragment))
                        count++;
            return count;
        }

        public string CompactSummary(TrainingSiteId site)
        {
            return string.Join("   ", LearningObjectiveCatalog.ForSite(site).Select(objective =>
                $"{objective.Id} {Mathf.Min(EarnedCount(objective.Id), objective.RequiredCriteria)}/{objective.RequiredCriteria}"));
        }

        static HashSet<string> Get(Dictionary<string, HashSet<string>> source, string key)
        {
            if (!source.TryGetValue(key, out var values))
            {
                values = new HashSet<string>();
                source.Add(key, values);
            }
            return values;
        }
    }
}
