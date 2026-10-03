using System.Collections.Generic;
using System.Linq;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Hands-on judgements (measure, install, inspect, setup) against the cited OSHA values.
    public sealed class HandsOnTests
    {
        [Test]
        public void Catalog_PointsAtRealConditionsWithKits()
        {
            foreach (var id in HandsOnCatalog.InstallIds)
                Assert.That(ControlKits.Has(id), Is.True, id + " has an install kit");
            Assert.That(HandsOnCatalog.InstallFor("nope"), Is.EqualTo(InstallTask.None));
            Assert.That(HandsOnCatalog.InspectFor("mon-damaged-cord"), Is.EqualTo(InspectItem.Cord));
            Assert.That(EvidenceModel.Knows("inspect_close"), Is.True);
        }

        [Test]
        public void FeetInches_AndOnCondition()
        {
            Assert.That(HandsOnRules.FeetInches(1.8288f), Is.EqualTo("6 ft 0 in"));
            Assert.That(HandsOnRules.FeetInches(0.2f), Is.EqualTo("8 in"));
            var min = new[] { 0f, 0f, 0f }; var max = new[] { 2f, 2f, 2f };
            Assert.That(HandsOnRules.OnCondition(new[] { 1f, 0f, 1f }, new[] { 1f, 2f, 1f }, min, max), Is.True);
            Assert.That(HandsOnRules.OnCondition(new[] { 1f, 0f, 1f }, new[] { 20f, 0f, 1f }, min, max), Is.False, "one end across the site");
        }

        [Test]
        public void Midrail_GoesMidway()
        {
            Assert.That(HandsOnRules.Midrail(21f).Ok, Is.True);
            Assert.That(HandsOnRules.Midrail(23.5f).Ok, Is.True);
            var low = HandsOnRules.Midrail(10f);
            Assert.That(low.Ok, Is.False); StringAssert.Contains("21 in", low.Feedback);
            Assert.That(low.Score, Is.LessThan(1f));
            Assert.That(HandsOnRules.Midrail(21f).Cfr, Is.EqualTo("1926.502(b)(2)(i)"));
        }

        [Test]
        public void Ladder_ThreeFeetUp_OneOutForFourUp()
        {
            Assert.That(HandsOnRules.Ladder(36f, 4f, 1f).Ok, Is.True);
            Assert.That(HandsOnRules.Ladder(20f, 4f, 1f).Ok, Is.False, "too short");
            var steep = HandsOnRules.Ladder(40f, 4f, 0.5f); Assert.That(steep.Ok, Is.False); StringAssert.Contains("too steep", steep.Feedback);
            var flat = HandsOnRules.Ladder(40f, 4f, 2f); Assert.That(flat.Ok, Is.False); StringAssert.Contains("kick out", flat.Feedback);
            Assert.That(HandsOnRules.Ladder(20f, 4f, 1f).Score, Is.EqualTo(0.5f).Within(1e-4));
        }

        [Test]
        public void Barricade_RingsTheSwingPath()
        {
            List<(float, float)> Ring(int n, float r) => Enumerable.Range(0, n).Select(i => ((float)System.Math.Cos(i * 2 * System.Math.PI / n) * r, (float)System.Math.Sin(i * 2 * System.Math.PI / n) * r)).ToList();
            Assert.That(HandsOnRules.Barricade(Ring(5, 6f), 0, 0, 5f).Ok, Is.True);
            Assert.That(HandsOnRules.Barricade(Ring(3, 6f), 0, 0, 5f).Ok, Is.False, "too few");
            var inside = Ring(6, 6f); inside[0] = (2f, 0f);
            var r = HandsOnRules.Barricade(inside, 0, 0, 5f); Assert.That(r.Ok, Is.False); StringAssert.Contains("inside the swing path", r.Feedback);
            var half = Ring(8, 6f).Where(c => c.Item2 >= 0).ToList();
            var g = HandsOnRules.Barricade(half, 0, 0, 5f); Assert.That(g.Ok, Is.False); StringAssert.Contains("opening", g.Feedback);
        }

        [Test]
        public void Cover_LapsEveryEdge_AndIsMarked()
        {
            Assert.That(HandsOnRules.Cover(0, 0, 1.2f, 1.2f, 0.6f, 0.6f, 0.8f, 0.8f, "HOLE").Ok, Is.True);
            Assert.That(HandsOnRules.Cover(0, 0, 1.2f, 1.2f, 0.6f, 0.6f, 0.8f, 0.8f, "").Ok, Is.False, "unmarked");
            Assert.That(HandsOnRules.Cover(0, 0, 1.2f, 1.2f, 1.0f, 0.6f, 0.8f, 0.8f, "COVER").Ok, Is.False, "slid off one edge");
            Assert.That(HandsOnRules.Cover(0, 0, 1.2f, 1.2f, 0.6f, 0.6f, 0.8f, 0.8f, "").Score, Is.EqualTo(0.6f).Within(1e-4));
        }

        [Test]
        public void Inspection_FindTheDefect_OrCallItSound()
        {
            Assert.That(HandsOnRules.Inspection(true, true, 0, false, "cord").Score, Is.EqualTo(1f));
            Assert.That(HandsOnRules.Inspection(true, true, 2, false, "cord").Score, Is.EqualTo(0.5f).Within(1e-4));
            Assert.That(HandsOnRules.Inspection(true, false, 0, true, "cord").Ok, Is.False, "missed the damage");
            Assert.That(HandsOnRules.Inspection(false, false, 0, true, "sling").Ok, Is.True);
            Assert.That(HandsOnRules.Inspection(false, false, 1, true, "sling").Ok, Is.False, "marked a sound spot");
            Assert.That(HandsOnRules.Inspection(true, true, 0, false, "sling").Cfr, Is.EqualTo("1926.251(c)(4)(iv)"));
        }
    }
}
