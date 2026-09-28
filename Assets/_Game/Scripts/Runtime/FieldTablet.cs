using System;
using System.Collections;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Diegetic field tablet (GDD §4): photo -> energy tag -> P x S card -> Fix/Assign/PPE/Stop.
    // Art: Higgsfield rugged frame + icon sheets under Resources/UI; cards stay <= 12 words.
    public sealed class FieldTablet : MonoBehaviour
    {
        [SerializeField] private ShiftDirector director;
        [SerializeField] private RectTransform content;
        [SerializeField] private RectTransform frame;
        [SerializeField] private Text radioText;
        [SerializeField] private Font font;
        [SerializeField] private Image flash;
        private EnergySource energy = EnergySource.Gravity;
        private int probability = 3, severity = 3;

        static readonly Color Ink = new Color(.84f, .87f, .86f);
        static readonly Color Accent = new Color(1f, .78f, .1f);
        static readonly Color Chip = new Color(.14f, .17f, .18f);

        public void Configure(ShiftDirector shift, RectTransform screen, Text radio, Font face, RectTransform tabletFrame = null, Image photoFlash = null)
        { director = shift; content = screen; radioText = radio; font = face; frame = tabletFrame; flash = photoFlash; }

        private void Update() { if (director != null) radioText.text = director.Notice; }

        // Camera-shutter feedback on a valid photo: white flash fading over 0.18 s.
        public void Flash()
        {
            if (flash == null || !isActiveAndEnabled) return;
            StopAllCoroutines();
            StartCoroutine(FlashRoutine());
        }

        private IEnumerator FlashRoutine()
        {
            for (var t = 0f; t < 0.18f; t += Time.unscaledDeltaTime)
            {
                flash.color = new Color(1, 1, 1, 0.85f * (1 - t / 0.18f));
                yield return null;
            }
            flash.color = Color.clear;
        }

        public void Refresh()
        {
            if (content == null || director == null) return;
            (frame != null ? frame : content).gameObject.SetActive(director.MenuOpen);
            foreach (Transform child in content) { child.gameObject.SetActive(false); Destroy(child.gameObject); }
            if (!director.MenuOpen) return;
            Label("COMPETENT PERSON · MONDAY", 22, Accent);
            if (!director.Started)
            {
                Label("Dolores: walk the site with me.", 32, Color.white);
                Label("Photograph conditions. Rate the risk. Choose a control.");
                Label("WASD walk · Shift hurry · Right mouse look");
                Label("E photographs · Tab opens this tablet");
                Label("Stopping work never lowers your rating.", 22, Accent);
                Button("Begin shift", director.Begin);
                return;
            }
            if (director.Finished)
            {
                Label("SHIFT CLOSED", 34, Color.white);
                Label($"Hazards found {director.Session.HazardIdentificationIndex:P0} · Precision {director.Session.ReportPrecision:P0}");
                Label($"Crew trust {director.Session.CrewTrust:+0;-0;0} · Incidents {director.Session.NearMisses + director.Session.Recordables}");
                foreach (var condition in director.Conditions)
                    if (condition.IsHazard)
                    {
                        var st = director.Session.GetState(condition.Id);
                        Label((st == HazardState.Latent ? "MISSED  " : "") + condition.DisplayName + " · " + st, 20,
                            st == HazardState.Latent ? new Color(1f, .45f, .35f) : Ink);
                    }
                Label("Tomorrow's toolbox talk opens with:", 22, Accent);
                Button("Protect edges. Clear access. Verify controls.", () => director.ExplainBack(0));
                Button("Keep schedule. Rely on reminders and PPE.", () => director.ExplainBack(1));
                return;
            }
            if (director.Selected == null)
            {
                Label("SITE WALK", 34, Color.white);
                Label("Center a condition. Move close. Press E.");
                Button("Return to site", director.ToggleTablet);
                Button("Finish shift", director.EndShift);
                return;
            }
            var target = director.Selected;
            var state = director.Session.GetState(target.Id);
            Label(target.DisplayName, 32, Color.white);
            if (state == HazardState.Latent || state == HazardState.Lapsed)
            {
                Label("Energy source", 20, Accent);
                EnergyGrid();
                Button("Probability  " + Dots(probability), () => { probability = probability % 5 + 1; Refresh(); });
                Button("Severity     " + Dots(severity), () => { severity = severity % 5 + 1; Refresh(); });
                Button("Submit report", () => director.Report(energy, probability, severity), null, true);
            }
            else
            {
                Label("Reported · " + state, 22, Ink);
                if (state == HazardState.Reported || state == HazardState.Stopped)
                {
                    Button("Fix · engineered control", () => director.Control(ControlLevel.Engineering), Icon("control_Engineering"));
                    Button("Assign · crew reminder", () => director.Control(ControlLevel.Administrative), Icon("control_Administrative"));
                    Button("PPE · individual protection", () => director.Control(ControlLevel.Ppe), Icon("control_Ppe"));
                    Button("Radio · stop work", director.StopWork, Icon("control_StopWork"));
                }
            }
            Button("Return to site", director.ToggleTablet);
            Button("Finish shift", director.EndShift);
        }

        static string Dots(int n) => new string('●', n) + new string('○', 5 - n);

        static Sprite Icon(string name) => Resources.Load<Sprite>("UI/" + name);

        private void EnergyGrid()
        {
            var row = new GameObject("EnergyGrid", typeof(RectTransform), typeof(GridLayoutGroup), typeof(LayoutElement));
            row.transform.SetParent(content, false);
            var grid = row.GetComponent<GridLayoutGroup>();
            grid.cellSize = new Vector2(78, 78); grid.spacing = new Vector2(8, 8);
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount; grid.constraintCount = 5;
            row.GetComponent<LayoutElement>().preferredHeight = 170;
            foreach (EnergySource e in Enum.GetValues(typeof(EnergySource)))
            {
                var value = e;
                var cell = new GameObject(e.ToString(), typeof(RectTransform), typeof(Image), typeof(Button));
                cell.transform.SetParent(row.transform, false);
                cell.GetComponent<Image>().color = e == energy ? Accent : Chip;
                cell.GetComponent<Button>().onClick.AddListener(() => { energy = value; Refresh(); });
                var icon = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                icon.transform.SetParent(cell.transform, false);
                var r = icon.GetComponent<RectTransform>(); r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one; r.offsetMin = r.offsetMax = Vector2.zero;
                r.offsetMin = new Vector2(8, 8); r.offsetMax = new Vector2(-8, -8);
                var img = icon.GetComponent<Image>(); img.sprite = Icon("energy_" + e); img.preserveAspect = true; img.raycastTarget = false;
                if (e == energy) img.color = new Color(.1f, .1f, .1f);
            }
            Label(energy.ToString(), 22, Color.white);
        }

        private void Label(string value, int size = 22, Color? color = null)
        {
            var go = new GameObject("Card", typeof(RectTransform), typeof(Text), typeof(LayoutElement));
            go.transform.SetParent(content, false);
            var text = go.GetComponent<Text>(); text.font = font; text.fontSize = size;
            text.color = color ?? Ink; text.text = value;
            text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
            go.GetComponent<LayoutElement>().preferredHeight = size + 14;
        }

        private void Button(string title, UnityEngine.Events.UnityAction action, Sprite icon = null, bool primary = false)
        {
            var go = new GameObject(title, typeof(RectTransform), typeof(Image), typeof(Button), typeof(LayoutElement));
            go.transform.SetParent(content, false);
            go.GetComponent<Image>().color = primary ? Accent : Chip;
            go.GetComponent<LayoutElement>().preferredHeight = 58;
            go.GetComponent<Button>().onClick.AddListener(action);
            var left = 16f;
            if (icon != null)
            {
                var ic = new GameObject("Icon", typeof(RectTransform), typeof(Image));
                ic.transform.SetParent(go.transform, false);
                var ir = ic.GetComponent<RectTransform>(); ir.anchorMin = new Vector2(0, .5f); ir.anchorMax = new Vector2(0, .5f);
                ir.sizeDelta = new Vector2(44, 44); ir.anchoredPosition = new Vector2(34, 0);
                var ii = ic.GetComponent<Image>(); ii.sprite = icon; ii.preserveAspect = true; ii.raycastTarget = false;
                left = 64f;
            }
            var label = new GameObject("Label", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(go.transform, false);
            var rect = label.GetComponent<RectTransform>(); rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one; rect.offsetMin = new Vector2(left, 0); rect.offsetMax = new Vector2(-10, 0);
            var text = label.GetComponent<Text>(); text.font = font; text.fontSize = 24; text.color = primary ? new Color(.08f, .08f, .08f) : Color.white;
            text.text = title; text.alignment = TextAnchor.MiddleLeft; text.raycastTarget = false;
        }
    }
}
