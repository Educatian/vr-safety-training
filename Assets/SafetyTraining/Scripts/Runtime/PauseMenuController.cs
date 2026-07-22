using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.UI;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// Desktop pause menu: ESC toggles a resume/quit overlay whenever the NPC
    /// chat is not consuming the key. While open it blocks locomotion and the
    /// world pointer (checked by DesktopExplorerController).
    /// </summary>
    public sealed class PauseMenuController : MonoBehaviour
    {
        public struct Bindings
        {
            public GameObject Panel;
            public Button MenuButton;
            public Button ResumeButton;
            public Button QuitButton;
        }

        public static PauseMenuController Instance { get; private set; }
        public static bool VisibleNow => Instance != null && Instance.IsVisible;

        [SerializeField] GameObject panel;
        [SerializeField] Button menuButton;
        [SerializeField] Button resumeButton;
        [SerializeField] Button quitButton;
        bool controlsBound;

        public bool IsVisible => panel != null && panel.activeSelf;

        void Awake()
        {
            Instance = this;
            EnsureSideMenuButton();
            BindControls();
            SetVisible(false);
        }

        void EnsureSideMenuButton()
        {
            if (GetComponent<TrackedDeviceGraphicRaycaster>() == null)
                gameObject.AddComponent<TrackedDeviceGraphicRaycaster>();
            if (menuButton != null)
                return;
            var existing = transform.Find("Side Menu Button")?.GetComponent<Button>();
            if (existing != null)
            {
                menuButton = existing;
                return;
            }
            if (resumeButton == null)
                return;

            var clone = Instantiate(resumeButton.gameObject, transform);
            clone.name = "Side Menu Button";
            clone.SetActive(true);
            menuButton = clone.GetComponent<Button>();
            var rect = clone.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(1f, 0.5f);
            rect.anchoredPosition = new Vector2(-18f, 0f);
            rect.sizeDelta = new Vector2(110f, 48f);
            var label = clone.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = "MENU";
                label.rectTransform.sizeDelta = rect.sizeDelta;
            }
        }

        void OnDestroy()
        {
            UnbindControls();
            if (Instance == this)
                Instance = null;
        }

        void Update()
        {
            if (!Input.GetKeyDown(KeyCode.Escape))
                return;
            // The chat panel owns ESC while it is open (it closes itself); skip
            // this frame whether it already handled the key or is about to.
            var chat = NpcChatPanel.Instance;
            if (chat != null && (chat.IsVisible || chat.EscapeHandledFrame == Time.frameCount))
                return;
            SetVisible(!IsVisible);
        }

        public void Resume()
        {
            SetVisible(false);
        }

        public void ToggleMenu()
        {
            SetVisible(!IsVisible);
        }

        public void Quit()
        {
            Application.Quit();
        }

        public void Configure(Bindings bindings)
        {
            panel = bindings.Panel;
            menuButton = bindings.MenuButton;
            resumeButton = bindings.ResumeButton;
            quitButton = bindings.QuitButton;
            controlsBound = false;
            BindControls();
        }

        void BindControls()
        {
            if (controlsBound || menuButton == null || resumeButton == null || quitButton == null)
                return;
            menuButton.onClick.AddListener(ToggleMenu);
            resumeButton.onClick.AddListener(Resume);
            quitButton.onClick.AddListener(Quit);
            controlsBound = true;
        }

        void UnbindControls()
        {
            if (!controlsBound)
                return;
            menuButton.onClick.RemoveListener(ToggleMenu);
            resumeButton.onClick.RemoveListener(Resume);
            quitButton.onClick.RemoveListener(Quit);
            controlsBound = false;
        }

        void SetVisible(bool visible)
        {
            if (panel != null)
                panel.SetActive(visible);
        }
    }
}
