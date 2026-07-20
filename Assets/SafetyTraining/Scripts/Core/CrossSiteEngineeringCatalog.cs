using System.Collections.Generic;

namespace SafetyTraining.Core
{
    public static class CrossSiteEngineeringCatalog
    {
        static readonly IReadOnlyDictionary<TrainingSiteId, EngineeringDecisionDefinition> Decisions =
            new Dictionary<TrainingSiteId, EngineeringDecisionDefinition>
            {
                [TrainingSiteId.Warehouse] = new(
                    "rack-load-capacity", "RACK LOAD CAPACITY CHECK",
                    "Observed pallet load = 3,200 lb per level; four occupied levels. Posted bay capacity = 11,000 lb.",
                    "What control is supported by the documented rack capacity?",
                    "Demand = 3,200 x 4 = 12,800 lb. D/C = 12,800 / 11,000 = 1.16 > 1.00.",
                    "OSHA 1910.176(b) | Store material securely; do not create collapse or falling-object hazards.",
                    new("continue", "KEEP RECEIVING", false, "Unsafe. The observed demand exceeds the posted bay capacity by 1,800 lb."),
                    new("strap", "ADD STRAPS ONLY", false, "Insufficient. Restraint does not correct an overloaded structural bay."),
                    new("unload", "ISOLATE + UNLOAD\nVERIFY RATING", true, "Correct. Isolate the aisle, reduce the load, and verify the rack and posted rating before reuse.")),
                [TrainingSiteId.FireResponse] = new(
                    "extinguisher-travel", "EXTINGUISHER TRAVEL CHECK",
                    "Farthest Class A work point follows a 92 ft walking path. A relocation point creates a 64 ft path.",
                    "Where should the accessible extinguisher be staged?",
                    "Existing travel = 92 ft > 75 ft. Relocated travel = 64 ft <= 75 ft.",
                    "OSHA 1910.157(d)(2) | Class A extinguisher travel distance must not exceed 75 ft.",
                    new("existing", "LEAVE AT 92 FT", false, "Unsafe. The measured travel path exceeds the Class A limit."),
                    new("direct", "MEASURE STRAIGHT LINE", false, "Incorrect. Use the actual unobstructed travel path, not a line through storage."),
                    new("relocate", "RELOCATE TO 64 FT\nKEEP ACCESS CLEAR", true, "Correct. The measured route is within 75 ft and preserves immediate access.")),
                [TrainingSiteId.ChemicalProcessing] = new(
                    "spill-response-capacity", "SPILL RESPONSE CAPACITY",
                    "Observed release estimate = 42 gal. Local absorbent kit capacity = 30 gal. Vapor behavior is not yet characterized.",
                    "What action is defensible before cleanup begins?",
                    "Capacity shortfall = 42 - 30 = 12 gal. Local kit covers 71% of the estimated release.",
                    "OSHA 1910.120(q), 1910.1200 | Isolate emergency releases; use hazard information and trained response.",
                    new("absorb", "ENTER + ABSORB", false, "Unsafe. The kit is undersized and the exposure path is not characterized."),
                    new("dilute", "WASH TO DRAIN", false, "Unsafe. Dilution can spread an unknown release and create environmental harm."),
                    new("isolate", "ISOLATE + NOTIFY\nSTAGE RESPONSE", true, "Correct. Control access, identify the substance, and mobilize adequate trained response capacity.")),
                [TrainingSiteId.ElectricalMaintenance] = new(
                    "absence-voltage", "ABSENCE OF VOLTAGE CHECK",
                    "Source measured 480 V before opening. After disconnect: L1-L2 = 0 V, L2-L3 = 0 V, but L3-ground = 277 V.",
                    "May the equipment be treated as de-energized?",
                    "One required test point remains at 277 V. Absence of voltage has not been established on all conductors.",
                    "OSHA 1910.147(d), 1910.333(b) | Isolate all energy, lock/tag, and verify de-energization before work.",
                    new("proceed", "PROCEED: LINE-LINE IS 0", false, "Unsafe. The phase-to-ground reading indicates another energy source or backfeed."),
                    new("ppe", "WORK ENERGIZED WITH PPE", false, "Not justified. PPE does not replace the required de-energized work condition."),
                    new("stop", "STOP + FIND BACKFEED\nRETEST ALL POINTS", true, "Correct. Identify and isolate the secondary source, then perform a complete live-dead-live verification.")),
            };

        public static EngineeringDecisionDefinition ForSite(TrainingSiteId site) => Decisions[site];
    }
}