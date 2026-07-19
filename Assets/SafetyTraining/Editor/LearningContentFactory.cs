using System.Text;
using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Editor
{
    internal static class LearningContentFactory
    {
        static readonly Color Navy = new(0.035f, 0.075f, 0.11f);
        static readonly Color Steel = new(0.18f, 0.22f, 0.25f);
        static readonly Color Cyan = new(0.18f, 0.82f, 0.92f);
        static readonly Color Amber = new(1f, 0.58f, 0.08f);

        public static void CreateAll(Transform construction, Transform warehouse, Transform fire,
            Transform chemical, Transform electrical)
        {
            CreateObjectiveBoard(construction, TrainingSiteId.Construction);
            CreateObjectiveBoard(warehouse, TrainingSiteId.Warehouse);
            CreateObjectiveBoard(fire, TrainingSiteId.FireResponse);
            CreateObjectiveBoard(chemical, TrainingSiteId.ChemicalProcessing);
            CreateObjectiveBoard(electrical, TrainingSiteId.ElectricalMaintenance);

            var positions = new[]
            {
                new Vector3(11.35f, 0f, -6.2f),
                new Vector3(11.35f, 0f, 0.2f),
                new Vector3(11.35f, 0f, 6.6f)
            };
            for (var index = 0; index < ConstructionEngineeringCatalog.All.Count; index++)
                CreateEngineeringStation(construction, ConstructionEngineeringCatalog.All[index], positions[index]);
        }

        static void CreateObjectiveBoard(Transform site, TrainingSiteId siteId)
        {
            var root = new GameObject($"Learning Objectives - {siteId}");
            root.transform.SetParent(site, false);
            root.transform.localPosition = new Vector3(-7.7f, 0f, -10.65f);
            root.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

            Primitive(PrimitiveType.Cube, "Powder-Coated Frame", root.transform,
                new Vector3(0f, 1.55f, 0f), new Vector3(5.1f, 3.05f, 0.16f), Steel);
            var display = Primitive(PrimitiveType.Cube, "Learning Display", root.transform,
                new Vector3(0f, 1.55f, -0.105f), new Vector3(4.72f, 2.68f, 0.055f), Navy);
            display.AddComponent<XRSimpleInteractable>();
            display.AddComponent<InteractiveHoverFeedback>();
            var body = Label(string.Empty, root.transform, new Vector3(0f, 1.55f, -0.15f), 0.052f);
            body.anchor = TextAnchor.MiddleCenter;
            body.alignment = TextAlignment.Center;
            body.color = Color.white;
            display.AddComponent<LearningObjectiveBoard>().Configure(siteId, body);

            foreach (var x in new[] { -2.3f, 2.3f })
                Primitive(PrimitiveType.Cylinder, "Board Post", root.transform,
                    new Vector3(x, 0.72f, 0.08f), new Vector3(0.12f, 0.72f, 0.12f), Steel);
            Primitive(PrimitiveType.Cube, "Objective Accent", root.transform,
                new Vector3(0f, 2.83f, -0.15f), new Vector3(4.7f, 0.055f, 0.04f), Cyan);
        }

        static void CreateEngineeringStation(Transform site, EngineeringDecisionDefinition decision,
            Vector3 localPosition)
        {
            var root = new GameObject($"Engineering Decision - {decision.Id}");
            root.transform.SetParent(site, false);
            root.transform.localPosition = localPosition;
            root.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);

            Primitive(PrimitiveType.Cube, "Console Plinth", root.transform,
                new Vector3(0f, 0.12f, 0.12f), new Vector3(4.9f, 0.24f, 1.45f), Steel);
            Primitive(PrimitiveType.Cube, "Console Body", root.transform,
                new Vector3(0f, 1.18f, 0.28f), new Vector3(5.15f, 2.25f, 0.35f), Steel);
            var screen = Primitive(PrimitiveType.Cube, "Calculation Display", root.transform,
                new Vector3(0f, 1.45f, 0.075f), new Vector3(4.75f, 1.55f, 0.055f), Navy);
            var prompt = Label(Wrap($"{decision.Title}\n\nFIELD DATA\n{decision.LearningMaterial}\n\nCHECK\n{decision.Calculation}\n\nDECISION\n{decision.Question}\n\n{decision.Standard}", 64),
                root.transform, new Vector3(0f, 1.45f, -0.13f), 0.036f);
            prompt.anchor = TextAnchor.MiddleCenter;
            prompt.alignment = TextAlignment.Center;
            prompt.color = Color.white;

            var feedback = Label("SELECT A CONTROL DECISION", root.transform,
                new Vector3(0f, 0.52f, -0.78f), 0.055f);
            feedback.anchor = TextAnchor.MiddleCenter;
            feedback.alignment = TextAlignment.Center;
            feedback.color = Cyan;
            var station = root.AddComponent<EngineeringDecisionStation>();
            station.Configure(decision.Id, decision.Title, feedback);

            for (var index = 0; index < decision.Options.Count; index++)
            {
                var option = decision.Options[index];
                var x = -1.65f + index * 1.65f;
                var button = Primitive(PrimitiveType.Cube, $"Decision Option - {option.Id}", root.transform,
                    new Vector3(x, 0.3f, -0.62f), new Vector3(1.45f, 0.32f, 0.85f),
                    option.IsCorrect ? new Color(0.08f, 0.34f, 0.27f) : new Color(0.21f, 0.25f, 0.29f));
                button.AddComponent<XRSimpleInteractable>();
                button.AddComponent<InteractiveHoverFeedback>();
                button.AddComponent<EngineeringDecisionOption>().Configure(option.Id, option.Label,
                    option.IsCorrect, option.Feedback, station);
                var label = Label(Wrap(option.Label.ToUpperInvariant(), 18), root.transform,
                    new Vector3(x, 0.43f, -1.06f), 0.06f);
                label.anchor = TextAnchor.MiddleCenter;
                label.alignment = TextAlignment.Center;
                label.color = option.IsCorrect ? new Color(0.66f, 1f, 0.82f) : Color.white;
            }

            Primitive(PrimitiveType.Cube, "Hazard Stripe Left", root.transform,
                new Vector3(-2.42f, 2.2f, -0.15f), new Vector3(0.08f, 0.18f, 0.08f), Amber);
            Primitive(PrimitiveType.Cube, "Hazard Stripe Right", root.transform,
                new Vector3(2.42f, 2.2f, -0.15f), new Vector3(0.08f, 0.18f, 0.08f), Amber);
        }

        static GameObject Primitive(PrimitiveType type, string name, Transform parent,
            Vector3 position, Vector3 scale, Color color)
        {
            return SafetyScenePrimitives.Primitive(type, name, parent, position, scale, color);
        }

        static TextMesh Label(string value, Transform parent, Vector3 position, float size)
        {
            return SafetyScenePrimitives.Label(value, parent, position, size);
        }

        static string Wrap(string value, int width)
        {
            var output = new StringBuilder();
            foreach (var paragraph in value.Split('\n'))
            {
                var lineLength = 0;
                foreach (var word in paragraph.Split(' '))
                {
                    if (lineLength > 0 && lineLength + word.Length + 1 > width)
                    {
                        output.Append('\n');
                        lineLength = 0;
                    }
                    if (lineLength > 0) { output.Append(' '); lineLength++; }
                    output.Append(word);
                    lineLength += word.Length;
                }
                output.Append('\n');
            }
            return output.ToString().TrimEnd();
        }
    }
}
