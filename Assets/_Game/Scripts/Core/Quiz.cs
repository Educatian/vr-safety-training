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
    }
}
