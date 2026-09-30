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
    // 2026-09-30 upgrade checks that need the scene: hand tools are in the palms (contact <= 2 cm), below-grade crew get
    // the trench lighting, the ECD data file loads and validates, and the shift starts with the new affect state.
    public sealed class UpgradeTests
    {
        static IEnumerator Load(int episode)
        {
            EpisodeDirector.Selected = Episodes.Get(episode); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
        }

        static float DistanceToAxis(Vector3 p, Transform tool)
        {
            var o = tool.position; var d = tool.forward;
            var v = p - o;
            return (v - Vector3.Dot(v, d) * d).magnitude;
        }

        [UnityTest]
        public IEnumerator Ep2_WorkingCrew_HoldTheirTools()
        {
            yield return Load(2);
            yield return new WaitForSeconds(0.6f);
            var workers = Object.FindObjectsByType<CrewGestures>(FindObjectsSortMode.None);
            var diggers = workers.Where(w => w.CurrentActivity == CrewGestures.Activity.Dig && w.isActiveAndEnabled).ToList();
            var sawyers = workers.Where(w => w.CurrentActivity == CrewGestures.Activity.Saw && w.isActiveAndEnabled).ToList();
            Assert.That(diggers.Count + sawyers.Count, Is.GreaterThan(0), "EP2 has working crew");
            // Measure after CrewGestures.LateUpdate posed the bones and placed the tool (probe runs last).
            var probes = diggers.Concat(sawyers).Select(w => { var p = w.gameObject.AddComponent<ToolProbe>(); p.Worker = w; return p; }).ToList();
            yield return null; yield return null;
            foreach (var p in probes)
            {
                if (!p.Measured) continue;   // reacting this frame (tool set down)
                Assert.That(p.Worker.Tool, Is.Not.Null, p.Worker.name + " has a tool");
                Assert.That(p.Right, Is.LessThanOrEqualTo(0.02f), p.Worker.name + ": right palm on the tool");
                if (p.Worker.CurrentActivity == CrewGestures.Activity.Dig) Assert.That(p.Left, Is.LessThanOrEqualTo(0.02f), p.Worker.name + ": left palm on the handle");
            }
            Assert.That(probes.Any(p => p.Measured), "at least one working crew member measured");
        }

        [DefaultExecutionOrder(32000)]
        sealed class ToolProbe : MonoBehaviour
        {
            public CrewGestures Worker; public float Right = 99f, Left = 99f; public bool Measured;
            void LateUpdate()
            {
                if (Worker == null || Worker.Tool == null || Worker.Busy) return;
                if (Worker.CurrentActivity == CrewGestures.Activity.Dig)
                { Right = DistanceToAxis(Worker.Palm(true), Worker.Tool); Left = DistanceToAxis(Worker.Palm(false), Worker.Tool); }
                else Right = Vector3.Distance(Worker.Palm(true), Worker.Tool.position);
                Measured = true;
            }
        }

        [UnityTest]
        public IEnumerator Ep2_BelowGradeCrew_GetTrenchLighting()
        {
            yield return Load(2);
            var below = Object.FindObjectsByType<SkinnedMeshRenderer>(FindObjectsSortMode.None).Where(r => r.bounds.center.y < TrenchLighting.BelowGrade + 0.9f).ToList();
            Assert.That(below.Count, Is.GreaterThan(0), "someone works in the trench on EP2");
            Assert.That(below.All(r => r.lightProbeUsage == UnityEngine.Rendering.LightProbeUsage.CustomProvided), "below-grade renderers use the trench probe");
        }

        [UnityTest]
        public IEnumerator EcdData_Loads_AndShiftTracksAffect()
        {
            yield return Load(1);
            StringAssert.StartsWith("ecd.json", EcdLoader.Status, "Resources/ecd.json loaded and validated");
            Assert.That(EvidenceModel.Current.Validate(), Is.Empty);
            var d = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.That(d.Session.Affect, Is.Not.Null);
            Assert.That(d.Session.Affect.Trust(CrewAffect.Crew), Is.InRange(-1f, 1f));
        }
    
        [UnityTest]
        public IEnumerator Ep1_CrewRequest_IssuesAndCompletesOnTheRealAction()
        {
            yield return Load(1);
            var d = Object.FindFirstObjectByType<ShiftDirector>();
            foreach (CheckInStation.Kind k in System.Enum.GetValues(typeof(CheckInStation.Kind))) d.CheckIn(k);
            d.SubmitHierarchy(HierarchyOrdering.Correct);
            while (!d.Quiz.Done) d.AnswerQuiz(d.Quiz.Current.Correct);
            d.Begin(); yield return null;
            var req = CrewRequests.For(1)[0];
            d.Session.Spend(req.AtSeconds + 5f); yield return null; yield return null;
            Assert.That(d.OpenRequests.Any(r => r.Id == req.Id), "the request came in over the radio");
            var water = d.Conditions.First(c => c.Id == req.Step.Targets[0]);
            var xp = d.Xp;
            d.Photograph(water); yield return null;
            Assert.That(d.RequestsDone, Is.EqualTo(1), "photographing the water station answers it");
            Assert.That(d.Xp, Is.GreaterThanOrEqualTo(xp + req.Xp));
            StringAssert.Contains("Crew request done", SiteFx.LastToast);
        }
    }
}
