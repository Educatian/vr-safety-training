using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    public sealed class EpisodeDataTests
    {
        [Test]
        public void Episodes_AreOnePerDay_AndPlayableOnesHaveAFullIntro()
        {
            Assert.That(Episodes.All.Select(e => e.DayIndex).Distinct().Count(), Is.EqualTo(Episodes.All.Count));
            foreach (var e in Episodes.All.Where(e => e.Playable))
            {
                Assert.That(e.ColdOpen.Count, Is.GreaterThanOrEqualTo(3), e.Title);
                Assert.That(e.Shots.Count, Is.GreaterThan(0), e.Title);
                Assert.That(e.Epilogue.Count, Is.GreaterThan(0), e.Title);
                Assert.That(e.ToolboxQuiz().Count, Is.GreaterThan(0));
                Assert.That(e.ClosingQuiz().All(q => q.Correct >= 0 && q.Correct < q.Options.Length), Is.True);
            }
        }
    }
}
