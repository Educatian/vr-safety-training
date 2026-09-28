using System;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    public sealed class FieldTablet : MonoBehaviour
    {
        [SerializeField] private ShiftDirector director;
        [SerializeField] private RectTransform content;
        [SerializeField] private Text radioText;
        [SerializeField] private Font font;
        private EnergySource energy = EnergySource.Gravity;
        private int probability = 3, severity = 3;
        public void Configure(ShiftDirector shift, RectTransform panel, Text radio, Font face)
        { director = shift; content = panel; radioText = radio; font = face; }
        private void Update() { if (director != null) radioText.text = director.Notice; }
        public void Refresh()
        {
            if (content == null || director == null) return;
            content.gameObject.SetActive(director.MenuOpen);
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            if (!director.MenuOpen) return;
            Label("COMPETENT PERSON / MONDAY", 23, new Color(.96f, .72f, .3f));
            if (!director.Started)
            {
                Label("Dolores · Your first site walk", 28, Color.white);
                Label("Notice conditions. Photograph, assess, then choose a control.");
                Label("WASD move · Hold right mouse to look · E inspect");
                Label("TAB opens your tablet. Stops never reduce safety ratings.");
                Button("Begin shift", director.Begin);
                return;
            }
            if (director.Finished)
            {
                Label("SHIFT CLOSED", 28, Color.white);
                Label($"Hazards found: {director.Session.HazardIdentificationIndex:P0} · Precision: {director.Session.ReportPrecision:P0}");
                Label($"Crew trust: {director.Session.CrewTrust} · Incidents: {director.Session.NearMisses + director.Session.Recordables}");
                foreach (var condition in director.Conditions)
                    if (condition.IsHazard) Label(condition.DisplayName + " · " + director.Session.GetState(condition.Id));
                Label("Tomorrow's toolbox talk: what comes first?");
                Button("Protect edges. Clear access. Verify controls.", () => director.ExplainBack(0));
                Button("Keep schedule. Rely on reminders and PPE.", () => director.ExplainBack(1));
                return;
            }
            if (director.Selected == null)
            {
                Label("SITE WALK", 28, Color.white);
                Label("Center a condition. Move close. Press E to photograph.");
                Button("Return to site", director.ToggleTablet);
                Button("Finish shift", director.EndShift);
                return;
            }
            var target = director.Selected;
            var state = director.Session.GetState(target.Id);
            Label(target.DisplayName, 28, Color.white);
            if (state == HazardState.Latent || state == HazardState.Lapsed)
            {
                Button("Energy: " + energy, () => { energy = (EnergySource)(((int)energy + 1) % Enum.GetValues(typeof(EnergySource)).Length); Refresh(); });
                Button("Probability: " + probability + " / 5", () => { probability = probability % 5 + 1; Refresh(); });
                Button("Severity: " + severity + " / 5", () => { severity = severity % 5 + 1; Refresh(); });
                Button("Submit report", () => director.Report(energy, probability, severity));
            }
            else
            {
                Label("Report status · " + state);
                if (state == HazardState.Reported || state == HazardState.Stopped)
                {
                    Button("Fix · install engineered control", () => director.Control(ControlLevel.Engineering));
                    Button("Assign · temporary reminder", () => director.Control(ControlLevel.Administrative));
                    Button("PPE · individual protection", () => director.Control(ControlLevel.Ppe));
                    Button("Radio · stop work", director.StopWork);
                }
            }
            Button("Return to site", director.ToggleTablet);
            Button("Finish shift", director.EndShift);
        }
        private void Label(string value, int size = 20, Color? color = null)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(content, false);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size;
            text.color = color ?? new Color(.8f, .84f, .83f); text.text = value;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredHeight = size > 20 ? 44 : 36;
        }
        private void Button(string title, UnityEngine.Events.UnityAction action)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(content, false);
            go.GetComponent<Image>().color = new Color(.19f, .26f, .27f);
            go.GetComponent<LayoutElement>().preferredHeight = 46;
            go.GetComponent<Button>().onClick.AddListener(action);
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var rect = label.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(14, 0); rect.offsetMax = new Vector2(-10, 0);
            var text = label.GetComponent<Text>(); text.font = font; text.fontSize = 21; text.color = Color.white; text.text = title; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        }
    }
}
