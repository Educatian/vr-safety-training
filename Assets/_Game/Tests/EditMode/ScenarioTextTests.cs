using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Jobsite.Core;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Scenario text checks (quality review 2026-09-30, area 10): every episode's authored text belongs to that episode,
    // pre-report names never leak the diagnosis, no development text ships, and captions stay short enough to read.
    public sealed class ScenarioTextTests
    {
        static readonly string[] DayPrefix = { "", "mon-", "tue-", "wed-", "thu-", "fri-" };
        static readonly Regex DevText = new Regex(@"\b(TODO|TBD|FIXME|lorem|SME check|placeholder|xxx)\b", RegexOptions.IgnoreCase);
        static readonly Regex Diagnosis = new Regex(@"\b(damaged|frayed|missing|unprotected|unguarded|uncapped|capped|screened|without|broken|exposed|violation|non-?compliant|compliant|hazard|unsafe|safe|ok|no|matted)\b", RegexOptions.IgnoreCase);

        static IEnumerable<(int ep, string where, string text)> AuthoredText()
        {
            foreach (var e in Episodes.All)
            {
                yield return (e.Number, "title", e.Title);
                foreach (var l in e.ColdOpen) yield return (e.Number, "cold open", l.Text);
                foreach (var l in e.Epilogue) yield return (e.Number, "epilogue", l.Text);
                foreach (var q in (e.ToolboxQuiz?.Invoke() ?? new QuizItem[0]).Concat(e.ClosingQuiz?.Invoke() ?? new QuizItem[0]))
                {
                    yield return (e.Number, "quiz " + q.Id, q.Prompt);
                    foreach (var o in q.Options) yield return (e.Number, "quiz option " + q.Id, o);
                    yield return (e.Number, "quiz why " + q.Id, q.Explanation);
                }
                var m = Missions.For(e.Number);
                if (m == null) continue;
                yield return (e.Number, "mission title", m.Title);
                foreach (var s in m.Steps) { yield return (e.Number, "mission step", s.Text); yield return (e.Number, "mission why", s.Why); }
                foreach (var r in CrewRequests.For(e.Number))
                {
                    yield return (e.Number, "crew request", r.Line); yield return (e.Number, "crew thanks", r.DoneLine);
                    yield return (e.Number, "mission step", r.Step.Text); yield return (e.Number, "mission why", r.Step.Why);
                }
            }
        }

        [Test]
        public void MissionTargets_BelongToTheirOwnEpisode()
        {
            foreach (var e in Episodes.All)
            {
                var m = Missions.For(e.Number);
                if (m == null) continue;
                foreach (var s in m.Steps)
                    foreach (var t in s.Targets.Where(ConditionNames.Has))
                        Assert.That(t.StartsWith(DayPrefix[e.Number]), $"EP{e.Number} mission step \"{s.Text}\" targets {t} from another day");
            }
        }

        [Test]
        public void CrewRequests_BelongToTheirEpisode_AndAreWellFormed()
        {
            var ids = CrewRequests.All.Select(r => r.Id).ToList();
            Assert.That(ids.Count, Is.GreaterThanOrEqualTo(5), "at least one request per episode");
            Assert.That(ids.Distinct().Count() == ids.Count, "request ids are unique");
            foreach (var e in Episodes.All)
                foreach (var r in CrewRequests.For(e.Number))
                {
                    Assert.That(r.Xp > 0 && r.AtSeconds > 0 && r.AtSeconds < 600, r.Id + ": xp and timing");
                    Assert.That(r.Line.StartsWith(r.Npc + ":"), r.Id + ": the radio line is spoken by its NPC");
                    foreach (var t in r.Step.Targets.Where(ConditionNames.Has))
                        Assert.That(t.StartsWith(DayPrefix[e.Number]), $"EP{e.Number} request {r.Id} targets {t} from another day");
                    Assert.That(r.Step.Targets.Length > 0 && (r.Step.Targets.Any(ConditionNames.Has) || r.Step.Kind == "radio_query_open"), r.Id + ": completes on a real condition or a talk");
                    Assert.That(EvidenceModel.Knows("crew_request_done"));
                }
        }

        [Test]
        public void NeutralNames_NeverLeakTheDiagnosis()
        {
            foreach (var id in new[] { "mon-damaged-cord", "mon-no-gfci", "tue-no-protective-system", "tue-spoil-at-edge", "wed-missing-midrail",
                         "wed-open-hole", "thu-outrigger-no-mat", "thu-frayed-sling", "thu-open-skylight", "fri-boom-near-line", "fri-rebar-impalement" })
            {
                var name = ConditionNames.NeutralName(id);
                Assert.That(!Diagnosis.IsMatch(name), $"{id}: neutral name \"{name}\" gives the answer away");
            }
        }

        [Test]
        public void NoDevelopmentText_Ships()
        {
            foreach (var (ep, where, text) in AuthoredText())
                Assert.That(!DevText.IsMatch(text ?? ""), $"EP{ep} {where}: \"{text}\"");
        }

        [Test]
        public void CaptionsAndCards_AreShortEnoughToRead()
        {
            int Words(string s) => (s ?? "").Split(new[] { ' ' }, System.StringSplitOptions.RemoveEmptyEntries).Length;
            foreach (var (ep, where, text) in AuthoredText())
            {
                var limit = where == "cold open" || where == "epilogue" ? 40 : where.StartsWith("quiz why") || where == "mission why" ? 45 : 32;
                Assert.That(Words(text) <= limit, $"EP{ep} {where} has {Words(text)} words (limit {limit}): \"{text}\"");
            }
        }
    }
}
