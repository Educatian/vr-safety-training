using SafetyTraining.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace SafetyTraining.Editor
{
    static class PauseMenuBuilder
    {
        const string PanelPath = "Assets/ThirdParty/Kenney/UI-Pack-SciFi/PNG/Extra/Default/panel_glass_notches.png";
        const string FontPath = "Assets/ThirdParty/Kenney/UI-Pack-SciFi/Font/Kenney Future Narrow.ttf";
        const string MaterialPath = "Assets/SafetyTraining/GeneratedMaterials/Safety HUD UI.mat";

        static readonly Color Panel = new Color(0.05f, 0.09f, 0.125f, 0.94f);
        static readonly Color Field = new Color(0.063f, 0.11f, 0.153f, 1f);
        static readonly Color Primary = new Color(0.957f, 0.973f, 0.984f);
        static readonly Color Muted = new Color(0.624f, 0.69f, 0.749f);
        static readonly Color Accent = new Color(0.216f, 0.839f, 0.753f);
        static readonly Color Danger = new Color(0.83f, 0.29f, 0.24f);

        public static void Create()
        {
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(PanelPath);
            var displayFont = AssetDatabase.LoadAssetAtPath<Font>(FontPath);
            var bodyFont = SafetyUiFonts.Body;
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);

            var canvasObject = new GameObject("Pause Menu Canvas");
            var canvas = canvasObject.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 140;
            var scaler = canvasObject.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1200f, 700f);
            scaler.matchWidthOrHeight = 0.5f;
            canvasObject.AddComponent<GraphicRaycaster>();

            var dimObject = new GameObject("Pause Dim");
            dimObject.transform.SetParent(canvasObject.transform, false);
            var dimRect = dimObject.AddComponent<RectTransform>();
            dimRect.anchorMin = Vector2.zero;
            dimRect.anchorMax = Vector2.one;
            dimRect.offsetMin = Vector2.zero;
            dimRect.offsetMax = Vector2.zero;
            dimObject.AddComponent<Image>().color = new Color(0f, 0.02f, 0.04f, 0.62f);

            var panel = new GameObject("Pause Panel");
            panel.transform.SetParent(dimObject.transform, false);
            var panelRect = panel.AddComponent<RectTransform>();
            panelRect.anchorMin = new Vector2(0.5f, 0.5f);
            panelRect.anchorMax = new Vector2(0.5f, 0.5f);
            panelRect.pivot = new Vector2(0.5f, 0.5f);
            panelRect.anchoredPosition = Vector2.zero;
            panelRect.sizeDelta = new Vector2(460f, 330f);
            var panelImage = panel.AddComponent<Image>();
            panelImage.sprite = sprite;
            panelImage.material = material;
            panelImage.type = Image.Type.Sliced;
            panelImage.color = Panel;

            Text("Pause Title", panel.transform, "TRAINING PAUSED", displayFont, 26, Accent,
                new Vector2(0f, -30f), new Vector2(420f, 36f), TextAnchor.MiddleCenter);
            Text("Pause Hint", panel.transform,
                "PROGRESS IS KEPT FOR THIS SESSION\nWASD MOVE   LEFT CLICK INSPECT   H FOLD HUD",
                bodyFont, 14, Muted, new Vector2(0f, -70f), new Vector2(420f, 54f), TextAnchor.MiddleCenter);

            var resume = MenuButton("Resume Button", panel.transform, sprite, material, Accent,
                new Vector2(0f, -144f), "RESUME TRAINING", displayFont,
                new Color(0.02f, 0.07f, 0.09f));
            var quit = MenuButton("Quit Button", panel.transform, sprite, material, Danger,
                new Vector2(0f, -212f), "QUIT TO DESKTOP", displayFont, Primary);
            Text("Pause Footer", panel.transform, "ESC  RESUME", bodyFont, 14, Muted,
                new Vector2(0f, -272f), new Vector2(420f, 24f), TextAnchor.MiddleCenter);

            var controller = canvasObject.AddComponent<PauseMenuController>();
            controller.Configure(new PauseMenuController.Bindings
            {
                Panel = dimObject,
                ResumeButton = resume,
                QuitButton = quit
            });
            dimObject.SetActive(false);
        }

        static Button MenuButton(string name, Transform parent, Sprite sprite, Material material,
            Color background, Vector2 position, string label, Font font, Color labelColor)
        {
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var rect = item.AddComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 1f);
            rect.anchorMax = new Vector2(0.5f, 1f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = new Vector2(320f, 54f);
            var image = item.AddComponent<Image>();
            image.sprite = sprite;
            image.material = material;
            image.type = Image.Type.Sliced;
            image.color = background;
            var button = item.AddComponent<Button>();
            button.targetGraphic = image;
            var colors = button.colors;
            colors.highlightedColor = new Color(1.12f * background.r, 1.12f * background.g, 1.12f * background.b);
            colors.pressedColor = new Color(0.72f * background.r, 0.72f * background.g, 0.72f * background.b);
            button.colors = colors;
            Text($"{name} Label", item.transform, label, font, 19, labelColor,
                Vector2.zero, new Vector2(320f, 54f), TextAnchor.MiddleCenter, new Vector2(0.5f, 0.5f));
            return button;
        }

        static Text Text(string name, Transform parent, string value, Font font, int size, Color color,
            Vector2 position, Vector2 dimensions, TextAnchor alignment, Vector2? anchor = null)
        {
            var pivot = anchor ?? new Vector2(0.5f, 1f);
            var item = new GameObject(name);
            item.transform.SetParent(parent, false);
            var rect = item.AddComponent<RectTransform>();
            rect.anchorMin = pivot;
            rect.anchorMax = pivot;
            rect.pivot = pivot;
            rect.anchoredPosition = position;
            rect.sizeDelta = dimensions;
            var text = item.AddComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = size;
            text.color = color;
            text.alignment = alignment;
            return text;
        }
    }
}
