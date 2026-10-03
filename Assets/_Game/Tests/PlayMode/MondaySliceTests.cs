using System.Collections;
using System.IO;
using System.Linq;
using Jobsite.Core;
using Jobsite.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jobsite.PlayTests
{
    // T5.2-style smoke of the Monday slice: walk up, photograph, report, control, stop-work, debrief.
    // Captures the real player-camera view (tablet UI included) into Captures/t_monday_play/.
    public sealed class MondaySliceTests
    {
        const string ScenePath = "Assets/_Game/Scenes/Jobsite.unity";
        ShiftDirector director;
        SitePlayer player;

        [UnitySetUp]
        public IEnumerator LoadScene()
        {
            EpisodeDirector.Selected = Episodes.Get(1); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync(ScenePath, LoadSceneMode.Single);
            yield return null;
            director = Object.FindFirstObjectByType<ShiftDirector>();
            player = Object.FindFirstObjectByType<SitePlayer>();
            Assert.That(director, Is.Not.Null);
            Assert.That(player, Is.Not.Null);
        }

        [UnityTest]
        public IEnumerator MondayLoop_ReportsControlsAndDebriefs()
        {
            Capture("01_start_briefing");
            director.Begin();
            yield return null;
            Capture("02_site_walk_gate");

            // Ladder: photograph -> report -> engineered fix -> collect at rack -> install.
            Photograph("mon-trailer-ladder", 3.5f, Vector3.right); // between trailers A and B
            yield return new WaitForSecondsRealtime(0.25f); // let the shutter flash fade
            Capture("03_ladder_photographed_tablet");
            director.Report(EnergySource.Gravity, 3, 4);
            director.Control(ControlLevel.Engineering);
            Assert.That(director.Session.GetState("mon-trailer-ladder"), Is.EqualTo(HazardState.Installing));
            Assert.That(director.KitOptions, Is.Not.Null, "engineered fix asks which control");
            director.ChooseKit(director.KitCorrect);
            Face(GameObject.Find("SupplyRack").transform.position, 2.2f);
            Physics.Raycast(player.View.ViewportPointToRay(new Vector3(.5f, .5f)), out var rackHit, 5f);
            director.Interact();
            Assert.That(director.Carrying, Is.True, director.Notice + " | ray hit: " + (rackHit.collider ? rackHit.collider.transform.parent.name + " " + rackHit.collider.bounds + " at " + rackHit.point + " from " + player.View.transform.position : "nothing"));
            yield return null;
            Capture("03b_carrying_kit_gloved");
            Face(Condition("mon-trailer-ladder").PhotoBounds.center, 3.5f, Vector3.right);
            director.Interact();
            // Setting the kit down opens the hands-on ladder setup: 3 ft above the landing, base 1:4.
            Assert.That(director.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Ladder), director.Notice);
            director.HandsOn.SetLadder(40f, 0.75f); director.HandsOn.SimPrimary();
            yield return null;
            Assert.That(director.Session.GetState("mon-trailer-ladder"), Is.EqualTo(HazardState.Controlled), director.Notice);
            yield return null;
            Capture("04_ladder_fixed");

            // Heat: administrative control is the best feasible one, so it must not lapse.
            Photograph("mon-empty-water");
            director.Report(EnergySource.Temperature, 4, 3);
            director.Control(ControlLevel.Administrative);
            Assert.That(director.Session.GetState("mon-empty-water"), Is.EqualTo(HazardState.Controlled));

            // Look-alike: a ramped cord costs precision, never safety rating.
            Photograph("mon-cord-ramp", 4.5f);
            director.Report(EnergySource.Electrical, 1, 1);
            Assert.That(director.Session.ReportPrecision, Is.LessThan(1f));

            // Damaged cord: stop work first (escalation), then leave it stopped.
            Photograph("mon-damaged-cord", 4.5f);
            director.Report(EnergySource.Electrical, 3, 4);
            director.StopWork();
            Assert.That(director.Session.GetState("mon-damaged-cord"), Is.EqualTo(HazardState.Stopped));
            yield return new WaitForSecondsRealtime(0.25f);
            Capture("05_stop_work_radio");

            director.EndShift();
            yield return null;
            Capture("06_shift_debrief");
            Assert.That(director.Finished, Is.True);
            Assert.That(director.Session.HazardIdentificationIndex, Is.EqualTo(3f / 4f).Within(1e-4));
        }

        SiteCondition Condition(string id) => director.Conditions.First(c => c.Id == id);

        void Photograph(string id, float distance = 2.6f, Vector3? approach = null)
        {
            if (director.MenuOpen) director.ToggleTablet();
            Face(Condition(id).PhotoBounds.center, distance, approach);
            director.Interact();
            Assert.That(director.Selected != null && director.Selected.Id == id, Is.True, $"{id}: {director.Notice}");
        }

        // Stand `distance` metres from the target on the `approach` side (default: south) and look at it.
        void Face(Vector3 target, float distance, Vector3? approach = null)
        {
            var cc = player.GetComponent<CharacterController>();
            cc.enabled = false;
            var side = approach ?? Vector3.back;
            var from = new Vector3(target.x, 0, target.z) + side * distance;
            from.y = 0.05f; // the site is graded flat; a downward ray could land on a trailer roof
            player.transform.position = from;
            player.transform.rotation = Quaternion.LookRotation(-side);
            player.View.transform.LookAt(target);
            cc.enabled = true;
            Physics.SyncTransforms();
        }

        void Capture(string name)
        {
            var cam = player.View;
            var rt = new RenderTexture(1600, 900, 24) { antiAliasing = 4 };
            var old = cam.targetTexture;
            cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases();
            cam.Render();
            RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0);
            tex.Apply();
            Directory.CreateDirectory("Captures/t_monday_play");
            File.WriteAllBytes($"Captures/t_monday_play/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null;
            cam.targetTexture = old;
            Object.Destroy(rt);
        }
    }
}
