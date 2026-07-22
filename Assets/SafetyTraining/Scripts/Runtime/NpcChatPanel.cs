using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace SafetyTraining.Runtime
{
    public sealed class NpcChatPanel : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Bindings
        {
            public GameObject Panel;
            public Text Title;
            public Text Transcript;
            public ScrollRect TranscriptScroll;
            public InputField Input;
            public Text SendLabel;
            public Button SendButton;
            public Button CloseButton;
        }

        public static NpcChatPanel Instance { get; private set; }

        [SerializeField] GameObject panel;
        [SerializeField] Text title;
        [SerializeField] Text transcript;
        [SerializeField] ScrollRect transcriptScroll;
        [SerializeField] InputField input;
        [SerializeField] Text sendLabel;
        [SerializeField] Button sendButton;
        [SerializeField] Button closeButton;
        NpcConversationAgent activeAgent;
        string lastReply;
        string conversationLog = string.Empty;
        Coroutine refresh;
        bool controlsBound;

        const int MaxTranscriptChars = 6000;

        public bool IsVisible => panel != null && panel.activeSelf;
        public bool IsOpenFor(NpcConversationAgent agent) => activeAgent == agent && IsVisible;
        // Frame stamp letting the pause menu skip an ESC press this panel consumed.
        public int EscapeHandledFrame { get; private set; } = -1;

        void Awake()
        {
            Instance = this;
            EnsureHistoryScrollAffordance();
            BindControls();
            SetVisible(false);
        }

        void EnsureHistoryScrollAffordance()
        {
            if (transcriptScroll == null || transcriptScroll.verticalScrollbar != null)
                return;
            var viewport = transcriptScroll.GetComponent<RectTransform>();
            if (viewport == null || viewport.parent == null)
                return;
            var parent = viewport.parent;
            var existing = parent.Find("History Scrollbar")?.GetComponent<Scrollbar>();
            if (existing != null)
            {
                transcriptScroll.verticalScrollbar = existing;
                return;
            }

            var panelImage = parent.GetComponent<Image>();
            var trackObject = new GameObject("History Scrollbar", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image), typeof(Scrollbar));
            trackObject.transform.SetParent(parent, false);
            var trackRect = trackObject.GetComponent<RectTransform>();
            trackRect.anchorMin = trackRect.anchorMax = trackRect.pivot = new Vector2(0f, 1f);
            trackRect.anchoredPosition = new Vector2(480f, -96f);
            trackRect.sizeDelta = new Vector2(12f, 108f);
            var trackImage = trackObject.GetComponent<Image>();
            trackImage.sprite = panelImage != null ? panelImage.sprite : null;
            trackImage.material = panelImage != null ? panelImage.material : null;
            trackImage.type = Image.Type.Sliced;
            trackImage.color = new Color(0.063f, 0.11f, 0.153f, 1f);

            var handleObject = new GameObject("Handle", typeof(RectTransform),
                typeof(CanvasRenderer), typeof(Image));
            handleObject.transform.SetParent(trackObject.transform, false);
            var handleRect = handleObject.GetComponent<RectTransform>();
            handleRect.anchorMin = handleRect.anchorMax = handleRect.pivot = new Vector2(0.5f, 1f);
            handleRect.anchoredPosition = new Vector2(0f, -2f);
            handleRect.sizeDelta = new Vector2(10f, 42f);
            var handleImage = handleObject.GetComponent<Image>();
            handleImage.sprite = trackImage.sprite;
            handleImage.material = trackImage.material;
            handleImage.type = Image.Type.Sliced;
            handleImage.color = new Color(0.216f, 0.839f, 0.753f, 1f);

            var scrollbar = trackObject.GetComponent<Scrollbar>();
            scrollbar.targetGraphic = handleImage;
            scrollbar.handleRect = handleRect;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            transcriptScroll.viewport = viewport;
            transcriptScroll.verticalScrollbar = scrollbar;
            transcriptScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;
            transcriptScroll.verticalScrollbarSpacing = 4f;
            if (transcript != null)
                transcript.rectTransform.sizeDelta = new Vector2(444f, transcript.rectTransform.sizeDelta.y);
            var status = parent.Find("Coach Status")?.GetComponent<Text>();
            if (status != null)
                status.text = "HISTORY  /  MOUSE WHEEL OR XR DRAG TO REVIEW";
        }

        void Update()
        {
            if (IsVisible && Input.GetKeyDown(KeyCode.Escape))
            {
                EscapeHandledFrame = Time.frameCount;
                Close();
            }
        }

        void OnDestroy()
        {
            UnbindControls();
            if (Instance == this)
                Instance = null;
        }

        public void Configure(Bindings bindings)
        {
            panel = bindings.Panel;
            title = bindings.Title;
            transcript = bindings.Transcript;
            transcriptScroll = bindings.TranscriptScroll;
            input = bindings.Input;
            sendLabel = bindings.SendLabel;
            sendButton = bindings.SendButton;
            closeButton = bindings.CloseButton;
        }

        public void Open(NpcConversationAgent agent, string npcName)
        {
            activeAgent = agent;
            var conciseSiteName = agent.SiteName.Split(' ')[0].ToUpperInvariant();
            title.text = $"{conciseSiteName} SAFETY COACH";
            // Restore this coach's earlier exchanges so reopening the chat keeps
            // prior suggestions reachable through the scrollback.
            var history = agent.TranscriptLog;
            conversationLog = $"{agent.SiteName} coach ready.\nAsk about the visible condition, risk, or required control.";
            if (!string.IsNullOrEmpty(history))
                conversationLog = $"{conversationLog}\n\n{history}";
            transcript.text = conversationLog;
            lastReply = agent.LastReply;
            SetVisible(true);
            ScrollToLatest();
            input.Select();
            input.ActivateInputField();
            if (refresh != null)
                StopCoroutine(refresh);
            refresh = StartCoroutine(RefreshReply());
        }

        public void Close()
        {
            DesktopPointerInputGate.BlockFor(0.15f);
            if (refresh != null)
                StopCoroutine(refresh);
            refresh = null;
            var previousAgent = activeAgent;
            var restoreHud = previousAgent != null && TrainingCoordinator.Instance?.ActiveSite != null;
            activeAgent = null;
            SetVisible(false);
            previousAgent?.GetComponent<NpcTalkInteractable>()?.EndConversation();
            if (restoreHud)
                TrainingHud.Instance?.SetVisible(true);
        }

        public void CloseFor(NpcConversationAgent agent)
        {
            if (activeAgent == agent)
                Close();
        }

        public void SubmitCurrent()
        {
            OnSubmit(input != null ? input.text : string.Empty);
        }

        void OnSubmit(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || activeAgent == null)
                return;
            var message = value.Trim();
            input.text = string.Empty;
            AppendToLog($"You: {message}");
            transcript.text = $"{conversationLog}\n\nCoach is responding...";
            ScrollToLatest();
            activeAgent.Ask(message);
        }

        IEnumerator RefreshReply()
        {
            while (activeAgent != null)
            {
                if (activeAgent.LastReply != lastReply)
                {
                    lastReply = activeAgent.LastReply;
                    AppendToLog($"Coach: {lastReply}");
                    transcript.text = conversationLog;
                    ScrollToLatest();
                    input.Select();
                    input.ActivateInputField();
                }
                yield return null;
            }
        }

        void ScrollToLatest()
        {
            if (transcriptScroll == null)
                return;
            Canvas.ForceUpdateCanvases();
            transcriptScroll.verticalNormalizedPosition = 0f;
        }

        void AppendToLog(string line)
        {
            conversationLog = string.IsNullOrEmpty(conversationLog)
                ? line
                : $"{conversationLog}\n\n{line}";
            if (conversationLog.Length > MaxTranscriptChars)
                conversationLog = conversationLog[^MaxTranscriptChars..];
        }

        void SetVisible(bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }

        void BindControls()
        {
            if (controlsBound)
                return;
            input.onEndEdit.AddListener(OnSubmit);
            sendButton.onClick.AddListener(SubmitCurrent);
            closeButton.onClick.AddListener(Close);
            controlsBound = true;
        }

        void UnbindControls()
        {
            if (!controlsBound)
                return;
            input.onEndEdit.RemoveListener(OnSubmit);
            sendButton.onClick.RemoveListener(SubmitCurrent);
            closeButton.onClick.RemoveListener(Close);
            controlsBound = false;
        }
    }
}
