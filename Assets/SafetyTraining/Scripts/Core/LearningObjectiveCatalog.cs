using System;
using System.Collections.Generic;

namespace SafetyTraining.Core
{
    [Serializable]
    public sealed class LearningObjectiveDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string Statement { get; }
        public string AssessmentEvidence { get; }
        public string Standards { get; }
        public int RequiredCriteria { get; }

        public LearningObjectiveDefinition(string id, string title, string statement,
            string assessmentEvidence, string standards, int requiredCriteria)
        {
            Id = id;
            Title = title;
            Statement = statement;
            AssessmentEvidence = assessmentEvidence;
            Standards = standards;
            RequiredCriteria = requiredCriteria;
        }
    }

    public static class LearningObjectiveCatalog
    {
        static readonly IReadOnlyDictionary<TrainingSiteId, IReadOnlyList<LearningObjectiveDefinition>> Objectives =
            new Dictionary<TrainingSiteId, IReadOnlyList<LearningObjectiveDefinition>>
            {
                [TrainingSiteId.Construction] = new[]
                {
                    O("CON-01", "Diagnose site risk", "Collect and classify field evidence for fall, struck-by, excavation, access, and temporary-works hazards.", "At least four relevant observations, with distractors distinguished.", "ABET SO1; OSHA 29 CFR 1926", 4),
                    O("CON-02", "Make engineering decisions", "Calculate demand/capacity or geometric compliance and select a defensible control for formwork, crane, and trench cases.", "Correct decision and calculation rationale at all three engineering stations.", "ABET SO1, SO2, SO4; 1926.651, .652, .703, .1417", 3),
                    O("CON-03", "Implement controls", "Complete hands-on control installation in the required sequence and verify the result.", "Successful physical placement steps recorded by the practical task.", "ABET SO6; OSHA 29 CFR 1926", 3),
                    O("CON-04", "Defend the safety plan", "Synthesize observations, calculations, and standards into a concise site-control recommendation.", "Evidence-gated hypothesis and final report submission.", "ABET SO3, SO4", 2)
                },
                [TrainingSiteId.Warehouse] = new[]
                {
                    O("WAR-01", "Diagnose material-flow risk", "Collect evidence about storage, loading, vehicle, and pedestrian conflicts.", "Three relevant observations with distractor discrimination.", "OSHA 1910.176; ABET SO1", 3),
                    O("WAR-02", "Restore safe flow", "Reconfigure the work area to separate people, equipment, and stored materials.", "All hands-on placement steps completed correctly.", "OSHA 1910.176; ABET SO2, SO6", 3),
                    O("WAR-03", "Justify the control", "Use collected evidence to defend a material-handling control plan.", "Hypothesis and final report linked to field evidence.", "ABET SO3, SO4", 2)
                },
                [TrainingSiteId.FireResponse] = new[]
                {
                    O("FIR-01", "Assess fire response conditions", "Distinguish alarm, egress, extinguisher, and scene-condition evidence.", "Three relevant observations with distractor discrimination.", "OSHA 1910.38, 1910.157; ABET SO1", 3),
                    O("FIR-02", "Execute response sequence", "Place response equipment and controls in the safe operational sequence.", "All hands-on placement steps completed correctly.", "OSHA 1910.38, 1910.157; ABET SO6", 3),
                    O("FIR-03", "Defend response choice", "Explain when to evacuate and when trained extinguisher use is defensible.", "Evidence-gated hypothesis and final report.", "ABET SO3, SO4", 2)
                },
                [TrainingSiteId.ChemicalProcessing] = new[]
                {
                    O("CHE-01", "Characterize chemical risk", "Collect label, SDS, incompatibility, exposure-path, and containment evidence.", "Three relevant observations with distractor discrimination.", "OSHA 1910.1200; ABET SO1", 3),
                    O("CHE-02", "Control the release", "Select and place compatible spill-control and exposure-response resources.", "All hands-on placement steps completed correctly.", "OSHA 1910.120, 1910.1200; ABET SO2, SO6", 3),
                    O("CHE-03", "Communicate the response", "Defend the response using hazard communication evidence.", "Evidence-gated hypothesis and final report.", "ABET SO3, SO4", 2)
                },
                [TrainingSiteId.ElectricalMaintenance] = new[]
                {
                    O("ELE-01", "Identify electrical energy risk", "Collect evidence for energized sources, damaged conductors, wet interfaces, and lockout status.", "Three relevant observations with distractor discrimination.", "OSHA 1910.147, Subpart S; ABET SO1", 3),
                    O("ELE-02", "Establish safe work condition", "Complete isolation, verification, and access-control actions in sequence.", "All hands-on placement steps completed correctly.", "OSHA 1910.147, Subpart S; ABET SO2, SO6", 3),
                    O("ELE-03", "Defend the isolation plan", "Explain the selected electrical control using field evidence.", "Evidence-gated hypothesis and final report.", "ABET SO3, SO4", 2)
                },
                [TrainingSiteId.TowerCrane] = new[]
                {
                    O("TCR-01", "Diagnose lift-zone risk", "Collect evidence for fall-zone control, power line clearance, rigging condition, and wind limits around the tower crane.", "Three relevant observations with distractor discrimination.", "OSHA 1926.1408, .1413, .1425; ABET SO1", 3),
                    O("TCR-02", "Implement lift controls", "Stage barricades, taglines, spotter post, and landing controls in the safe sequence.", "All hands-on placement steps completed correctly.", "OSHA 1926.1417, .1425; ABET SO2, SO6", 3),
                    O("TCR-03", "Defend the lift plan", "Justify the lift decision using load chart, wind, and rigging evidence.", "Evidence-gated hypothesis and final report.", "ABET SO3, SO4", 2)
                }
            };

        public static IReadOnlyList<LearningObjectiveDefinition> ForSite(TrainingSiteId site) => Objectives[site];

        public static LearningObjectiveDefinition ObjectiveAt(TrainingSiteId site, int index)
        {
            var items = ForSite(site);
            return items[Math.Max(0, Math.Min(index, items.Count - 1))];
        }

        static LearningObjectiveDefinition O(string id, string title, string statement,
            string evidence, string standards, int required) =>
            new(id, title, statement, evidence, standards, required);
    }
}
