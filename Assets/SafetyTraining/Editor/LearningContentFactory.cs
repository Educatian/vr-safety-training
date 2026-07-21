using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Editor
{
    internal static class LearningContentFactory
    {
        const string FontPath = "Assets/TextMesh Pro/Fonts/LiberationSans.ttf";
        static readonly Color Backplate = new(0.035f, 0.055f, 0.072f);
        static readonly Color Panel = new(0.055f, 0.095f, 0.125f, 0.98f);
        static readonly Color Card = new(0.075f, 0.13f, 0.165f, 0.98f);
        static readonly Color Steel = new(0.16f, 0.2f, 0.23f);
        static readonly Color Primary = new(0.93f, 0.97f, 1f);
        static readonly Color Muted = new(0.58f, 0.7f, 0.78f);
        static readonly Color Cyan = new(0.16f, 0.82f, 0.92f);
        static readonly Color Amber = new(1f, 0.58f, 0.08f);
        static Font font;

        public static void CreateAll(Transform construction, Transform warehouse, Transform fire,
            Transform chemical, Transform electrical, Transform towerCrane)
        {
            font = SafetyUiFonts.Body;
            CreateObjectiveBoard(construction, TrainingSiteId.Construction);
            CreateObjectiveBoard(warehouse, TrainingSiteId.Warehouse);
            CreateObjectiveBoard(fire, TrainingSiteId.FireResponse);
            CreateObjectiveBoard(chemical, TrainingSiteId.ChemicalProcessing);
            CreateObjectiveBoard(electrical, TrainingSiteId.ElectricalMaintenance);
            CreateObjectiveBoard(towerCrane, TrainingSiteId.TowerCrane);

            var positions = new[]
            {
                new Vector3(11.35f, 0f, -6.2f),
                new Vector3(11.35f, 0f, 0.2f),
                new Vector3(11.35f, 0f, 6.6f)
            };
            for (var index = 0; index < ConstructionEngineeringCatalog.All.Count; index++)
                CreateEngineeringStation(construction, TrainingSiteId.Construction, "CON-02",
                    ConstructionEngineeringCatalog.All[index], positions[index]);

            CreateEngineeringStation(warehouse, TrainingSiteId.Warehouse, "WAR-02",
                CrossSiteEngineeringCatalog.ForSite(TrainingSiteId.Warehouse), new Vector3(11.35f, 0f, 0.2f));
            CreateEngineeringStation(fire, TrainingSiteId.FireResponse, "FIR-02",
                CrossSiteEngineeringCatalog.ForSite(TrainingSiteId.FireResponse), new Vector3(11.35f, 0f, 0.2f));
            CreateEngineeringStation(chemical, TrainingSiteId.ChemicalProcessing, "CHE-02",
                CrossSiteEngineeringCatalog.ForSite(TrainingSiteId.ChemicalProcessing), new Vector3(11.35f, 0f, 0.2f));
            CreateEngineeringStation(electrical, TrainingSiteId.ElectricalMaintenance, "ELE-02",
                CrossSiteEngineeringCatalog.ForSite(TrainingSiteId.ElectricalMaintenance), new Vector3(11.35f, 0f, 0.2f));
            CreateEngineeringStation(towerCrane, TrainingSiteId.TowerCrane, "TCR-02",
                CrossSiteEngineeringCatalog.ForSite(TrainingSiteId.TowerCrane), new Vector3(11.35f, 0f, 0.2f));
        }

        static void CreateObjectiveBoard(Transform site, TrainingSiteId siteId)
        {
            var root = new GameObject($"Learning Objectives - {siteId}");
            root.transform.SetParent(site, false);
            root.transform.localPosition = new Vector3(-11.45f, 0f, -8.5f);
            root.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            Primitive(PrimitiveType.Cube, "Objective Kiosk Frame", root.transform,
                new Vector3(0f, 1.62f, 0f), new Vector3(3.58f, 2.12f, 0.16f), Steel);
            var surface = Primitive(PrimitiveType.Cube, "Objective Touch Surface", root.transform,
                new Vector3(0f, 1.62f, -0.11f), new Vector3(3.38f, 1.92f, 0.055f), Backplate);
            surface.AddComponent<XRSimpleInteractable>();
            surface.AddComponent<InteractiveHoverFeedback>();

            var canvas = WorldCanvas("Objective UI", root.transform, new Vector3(0f, 1.62f, -0.15f),
                new Vector2(1400f, 800f), 0.0024f, 12);
            UiImage("Header", canvas.transform, new Vector2(0f, 330f), new Vector2(1400f, 140f), Panel);
            UiImage("Header Accent", canvas.transform, new Vector2(-670f, 330f), new Vector2(14f, 140f), Cyan);
            UiText("Board Eyebrow", canvas.transform, "SITE LEARNING PLAN", new Vector2(-245f, 360f),
                new Vector2(760f, 34f), 24, Cyan, TextAnchor.MiddleLeft, FontStyle.Bold);
            var objectiveId = UiText("Objective Id", canvas.transform, "CON-01", new Vector2(580f, 330f),
                new Vector2(220f, 58f), 34, Primary, TextAnchor.MiddleCenter, FontStyle.Bold);
            var title = UiText("Objective Title", canvas.transform, "DIAGNOSE SITE RISK", new Vector2(-105f, 300f),
                new Vector2(1040f, 56f), 42, Primary, TextAnchor.MiddleLeft, FontStyle.Bold);

            UiText("Objective Label", canvas.transform, "PERFORMANCE OBJECTIVE", new Vector2(-340f, 205f),
                new Vector2(570f, 34f), 23, Cyan, TextAnchor.MiddleLeft, FontStyle.Bold);
            var statement = UiText("Objective Statement", canvas.transform, string.Empty, new Vector2(-340f, 75f),
                new Vector2(570f, 205f), 31, Primary, TextAnchor.UpperLeft, FontStyle.Normal);
            UiImage("Column Divider", canvas.transform, new Vector2(0f, 80f), new Vector2(3f, 310f), new Color(0.25f, 0.42f, 0.5f, 0.7f));
            UiText("Evidence Label", canvas.transform, "WHAT COUNTS AS EVIDENCE", new Vector2(340f, 205f),
                new Vector2(570f, 34f), 23, Amber, TextAnchor.MiddleLeft, FontStyle.Bold);
            var evidence = UiText("Evidence Statement", canvas.transform, string.Empty, new Vector2(340f, 75f),
                new Vector2(570f, 205f), 31, Primary, TextAnchor.UpperLeft, FontStyle.Normal);

            UiImage("Reference Strip", canvas.transform, new Vector2(0f, -185f), new Vector2(1250f, 90f), Card);
            UiText("Reference Label", canvas.transform, "STANDARD", new Vector2(-525f, -185f),
                new Vector2(170f, 44f), 22, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            var standard = UiText("Reference", canvas.transform, string.Empty, new Vector2(115f, -185f),
                new Vector2(1020f, 52f), 25, Primary, TextAnchor.MiddleLeft, FontStyle.Normal);
            var page = UiText("Page Prompt", canvas.transform, "SELECT PANEL FOR NEXT OBJECTIVE", new Vector2(0f, -330f),
                new Vector2(1250f, 48f), 25, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);

            surface.AddComponent<LearningObjectiveBoard>().Configure(siteId, objectiveId, title,
                statement, evidence, standard, page);
            foreach (var x in new[] { -1.55f, 1.55f })
                Primitive(PrimitiveType.Cylinder, "Kiosk Post", root.transform,
                    new Vector3(x, 0.52f, 0.08f), new Vector3(0.095f, 0.52f, 0.095f), Steel);
        }

        static void CreateEngineeringStation(Transform site, TrainingSiteId siteId, string objectiveId,
            EngineeringDecisionDefinition decision, Vector3 localPosition)
        {
            var root = new GameObject($"Engineering Decision - {decision.Id}");
            root.transform.SetParent(site, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            Primitive(PrimitiveType.Cube, "HMI Backplate", root.transform,
                new Vector3(0f, 1.7f, 0.04f), new Vector3(3.78f, 1.72f, 0.18f), Steel);
            Primitive(PrimitiveType.Cube, "HMI Screen", root.transform,
                new Vector3(0f, 1.7f, -0.075f), new Vector3(3.58f, 1.52f, 0.055f), Backplate);
            foreach (var x in new[] { -1.62f, 1.62f })
                Primitive(PrimitiveType.Cylinder, "Console Support", root.transform,
                    new Vector3(x, 0.55f, 0.12f), new Vector3(0.09f, 0.55f, 0.09f), Steel);
            Primitive(PrimitiveType.Cube, "Control Deck", root.transform,
                new Vector3(0f, 0.82f, -0.38f), new Vector3(3.62f, 0.18f, 0.72f), Steel);
            foreach (var x in new[] { -2.02f, 2.02f })
                Primitive(PrimitiveType.Cube, "Focus Wing", root.transform,
                    new Vector3(x, 1.35f, -0.98f), new Vector3(0.12f, 2.35f, 2.25f), Steel);

            var canvas = WorldCanvas("Engineering HMI", root.transform, new Vector3(0f, 1.7f, -0.13f),
                new Vector2(1600f, 700f), 0.0022f, 14);
            UiImage("Header", canvas.transform, new Vector2(0f, 295f), new Vector2(1600f, 110f), Panel);
            UiImage("Header Accent", canvas.transform, new Vector2(-775f, 295f), new Vector2(14f, 110f), Amber);
            UiText("System Label", canvas.transform, siteId == TrainingSiteId.Construction
                    ? "CIVIL ENGINEERING DECISION STATION"
                    : "APPLIED SAFETY ENGINEERING DECISION", new Vector2(-250f, 318f),
                new Vector2(980f, 30f), 21, Amber, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiText("Station Title", canvas.transform, decision.Title, new Vector2(-205f, 278f),
                new Vector2(1070f, 52f), 37, Primary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiImage("Objective Chip", canvas.transform, new Vector2(670f, 295f), new Vector2(190f, 62f), new Color(0.08f, 0.35f, 0.42f));
            UiText("Objective Chip Text", canvas.transform, objectiveId, new Vector2(670f, 295f),
                new Vector2(190f, 62f), 30, Primary, TextAnchor.MiddleCenter, FontStyle.Bold);

            UiImage("Field Card", canvas.transform, new Vector2(-385f, 65f), new Vector2(710f, 320f), Card);
            UiText("Field Card Label", canvas.transform, "01  FIELD INPUTS", new Vector2(-385f, 190f),
                new Vector2(620f, 34f), 23, Cyan, TextAnchor.MiddleLeft, FontStyle.Bold);
            var fieldData = UiText("Field Data", canvas.transform, FormatRows(decision.LearningMaterial), new Vector2(-385f, 55f),
                new Vector2(620f, 220f), 29, Primary, TextAnchor.UpperLeft, FontStyle.Normal);

            UiImage("Check Card", canvas.transform, new Vector2(385f, 65f), new Vector2(710f, 320f), Card);
            UiText("Check Card Label", canvas.transform, "02  ENGINEERING CHECK", new Vector2(385f, 190f),
                new Vector2(620f, 34f), 23, Amber, TextAnchor.MiddleLeft, FontStyle.Bold);
            var calculation = UiText("Calculation", canvas.transform, FormatRows(decision.Calculation), new Vector2(385f, 55f),
                new Vector2(620f, 220f), 29, Primary, TextAnchor.UpperLeft, FontStyle.Normal);

            UiImage("Question Strip", canvas.transform, new Vector2(0f, -165f), new Vector2(1480f, 105f), Panel);
            UiText("Decision Prompt Label", canvas.transform, "03  CONTROL DECISION", new Vector2(-555f, -165f),
                new Vector2(330f, 44f), 21, Cyan, TextAnchor.MiddleLeft, FontStyle.Bold);
            var questionText = UiText("Decision Prompt", canvas.transform, decision.Question, new Vector2(175f, -165f),
                new Vector2(1030f, 72f), 27, Primary, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiImage("Standard Strip", canvas.transform, new Vector2(0f, -90f), new Vector2(1480f, 42f),
                new Color(0.045f, 0.08f, 0.105f, 0.98f));
            UiText("Standard", canvas.transform, decision.Standard, new Vector2(0f, -90f),
                new Vector2(1420f, 34f), 22, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);

            var feedbackCanvas = WorldCanvas("Decision Feedback", root.transform, new Vector3(0f, 1.02f, -0.42f),
                new Vector2(1420f, 90f), 0.0022f, 16);
            UiImage("Feedback Background", feedbackCanvas.transform, Vector2.zero, new Vector2(1420f, 90f), Panel);
            var feedback = UiText("Feedback Text", feedbackCanvas.transform, "SELECT A CONTROL DECISION", Vector2.zero,
                new Vector2(1320f, 72f), 28, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            var station = root.AddComponent<EngineeringDecisionStation>();
            station.Configure(siteId, objectiveId, decision.Id, decision.Title, feedback);

            var optionComponents = new EngineeringDecisionOption[decision.Options.Count];
            var optionLabels = new Text[decision.Options.Count];
            for (var index = 0; index < decision.Options.Count; index++)
            {
                var option = decision.Options[index];
                var x = -1.18f + index * 1.18f;
                var button = Primitive(PrimitiveType.Cube, $"Decision Option - {option.Id}", root.transform,
                    new Vector3(x, 0.72f, -0.68f), new Vector3(1.02f, 0.28f, 0.46f),
                    new Color(0.12f, 0.18f, 0.22f));
                button.AddComponent<XRSimpleInteractable>();
                button.AddComponent<InteractiveHoverFeedback>();
                optionComponents[index] = button.AddComponent<EngineeringDecisionOption>();
                optionComponents[index].Configure(option.Id, option.Label,
                    option.IsCorrect, option.Feedback, station);
                var labelCanvas = WorldCanvas($"Option Label - {option.Id}", root.transform,
                    new Vector3(x, 0.72f, -0.925f), new Vector2(460f, 120f), 0.00215f, 18);
                optionLabels[index] = UiText("Option Text", labelCanvas.transform,
                    option.Label.ToUpperInvariant(), Vector2.zero,
                    new Vector2(430f, 105f), 27, Primary,
                    TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            station.ConfigureVariantUi(fieldData, calculation, questionText, optionComponents, optionLabels);

            Primitive(PrimitiveType.Cube, "Station Number Rail", root.transform,
                new Vector3(-1.88f, 1.7f, -0.15f), new Vector3(0.055f, 1.52f, 0.045f), Amber);
            CreateConsequenceVisual(root.transform, decision.Id);
        }

        // Hidden world-scale failure preview shown when the learner commits an
        // unsafe choice. Authored per known decision; a generic unsafe-state dome
        // covers the rest. Colliders are stripped so the ghost never intercepts
        // interaction or gaze rays.
        static void CreateConsequenceVisual(Transform stationRoot, string decisionId)
        {
            var ghostRoot = new GameObject($"Consequence Visual - {decisionId}");
            ghostRoot.transform.SetParent(stationRoot, false);
            ghostRoot.transform.localPosition = new Vector3(0f, 0f, 2.4f);
            var visual = ghostRoot.AddComponent<ConsequenceVisual>();
            visual.Configure(decisionId);
            var alertRed = new Color(0.62f, 0.14f, 0.1f);
            var ghostGray = new Color(0.55f, 0.5f, 0.48f);

            switch (decisionId)
            {
                case "formwork-capacity":
                {
                    var slab = GhostPrimitive(PrimitiveType.Cube, "Sagging Slab Ghost", ghostRoot.transform,
                        new Vector3(0f, 1.1f, 0f), new Vector3(3.4f, 0.22f, 2.4f), ghostGray);
                    slab.transform.localRotation = Quaternion.Euler(0f, 0f, 8f);
                    GhostPrimitive(PrimitiveType.Cube, "Buckled Shore A", ghostRoot.transform,
                            new Vector3(-1.2f, 0.5f, 0f), new Vector3(0.14f, 1f, 0.14f), alertRed)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 14f);
                    GhostPrimitive(PrimitiveType.Cube, "Buckled Shore B", ghostRoot.transform,
                            new Vector3(1.1f, 0.42f, 0.4f), new Vector3(0.14f, 0.85f, 0.14f), alertRed)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, -18f);
                    GhostLabel(ghostRoot.transform, "DEMAND 19,000 LB > CAPACITY 16,000 LB",
                        new Vector3(0f, 2.15f, 0f));
                    break;
                }
                case "crane-radius":
                {
                    GhostPrimitive(PrimitiveType.Cylinder, "Swing Radius Disc", ghostRoot.transform,
                        new Vector3(0f, 0.03f, 1.2f), new Vector3(7.2f, 0.015f, 7.2f),
                        new Color(0.62f, 0.14f, 0.1f, 1f));
                    GhostPrimitive(PrimitiveType.Cylinder, "Load Ghost", ghostRoot.transform,
                        new Vector3(0f, 1.6f, 1.2f), new Vector3(0.8f, 0.5f, 0.8f), ghostGray);
                    GhostLabel(ghostRoot.transform, "UNCONTROLLED SWING RADIUS  KEEP OUT",
                        new Vector3(0f, 2.3f, 1.2f));
                    break;
                }
                case "trench-system":
                {
                    GhostPrimitive(PrimitiveType.Cube, "Slough Wedge", ghostRoot.transform,
                            new Vector3(-0.6f, 0.7f, 0f), new Vector3(1.9f, 1.4f, 1.6f), alertRed)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, 32f);
                    GhostPrimitive(PrimitiveType.Capsule, "Worker Height Reference", ghostRoot.transform,
                        new Vector3(1.1f, 0.9f, 0f), new Vector3(0.42f, 0.9f, 0.42f), ghostGray);
                    GhostLabel(ghostRoot.transform, "6.5 FT WALL ABOVE HEAD HEIGHT  CAVE-IN RISK",
                        new Vector3(0f, 2.25f, 0f));
                    break;
                }
                default:
                {
                    GhostPrimitive(PrimitiveType.Sphere, "Unsafe State Dome", ghostRoot.transform,
                        new Vector3(0f, 0.1f, 0f), new Vector3(3.4f, 1.4f, 3.4f), alertRed);
                    GhostLabel(ghostRoot.transform, "UNSAFE STATE IF EXECUTED", new Vector3(0f, 1.9f, 0f));
                    break;
                }
            }
            ghostRoot.SetActive(false);
        }

        static GameObject GhostPrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 position, Vector3 scale, Color color)
        {
            var item = Primitive(type, name, parent, position, scale, color);
            var collider = item.GetComponent<Collider>();
            if (collider != null)
                Object.DestroyImmediate(collider);
            return item;
        }

        static void GhostLabel(Transform parent, string text, Vector3 position)
        {
            var label = SafetyScenePrimitives.Label(text, parent, position, 0.085f);
            label.color = new Color(1f, 0.62f, 0.5f);
        }

        static Canvas WorldCanvas(string name, Transform parent, Vector3 localPosition,
            Vector2 referenceSize, float worldScale, int sortingOrder)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(Canvas));
            item.transform.SetParent(parent, false);
            item.transform.localPosition = localPosition;
            item.transform.localRotation = Quaternion.identity;
            item.transform.localScale = Vector3.one * worldScale;
            var rect = item.GetComponent<RectTransform>();
            rect.sizeDelta = referenceSize;
            var canvas = item.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            canvas.sortingOrder = sortingOrder;
            return canvas;
        }

        static Image UiImage(string name, Transform parent, Vector2 position, Vector2 size, Color color)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var image = item.GetComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        static Text UiText(string name, Transform parent, string value, Vector2 position, Vector2 size,
            int fontSize, Color color, TextAnchor alignment, FontStyle style)
        {
            var item = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            item.transform.SetParent(parent, false);
            var rect = item.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            var text = item.GetComponent<Text>();
            text.text = value;
            text.font = font;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.lineSpacing = 1.05f;
            text.raycastTarget = false;
            return text;
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent,
            Vector3 position, Vector3 scale, Color color)
        {
            return SafetyScenePrimitives.Primitive(type, name, parent, position, scale, color);
        }

        static string FormatRows(string value)
        {
            return value.Replace("; ", "\n").Replace(". ", ".\n");
        }
    }
}
