using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // Knowledge / Skills / Attitudes of a construction competent person (29 CFR 1926.32(f): "capable of identifying
    // existing and predictable hazards ... and who has authorization to take prompt corrective measures").
    // Every scored action becomes one evidence row: (KSA, OSHA standard, score 0..1). The ledger feeds the learner's
    // feedback, the end-of-shift profile and the instructor telemetry.
    public enum Ksa
    {
        KStandard,      // K: knows the OSHA rule and its trigger/threshold
        KEnergy,        // K: names the energy source behind the hazard
        KHierarchy,     // K: ranks controls (eliminate > engineer > administrate > PPE)
        SRecognize,     // S: finds the hazard on a live site
        SAssess,        // S: rates probability x severity close to the site assessment
        SControl,       // S: picks the most effective feasible control
        SInstall,       // S: puts the control in the right place and verifies it
        SInspect,       // S: measures / tests with field instruments before deciding
        SDiscriminate,  // S: tells a compliant look-alike from a real hazard
        SCommunicate,   // S: briefs and coordinates the crew
        AProactive,     // A: safety-first mindset: acts early, before exposure turns into an incident
        AIntervene,     // A: willingness to stop the job under schedule / weather pressure
        AThorough,      // A: conscientiousness: completes the inspection routine instead of guessing
        ACare,          // A: care for the crew: engages them, keeps their trust (no crying wolf)
    }

    public static class KsaInfo
    {
        public static char Domain(Ksa k) => k.ToString()[0];

        public static string Name(Ksa k) => k switch
        {
            Ksa.KStandard => "OSHA standards",
            Ksa.KEnergy => "Energy sources",
            Ksa.KHierarchy => "Hierarchy of controls",
            Ksa.SRecognize => "Hazard recognition",
            Ksa.SAssess => "Risk assessment",
            Ksa.SControl => "Control selection",
            Ksa.SInstall => "Control installation",
            Ksa.SInspect => "Field inspection & measurement",
            Ksa.SDiscriminate => "Real hazard vs. look-alike",
            Ksa.SCommunicate => "Crew communication",
            Ksa.AProactive => "Proactive safety mindset",
            Ksa.AIntervene => "Willingness to intervene",
            Ksa.AThorough => "Thoroughness",
            _ => "Care for the crew",
        };

        // What the competency looks like on a real site (shown in the end-of-shift profile).
        public static string OnTheJob(Ksa k) => k switch
        {
            Ksa.KStandard => "You can quote the trigger: 6 ft for falls, 5 ft for trenches, 10 ft from lines.",
            Ksa.KEnergy => "You name what can hurt: gravity, motion, electrical, pressure, chemical.",
            Ksa.KHierarchy => "You reach for elimination and engineering before rules and PPE.",
            Ksa.SRecognize => "You walk the site and see what the crew stopped noticing.",
            Ksa.SAssess => "You judge how likely and how bad, not just whether.",
            Ksa.SControl => "You fix the condition, not the worker.",
            Ksa.SInstall => "You put the guard where the exposure is and check it holds.",
            Ksa.SInspect => "You measure and test before you sign: depth, soil, GFCI, clearance.",
            Ksa.SDiscriminate => "You confirm compliant work instead of reporting it.",
            Ksa.SCommunicate => "You brief the foreman and the crew so the fix sticks.",
            Ksa.AProactive => "You look for trouble before it finds the crew, all shift long.",
            Ksa.AIntervene => "You stop the job when it's right, even with the schedule leaning on you.",
            Ksa.AThorough => "You finish the checklist every time, even on a routine day.",
            _ => "You talk with the crew, not at them, and you don't cry wolf.",
        };
    }

    public readonly struct KsaEvidence
    {
        public readonly Ksa Ksa; public readonly string Cfr; public readonly float Score; public readonly string Source;
        public KsaEvidence(Ksa ksa, string cfr, float score, string source)
        { Ksa = ksa; Cfr = cfr ?? ""; Score = Math.Max(0f, Math.Min(1f, score)); Source = source ?? ""; }
    }

    public sealed class KsaLedger
    {
        private readonly List<KsaEvidence> rows = new List<KsaEvidence>();
        public IReadOnlyList<KsaEvidence> Rows => rows;
        public event Action<KsaEvidence> Recorded;

        public KsaEvidence Record(Ksa ksa, string cfr, float score, string source)
        {
            var e = new KsaEvidence(ksa, cfr, score, source);
            rows.Add(e); Recorded?.Invoke(e);
            return e;
        }

        public float? Mean(Ksa k)
        {
            var s = rows.Where(r => r.Ksa == k).ToList();
            return s.Count == 0 ? (float?)null : s.Average(r => r.Score);
        }

        public int Count(Ksa k) => rows.Count(r => r.Ksa == k);

        // Standards the learner handled worst this shift (for "review these" feedback).
        public IEnumerable<(string cfr, float mean, int n)> ByStandard() =>
            rows.Where(r => r.Cfr.Length > 0).GroupBy(r => r.Cfr)
                .Select(g => (g.Key, g.Average(r => r.Score), g.Count())).OrderBy(t => t.Item2);

        // Scoring rules (starting values; tune from the pilot data).
        public static float ControlScore(ControlLevel chosen, ControlLevel best) =>
            chosen <= best ? 1f : Math.Max(0f, 1f - 0.4f * ((int)chosen - (int)best));

        public static float RiskScore(int deviation) => 1f - Math.Min(8, Math.Max(0, deviation)) / 8f;

        // Found in the first half of the shift = full vigilance; later finds still count, a bit less.
        public static float DetectScore(float seconds, float shiftLength, bool hinted) =>
            (hinted ? 0.5f : 1f) * (seconds <= shiftLength * 0.5f ? 1f : 0.75f);
    }

    // ---------- field-practice missions: the competent person's real inspection routines ----------
    // Each step completes on a matching game event (the same stream that feeds telemetry), so a mission is
    // practised by doing the work on site, never by clicking a checkbox.
    public sealed class MissionStep
    {
        public string Text { get; }
        public string Kind { get; }          // event kind that completes it ("measure", "photo", "report", "stop_work", ...)
        public string[] Targets { get; }     // condition ids (or detail prefixes) that satisfy it; empty = any
        public string Detail { get; }        // optional required substring in the event detail (e.g. instrument)
        public string Cfr { get; }
        public Ksa Ksa { get; }
        public string Why { get; }           // one line: why a competent person does this
        public MissionStep(string text, string kind, string[] targets, string detail, string cfr, Ksa ksa, string why)
        { Text = text; Kind = kind; Targets = targets ?? new string[0]; Detail = detail; Cfr = cfr; Ksa = ksa; Why = why; }

        public bool Matches(string kind, string id, string detail) =>
            kind == Kind && (Targets.Length == 0 || Targets.Contains(id)) &&
            (string.IsNullOrEmpty(Detail) || (detail ?? "").Contains(Detail));
    }

    public sealed class Mission
    {
        public string Title { get; }
        public string Brief { get; }
        public string Form { get; }                       // the real document it mirrors
        public IReadOnlyList<MissionStep> Steps { get; }
        public IReadOnlyList<GearId> Issued { get; }      // instruments loaned from the gang box for this mission
        public Mission(string title, string brief, string form, GearId[] issued, params MissionStep[] steps)
        { Title = title; Brief = brief; Form = form; Issued = issued; Steps = steps; }
    }

    public sealed class MissionRun
    {
        private readonly bool[] done;
        public Mission Mission { get; }
        public MissionRun(Mission m) { Mission = m; done = new bool[m.Steps.Count]; }
        public bool IsDone(int i) => done[i];
        public int Completed => done.Count(d => d);
        public bool Complete => Completed == done.Length;
        // Next open step (steps may be done in any order; the cue follows the first open one).
        public MissionStep Next => Mission.Steps.Where((s, i) => !done[i]).FirstOrDefault();

        // Returns the step this event completed, or null.
        public MissionStep OnEvent(string kind, string id, string detail)
        {
            for (var i = 0; i < done.Length; i++)
                if (!done[i] && Mission.Steps[i].Matches(kind, id, detail)) { done[i] = true; return Mission.Steps[i]; }
            return null;
        }
    }

    public static class Missions
    {
        static MissionStep S(string text, string kind, string[] ids, string detail, string cfr, Ksa k, string why) =>
            new MissionStep(text, kind, ids, detail, cfr, k, why);

        public static Mission For(int episode) => episode switch
        {
            1 => new Mission("Pre-shift walk: temporary power",
                "Before the crew plugs in, walk the temporary power and access. Test, don't assume.",
                "Daily site inspection log (1926.20(b)(2))", new[] { GearId.GfciTester, GearId.LaserMeasure },
                S("Sign in and don full PPE at the gate", "shift_begin", null, null, "1926.95", Ksa.KStandard,
                    "PPE is the last line, but it is the entry ticket to the site."),
                S("Test the spider box outlets with the GFCI tester", "measure", new[] { "mon-no-gfci", "mon-gfci-ok" }, "GfciTester", "1926.404(b)(1)", Ksa.SInspect,
                    "A GFCI that doesn't trip protects no one. Press the test button."),
                S("Inspect the extension cords for damage", "photo", new[] { "mon-damaged-cord", "mon-cord-ramp" }, null, "1926.405(a)(2)(ii)(I)", Ksa.SRecognize,
                    "Cut jackets and missing ground pins are the classic temporary-power killer."),
                S("Check ladder access to the office trailer", "photo", new[] { "mon-trailer-ladder" }, null, "1926.1053(b)(1)", Ksa.SInspect,
                    "Rails must reach 3 ft above the landing."),
                S("Brief the foreman (talk to Ray)", "radio_query_open", new[] { "Ray" }, null, "1926.21(b)(2)", Ksa.SCommunicate,
                    "The CP instructs each employee to recognize and avoid unsafe conditions.")),

            2 => new Mission("Daily trench inspection before entry",
                "No one enters the cut until the competent person inspects it: soil, depth, protection, access, spoil.",
                "Excavation daily inspection checklist (1926.651(k)(1))", new[] { GearId.Penetrometer, GearId.LaserMeasure, GearId.DustMonitor },
                S("Classify the soil with the pocket penetrometer", "measure", new[] { "tue-no-protective-system", "tue-box-ok" }, "Penetrometer", "1926 Subpart P App. A", Ksa.SInspect,
                    "Soil type (A/B/C) sets the slope or shoring you need."),
                S("Measure the trench depth with the laser", "measure", new[] { "tue-no-protective-system", "tue-box-ok" }, "LaserMeasure", "1926.652(a)(1)", Ksa.SInspect,
                    "5 ft or deeper needs a protective system."),
                S("Check the spoil pile setback", "measure", new[] { "tue-spoil-at-edge" }, "LaserMeasure", "1926.651(j)(2)", Ksa.SInspect,
                    "Spoil at least 2 ft back from the edge."),
                S("Verify a way out within 25 ft", "photo", new[] { "tue-no-egress" }, null, "1926.651(c)(2)", Ksa.SRecognize,
                    "Ladder or ramp within 25 ft of lateral travel in trenches 4 ft deep or more."),
                S("Sample dust at the cut-off saw", "measure", new[] { "tue-dry-cutting" }, "DustMonitor", "1926.1153", Ksa.SInspect,
                    "Dry cutting concrete pipe throws respirable silica.")),

            3 => new Mission("Fall-protection walk of the deck",
                "Walk every edge and opening on the deck. Measure rails, check covers, check the ladder.",
                "Fall protection inspection (Subpart M)", new[] { GearId.LaserMeasure },
                S("Measure the guardrail at the deck edge", "measure", new[] { "wed-missing-midrail" }, "LaserMeasure", "1926.502(b)", Ksa.SInspect,
                    "Top rail 42 in ± 3, midrail about halfway."),
                S("Check the floor opening", "photo", new[] { "wed-open-hole", "wed-covered-hole" }, null, "1926.502(i)", Ksa.SRecognize,
                    "Covers: secured, marked HOLE/COVER, twice the load."),
                S("Check the access ladder", "measure", new[] { "wed-short-ladder" }, "LaserMeasure", "1926.1053(b)(1)", Ksa.SInspect,
                    "Side rails 3 ft above the landing."),
                S("Verify the ironworker's tie-off", "photo", new[] { "wed-tied-off" }, null, "1926.501(b)(1)", Ksa.SDiscriminate,
                    "Compliant work is worth confirming, not reporting.")),

            4 => new Mission("Pre-lift crane check",
                "Before the pick: ground, rigging, swing radius, signal person, and the roof crew below the path.",
                "Shift crane inspection + lift plan (1926.1412(d), 1926.1402)", new[] { GearId.Penetrometer, GearId.LaserMeasure },
                S("Test ground under the outrigger floats", "measure", new[] { "thu-outrigger-no-mat", "thu-outrigger-matted" }, "Penetrometer", "1926.1402(b)", Ksa.SInspect,
                    "Ground must be firm, drained and graded, with mats if needed."),
                S("Inspect the rigging slings", "photo", new[] { "thu-frayed-sling" }, null, "1926.251(c)(4)(iv)", Ksa.SRecognize,
                    "Broken wires, kinks or crushing: remove it from service."),
                S("Check the swing radius barricade", "measure", new[] { "thu-swing-radius" }, "LaserMeasure", "1926.1424(a)(2)", Ksa.SInspect,
                    "Barricade the counterweight's swing path."),
                S("Confirm the signal person", "photo", new[] { "thu-signal-person" }, null, "1926.1428", Ksa.SDiscriminate,
                    "Qualified signal person with standard hand signals."),
                S("Take the stair tower to the roof", "access", null, null, "1926.501(b)(10)", Ksa.SInspect,
                    "Roof edge and skylights: see the exposure yourself.")),

            _ => new Mission("Power-line and pour setup",
                "Capstone: the pump boom, the line, the mixer backing up, the rebar, the weather. Make the calls.",
                "Pre-pour checklist + power-line encroachment plan (1926.1408)", new[] { GearId.LaserMeasure },
                S("Measure the boom's clearance to the line", "measure", new[] { "fri-boom-near-line" }, "LaserMeasure", "1926.1408(a)(2)", Ksa.SInspect,
                    "Under 350 kV: stay 10 ft away, or 20 ft if you can't confirm voltage."),
                S("Stop the pump until clearance is fixed", "stop_work", new[] { "fri-boom-near-line" }, "Justified", "1926.1408", Ksa.AIntervene,
                    "Encroachment is a stop-the-job call, not a reminder."),
                S("Check the backing mixer's spotter", "photo", new[] { "fri-backing-mixer" }, null, "1926.601(b)(4)", Ksa.SRecognize,
                    "Obstructed rear view: backup alarm or an observer."),
                S("Check the rebar caps at the step-down", "photo", new[] { "fri-rebar-impalement", "fri-rebar-capped" }, null, "1926.701(b)", Ksa.SRecognize,
                    "Protruding rebar must be guarded against impalement."),
                S("Make the weather call on the storm", "weather_decision", null, null, "1926.21(b)(2)", Ksa.AIntervene,
                    "Thunder heard: stop, shelter, wait 30 minutes after the last strike.")),
        };
    }
}
