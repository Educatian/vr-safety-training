using System.Collections;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    [RequireComponent(typeof(Collider), typeof(XRSimpleInteractable), typeof(NpcConversationAgent))]
    public sealed class NpcTalkInteractable : MonoBehaviour
    {
        [SerializeField] NpcConversationAgent agent;
        [SerializeField] NpcSpeechBubbleView speechBubble;
        [SerializeField] TextMesh siteTitle;
        [SerializeField] string[] conversationPrompts =
        {
            "What should I inspect at this site?",
            "Give me a concise hint based on my current progress.",
            "Explain the safest control for the conditions I have found.",
            "Debrief my current performance without changing my score."
        };

        XRSimpleInteractable interactable;
        string displayedReply;
        int nextPrompt;
        bool conversationStarted;
        Coroutine dismissal;
        InteractiveHoverFeedback hoverFeedback;

        public bool ConversationActive => conversationStarted;

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            if (agent == null)
                agent = GetComponent<NpcConversationAgent>();
            hoverFeedback = GetComponent<InteractiveHoverFeedback>();
            if (hoverFeedback == null)
                hoverFeedback = gameObject.AddComponent<InteractiveHoverFeedback>();
        }

        void OnEnable()
        {
            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }

        void OnDisable()
        {
            if (interactable != null)
            {
                interactable.selectEntered.RemoveListener(OnSelected);
                interactable.hoverEntered.RemoveListener(OnHoverEntered);
                interactable.hoverExited.RemoveListener(OnHoverExited);
            }
        }

        void OnMouseEnter() => hoverFeedback?.SetHovered(true);
        void OnMouseExit() => hoverFeedback?.SetHovered(false);

        void Update()
        {
            if (!conversationStarted || agent == null || speechBubble == null)
                return;

            if (displayedReply != agent.LastReply)
            {
                displayedReply = agent.LastReply;
                speechBubble.Show(BuildBubblePreview(displayedReply), 0, 1);
            }
        }

        void OnMouseDown()
        {
            if (!DesktopPointerInputGate.CanUseWorldPointer)
                return;
            Ask();
        }

        public void Configure(
            NpcConversationAgent conversationAgent,
            NpcSpeechBubbleView bubble,
            TextMesh title)
        {
            agent = conversationAgent;
            speechBubble = bubble;
            siteTitle = title;
        }

        void OnSelected(SelectEnterEventArgs _)
        {
            Ask();
        }

        void OnHoverEntered(HoverEnterEventArgs _) => hoverFeedback?.SetHovered(true);
        void OnHoverExited(HoverExitEventArgs _) => hoverFeedback?.SetHovered(false);

        public void Ask()
        {
            if (agent == null || conversationPrompts == null || conversationPrompts.Length == 0)
                return;

            speechBubble?.Show("Ask me about the condition, risk, or safest control.", 0, 1);
            if (siteTitle != null)
                siteTitle.gameObject.SetActive(false);
            TrainingHud.Instance?.SetVisible(false);
            conversationStarted = true;
            var overlayChat = NpcChatPanel.Instance;
            if (overlayChat != null)
            {
                overlayChat.Open(agent, agent.name);
                return;
            }

            agent.Ask(conversationPrompts[nextPrompt]);
            nextPrompt = (nextPrompt + 1) % conversationPrompts.Length;
            if (dismissal != null)
                StopCoroutine(dismissal);
            dismissal = StartCoroutine(DismissAfterDelay());
        }

        public void PresentCoachFeedback(string message)
        {
            if (agent == null || speechBubble == null || string.IsNullOrWhiteSpace(message))
                return;
            if (NpcChatPanel.Instance?.IsOpenFor(agent) ?? false)
                return;

            agent.SetScriptedReply(message);
            displayedReply = null;
            conversationStarted = true;
            if (siteTitle != null)
                siteTitle.gameObject.SetActive(false);
            if (dismissal != null)
                StopCoroutine(dismissal);
            dismissal = StartCoroutine(DismissAfterDelay());
        }

        IEnumerator DismissAfterDelay()
        {
            yield return new WaitForSecondsRealtime(25f);
            dismissal = null;
            EndConversation();
        }

        public void EndConversation()
        {
            if (dismissal != null)
            {
                StopCoroutine(dismissal);
                dismissal = null;
            }
            speechBubble?.Hide();
            if (siteTitle != null)
                siteTitle.gameObject.SetActive(true);
            conversationStarted = false;
            var ownsCurrentChat = NpcChatPanel.Instance?.IsOpenFor(agent) ?? false;
            if (ownsCurrentChat)
            {
                NpcChatPanel.Instance.CloseFor(agent);
                TrainingHud.Instance?.SetVisible(true);
            }
        }

        public static string BuildBubblePreview(string value, int maximumCharacters = 280)
        {
            if (string.IsNullOrWhiteSpace(value))
                return "Ask me about the condition, risk, or safest control.";
            var normalized = value.Trim();
            if (normalized.Length <= maximumCharacters)
                return normalized;

            var cut = normalized.LastIndexOfAny(new[] { '.', '!', '?' },
                Mathf.Min(maximumCharacters, normalized.Length - 1));
            if (cut < maximumCharacters / 2)
                cut = normalized.LastIndexOf(' ', maximumCharacters);
            if (cut < 0)
                cut = maximumCharacters;
            return normalized[..(cut + 1)].TrimEnd() + "\nOpen coach chat for the full response and history.";
        }
    }
}
