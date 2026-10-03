using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // ECD model as data (quality review 2026-09-30, area 4): defaults are valid, every KSA is evidenced by at least two
    // sources, retuning the data changes scoring without code, and every observable the runtime logs is in the model.
    public sealed class EvidenceModelTests
    {
        [TearDown] public void Restore() => EvidenceModel.Reset();

        [Test]
        public void Defaults_AreValid()
        {
            Assert.That(new EvidenceModel().Validate(), Is.Empty);
        }

        [Test]
        public void EveryKsa_HasAtLeastTwoEvidenceSources()
        {
            var missionKsas = Episodes.All.SelectMany(e => Missions.For(e.Number)?.Steps ?? new MissionStep[0]).Select(s => s.Ksa);
            var thin = EvidenceModel.Current.UnderEvidenced(2, missionKsas).ToList();
            Assert.That(thin.Count == 0, "KSAs with fewer than 2 evidence sources: " + string.Join(", ", thin));
        }

        [Test]
        public void RetuningWeights_ChangesTheHazardScore_WithoutCode()
        {
            var spec = new HazardSpec("edge", true, EnergySource.Gravity, FocusFour.Falls, CpArea.FallProtection, 3, 4, ControlLevel.Engineering);
            var ev = new HazardEvidence { Detected = true, TagCorrect = true, RiskDeviation = 0, AppliedControl = ControlLevel.Engineering };
            var before = DaySession.HazardScore(spec, ev);
            var m = new EvidenceModel { wDetect = 0.25f, wControl = 0.35f };
            Assert.That(EvidenceModel.Use(m), Is.Empty);
            Assert.That(DaySession.HazardScore(spec, ev), Is.EqualTo(before).Within(1e-5), "same total when both weights are earned");
            ev.AppliedControl = ControlLevel.Ppe;
            var reweighted = DaySession.HazardScore(spec, ev);
            EvidenceModel.Reset();
            Assert.That(Math.Abs(reweighted - DaySession.HazardScore(spec, ev)) > 1e-4f, "control weight moved with the data");
        }

        [Test]
        public void InvalidData_IsRejected_AndDefaultsStay()
        {
            var bad = new EvidenceModel { wDetect = 0.9f };
            bad.observables = new[] { new EvidenceModel.Observable("x", "NotAKsa", "", "") };
            var problems = EvidenceModel.Use(bad);
            Assert.That(problems.Count, Is.GreaterThanOrEqualTo(2));
            Assert.That(EvidenceModel.Current.wDetect, Is.EqualTo(0.35f));
            Assert.Throws<System.Collections.Generic.KeyNotFoundException>(() => EvidenceModel.KsaOf("no-such-observable"));
        }

        [Test]
        public void CoverageMatrix_CoversEveryEpisodeAndKsa()
        {
            var rows = EvidenceModel.CoverageCsv().Skip(1).ToList();
            foreach (var ep in Episodes.All)
                foreach (Ksa k in Enum.GetValues(typeof(Ksa)))
                    Assert.That(rows.Any(r => r.StartsWith($"\"{ep.Number}\",\"{k}\"")), $"EP{ep.Number} has evidence for {k}");
        }

        // Every Ecd("key") the runtime uses is an observable in the model (source scan; skipped outside the project).
        [Test]
        public void RuntimeObservables_AreAllInTheModel()
        {
            var path = Path.Combine("Assets", "_Game", "Scripts", "Runtime", "ShiftDirector.cs");
            if (!File.Exists(path)) return;
            var keys = Regex.Matches(File.ReadAllText(path), "Ecd\\(\"([a-z_]+)\"\\)").Cast<Match>().Select(m => m.Groups[1].Value).Distinct().ToList();
            Assert.That(keys.Count, Is.GreaterThan(15));
            foreach (var k in keys) Assert.That(EvidenceModel.Knows(k), "observable '" + k + "' missing from the ECD model");
        }
    }
}
