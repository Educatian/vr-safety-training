using System;
using System.IO;
using System.Linq;
using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    public sealed class ShiftDirector : MonoBehaviour
    {
        [SerializeField] private SiteCondition[] conditions;
        [SerializeField] private SitePlayer player;
        [SerializeField] private FieldTablet tablet;
        [SerializeField] private AudioSource radio;
        private SiteCondition selected;
        private string pendingInstall;
        private string logPath;
        private bool carrying;
        private string notice = "Meet Dolores. Begin your first site walk.";
        public DaySession Session { get; private set; }
        public bool MenuOpen { get; private set; } = true;
        public bool Finished { get; private set; }
        public bool Started { get; private set; }
        public string Notice => notice;
        public SiteCondition Selected => selected;
        public bool Carrying => carrying;
        public SiteCondition[] Conditions => conditions;

        public void Configure(SiteCondition[] targets, SitePlayer explorer, FieldTablet ui, AudioSource speaker)
        { conditions = targets; player = explorer; tablet = ui; radio = speaker; }
        private void Start()
        {
            Session = new DaySession(conditions.Select(c => c.Spec));
            logPath = Path.Combine(Application.persistentDataPath, "jobsite-" + Guid.NewGuid().ToString("N") + ".jsonl");
            Log("session_start", "monday-desktop-slice", "config-v1");
            tablet.Refresh();
        }
        private void Update()
        {
            if (Session == null || !Started || Finished) return;
            Session.Paused = MenuOpen;
            foreach (var ev in Session.Advance(Time.deltaTime))
            {
                Say(ev.Kind == DayEventKind.Lapsed ? "Temporary control lapsed. Revisit your report." : "Near-miss reported. Secure the area; review follows.");
                Log(ev.Kind.ToString(), ev.HazardId, "");
            }
            if (Session.Clock >= 600) EndShift();
        }
        public void Begin()
        { Started = true; MenuOpen = false; Say("Photograph conditions. E inspects; right mouse looks."); tablet.Refresh(); Log("shift_begin", "monday", ""); }
        public void ToggleTablet()
        { if (!Started || Finished) return; MenuOpen = !MenuOpen; tablet.Refresh(); }
        public void Interact()
        {
            if (!Started || MenuOpen || Finished) return;
            var ray = player.View.ViewportPointToRay(new Vector3(.5f, .5f));
            if (!Physics.Raycast(ray, out var hit, 5f, ~0, QueryTriggerInteraction.Collide)) { Say("Move closer. Center the condition in your view."); return; }
            if (hit.collider.GetComponentInParent<ControlSupply>() != null)
            {
                if (pendingInstall == null) { Say("Choose an engineered fix before collecting materials."); return; }
                carrying = true; Say("Materials collected. Return to the reported condition."); Log("kit_collect", pendingInstall, ""); return;
            }
            var target = hit.collider.GetComponentInParent<SiteCondition>();
            if (target == null) { Say("Inspect a condition, or collect assigned materials."); return; }
            if (target.Id == pendingInstall && carrying)
            {
                if (Session.CompleteInstall(target.Id, true))
                { target.ShowControl(); carrying = false; pendingInstall = null; Say("Control installed. Crew can continue safely."); Log("install_success", target.Id, "Engineering"); }
                return;
            }
            if (!PhotoValid(target)) { Say("Move closer. Keep the condition inside your frame."); return; }
            selected = target; MenuOpen = true; tablet.Refresh(); Log("photo", target.Id, "valid-frame"); Ping();
        }
        public bool PhotoValid(SiteCondition target)
        {
            var bounds = target.PhotoBounds;
            var min = Vector2.one; var max = Vector2.zero;
            for (var i = 0; i < 8; i++)
            {
                var corner = bounds.center + Vector3.Scale(bounds.extents, new Vector3((i & 1) == 0 ? -1 : 1, (i & 2) == 0 ? -1 : 1, (i & 4) == 0 ? -1 : 1));
                var p = player.View.WorldToViewportPoint(corner);
                if (p.z <= 0) return false;
                min = Vector2.Min(min, p); max = Vector2.Max(max, p);
            }
            return min.x >= 0 && min.y >= 0 && max.x <= 1 && max.y <= 1 && (max.x - min.x) * (max.y - min.y) >= .15f;
        }
        public void Report(EnergySource energy, int probability, int severity)
        {
            if (selected == null || Finished) return;
            var outcome = Session.Report(selected.Id, energy, probability, severity);
            Say(outcome == ReportOutcome.FalseReport ? selected.Explanation : "Report recorded. Choose your control.");
            Log("report", selected.Id, outcome + ":" + energy + ":" + probability + ":" + severity); tablet.Refresh();
        }
        public void Control(ControlLevel level)
        {
            if (selected == null || Finished) return;
            if (pendingInstall != null && pendingInstall != selected.Id) { Say("Finish your current installation first."); return; }
            var result = Session.ChooseControl(selected.Id, level);
            Log("control_choose", selected.Id, result + ":" + level);
            if (result == ControlOutcome.Installing)
            { pendingInstall = selected.Id; carrying = false; MenuOpen = false; Say("Collect materials at the supply rack. E to take."); }
            else if (result == ControlOutcome.Assigned) Say("Temporary control assigned. Check it again later.");
            else Say("Report this condition before choosing a control.");
            tablet.Refresh();
        }
        public void StopWork()
        {
            if (selected == null || Finished) return;
            var result = Session.StopWork(selected.Id);
            Say(result == StopOutcome.Justified ? "Work stopped. Your safety rating is protected." : "Report an active hazard before stopping this crew.");
            Log("stop_work", selected.Id, result.ToString()); tablet.Refresh();
        }
        public void EndShift()
        {
            if (!Started || Finished) return;
            Finished = true; MenuOpen = true; Session.Paused = true;
            Say("Shift closed. Review what your crew needed.");
            Log("shift_end", "monday", "HII=" + Session.HazardIdentificationIndex.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)); tablet.Refresh();
        }
        public void ExplainBack(int choice)
        { if (Finished) { Log("toolbox_talk", "monday", choice.ToString()); Say(choice == 0 ? "Protect the edge; remove obstructions; then resume work." : "Tomorrow: controls first. Speed and PPE alone are insufficient."); tablet.Refresh(); } }
        private void Say(string text) { notice = text; Ping(); }
        private void Ping() { if (radio != null && radio.clip != null) radio.Play(); }
        [Serializable] private sealed class EventRow { public string timestamp; public string kind; public string condition; public string detail; public float shiftSeconds; }
        private void Log(string kind, string id, string detail)
        {
            if (logPath == null) return;
            try { File.AppendAllText(logPath, JsonUtility.ToJson(new EventRow { timestamp = DateTime.UtcNow.ToString("O"), kind = kind, condition = id, detail = detail, shiftSeconds = Session?.Clock ?? 0 }) + "\n"); }
            catch (IOException e) { Debug.LogWarning("Jobsite log unavailable: " + e.GetType().Name); }
        }
    }
}
