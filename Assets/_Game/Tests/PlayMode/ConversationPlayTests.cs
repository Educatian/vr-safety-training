using System.Collections;
using System.Linq;
using Jobsite.Core;
using Jobsite.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jobsite.PlayTests
{
    // Conversation as evidence in the scene: interview leads, coaching that sticks, own-words briefing, inspection log.
    public sealed class ConversationPlayTests
    {
        int consent;
        [SetUp] public void Offline() { consent = GameSettings.AiConsent; GameSettings.AiConsent = 0; }   // no model calls in tests
        [TearDown] public void Restore() { GameSettings.AiConsent = consent; ArcadeMode.Exit(); }

        static IEnumerator Load(int episode, bool arcade)
        {
            ArcadeMode.Active = arcade; ArcadeMode.Daily = false; ArcadeMode.Seed = 5;
            EpisodeDirector.Selected = Episodes.Get(episode); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null; yield return null;
        }
        static ShiftDirector D => Object.FindFirstObjectByType<ShiftDirector>();
        static CrewMember Crew(string name) => Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None).First(c => c.DisplayName.StartsWith(name));

        [UnityTest]
        public IEnumerator EveryClueHolder_IsOnSite_EveryEpisode()
        {
            foreach (var ep in Enumerable.Range(1, 5))
            {
                yield return Load(ep, true);
                foreach (var npc in CrewInterview.For(ep).Select(c => c.Npc).Distinct())
                    Assert.That(Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None).Any(c => c.DisplayName.StartsWith(npc) && c.isActiveAndEnabled), Is.True, $"EP{ep}: {npc} can be asked");
                foreach (var c in CrewInterview.For(ep))
                    Assert.That(D.Conditions.Any(x => x.Id == c.HazardId), Is.True, $"EP{ep}: {c.Id} -> {c.HazardId} is on site");
            }
        }

        [UnityTest]
        public IEnumerator AskingTheRightQuestion_GivesALead()
        {
            yield return Load(2, true);
            var d = D; var dolores = Crew("Dolores");
            d.StartTalk(dolores); yield return null;
            d.AskCrew("Did last night's rain do anything to the walls?"); yield return null;
            Assert.That(d.AskedClue("tue-clue-rain"), Is.True);
            Assert.That(d.Leads.Count, Is.EqualTo(1));
            StringAssert.Contains("sloughed", dolores.Transcript.Last());
            Assert.That(d.ScoreArcade().Leads, Is.EqualTo(1), "a lead is worth arcade points");
            d.EndTalk(); yield return null;
            var ray = Crew("Ray");
            d.StartTalk(ray); yield return null;
            d.AskCrew("How's the schedule looking?"); yield return null; yield return null;
            Assert.That(d.Leads.Count, Is.EqualTo(1), "small talk is not a lead");
        }

        [UnityTest]
        public IEnumerator Coaching_MakesTheReminderStick()
        {
            yield return Load(4, true);
            var d = D; var c = d.Conditions.First(x => x.Id == "thu-roof-edge");
            d.Photograph(c); yield return null;
            d.Report(c.Spec.Energy, c.Spec.Probability, c.Spec.Severity); yield return null;
            d.Control(ControlLevel.Administrative); yield return null;
            Assert.That(d.PendingCoaching, Is.EqualTo(c.Id), "a reminder on a behavioural hazard asks you to coach");
            Assert.That(float.IsPositiveInfinity(d.Session.LapseTime(c.Id)), Is.False, "an uncoached reminder fades");
            d.SubmitCoaching(CoachingRubric.Samples(c.Id)[0]); yield return null;
            Assert.That(d.PendingCoaching, Is.Null);
            Assert.That(float.IsPositiveInfinity(d.Session.LapseTime(c.Id)), Is.True, "coached: the change sticks");
            Assert.That(d.LastCoaching.Value.Score, Is.GreaterThanOrEqualTo(0.99f));
        }

        [UnityTest]
        public IEnumerator Course_OwnWordsBriefing_AndInspectionLog()
        {
            yield return Load(2, false);
            var d = D;
            d.Begin(); yield return null;
            Assert.That(d.InspectionLogAvailable, Is.True, "Tuesday course shift has the excavation log");
            var trench = d.Conditions.First(x => x.Id == "tue-no-protective-system");
            var spoil = d.Conditions.First(x => x.Id == "tue-spoil-at-edge");
            d.Photograph(trench); yield return null; d.ToggleTablet();
            d.Photograph(spoil); yield return null; d.ToggleTablet();
            Assert.That(d.Selected, Is.Null, "putting the tablet away returns it to the site-walk page (log, leads, hints)");
            d.SubmitInspectionLog("Type C", true, "Trench box", new[] { trench.Id, spoil.Id }, "Moved the trench box over the crew; spoil pulled back 2 ft.");
            Assert.That(d.InspectionLogResult.Value.Score, Is.GreaterThanOrEqualTo(0.99f), d.InspectionLogResult.Value.Feedback);
            d.Photograph(trench); yield return null;
            d.Report(trench.Spec.Energy, trench.Spec.Probability, trench.Spec.Severity); yield return null;
            d.EndShift(); yield return null;
            d.BriefInOwnWords("Morning everyone. The trench walls can cave in and bury someone. Nobody goes in until the box is set; OSHA 1926.652 requires it at 5 ft. Tell me if you see a crack.");
            Assert.That(d.OwnWordsTalk.Value.Score, Is.EqualTo(1f).Within(1e-4), d.OwnWordsTalk.Value.Feedback);
        }
    
        [UnityTest]
        public IEnumerator NearMiss_OwnWordsReview_AndFieldReference()
        {
            yield return Load(1, true);
            var d = D;
            var ladder = d.Conditions.First(x => x.Id == "mon-trailer-ladder");
            d.Session.GetEvidence(ladder.Id).BecameIncident = true;
            typeof(ShiftDirector).GetMethod("Handle", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                .Invoke(d, new object[] { new DayEvent(DayEventKind.NearMiss, ladder.Id, d.Session.Clock) });
            yield return null;
            Assert.That(d.PendingIncident, Is.EqualTo(ladder.Id));
            Assert.That(Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).Any(b => b.name.StartsWith("File the review")), Is.True, "the stop-down card offers the review");
            d.ReviewIncident("The ladder kicked out because it wasn't tied off or extended 3 ft and nobody checked it. We tie it off and extend it before anyone climbs.");
            yield return null;
            Assert.That(d.PendingIncident, Is.Null, "a filed review secures the area");
            Assert.That(d.LastIncidentReview.Value.Score, Is.EqualTo(1f).Within(1e-4), d.LastIncidentReview.Value.Feedback);
            // Field reference from the site-walk page: one entry per standard, logged as a lookup.
            if (!d.MenuOpen) d.ToggleTablet();
            yield return null;
            var open = Object.FindObjectsByType<UnityEngine.UI.Button>(FindObjectsSortMode.None).First(b => b.name.StartsWith("Field reference"));
            open.onClick.Invoke(); yield return null;
            Assert.That(d.ReferenceLookups, Is.EqualTo(1));
            var texts = Object.FindObjectsByType<UnityEngine.UI.Text>(FindObjectsSortMode.None).Select(t => t.text).ToList();
            Assert.That(texts.Any(t => t.StartsWith("FIELD REFERENCE")), Is.True);
            Assert.That(texts.Count(t => t.StartsWith("29 CFR")), Is.GreaterThanOrEqualTo(2), "standards listed");
        }
    
        [UnityTest]
        public IEnumerator StopWork_StopsTheExcavator_UntilTheStopLifts()
        {
            yield return Load(2, true);
            var d = D;
            var rig = Object.FindFirstObjectByType<ExcavatorRig>();
            Assert.That(rig.Running, Is.True);
            var swing = d.Conditions.First(x => x.Id == "tue-swing-radius");
            d.Photograph(swing); yield return null;
            d.Report(swing.Spec.Energy, swing.Spec.Probability, swing.Spec.Severity); yield return null;
            d.StopWork(); yield return null;
            Assert.That(rig.Running, Is.False, "the machine stands down with the crew");
            Assert.That(d.MachinesHalted, Is.True);
            d.Session.LiftStop(swing.Id);
            yield return new WaitForSeconds(0.7f);
            Assert.That(rig.Running, Is.True, "back to work when the stop lifts");
        }
    }
}
