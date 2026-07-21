using UnityEngine;
using UnityEngine.UI;

namespace SafetyTraining.Runtime
{
    public sealed class TrainingHud : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Bindings
        {
            public CanvasGroup CanvasGroup;
            public Text SiteDisplay;
            public Text ScoreDisplay;
            public Text StateDisplay;
            public Text FeedbackDisplay;
            public Text ObjectivesDisplay;
            public Text ProgressDisplay;
            public RectTransform ProgressFill;
            public Image StateIcon;
        }

        [SerializeField] CanvasGroup canvasGroup;
        [SerializeField] Text siteDisplay;
        [SerializeField] Text scoreDisplay;
        [SerializeField] Text stateDisplay;
        [SerializeField] Text feedbackDisplay;
        [SerializeField] Text objectivesDisplay;
        [SerializeField] Text progressDisplay;
        [SerializeField] RectTransform progressFill;
        [SerializeField] Image stateIcon;
        string shownFeedback;
        bool collapsed;
        Text foldToggleLabel;
        static readonly Color Accent = new Color(0.216f, 0.839f, 0.753f);
        static readonly Color Warning = new Color(0.949f, 0.722f, 0.294f);
        static readonly Color Danger = new Color(0.941f, 0.392f, 0.357f);

        public static TrainingHud Instance { get; private set; }
        public bool IsCollapsed => collapsed;

        void Awake()
        {
            Instance = this;
        }

        void Start()
        {
            CreateFoldToggle();
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (Input.GetKeyDown(KeyCode.H))
                SetCollapsed(!collapsed);
            var coordinator = TrainingCoordinator.Instance;
            if (coordinator == null || feedbackDisplay == null)
                return;

            var elapsedSeconds = Mathf.FloorToInt(coordinator.SessionElapsedSeconds);
            var stateKey = $"{coordinator.OverallScore}|{coordinator.HudFeedback}|" +
                           $"{coordinator.ActiveSiteName}|{coordinator.CertificationComplete}|" +
                           $"{coordinator.ReviewedConditionCount}|{coordinator.ActiveLearningSummary}|{elapsedSeconds}";
            if (shownFeedback == stateKey)
                return;

            shownFeedback = stateKey;
            feedbackDisplay.text = coordinator.HudFeedback;
            if (siteDisplay != null)
                siteDisplay.text = coordinator.CertificationComplete ? "MISSION COMPLETE" : coordinator.ActiveSiteName;
            if (scoreDisplay != null)
                scoreDisplay.text = ExperimentConditions.DelayedFeedback
                    ? "----" : coordinator.OverallScore.ToString("0000");
            if (objectivesDisplay != null)
                objectivesDisplay.text = coordinator.ActiveLearningSummary;
            if (progressDisplay != null)
                progressDisplay.text = $"SESSION {elapsedSeconds / 60:00}:{elapsedSeconds % 60:00}";
            if (progressFill != null)
            {
                progressFill.localScale = new Vector3(Mathf.Max(0.02f, coordinator.GuidedProgress01), 1f, 1f);
            }
            ApplyState(coordinator.HudFeedback, coordinator.CertificationComplete);
        }

        public void Configure(Bindings bindings)
        {
            canvasGroup = bindings.CanvasGroup;
            siteDisplay = bindings.SiteDisplay;
            scoreDisplay = bindings.ScoreDisplay;
            stateDisplay = bindings.StateDisplay;
            feedbackDisplay = bindings.FeedbackDisplay;
            objectivesDisplay = bindings.ObjectivesDisplay;
            progressDisplay = bindings.ProgressDisplay;
            progressFill = bindings.ProgressFill;
            stateIcon = bindings.StateIcon;
        }

        public void SetVisible(bool visible)
        {
            if (canvasGroup == null)
                return;
            canvasGroup.alpha = visible ? 1f : 0f;
            canvasGroup.blocksRaycasts = false;
        }

        bool objectivesVisible = true;
        bool feedbackVisible = true;
        bool timeVisible = true;
        readonly System.Collections.Generic.Dictionary<string, Text> sectionButtons = new();

        public void SetCollapsed(bool value)
        {
            collapsed = value;
            SetSectionVisible("OBJ", !collapsed);
            SetSectionVisible("MSG", !collapsed);
            SetSectionVisible("TIME", !collapsed);
            if (foldToggleLabel != null)
                foldToggleLabel.text = collapsed ? "+" : "-";
        }

        /// <summary>Per-section HUD toggle: OBJ (objectives), MSG (feedback), TIME (session/progress).</summary>
        public void SetSectionVisible(string section, bool visible)
        {
            switch (section)
            {
                case "OBJ":
                    objectivesVisible = visible;
                    // The objectives text owns its shell panel; hide the whole panel
                    // so no empty frame is left behind.
                    SetPanelActive(objectivesDisplay, visible);
                    break;
                case "MSG":
                    feedbackVisible = visible;
                    SetSectionActive(feedbackDisplay, visible);
                    break;
                case "TIME":
                    timeVisible = visible;
                    SetSectionActive(progressDisplay, visible);
                    if (progressFill != null)
                        progressFill.gameObject.SetActive(visible);
                    break;
            }
            // Feedback and session timer share one shell; drop the panel only when
            // both sections are off.
            SetPanelActive(feedbackDisplay, feedbackVisible || timeVisible);
            collapsed = !objectivesVisible && !feedbackVisible && !timeVisible;
            if (foldToggleLabel != null)
                foldToggleLabel.text = collapsed ? "+" : "-";
            if (sectionButtons.TryGetValue(section, out var buttonLabel) && buttonLabel != null)
                buttonLabel.color = visible ? Accent : new Color(0.42f, 0.47f, 0.52f);
        }

        bool SectionVisible(string section) => section switch
        {
            "OBJ" => objectivesVisible,
            "MSG" => feedbackVisible,
            "TIME" => timeVisible,
            _ => true
        };

        static void SetSectionActive(Text section, bool active)
        {
            if (section != null)
                section.gameObject.SetActive(active);
        }

        static void SetPanelActive(Text section, bool active)
        {
            if (section != null && section.transform.parent != null)
                section.transform.parent.gameObject.SetActive(active);
        }

        void CreateFoldToggle()
        {
            if (canvasGroup == null || foldToggleLabel != null)
                return;
            var canvas = canvasGroup.GetComponentInParent<Canvas>();
            if (canvas == null)
                return;
            if (canvas.GetComponent<GraphicRaycaster>() == null)
                canvas.gameObject.AddComponent<GraphicRaycaster>();

            foldToggleLabel = CreateHudButton("-", 0, () => SetCollapsed(!collapsed));
            sectionButtons["OBJ"] = CreateHudButton("OBJ", 1, () => SetSectionVisible("OBJ", !SectionVisible("OBJ")));
            sectionButtons["MSG"] = CreateHudButton("MSG", 2, () => SetSectionVisible("MSG", !SectionVisible("MSG")));
            sectionButtons["TIME"] = CreateHudButton("TIME", 3, () => SetSectionVisible("TIME", !SectionVisible("TIME")));
        }

        Text CreateHudButton(string caption, int slot, UnityEngine.Events.UnityAction onClick)
        {
            var buttonRoot = new GameObject($"HUD Toggle {caption}",
                typeof(RectTransform), typeof(CanvasGroup), typeof(Image), typeof(Button));
            buttonRoot.transform.SetParent(canvasGroup.transform, false);
            var group = buttonRoot.GetComponent<CanvasGroup>();
            group.ignoreParentGroups = true;
            group.blocksRaycasts = true;
            group.interactable = true;
            var rect = buttonRoot.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(1f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(1f, 1f);
            rect.anchoredPosition = new Vector2(-4f - slot * 50f, -4f);
            rect.sizeDelta = new Vector2(slot == 0 ? 34f : 46f, 30f);
            buttonRoot.GetComponent<Image>().color = new Color(0.06f, 0.1f, 0.14f, 0.85f);
            buttonRoot.GetComponent<Button>().onClick.AddListener(onClick);

            var labelObject = new GameObject("Label", typeof(RectTransform), typeof(Text));
            labelObject.transform.SetParent(buttonRoot.transform, false);
            var labelRect = labelObject.GetComponent<RectTransform>();
            labelRect.anchorMin = Vector2.zero;
            labelRect.anchorMax = Vector2.one;
            labelRect.offsetMin = Vector2.zero;
            labelRect.offsetMax = Vector2.zero;
            var label = labelObject.GetComponent<Text>();
            label.text = caption;
            label.alignment = TextAnchor.MiddleCenter;
            label.fontSize = slot == 0 ? 22 : 13;
            label.fontStyle = FontStyle.Bold;
            label.color = Accent;
            label.raycastTarget = false;
            if (scoreDisplay != null)
                label.font = scoreDisplay.font;
            return label;
        }

        void ApplyState(string feedback, bool complete)
        {
            var label = "MISSION BRIEF";
            var color = Accent;
            if (complete)
                label = "REVIEW COMPLETE";
            else if (feedback.Contains("False positive"))
            {
                label = "REVIEW NOTE";
                color = Warning;
            }
            else if (feedback.Contains("Correct") || feedback.Contains("complete"))
                label = "CONTROL VERIFIED";
            else if (feedback.Contains("hazard", System.StringComparison.OrdinalIgnoreCase))
            {
                label = "HAZARD REVIEW";
                color = Danger;
            }
            if (stateDisplay != null)
            {
                stateDisplay.text = label;
                stateDisplay.color = color;
            }
            if (stateIcon != null)
                stateIcon.color = color;
        }
    }
}
