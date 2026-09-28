using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using SafetyTraining.Runtime;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Named crew NPC (GDD §14): idle gestures via NpcRelaxedPose, turns to face the learner when spoken to,
    // and answers through an OpenAI-compatible endpoint (OpenRouter). The reply is grounded in authored facts
    // and can never change DaySession state. Offline fallback keeps the game playable without a key.
    public sealed class CrewMember : MonoBehaviour
    {
        [SerializeField] private string displayName = "Dolores";
        [SerializeField] private string role = "veteran site safety manager and your mentor";
        [TextArea, SerializeField] private string facts = "";
        [SerializeField] private LlmEndpointConfig endpoint;
        [SerializeField] private string offlineLine = "Look for the energy: what could fall, move, shock or collapse here?";

        private readonly List<string> transcript = new List<string>();
        private Quaternion restRotation;
        private Transform listener;
        private NpcRelaxedPose pose;

        public string DisplayName => displayName;
        public IReadOnlyList<string> Transcript => transcript;
        public bool Thinking { get; private set; }

        public void Configure(string name, string npcRole, string groundedFacts, LlmEndpointConfig config)
        { displayName = name; role = npcRole; facts = groundedFacts; endpoint = config; }

        private void Awake()
        {
            restRotation = transform.rotation;
            pose = GetComponent<NpcRelaxedPose>();
        }

        private void Update()
        {
            // Turn toward the learner while talking; drift back to the work orientation afterwards.
            var goal = restRotation;
            if (listener != null)
            {
                var d = listener.position - transform.position; d.y = 0;
                if (d.sqrMagnitude > 0.01f) goal = Quaternion.LookRotation(d);
            }
            transform.rotation = Quaternion.Slerp(transform.rotation, goal, 1 - Mathf.Exp(-4f * Time.deltaTime));
        }

        public void BeginTalk(Transform learner)
        {
            listener = learner;
            if (transcript.Count == 0) transcript.Add($"{displayName}: What do you need?");
        }

        public void EndTalk()
        {
            listener = null;
            pose?.ReturnToIdle();
        }

        public async Task Ask(string question, string selectedContext)
        {
            transcript.Add("You: " + question);
            Thinking = true;
            string answer;
            try
            {
                if (endpoint == null) throw new InvalidOperationException("no endpoint");
                var service = new OpenAiCompatibleConversationService(endpoint);
                using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(endpoint.TimeoutSeconds));
                var reply = await service.ReplyAsync(new ConversationRequest
                {
                    siteName = "Municipal pump station jobsite, Tuscaloosa County, Alabama",
                    npcRole = $"{displayName}, {role}",
                    safetyFacts = facts + " " + selectedContext,
                    progress = "",
                    transcript = string.Join("\n", transcript.GetRange(Math.Max(0, transcript.Count - 8), Math.Min(8, transcript.Count))),
                    learnerMessage = question,
                }, cts.Token);
                answer = reply.Text;
                pose?.PlayEncouragement(false);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[Crew] {displayName} offline: {e.GetType().Name}");
                answer = offlineLine;
            }
            Thinking = false;
            transcript.Add($"{displayName}: {answer}");
        }
    }
}
