using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // Evidence-Centered Design model as DATA (quality review 2026-09-30, area 4): every weight, threshold and the
    // observable -> KSA mapping lives here instead of at the call sites. The defaults below reproduce the shipped
    // behaviour exactly; Resources/ecd.json (loaded by the runtime with JsonUtility) overrides them, so a designer or
    // SME can retune scoring without touching code. Plain [Serializable] fields so JsonUtility can read it.
    // All numbers are starting values pending SME review and pilot data (docs/DesignUpgrade_2026-09-30.md §4).
    [Serializable]
    public sealed class EvidenceModel
    {
        public string version = "ecd-v1.2";
        public float competentThreshold = 0.7f;

        // Composite hazard score (sums to 1): detect, tag (energy), risk, control, escalation.
        public float wDetect = 0.35f, wTag = 0.15f, wRisk = 0.15f, wControl = 0.25f, wEscalation = 0.10f;
        public float cuedOrHintedFactor = 0.5f;   // a find the game pointed at earns half
        public float riskDeviationSpan = 8f;      // |P-P*| + |S-S*| at which risk credit reaches 0
        public float[] controlGapQuality = { 1f, 0.5f, 0.25f };   // chosen control 0 / 1 / 2+ levels below the best feasible
        public float controlScoreStep = 0.4f;     // ledger: credit lost per level below the best feasible control
        public float detectHintedFactor = 0.5f, detectLateFactor = 0.75f, detectEarlyWindow = 0.5f;
        public float speakAssertive = 1f, speakAggressive = 0.5f, speakPassive = 0f;

        // Observable (logged event) -> the KSA it is evidence for, its default standard and what it means.
        public Observable[] observables = Defaults();

        [Serializable]
        public sealed class Observable
        {
            public string key;       // stable id used by the runtime's Log calls
            public string ksa;       // Ksa enum name
            public string cfr;       // default standard when the event carries none ("" = the hazard's own CFR)
            public string evidence;  // what the learner did (one line, for the coverage matrix)
            public Observable() { }
            public Observable(string key, string ksa, string cfr, string evidence) { this.key = key; this.ksa = ksa; this.cfr = cfr; this.evidence = evidence; }
        }

        static Observable[] Defaults() => new[]
        {
            new Observable("hierarchy_order", nameof(Ksa.KHierarchy), "1926.20(b)", "orders the hierarchy of controls at the gate"),
            new Observable("quiz", nameof(Ksa.KStandard), "1926.21(b)(2)", "answers the toolbox quiz on the day's standards"),
            new Observable("measure", nameof(Ksa.SInspect), "", "measures with a field instrument before judging"),
            new Observable("confirm_compliant", nameof(Ksa.SDiscriminate), "", "confirms a compliant look-alike instead of reporting it"),
            new Observable("lookalike_reported", nameof(Ksa.SDiscriminate), "", "reports a compliant look-alike (false alarm)"),
            new Observable("lookalike_trust", nameof(Ksa.ACare), "", "false alarm costs crew trust"),
            new Observable("recognize", nameof(Ksa.SRecognize), "", "photographs and reports a real hazard"),
            new Observable("missed", nameof(Ksa.SRecognize), "", "hazard still latent at the end of the shift"),
            new Observable("energy", nameof(Ksa.KEnergy), "", "names the energy source"),
            new Observable("risk", nameof(Ksa.SAssess), "", "rates probability x severity"),
            new Observable("detect_time", nameof(Ksa.AProactive), "", "finds it early, before exposure grows"),
            new Observable("incident", nameof(Ksa.AProactive), "", "hazard turned into a near miss / recordable"),
            new Observable("control_choose", nameof(Ksa.SControl), "", "chooses a control level (vs. best feasible)"),
            new Observable("install_wrong_kit", nameof(Ksa.SControl), "", "brings a kit that doesn't meet the standard"),
            new Observable("install_misplaced", nameof(Ksa.SInstall), "", "sets the control down away from the exposure"),
            new Observable("install_success", nameof(Ksa.SInstall), "", "installs / removes and verifies the control"),
            new Observable("stop_work", nameof(Ksa.AIntervene), "", "radios stop-work with a named reason"),
            new Observable("weather_decision", nameof(Ksa.AIntervene), "", "changes the plan when conditions change"),
            new Observable("speakup_choice", nameof(Ksa.SCommunicate), "", "holds the stop under the foreman's pushback"),
            new Observable("toolbox_talk", nameof(Ksa.SCommunicate), "1926.21(b)(2)", "orders tomorrow's toolbox talk by risk"),
            new Observable("why_choice", nameof(Ksa.KHierarchy), "1926.21(b)(2)", "explains why the top risk leads the talk"),
            new Observable("mission", nameof(Ksa.AThorough), "", "completes the inspection routine"),
            new Observable("crew_self_report", nameof(Ksa.ACare), "", "crew trust high enough that a worker self-reports"),
            new Observable("instrument_match", nameof(Ksa.KEnergy), "", "picks an instrument that reads the hazard's energy"),
            new Observable("measured_first", nameof(Ksa.AThorough), "", "measures before making the call when an instrument can read it"),
            new Observable("crew_request_done", nameof(Ksa.ACare), "", "answers a crew member's request during the shift"),
            new Observable("crew_request_missed", nameof(Ksa.ACare), "", "a crew request still open at the whistle"),
            new Observable("inspect_close", nameof(Ksa.SInspect), "", "inspects an item up close: finds the defect, or calls a sound item sound"),
            new Observable("talk_top_risk", nameof(Ksa.SAssess), "1926.21(b)(2)", "leads tomorrow's toolbox talk with the highest-risk finding"),
        };

        // ---- runtime access ------------------------------------------------------------------------------------
        static EvidenceModel current = new EvidenceModel();
        public static EvidenceModel Current => current;

        // Install a model (from ecd.json). Invalid data is rejected with the reasons; the previous model stays.
        public static IReadOnlyList<string> Use(EvidenceModel model)
        {
            var problems = model == null ? new List<string> { "null model" } : model.Validate();
            if (problems.Count == 0) { current = model; map = null; }
            return problems;
        }
        public static void Reset() { current = new EvidenceModel(); map = null; }

        static Dictionary<string, Observable> map;
        static Dictionary<string, Observable> Map => map ??= current.observables.ToDictionary(o => o.key);

        public static bool Knows(string key) => key != null && Map.ContainsKey(key);

        // The KSA an observable is evidence for. Unknown keys are a programming error (a test enumerates them).
        public static Ksa KsaOf(string key)
        {
            if (!Map.TryGetValue(key, out var o)) throw new KeyNotFoundException("ECD: unknown observable '" + key + "'");
            return (Ksa)Enum.Parse(typeof(Ksa), o.ksa);
        }

        public static string CfrOf(string key) => Map.TryGetValue(key, out var o) ? o.cfr ?? "" : "";

        public List<string> Validate()
        {
            var p = new List<string>();
            var sum = wDetect + wTag + wRisk + wControl + wEscalation;
            if (Math.Abs(sum - 1f) > 1e-3f) p.Add($"hazard weights sum to {sum:0.###}, not 1");
            if (competentThreshold <= 0f || competentThreshold >= 1f) p.Add("competentThreshold must be in (0,1)");
            if (controlGapQuality == null || controlGapQuality.Length < 3) p.Add("controlGapQuality needs 3 values");
            if (riskDeviationSpan <= 0f) p.Add("riskDeviationSpan must be > 0");
            if (observables == null || observables.Length == 0) { p.Add("no observables"); return p; }
            foreach (var o in observables)
            {
                if (string.IsNullOrEmpty(o.key)) p.Add("observable with empty key");
                if (!Enum.TryParse<Ksa>(o.ksa, out _)) p.Add($"observable '{o.key}': unknown KSA '{o.ksa}'");
            }
            foreach (var dup in observables.GroupBy(o => o.key).Where(g => g.Count() > 1)) p.Add($"duplicate observable '{dup.Key}'");
            return p;
        }

        // KSAs with fewer than n distinct observables in the model (plus mission steps, which also evidence KSAs).
        public IEnumerable<Ksa> UnderEvidenced(int n, IEnumerable<Ksa> extra = null)
        {
            var counts = Enum.GetValues(typeof(Ksa)).Cast<Ksa>().ToDictionary(k => k, k => 0);
            foreach (var o in observables) if (Enum.TryParse<Ksa>(o.ksa, out var k)) counts[k]++;
            if (extra != null) foreach (var k in extra) counts[k]++;
            return counts.Where(kv => kv.Value < n).Select(kv => kv.Key);
        }

        // Scenario x competency coverage (area 1): one row per episode x KSA with the evidence sources that can fire
        // in that episode: every choice observable, plus the episode's mission steps with their standards.
        public static IEnumerable<string> CoverageCsv()
        {
            yield return "episode,ksa,domain,source,key,cfr,evidence";
            foreach (var ep in Episodes.All)
            {
                var mission = Missions.For(ep.Number);
                foreach (Ksa k in Enum.GetValues(typeof(Ksa)))
                {
                    foreach (var o in current.observables.Where(o => o.ksa == k.ToString()))
                        yield return Csv(ep.Number.ToString(), k.ToString(), KsaInfo.Domain(k).ToString(), "observable", o.key, o.cfr, o.evidence);
                    if (mission != null)
                        foreach (var s in mission.Steps.Where(s => s.Ksa == k))
                            yield return Csv(ep.Number.ToString(), k.ToString(), KsaInfo.Domain(k).ToString(), "mission", s.Kind, s.Cfr, s.Text);
                }
            }
        }

        static string Csv(params string[] cells) => string.Join(",", cells.Select(c => "\"" + (c ?? "").Replace("\"", "\"\"") + "\""));
    }
}
