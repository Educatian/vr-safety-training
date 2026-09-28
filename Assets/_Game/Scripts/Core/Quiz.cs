using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // Quiz items aligned to the course's Hazard Safety Engineering Assessment construct (hierarchy of
    // controls, risk assessment) and to verified OSHA thresholds. Deterministic scoring; no LLM.
    public sealed class QuizItem
    {
        public QuizItem(string id, string prompt, string[] options, int correct, string explanation, string cfr = "")
        {
            if (options == null || options.Length < 2) throw new ArgumentException("Need 2+ options.", nameof(options));
            if (correct < 0 || correct >= options.Length) throw new ArgumentOutOfRangeException(nameof(correct));
            Id = id; Prompt = prompt; Options = options; Correct = correct; Explanation = explanation; Cfr = cfr;
        }

        public string Id { get; }
        public string Prompt { get; }
        public string[] Options { get; }
        public int Correct { get; }
        public string Explanation { get; }
        public string Cfr { get; }
    }

    public sealed class QuizSession
    {
        readonly List<QuizItem> items;
        readonly Dictionary<string, int> answers = new Dictionary<string, int>();

        public QuizSession(IEnumerable<QuizItem> items) => this.items = items.ToList();

        // Shuffled option order per session (removes the authored answer-position bias); scoring follows the shuffle.
        public QuizSession(IEnumerable<QuizItem> items, int seed) : this(items.Select((q, i) => Shuffled(q, new Random(seed + i * 7919)))) { }

        static QuizItem Shuffled(QuizItem q, Random rng)
        {
            var order = Enumerable.Range(0, q.Options.Length).OrderBy(_ => rng.Next()).ToArray();
            return new QuizItem(q.Id, q.Prompt, order.Select(k => q.Options[k]).ToArray(), Array.IndexOf(order, q.Correct), q.Explanation, q.Cfr);
        }

        public IReadOnlyList<QuizItem> Items => items;
        public int Index { get; private set; }
        public QuizItem Current => Index < items.Count ? items[Index] : null;
        public bool Done => Index >= items.Count;
        public int CorrectCount => items.Count(i => answers.TryGetValue(i.Id, out var a) && a == i.Correct);

        // First answer counts (no retry farming); returns whether it was correct and advances.
        public bool Answer(int option)
        {
            var item = Current ?? throw new InvalidOperationException("Quiz is finished.");
            if (!answers.ContainsKey(item.Id)) answers[item.Id] = option;
            Index++;
            return option == item.Correct;
        }
    }

    // Drag-and-drop ordering of the hierarchy of controls (most -> least effective).
    public static class HierarchyOrdering
    {
        public static readonly string[] Correct = { "Elimination", "Substitution", "Engineering", "Administrative", "PPE" };

        // Number of cards in the right slot (0-5); 5 = fully correct.
        public static int Score(IReadOnlyList<string> placed)
        {
            var n = 0;
            for (var i = 0; i < Correct.Length && i < placed.Count; i++)
                if (placed[i] == Correct[i]) n++;
            return n;
        }
    }

    public static class QuizBank
    {
        public static IReadOnlyList<QuizItem> GateToolboxTalk() => new[]
        {
            new QuizItem("gate-ppe-first", "PPE is the first line of defense against site hazards.",
                new[] { "True", "False" }, 1,
                "False. PPE is the last line. Eliminate or engineer the hazard out first."),
            new QuizItem("gate-fall-trigger", "In construction, fall protection is required at what height?",
                new[] { "4 ft", "6 ft", "10 ft", "15 ft" }, 1,
                "6 ft to a lower level, for most construction work.", "29 CFR 1926.501(b)(1)"),
            new QuizItem("gate-engineering", "Which one is an engineering control?",
                new[] { "Toolbox talk", "Hard hat", "Guardrail around the edge", "Warning sign" }, 2,
                "A guardrail physically separates people from the edge."),
        };

        public static IReadOnlyList<QuizItem> EndOfDayTrench() => new[]
        {
            new QuizItem("tue-protective", "A trench needs a protective system starting at what depth?",
                new[] { "3 ft", "5 ft", "8 ft", "20 ft" }, 1,
                "5 ft or deeper, unless it is entirely in stable rock.", "29 CFR 1926.652(a)(1)"),
            new QuizItem("tue-spoil", "How far back from the edge must spoil stay?",
                new[] { "At least 2 ft", "At least 6 in", "Anywhere outside the trench box", "10 ft" }, 0,
                "At least 2 ft, so it cannot slide or add load at the edge.", "29 CFR 1926.651(j)(2)"),
        };

        public static IReadOnlyList<QuizItem> EndOfDayPower() => new[]
        {
            new QuizItem("mon-gfci", "Which temporary receptacles need GFCI protection?",
                new[] { "Only outdoor ones", "120 V, 15 A and 20 A receptacles not part of permanent wiring", "Only ones feeding power tools over 1 hp", "None if cords are inspected" }, 1,
                "All 120 V single-phase 15 A and 20 A receptacles that are not permanent wiring, unless an assured grounding program is used.", "29 CFR 1926.404(b)(1)(ii)"),
            new QuizItem("mon-frayed", "A cord has a split jacket with conductors showing. What do you do?",
                new[] { "Tape it and keep working", "Remove it from service", "Use it only on GFCI", "Flag it for Friday" }, 1,
                "Worn or frayed cords shall not be used. Tag it out and replace it.", "29 CFR 1926.416(e)(1)"),
        };

        public static IReadOnlyList<QuizItem> ToolboxTrench() => new[]
        {
            new QuizItem("tue-tb-ladder", "In a trench 4 ft or deeper, a ladder or other way out must be within...",
                new[] { "10 ft of lateral travel", "25 ft of lateral travel", "50 ft", "Sight of the foreman" }, 1,
                "25 ft of lateral travel for workers in the trench.", "29 CFR 1926.651(c)(2)"),
            new QuizItem("tue-tb-silica", "Cutting concrete pipe with a handheld saw. The best control is...",
                new[] { "A dust mask", "Integrated water delivery on the saw", "Cutting upwind", "Working faster" }, 1,
                "Table 1 calls for water fed to the blade. Respirators are added only where Table 1 requires them.", "29 CFR 1926.1153(c)(1)"),
            new QuizItem("tue-tb-swing", "Where should walkways run around a working excavator?",
                new[] { "Anywhere with a spotter", "Outside the swing radius, barricaded", "Behind the counterweight", "Under the boom" }, 1,
                "Keep people out of the swing radius; barricade it so nobody wanders in."),
        };

        public static IReadOnlyList<QuizItem> ToolboxFalls() => new[]
        {
            new QuizItem("wed-tb-toprail", "A guardrail top rail must sit at...",
                new[] { "36 in", "42 in, plus or minus 3 in", "48 in minimum", "Any height with a toeboard" }, 1,
                "42 in plus or minus 3 in above the walking-working level.", "29 CFR 1926.502(b)(1)"),
            new QuizItem("wed-tb-cover", "A floor-hole cover must be...",
                new[] { "Painted orange", "Secured and marked HOLE or COVER", "Plywood at least 1/4 in", "Removed at lunch" }, 1,
                "Secured against displacement and marked HOLE or COVER; able to hold twice the load.", "29 CFR 1926.502(i)"),
        };

        public static IReadOnlyList<QuizItem> EndOfDayFalls() => new[]
        {
            new QuizItem("wed-ladder", "An extension ladder to a deck must extend how far above the landing?",
                new[] { "1 ft", "3 ft", "6 ft", "It does not matter if tied off" }, 1,
                "At least 3 ft above the upper landing, or use a grab rail.", "29 CFR 1926.1053(b)(1)"),
            new QuizItem("wed-midrail", "When there is no wall above the top rail's height, what fills the gap below it?",
                new[] { "Nothing", "A midrail, screen or mesh", "Caution tape", "A warning sign" }, 1,
                "Midrails, screens or mesh between the top rail and the walking surface.", "29 CFR 1926.502(b)(2)"),
        };
    }
}
