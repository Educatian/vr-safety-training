using System;
using System.Collections.Generic;

namespace SafetyTraining.Core
{
    public enum SafetyAuthority
    {
        Worker,
        CompetentPerson,
        QualifiedPerson,
        EmployerProgram
    }

    public sealed class OshaScenarioDefinition
    {
        public OshaScenarioDefinition(string citation, SafetyAuthority authority, string learnerAction)
        {
            Citation = citation;
            Authority = authority;
            LearnerAction = learnerAction;
        }

        public string Citation { get; }
        public SafetyAuthority Authority { get; }
        public string LearnerAction { get; }
        public string HudReference => $"OSHA {Citation}";
    }

    public static class OshaScenarioCatalog
    {
        static readonly Dictionary<string, OshaScenarioDefinition> definitions = new(StringComparer.Ordinal)
        {
            { Key(TrainingSiteId.Construction, "fall-edge"), Define("1926.501; 1926.502", SafetyAuthority.Worker, "Stop access, install the planned control, and report missing protection.") },
            { Key(TrainingSiteId.Construction, "blocked-access"), Define("1926.25", SafetyAuthority.Worker, "Restore the access route by moving materials to approved staging.") },
            { Key(TrainingSiteId.Construction, "guarded-edge"), Define("1926.502", SafetyAuthority.Worker, "Keep the existing guardrail intact and report damage.") },
            { Key(TrainingSiteId.Construction, "stored-materials"), Define("1926.250", SafetyAuthority.Worker, "Keep stored material stable and outside the access route.") },

            { Key(TrainingSiteId.Warehouse, "floor-spill"), Define("1926.25; 1926.59", SafetyAuthority.Worker, "Stop traffic, isolate the area, and follow the site spill procedure.") },
            { Key(TrainingSiteId.Warehouse, "vehicle-route"), Define("1926.250", SafetyAuthority.Worker, "Stop movement and return the load to an approved staging position.") },
            { Key(TrainingSiteId.Warehouse, "marked-maintenance"), Define("1926.200", SafetyAuthority.Worker, "Keep the marked work boundary intact until released by the responsible team.") },
            { Key(TrainingSiteId.Warehouse, "racked-cargo"), Define("1926.250", SafetyAuthority.Worker, "Maintain stable storage and clear travel paths.") },

            { Key(TrainingSiteId.FireResponse, "blocked-extinguisher"), Define("1926.150", SafetyAuthority.Worker, "Clear the access path and report the obstruction.") },
            { Key(TrainingSiteId.FireResponse, "blocked-exit"), Define("1926.34; 1926.150", SafetyAuthority.Worker, "Remove the obstruction and preserve a clear egress route.") },
            { Key(TrainingSiteId.FireResponse, "clear-extinguisher"), Define("1926.150", SafetyAuthority.Worker, "Preserve access and report an inspection issue.") },
            { Key(TrainingSiteId.FireResponse, "clear-egress"), Define("1926.34", SafetyAuthority.Worker, "Keep the egress route clear of storage and equipment.") },

            { Key(TrainingSiteId.ChemicalProcessing, "chemical-leak"), Define("1926.59", SafetyAuthority.Worker, "Stop transfer, isolate the area, and follow the written spill procedure.") },
            { Key(TrainingSiteId.ChemicalProcessing, "unlabeled-drum"), Define("1926.59", SafetyAuthority.Worker, "Do not handle the container; quarantine it and report the missing label.") },
            { Key(TrainingSiteId.ChemicalProcessing, "labeled-drum"), Define("1926.59", SafetyAuthority.Worker, "Keep the label legible and verify compatible storage.") },
            { Key(TrainingSiteId.ChemicalProcessing, "clear-eyewash"), Define("1926.50", SafetyAuthority.EmployerProgram, "Keep the approach clear and report an unavailable emergency facility.") },

            { Key(TrainingSiteId.ElectricalMaintenance, "open-panel"), Define("1926.416; 1926.417", SafetyAuthority.QualifiedPerson, "Stop work and escalate isolation, lockout, and verification to authorized personnel.") },
            { Key(TrainingSiteId.ElectricalMaintenance, "loose-cable"), Define("1926.416", SafetyAuthority.Worker, "Keep people clear until the cable is protected or rerouted.") },
            { Key(TrainingSiteId.ElectricalMaintenance, "locked-panel"), Define("1926.417", SafetyAuthority.QualifiedPerson, "Do not disturb isolation; confirm the tag is legible and escalate damage.") },
            { Key(TrainingSiteId.ElectricalMaintenance, "protected-cable"), Define("1926.416", SafetyAuthority.Worker, "Maintain the protector and report a displaced or damaged ramp.") }
        };

        public static bool TryGet(TrainingSiteId siteId, string targetId, out OshaScenarioDefinition definition)
        {
            return definitions.TryGetValue(Key(siteId, targetId), out definition);
        }

        public static OshaScenarioDefinition GetRequired(TrainingSiteId siteId, string targetId)
        {
            if (TryGet(siteId, targetId, out var definition))
                return definition;

            throw new InvalidOperationException($"No OSHA scenario definition exists for {siteId}/{targetId}.");
        }

        public static string GetSiteCoachBrief(TrainingSiteId siteId)
        {
            var citations = new HashSet<string>(StringComparer.Ordinal);
            foreach (var pair in definitions)
                if (pair.Key.StartsWith($"{siteId}/", StringComparison.Ordinal))
                    citations.Add(pair.Value.HudReference);

            return $"This is OSHA-aligned construction training, not a qualification or compliance certificate. " +
                   $"Use the site procedure and escalate work requiring a competent or qualified person. Sources: {string.Join(", ", citations)}.";
        }

        static OshaScenarioDefinition Define(string citation, SafetyAuthority authority, string learnerAction)
        {
            return new OshaScenarioDefinition(citation, authority, learnerAction);
        }

        static string Key(TrainingSiteId siteId, string targetId) => $"{siteId}/{targetId}";
    }
}
