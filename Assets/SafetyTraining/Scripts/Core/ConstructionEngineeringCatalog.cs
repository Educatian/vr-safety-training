using System;
using System.Collections.Generic;

namespace SafetyTraining.Core
{
    [Serializable]
    public sealed class EngineeringDecisionOptionDefinition
    {
        public string Id { get; }
        public string Label { get; }
        public bool IsCorrect { get; }
        public string Feedback { get; }

        public EngineeringDecisionOptionDefinition(string id, string label, bool correct, string feedback)
        {
            Id = id; Label = label; IsCorrect = correct; Feedback = feedback;
        }
    }

    [Serializable]
    public sealed class EngineeringDecisionDefinition
    {
        public string Id { get; }
        public string Title { get; }
        public string LearningMaterial { get; }
        public string Question { get; }
        public string Calculation { get; }
        public string Standard { get; }
        public IReadOnlyList<EngineeringDecisionOptionDefinition> Options { get; }

        public EngineeringDecisionDefinition(string id, string title, string material, string question,
            string calculation, string standard, params EngineeringDecisionOptionDefinition[] options)
        {
            Id = id; Title = title; LearningMaterial = material; Question = question;
            Calculation = calculation; Standard = standard; Options = options;
        }
    }

    public static class ConstructionEngineeringCatalog
    {
        public static IReadOnlyList<EngineeringDecisionDefinition> All { get; } = new[]
        {
            new EngineeringDecisionDefinition(
                "formwork-capacity", "FORMWORK / SHORING CHECK",
                "Slab: 14 ft x 10 ft x 8 in; concrete = 150 pcf. Crew/equipment = 3,500 lb. Forms = 1,500 lb. Four shore frames are rated 4,000 lb each.",
                "Can the documented shore configuration support the anticipated construction load?",
                "Concrete 14 x 10 x 0.667 x 150 = 14,000 lb. Total demand = 19,000 lb. Capacity = 16,000 lb. D/C = 1.19 > 1.00.",
                "OSHA 1926.703(a) | Support anticipated vertical/lateral loads; keep drawings and revisions onsite.",
                new("pour", "PROCEED WITH POUR", false, "Unsafe. The documented demand exceeds rated capacity by 3,000 lb."),
                new("reduce", "REMOVE CREW ONLY", false, "Insufficient. Even removing 3,500 lb leaves no verified margin and does not revise the engineered plan."),
                new("hold", "HOLD POUR\nREVISE SHORING", true, "Correct. Stop the pour, obtain a revised competent design, add verified capacity, and reinspect before placement.")),
            new EngineeringDecisionDefinition(
                "crane-radius", "CRANE LIFT PLAN CHECK",
                "Load = 6,200 lb; rigging = 800 lb. Chart capacity: 6,500 lb at 60 ft radius, 8,200 lb at 50 ft radius.",
                "Which lift decision is supported by the load chart?",
                "Gross load = 7,000 lb. At 60 ft: 7,000/6,500 = 108%. At 50 ft: 7,000/8,200 = 85%.",
                "OSHA 1926.1417(o) | Do not exceed rated capacity; verify load weight and operating configuration.",
                new("sixty", "LIFT AT 60 FT", false, "Unsafe. Gross load is 108% of chart capacity at this radius."),
                new("guess", "LIFT SLOWLY\nWATCH INDICATOR", false, "Unsafe. Monitoring does not authorize exceeding the load chart."),
                new("reconfigure", "STOP + RECONFIGURE\nVERIFIED 50 FT", true, "Correct. Reconfigure, verify radius and setup, control the lift zone, then execute within chart capacity.")),
            new EngineeringDecisionDefinition(
                "trench-system", "TRENCH PROTECTIVE SYSTEM",
                "Excavation = 6.5 ft deep in Type C soil. Spoil is 1 ft from the edge. Nearest ladder is 34 ft away. No protective system is installed.",
                "What must happen before a worker enters?",
                "Depth > 5 ft requires protection unless stable rock. Spoil needs >=2 ft setback. Lateral travel to egress must be <=25 ft for trenches >=4 ft deep.",
                "OSHA 1926.651(c)(2), (j)(2); 1926.652(a) | Protection, spoil setback, and egress before entry.",
                new("enter", "QUICK ENTRY", false, "Unsafe. Duration does not remove cave-in, surcharge, or egress requirements."),
                new("ladder", "MOVE LADDER ONLY", false, "Incomplete. Egress improves, but cave-in protection and spoil setback remain noncompliant."),
                new("protect", "STOP + PROTECTION\nSPOIL + EGRESS", true, "Correct. A competent person must verify the protective system and conditions before entry."))
        };

        /// <summary>
        /// Transfer variants: isomorph problems with new numbers and, deliberately,
        /// different correct answers, presented after the base decision is solved
        /// with the worked solution hidden. Telemetry-only (no score bonus) so the
        /// variant measures calculation transfer, not answer recall.
        /// </summary>
        public static IReadOnlyDictionary<string, EngineeringDecisionDefinition> Variants { get; } =
            new Dictionary<string, EngineeringDecisionDefinition>(StringComparer.Ordinal)
            {
                ["formwork-capacity"] = new EngineeringDecisionDefinition(
                    "formwork-capacity-transfer", "TRANSFER: NEW POUR PLAN",
                    "Slab: 12 ft x 12 ft x 9 in; concrete = 150 pcf. Crew/equipment = 2,800 lb. Forms = 1,000 lb. Five shore frames are rated 4,500 lb each.",
                    "Apply the same demand/capacity check. What does this configuration support?",
                    "No worked solution. Compute demand and capacity yourself before deciding.",
                    "OSHA 1926.703(a) | Support anticipated vertical/lateral loads.",
                    new("pour-t", "PROCEED WITH POUR\nPER VERIFIED PLAN", true, "Correct. Demand 20,000 lb vs capacity 22,500 lb: D/C = 0.89, within the engineered plan."),
                    new("hold-t", "HOLD POUR\nADD SHORES", false, "Not supported by the numbers. Demand 20,000 lb is under the 22,500 lb capacity; verify and proceed per plan."),
                    new("crew-t", "REMOVE CREW ONLY", false, "Not an engineering decision. The documented configuration already carries the full anticipated load.")),
                ["crane-radius"] = new EngineeringDecisionDefinition(
                    "crane-radius-transfer", "TRANSFER: NEW LIFT PLAN",
                    "Load = 9,000 lb; rigging = 600 lb. Chart capacity: 10,400 lb at 55 ft radius.",
                    "Apply the same chart check. Which decision does the load chart support?",
                    "No worked solution. Compute the gross load against the chart yourself.",
                    "OSHA 1926.1417(o) | Do not exceed rated capacity.",
                    new("lift-t", "LIFT AT 55 FT\nVERIFIED SETUP", true, "Correct. Gross 9,600 lb is 92% of the 10,400 lb chart capacity with verified configuration."),
                    new("slow-t", "LIFT SLOWLY\nWATCH INDICATOR", false, "Monitoring never substitutes for the chart check; here the chart already supports the verified lift."),
                    new("refuse-t", "REFUSE ALL LIFTS\nTODAY", false, "Refusing a chart-compliant lift is not an engineering decision. Verify and execute within capacity.")),
                ["trench-system"] = new EngineeringDecisionDefinition(
                    "trench-system-transfer", "TRANSFER: NEW EXCAVATION",
                    "Excavation = 4.5 ft deep in Type B soil. Spoil is 3 ft from the edge. Ladder is 20 ft from the work area. No protective system is installed.",
                    "Apply the same entry checks. What must happen before a worker enters?",
                    "No worked solution. Check depth, spoil setback, and egress limits yourself.",
                    "OSHA 1926.651, 1926.652 | Entry requirements scale with depth and conditions.",
                    new("inspect-t", "COMPETENT PERSON\nINSPECT, THEN ENTER", true, "Correct. Under 5 ft entry hinges on a competent person's inspection; spoil and egress already comply."),
                    new("full-t", "STOP + FULL\nPROTECTIVE SYSTEM", false, "Not automatically required under 5 ft absent indications of instability; the inspection decides."),
                    new("enter-t", "QUICK ENTRY", false, "No. Even shallow excavations require the competent person's inspection before entry."))
            };
    }
}
