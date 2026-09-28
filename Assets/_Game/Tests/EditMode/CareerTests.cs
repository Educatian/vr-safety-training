using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    public sealed class CareerTests
    {
        [Test]
        public void Award_AddsXpAndBadgeBonus_AndLevelsUp()
        {
            var c = new Career();
            Assert.That(c.Award(500, 2), Is.EqualTo(500 + 2 * Career.BadgeBonus));
            Assert.That(c.Points, Is.EqualTo(700));
            Assert.That(c.Level, Is.EqualTo(2));
        }

        [Test]
        public void Buy_ChecksOwnershipLevelAndPoints()
        {
            var c = new Career(lifetimeXp: 0, points: 120);
            Assert.That(c.Buy(GearId.Penetrometer), Is.EqualTo(BuyResult.NeedLevel));
            Assert.That(c.Buy(GearId.LaserMeasure), Is.EqualTo(BuyResult.NotEnoughPoints));
            Assert.That(c.Buy(GearId.FieldNotebook), Is.EqualTo(BuyResult.Bought));
            Assert.That(c.Points, Is.EqualTo(20));
            Assert.That(c.Buy(GearId.FieldNotebook), Is.EqualTo(BuyResult.AlreadyOwned));
            Assert.That(c.StartingHints, Is.EqualTo(Career.HintTokensPerShift + 1));
        }

        [Test]
        public void Laser_ExtendsPhotoRange()
        {
            Assert.That(new Career().PhotoRange, Is.EqualTo(5f));
            Assert.That(new Career(owned: new[] { GearId.LaserMeasure }).PhotoRange, Is.EqualTo(8f));
        }

        [Test]
        public void Hints_EscalateFromAreaToEnergyToTheCondition()
        {
            Assert.That(Career.HintText(1, "Open hole", EnergySource.Gravity, FocusFour.Falls, "northeast, about 20 m"), Does.Contain("northeast"));
            Assert.That(Career.HintText(2, "Open hole", EnergySource.Gravity, FocusFour.Falls, "deck"), Does.Contain("gravity"));
            Assert.That(Career.HintText(3, "Open hole", EnergySource.Gravity, FocusFour.Falls, "deck"), Does.Contain("open hole"));
        }

        [Test]
        public void Cast_EveryEpisodeBeatPointsAtARealEpisode_AndPersonaCarriesTheBeat()
        {
            foreach (var ch in Cast.All)
                Assert.That(ch.Beats.Keys.All(n => Episodes.All.Any(e => e.Number == n)), Is.True, ch.Name);
            Assert.That(Cast.Get("ray").Persona(2), Does.Contain("Marcus"));
            var speakers = Episodes.All.SelectMany(e => e.ColdOpen.Concat(e.Epilogue)).Select(l => l.Speaker).Where(s => s.Length > 0).Distinct();
            Assert.That(speakers.All(s => Cast.All.Any(c => c.Name.StartsWith(s))), Is.True, "every speaker is in the cast");
        }
    }
}
