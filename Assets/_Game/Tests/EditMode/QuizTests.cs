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
    }
}
