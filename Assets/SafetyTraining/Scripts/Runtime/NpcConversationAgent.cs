using System;
using System.Collections.Generic;
using System.Threading;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    public sealed class NpcConversationAgent : MonoBehaviour
    {
        [SerializeField] SafetyTraining.Core.TrainingSiteId siteId;
        [SerializeField] string npcRole = "site safety coach";
        [SerializeField, TextArea] string verifiedSafetyFacts = "Inspect the marked hazards before continuing.";
        [SerializeField] LlmEndpointConfig endpointConfig;
        [SerializeField] bool useLanguageModel = true;

        CancellationTokenSource lifetime;
        IConversationService fallback;
        readonly List<string> transcript = new List<string>();
        // Shared across coaches: one endpoint failure switches the whole session
        // to the instant scripted fallback (fresh installs have no local model).
        static bool endpointUnavailable;

        public string LastReply { get; private set; } = "Ask me about this training site.";
        public SafetyTraining.Core.TrainingSiteId SiteId => siteId;
        public string SiteName => siteId switch
        {
            SafetyTraining.Core.TrainingSiteId.FireResponse => "Fire Response",
            SafetyTraining.Core.TrainingSiteId.ChemicalProcessing => "Chemical Processing",
            SafetyTraining.Core.TrainingSiteId.ElectricalMaintenance => "Electrical Maintenance",
            SafetyTraining.Core.TrainingSiteId.TowerCrane => "Tower Crane",
            _ => siteId.ToString()
        };

        void Awake()
        {
            lifetime = new CancellationTokenSource();
            fallback = new ScriptedConversationService();
        }

        void OnDestroy()
        {
            lifetime.Cancel();
            lifetime.Dispose();
        }

        public async void Ask(string learnerMessage)
        {
            var golden = siteId == TrainingSiteId.Construction
                ? ConstructionGoldenModuleController.Instance
                : null;
            if (golden != null && golden.CanDebriefCoach)
                golden.TryCoachExplanation(learnerMessage);
            TrainingCoordinator.Instance?.RecordCoachTurn(siteId);
            var request = new ConversationRequest
            {
                siteName = siteId.ToString(),
                npcRole = npcRole,
                learnerMessage = learnerMessage,
                safetyFacts = $"{verifiedSafetyFacts} {OshaScenarioCatalog.GetSiteCoachBrief(siteId)}",
                progress = BuildSituationContext(Camera.main != null
                    ? Camera.main.transform.position : transform.position),
                transcript = string.Join("\n", transcript)
            };

            try
            {
                // Circuit breaker: once the endpoint fails (no local model on a fresh
                // install), stay on the instant scripted fallback for the rest of the
                // session instead of waiting out the HTTP timeout on every question.
                var service = useLanguageModel && endpointConfig != null && !endpointUnavailable
                    ? new OpenAiCompatibleConversationService(endpointConfig)
                    : fallback;
                LastReply = (await service.ReplyAsync(request, lifetime.Token)).Text;
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch (Exception) when (!lifetime.IsCancellationRequested)
            {
                if (!endpointUnavailable)
                {
                    endpointUnavailable = true;
                    Debug.LogWarning("LLM coach endpoint unavailable; using scripted coaching for this session.");
                }
                LastReply = (await fallback.ReplyAsync(request, lifetime.Token)).Text;
            }

            transcript.Add($"Learner: {learnerMessage}");
            transcript.Add($"Coach: {LastReply}");
            if (transcript.Count > 8)
                transcript.RemoveRange(0, transcript.Count - 8);
        }

        public void Configure(SafetyTraining.Core.TrainingSiteId trainingSite, string role, string facts)
        {
            siteId = trainingSite;
            npcRole = role;
            verifiedSafetyFacts = facts;
        }

        public void ConfigureEndpoint(LlmEndpointConfig config)
        {
            endpointConfig = config;
        }

        public void SetScriptedReply(string message)
        {
            LastReply = message;
            transcript.Add($"Coach: {message}");
            if (transcript.Count > 8)
                transcript.RemoveRange(0, transcript.Count - 8);
        }

        public string BuildSituationContext(Vector3 learnerPosition)
        {
            var coordinator = TrainingCoordinator.Instance;
            var inquiry = InquirySessionController.Instance;
            var progress = coordinator != null
                ? $"{coordinator.GetProgress(siteId)}. {coordinator.GetDebrief(siteId)}"
                : "not started";
            var evidence = inquiry != null
                ? $"Relevant evidence {inquiry.EvidenceCount(siteId)}, comparison samples {inquiry.DistractorCount(siteId)}"
                : "No inquiry evidence recorded";
            var hypothesis = inquiry?.Hypothesis(siteId);
            var zone = SpatialAnalyticsZone.FindContaining(siteId, learnerPosition);
            var location = zone != null
                ? $"Learner location: {zone.DisplayName} ({zone.ZoneId})"
                : "Learner location: outside a named investigation subzone";
            var nextAction = inquiry != null && inquiry.EvidenceCount(siteId) >=
                             InquirySessionController.DefaultMinimumEvidenceForReport
                ? "Ask the learner to compare controls and defend a decision."
                : "Prompt the learner toward a different subzone and ask for observable evidence.";
            return $"{progress}. {location}. {evidence}. " +
                   $"{(string.IsNullOrWhiteSpace(hypothesis) ? "No hypothesis selected." : $"Hypothesis: {hypothesis}.")} {nextAction}";
        }
    }
}
