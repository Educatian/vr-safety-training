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
        static readonly Color Accent = new Color(0.216f, 0.839f, 0.753f);
        static readonly Color Warning = new Color(0.949f, 0.722f, 0.294f);
        static readonly Color Danger = new Color(0.941f, 0.392f, 0.357f);

        public static TrainingHud Instance { get; private set; }

        void Awake()
        {
            Instance = this;
        }

        void OnDestroy()
        {
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
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
                scoreDisplay.text = coordinator.OverallScore.ToString("0000");
            if (objectivesDisplay != null)
                objectivesDisplay.text = coordinator.ActiveLearningSummary;
            if (progressDisplay != null)
                progressDisplay.text = $"SESSION {elapsedSeconds / 60:00}:{elapsedSeconds % 60:00} / 20:00";
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
