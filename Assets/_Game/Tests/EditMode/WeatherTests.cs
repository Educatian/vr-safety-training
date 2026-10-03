using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    public sealed class WeatherTests
    {
        [Test]
        public void HeatIndex_MatchesNwsChart()
        {
            Assert.That(HeatIndex.Compute(90, 70), Is.EqualTo(106).Within(1.5f));   // NWS chart: 106 °F
            Assert.That(HeatIndex.Compute(80, 40), Is.EqualTo(80).Within(1.5f));
            Assert.That(HeatIndex.Risk(108), Is.EqualTo("High"));
            Assert.That(HeatIndex.Risk(85), Is.EqualTo("Lower"));
        }

        [Test]
        public void PlayableEpisodes_HaveOneWeatherCall_WithExactlyOneBestAnswer()
        {
            foreach (var ep in Episodes.All.Where(e => e.Playable))
            {
                var plan = WeatherPlan.For(ep.Number);
                var calls = plan.Events.Where(e => e.IsDecision).ToList();
                Assert.That(calls.Count, Is.EqualTo(1), ep.Title);
                Assert.That(calls[0].Options.Count(o => o.Quality == 2), Is.EqualTo(1), ep.Title);
                Assert.That(plan.Events.Select(e => e.AtSeconds), Is.Ordered, ep.Title);
            }
            Assert.That(WeatherPlan.For(1).Events[0].State.HeatIndexF, Is.GreaterThan(100f), "EP1 heat call fires above 100 °F");
            Assert.That(WeatherPlan.For(3).Events[0].State.GustMph, Is.GreaterThan(28f), "EP3 gusts exceed a typical lift rating");
        }
    }
}
