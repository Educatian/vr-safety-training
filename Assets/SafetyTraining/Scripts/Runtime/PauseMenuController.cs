using UnityEngine;
using UnityEngine.UI;

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
            public Button ResumeButton;
            public Button QuitButton;
        }

        public static PauseMenuController Instance { get; private set; }
        public static bool VisibleNow => Instance != null && Instance.IsVisible;

        [SerializeField] GameObject panel;
        [SerializeField] Button resumeButton;
        [SerializeField] Button quitButton;
        bool controlsBound;

        public bool IsVisible => panel != null && panel.activeSelf;

        void Awake()
        {
            Instance = this;
            BindControls();
            SetVisible(false);
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

        public void Quit()
        {
            Application.Quit();
        }

        public void Configure(Bindings bindings)
        {
            panel = bindings.Panel;
            resumeButton = bindings.ResumeButton;
            quitButton = bindings.QuitButton;
            controlsBound = false;
            BindControls();
        }

        void BindControls()
        {
            if (controlsBound || resumeButton == null || quitButton == null)
                return;
            resumeButton.onClick.AddListener(Resume);
            quitButton.onClick.AddListener(Quit);
            controlsBound = true;
        }

        void UnbindControls()
        {
            if (!controlsBound)
                return;
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
