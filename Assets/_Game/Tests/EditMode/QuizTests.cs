using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    public sealed class QuizTests
    {
        [Test]
        public void FirstAnswerCounts_AndSessionAdvances()
        {
            var quiz = new QuizSession(QuizBank.GateToolboxTalk());
            Assert.That(quiz.Answer(1), Is.True);   // PPE is not first
            Assert.That(quiz.Answer(0), Is.False);  // 4 ft is wrong
            Assert.That(quiz.Answer(2), Is.True);   // guardrail
            Assert.That(quiz.Done, Is.True);
            Assert.That(quiz.CorrectCount, Is.EqualTo(2));
        }

        [Test]
        public void AnsweringAfterDone_Throws()
        {
            var quiz = new QuizSession(QuizBank.EndOfDayTrench());
            quiz.Answer(1); quiz.Answer(0);
            Assert.Throws<System.InvalidOperationException>(() => quiz.Answer(0));
        }

        [Test]
        public void HierarchyOrdering_ScoresSlotsInPlace()
        {
            Assert.That(HierarchyOrdering.Score(HierarchyOrdering.Correct), Is.EqualTo(5));
            Assert.That(HierarchyOrdering.Score(new[] { "PPE", "Substitution", "Engineering", "Administrative", "Elimination" }), Is.EqualTo(3));
        }

        [Test]
        public void EveryBankItem_HasAValidKeyAndExplanation()
        {
            foreach (var item in QuizBank.GateToolboxTalk())
                Assert.That(item.Explanation, Is.Not.Empty);
            foreach (var item in QuizBank.EndOfDayTrench())
                Assert.That(item.Cfr, Does.StartWith("29 CFR"));
        }

        [Test]
        public void SeededSession_ShufflesOptions_ButScoresTheSameAnswer()
        {
            var plain = QuizBank.ToolboxTrench();
            var s = new QuizSession(plain, seed: 12345);
            var positions = s.Items.Select(q => q.Correct).ToList();
            for (var i = 0; i < plain.Count; i++)
                Assert.That(s.Items[i].Options[s.Items[i].Correct], Is.EqualTo(plain[i].Options[plain[i].Correct]));
            foreach (var q in s.Items.ToList()) Assert.That(s.Answer(q.Correct), Is.True);
            Assert.That(s.CorrectCount, Is.EqualTo(plain.Count));
            var spread = Enumerable.Range(0, 20).Select(k => new QuizSession(plain, k).Items[0].Correct).Distinct().Count();
            Assert.That(spread, Is.GreaterThan(1), "answer position varies across sessions");
        }
    }
}
