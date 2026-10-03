using System.Collections.Generic;

namespace Jobsite.Core
{
    // What the tablet calls a condition BEFORE the learner has judged it (GDD pillar 1: nothing is highlighted).
    // The authored title ("Guardrail without midrail") is the diagnosis, so it stays hidden until the learner reports
    // the condition or confirms it compliant. A hazard and its look-alike share one neutral name.
    public static class ConditionNames
    {
        static readonly Dictionary<string, string> Neutral = new Dictionary<string, string>
        {
            { "mon-damaged-cord", "Extension cord" }, { "mon-cord-ramp", "Extension cord" },
            { "mon-no-gfci", "Temporary power outlet" }, { "mon-gfci-ok", "Temporary power outlet" },
            { "mon-trailer-ladder", "Ladder to the trailer roof" }, { "mon-empty-water", "Water station" },
            { "tue-no-protective-system", "Pipe crew in the trench" }, { "tue-box-ok", "Trench box" },
            { "tue-spoil-at-edge", "Spoil pile" }, { "tue-no-egress", "Trench access" },
            { "tue-swing-radius", "Walkway by the excavator" }, { "tue-dry-cutting", "Pipe cutting station" },
            { "tue-cp-inspection", "Inspection board" },
            { "wed-missing-midrail", "Deck-edge guardrail" }, { "wed-open-hole", "Floor opening" },
            { "wed-covered-hole", "Floor opening" }, { "wed-short-ladder", "Deck access ladder" },
            { "wed-tied-off", "Ironworker at the edge" },
            { "thu-outrigger-no-mat", "Crane outrigger" }, { "thu-outrigger-matted", "Crane outrigger" },
            { "thu-swing-radius", "Counterweight swing area" }, { "thu-under-load", "Load path" },
            { "thu-frayed-sling", "Rigging sling" }, { "thu-signal-person", "Signal person" },
            { "thu-roof-edge", "Roofer near the edge" }, { "thu-open-skylight", "Roof skylight" },
            { "thu-skylight-screened", "Roof skylight" },
            { "fri-boom-near-line", "Pump boom and power line" }, { "fri-backing-mixer", "Mixer truck backing" },
            { "fri-rebar-impalement", "Rebar at the step-down" }, { "fri-rebar-capped", "Rebar at the step-down" },
            { "fri-line-sign", "Overhead line signage" },
        };

        // Title of a hazard's compliant twin (replay variety, GDD §6): the crew did it right this run.
        static readonly Dictionary<string, string> CompliantTitle = new Dictionary<string, string>
        {
            { "mon-damaged-cord", "Extension cord in good condition" }, { "mon-no-gfci", "Tool on a GFCI-protected outlet" },
            { "mon-trailer-ladder", "Ladder extends above the landing" }, { "mon-empty-water", "Water station stocked" },
            { "tue-no-protective-system", "Pipe crew inside the trench box" }, { "tue-spoil-at-edge", "Spoil set back from the edge" },
            { "tue-no-egress", "Ladder within reach of the crew" }, { "tue-swing-radius", "Walkway outside the swing radius" },
            { "tue-dry-cutting", "Wet cutting with water at the blade" },
            { "wed-missing-midrail", "Guardrail with midrail" }, { "wed-open-hole", "Floor opening covered and marked" },
            { "wed-short-ladder", "Ladder extends above the deck" },
            { "thu-outrigger-no-mat", "Outrigger on crane mats" }, { "thu-swing-radius", "Swing radius barricaded" },
            { "thu-under-load", "Fall zone clear under the load" }, { "thu-frayed-sling", "Rigging sling in good condition" },
            { "thu-roof-edge", "Roofer inside the warning line" }, { "thu-open-skylight", "Skylight covered" },
            { "fri-boom-near-line", "Pump boom clear of the line" }, { "fri-backing-mixer", "Mixer backing with a spotter" },
            { "fri-rebar-impalement", "Rebar capped" },
        };

        public const string Fallback = "Site condition";

        public static bool Has(string id) => id != null && Neutral.ContainsKey(id);
        public static string NeutralName(string id) => id != null && Neutral.TryGetValue(id, out var n) ? n : Fallback;
        public static string Compliant(string id) =>
            id != null && CompliantTitle.TryGetValue(id, out var t) ? t : NeutralName(id) + ": compliant";
    }
}
