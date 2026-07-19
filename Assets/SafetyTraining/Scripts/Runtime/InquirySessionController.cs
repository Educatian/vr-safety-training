using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    [DefaultExecutionOrder(-900)]
    public sealed class InquirySessionController : MonoBehaviour
    {
        readonly Dictionary<TrainingSiteId, HashSet<string>> collectedEvidence = new();
        readonly Dictionary<TrainingSiteId, HashSet<string>> relevantEvidence = new();
        readonly Dictionary<TrainingSiteId, string> hypotheses = new();

        InquiryEventLogger eventLogger;

        public const int DefaultMinimumEvidenceForReport = 3;

        public static InquirySessionController Instance { get; private set; }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            eventLogger = GetComponent<InquiryEventLogger>() ?? gameObject.AddComponent<InquiryEventLogger>();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        public int EvidenceCount(TrainingSiteId site)
        {
            return relevantEvidence.TryGetValue(site, out var items) ? items.Count : 0;
        }

        public int CollectedItemCount(TrainingSiteId site)
        {
            return collectedEvidence.TryGetValue(site, out var items) ? items.Count : 0;
        }

        public int DistractorCount(TrainingSiteId site)
        {
            return CollectedItemCount(site) - EvidenceCount(site);
        }

        public string Hypothesis(TrainingSiteId site)
        {
            return hypotheses.TryGetValue(site, out var hypothesis) ? hypothesis : string.Empty;
        }

        public bool CanSubmitReport(TrainingSiteId site, int minimumEvidenceRequired)
        {
            return EvidenceCount(site) >= Mathf.Max(0, minimumEvidenceRequired);
        }

        public void StartInquiry(TrainingSiteId site, string prompt)
        {
            if (!collectedEvidence.ContainsKey(site))
                collectedEvidence.Add(site, new HashSet<string>());
            if (!relevantEvidence.ContainsKey(site))
                relevantEvidence.Add(site, new HashSet<string>());
            if (!hypotheses.ContainsKey(site))
                hypotheses.Add(site, string.Empty);
            Record(site, "inquiry_started", "question", string.Empty, prompt, false);
        }

        public static string PromptFor(TrainingSiteId site)
        {
            return site switch
            {
                TrainingSiteId.Construction =>
                    "Investigate which evidence proves the construction site needs fall, access, or struck-by controls.",
                TrainingSiteId.Warehouse =>
                    "Investigate how pedestrian movement, loading, and storage create or control material-handling risk.",
                TrainingSiteId.FireResponse =>
                    "Investigate whether the scene supports evacuation, alarm, and safe extinguisher decision-making.",
                TrainingSiteId.ChemicalProcessing =>
                    "Investigate label, SDS, exposure-path, and containment evidence before choosing a spill response.",
                TrainingSiteId.ElectricalMaintenance =>
                    "Investigate energized-source, LOTO, damaged-cord, and wet-interface evidence before action.",
                _ => "Investigate the scene, collect evidence, and explain the safest action."
            };
        }

        public void CollectEvidence(EvidenceObject evidence)
        {
            if (!collectedEvidence.TryGetValue(evidence.SiteId, out var items))
            {
                items = new HashSet<string>();
                collectedEvidence.Add(evidence.SiteId, items);
            }

            items.Add(evidence.EvidenceId);
            if (!evidence.IsDistractor)
            {
                if (!relevantEvidence.TryGetValue(evidence.SiteId, out var relevantItems))
                {
                    relevantItems = new HashSet<string>();
                    relevantEvidence.Add(evidence.SiteId, relevantItems);
                }
                relevantItems.Add(evidence.EvidenceId);
            }
            var eventType = evidence.IsDistractor ? "distractor_selected" : "evidence_collected";
            Record(evidence.SiteId, eventType, "evidence", evidence.EvidenceId,
                evidence.Observation, evidence.IsDistractor, evidence.HazardType);
            LearningOutcomeTracker.Instance?.Record(evidence.SiteId,
                LearningObjectiveCatalog.ObjectiveAt(evidence.SiteId, 0).Id,
                $"field:{evidence.EvidenceId}", !evidence.IsDistractor,
                $"{evidence.Title}: {evidence.Observation}");
            TrainingCoordinator.Instance?.SetContextFeedback(
                $"{evidence.Title}\nRelevant evidence: {EvidenceCount(evidence.SiteId)}. " +
                $"Comparison samples: {DistractorCount(evidence.SiteId)}. {evidence.Observation}");
            NotifyCoach(evidence, EvidenceCount(evidence.SiteId), DistractorCount(evidence.SiteId));
        }

        public void SelectHypothesis(TrainingSiteId site, string hypothesis)
        {
            hypotheses[site] = hypothesis;
            Record(site, "hypothesis_selected", "hypothesis", string.Empty, hypothesis, false);
            var objectives = LearningObjectiveCatalog.ForSite(site);
            LearningOutcomeTracker.Instance?.Record(site, objectives[objectives.Count - 1].Id,
                "hypothesis", !string.IsNullOrWhiteSpace(hypothesis), hypothesis);
        }

        public void SubmitFinalExplanation(TrainingSiteId site, string explanation)
        {
            Record(site, "final_explanation_submitted", "report", string.Empty, explanation, false);
            var objectives = LearningObjectiveCatalog.ForSite(site);
            LearningOutcomeTracker.Instance?.Record(site, objectives[objectives.Count - 1].Id,
                "final_report", !string.IsNullOrWhiteSpace(explanation), explanation);
        }

        public void BlockReportSubmission(TrainingSiteId site, int minimumEvidenceRequired, string hypothesis)
        {
            var collected = EvidenceCount(site);
            var required = Mathf.Max(0, minimumEvidenceRequired);
            var remaining = Mathf.Max(0, required - collected);
            var detail = $"Report blocked: collect {remaining} more evidence item(s) before submitting. Hypothesis: {hypothesis}";
            Record(site, "report_blocked_insufficient_evidence", "report", string.Empty, detail, false);
            TrainingCoordinator.Instance?.SetContextFeedback(
                $"Collect more evidence before submitting\nEvidence: {collected}/{required}. Need {remaining} more.");
        }

        void Record(TrainingSiteId site, string eventType, string phase,
            string objectId, string detail, bool isDistractor, string hazardType = "")
        {
            eventLogger?.Record(new InquiryTelemetryEvent
            {
                SiteId = site,
                EventType = eventType,
                Phase = phase,
                ObjectId = objectId,
                HazardType = hazardType,
                Detail = detail,
                Hypothesis = Hypothesis(site),
                EvidenceCount = EvidenceCount(site),
                CollectedItemCount = CollectedItemCount(site),
                DistractorCount = DistractorCount(site),
                IsDistractor = isDistractor
            });
        }

        static void NotifyCoach(EvidenceObject evidence, int evidenceCount, int distractorCount)
        {
            foreach (var coach in Object.FindObjectsByType<NpcConversationAgent>(FindObjectsSortMode.None))
            {
                if (coach.SiteId != evidence.SiteId)
                    continue;
                var talk = coach.GetComponent<NpcTalkInteractable>();
                if (talk == null)
                    return;
                var tone = evidence.IsDistractor
                    ? "Good comparison sample. Now look for evidence that changes the control decision."
                    : "Good catch. Connect this observation to the safest control before you submit.";
                talk.PresentCoachFeedback(
                    $"{tone}\n{evidence.Title}: {evidence.HazardType}. " +
                    $"Relevant evidence {evidenceCount}/3, comparison samples {distractorCount}.");
                return;
            }
        }
    }
}
