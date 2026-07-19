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
                "OSHA 1926.703(a): formwork must support reasonably anticipated vertical and lateral loads; drawings and revisions must be available onsite.",
                new("pour", "Proceed with pour", false, "Unsafe. The documented demand exceeds rated capacity by 3,000 lb."),
                new("reduce", "Remove crew only", false, "Insufficient. Even removing 3,500 lb leaves no verified margin and does not revise the engineered plan."),
                new("hold", "HOLD POUR + REVISE VERIFIED SHORING PLAN", true, "Correct. Stop the pour, obtain a revised competent design, add verified capacity, and reinspect before placement.")),
            new EngineeringDecisionDefinition(
                "crane-radius", "CRANE LIFT PLAN CHECK",
                "Load = 6,200 lb; rigging = 800 lb. Chart capacity: 6,500 lb at 60 ft radius, 8,200 lb at 50 ft radius.",
                "Which lift decision is supported by the load chart?",
                "Gross load = 7,000 lb. At 60 ft: 7,000/6,500 = 108%. At 50 ft: 7,000/8,200 = 85%.",
                "OSHA 1926.1417(o): do not exceed rated capacity; verify load weight and operating configuration.",
                new("sixty", "Lift at 60 ft", false, "Unsafe. Gross load is 108% of chart capacity at this radius."),
                new("guess", "Lift slowly and watch the indicator", false, "Unsafe. Monitoring does not authorize exceeding the load chart."),
                new("reconfigure", "STOP + RECONFIGURE TO VERIFIED 50 FT RADIUS", true, "Correct. Reconfigure, verify radius and setup, control the lift zone, then execute within chart capacity.")),
            new EngineeringDecisionDefinition(
                "trench-system", "TRENCH PROTECTIVE SYSTEM",
                "Excavation = 6.5 ft deep in Type C soil. Spoil is 1 ft from the edge. Nearest ladder is 34 ft away. No protective system is installed.",
                "What must happen before a worker enters?",
                "Depth > 5 ft requires protection unless stable rock. Spoil needs >=2 ft setback. Lateral travel to egress must be <=25 ft for trenches >=4 ft deep.",
                "OSHA 1926.651(c)(2), (j)(2) and 1926.652(a).",
                new("enter", "Enter for a quick inspection", false, "Unsafe. Duration does not remove cave-in, surcharge, or egress requirements."),
                new("ladder", "Move ladder only", false, "Incomplete. Egress improves, but cave-in protection and spoil setback remain noncompliant."),
                new("protect", "STOP + INSTALL PROTECTION + SETBACK SPOIL + ADD EGRESS", true, "Correct. A competent person must verify the protective system and conditions before entry."))
        };
    }
}
