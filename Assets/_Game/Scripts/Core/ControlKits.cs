using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // C4 "Control": choosing Engineering is not enough, the learner must pick WHICH control meets the standard.
    // Each engineered fix offers the right kit and two plausible wrong ones (tape, cones, signs, "be careful").
    // A wrong kit is carried out, fails at the hazard (an install attempt), and has to be chosen again.
    public sealed class KitChoice
    {
        public string Correct { get; }
        public string[] Wrong { get; }
        public string Why { get; }
        public KitChoice(string correct, string why, params string[] wrong) { Correct = correct; Why = why; Wrong = wrong; }
    }

    public static class ControlKits
    {
        static readonly Dictionary<string, KitChoice> Kits = new Dictionary<string, KitChoice>
        {
            { "mon-no-gfci", new KitChoice("Plug-in GFCI at the outlet", "GFCI on 120 V temporary outlets (1926.404(b)(1)).",
                "Heavy-duty cover over the cord", "\"120 V\" warning label on the box") },
            { "mon-trailer-ladder", new KitChoice("Ladder extended 3 ft past the roof and tied off", "Rails 3 ft above the landing (1926.1053(b)(1)).",
                "Non-slip feet on the same ladder", "\"Use caution\" tag on the top rung") },
            { "tue-no-protective-system", new KitChoice("Trench box moved so the crew works inside it", "Protective system at 5 ft or deeper (1926.652(a)(1)).",
                "Plywood sheets leaned on the walls", "Spotter at the edge watching the walls") },
            { "tue-spoil-at-edge", new KitChoice("Spoil pulled back at least 2 ft from the edge", "Spoil at least 2 ft back (1926.651(j)(2)).",
                "Tarp spread over the spoil pile", "Spoil packed down with the bucket") },
            { "tue-no-egress", new KitChoice("Ladder in the trench within 25 ft of the crew", "Way out within 25 ft of travel (1926.651(c)(2)).",
                "Rope tied off at the top of the trench", "Ladder staged at the truck, ready") },
            { "tue-swing-radius", new KitChoice("Barricade around the swing radius", "Keep people out of the swing radius; barricade it.",
                "Orange vests for everyone on the walkway", "Horn blast before each swing") },
            { "tue-dry-cutting", new KitChoice("Saw with water fed to the blade", "Table 1: integrated water delivery (1926.1153(c)(1)).",
                "Box fan blowing the dust downwind", "Dust masks for the saw crew") },
            { "wed-missing-midrail", new KitChoice("Midrail installed about 21 in up", "Midrail midway between top rail and deck (1926.502(b)(2)).",
                "Caution tape strung below the top rail", "Toeboard along the deck edge") },
            { "wed-open-hole", new KitChoice("Secured cover marked HOLE, rated for twice the load", "Covers secured and marked (1926.502(i)).",
                "Orange cone set over the opening", "Loose plywood laid across it") },
            { "wed-short-ladder", new KitChoice("Ladder extended 3 ft above the deck and secured", "Rails 3 ft above the landing (1926.1053(b)(1)).",
                "Ladder reset at a steeper angle", "\"Grab here\" tape on the top rung") },
            { "thu-outrigger-no-mat", new KitChoice("Crane mats under the outrigger float", "Ground firm, drained, graded, with mats as needed (1926.1402(b)).",
                "A single 2x4 under the float", "Outrigger left half-extended") },
            { "thu-swing-radius", new KitChoice("Barricade around the counterweight swing path", "Barricade the swing radius (1926.1424(a)(2)).",
                "Painted line on the ground", "Radio call before each swing") },
            { "thu-roof-edge", new KitChoice("Guardrail system along the roof edge", "Fall protection at unprotected sides and edges (1926.501(b)(10)).",
                "Warning line moved closer to the edge", "Roofer told to face the edge") },
            { "thu-open-skylight", new KitChoice("Screen or secured cover over the skylight", "Skylights are holes: cover or guard them (1926.501(b)(4)(i)).",
                "Warning flags around the skylight", "Tape X across the dome") },
            { "fri-boom-near-line", new KitChoice("Pump repositioned so the boom stays over 10 ft from the line", "Minimum clearance to lines up to 50 kV (1926.1408).",
                "Rubber gloves for the hose man", "Spotter watching while pumping continues") },
            { "fri-rebar-impalement", new KitChoice("Impalement-rated caps or troughs on the bars", "Guard protruding rebar against impalement (1926.701(b)).",
                "Plastic mushroom caps (scratch-only)", "Bars painted orange") },
        };

        // Hazards where removing the hazard is feasible and is the best control (overrides the scene's key).
        static readonly Dictionary<string, ControlLevel> Best = new Dictionary<string, ControlLevel>
        {
            { "mon-damaged-cord", ControlLevel.Elimination },   // remove from service (1926.416(e)(1))
            { "thu-frayed-sling", ControlLevel.Elimination },   // remove from service (1926.251(c)(4)(iv))
        };

        public static bool Has(string id) => id != null && Kits.ContainsKey(id);
        public static KitChoice Get(string id) => Has(id) ? Kits[id] : null;

        public static ControlLevel BestFeasible(string id, ControlLevel authored) =>
            id != null && Best.TryGetValue(id, out var b) ? b : authored;

        // Shuffled options for one pick; correctIndex points at the right kit.
        public static IReadOnlyList<string> Options(string id, int seed, out int correctIndex)
        {
            correctIndex = -1;
            var k = Get(id);
            if (k == null) return new string[0];
            var all = new[] { k.Correct }.Concat(k.Wrong).ToList();
            var rng = new Random(seed);
            var order = Enumerable.Range(0, all.Count).OrderBy(_ => rng.Next()).ToList();
            correctIndex = order.IndexOf(0);
            return order.Select(i => all[i]).ToList();
        }
    }
}
