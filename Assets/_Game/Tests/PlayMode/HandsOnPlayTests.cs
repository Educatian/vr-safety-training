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
    // Hands-on interactions in the real scene: laser and hold measurements, close inspection, and the hands-on installs
    // (midrail, ladder setup, cover, barricade, plug-in GFCI) - each failed once, then done right.
    // Captures: Captures/t_handson/ (world view of each task; the instruction panel is a screen overlay).
    public sealed class HandsOnPlayTests
    {
        static IEnumerator Load(int episode)
        {
            var career = new Career(lifetimeXp: 1200, points: 900);
            career.Buy(GearId.LaserMeasure); career.Buy(GearId.GfciTester); career.Buy(GearId.Penetrometer);
            CareerStore.Save(career);
            ArcadeMode.Active = true; ArcadeMode.Daily = false; ArcadeMode.Seed = 3;
            EpisodeDirector.Selected = Episodes.Get(episode); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null; yield return null;
        }

        [TearDown] public void Reset() { ArcadeMode.Exit(); CareerStore.Reset(); }

        static ShiftDirector D => Object.FindFirstObjectByType<ShiftDirector>();
        static SiteCondition C(string id) => D.Conditions.First(c => c.Id == id);

        static IEnumerator Frame(string name, SiteCondition c, Vector3 offset)
        {
            var player = Object.FindFirstObjectByType<SitePlayer>();
            var view = player.View;
            var cam = new GameObject("HandsOnCam").AddComponent<Camera>(); cam.CopyFrom(view); cam.enabled = false;
            var b = c.PhotoBounds;
            cam.transform.position = b.center + offset;
            cam.transform.rotation = Quaternion.LookRotation(b.center - cam.transform.position);
            yield return null;
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_handson"); System.IO.File.WriteAllBytes($"Captures/t_handson/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; Object.Destroy(rt); Object.Destroy(tex); Object.Destroy(cam.gameObject);
        }

        // Report it, pick the engineered fix and the right kit, carry it over and set it down (E): the hands-on step opens.
        static IEnumerator StartInstall(SiteCondition c)
        {
            var d = D;
            d.Photograph(c); yield return null;
            d.Report(c.Spec.Energy, c.Spec.Probability, c.Spec.Severity); yield return null;
            d.Control(ControlLevel.Engineering); yield return null;
            if (d.KitOptions != null) d.ChooseKit(d.KitCorrect);
            d.PickUpKit(); yield return null;
            d.PlaceKit(c, c.PhotoBounds.center); yield return null;
        }

        [UnityTest]
        public IEnumerator Laser_TwoPointsOnTheCondition_GiveAReading()
        {
            yield return Load(2);
            var d = D; var c = C("tue-spoil-at-edge");
            d.Photograph(c); yield return null;
            d.BeginMeasure(GearId.LaserMeasure); yield return null;
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Measure));
            Assert.That(d.MenuOpen, Is.False, "the tablet goes down while you measure");
            // Across the site first: no reading.
            d.HandsOn.SimAim(c.PhotoBounds.center); d.HandsOn.SimPrimary(); yield return null;
            d.HandsOn.SimAim(c.PhotoBounds.center + new Vector3(30f, 0, 0)); d.HandsOn.SimPrimary(); yield return null;
            Assert.That(d.Measured(c, GearId.LaserMeasure), Is.False);
            StringAssert.Contains("not on this condition", d.HandsOnNotes[c.Id]);
            // On the condition: reading recorded.
            d.BeginMeasure(GearId.LaserMeasure); yield return null;
            var b = c.PhotoBounds;
            d.HandsOn.SimAim(new Vector3(b.min.x, b.min.y, b.center.z)); d.HandsOn.SimPrimary(); yield return null;
            StringAssert.Contains("aim at the other end", d.HandsOn.Hint);
            yield return Frame("laser", c, new Vector3(3f, 2.2f, -3f));
            d.HandsOn.SimAim(new Vector3(b.max.x, b.min.y, b.center.z)); d.HandsOn.SimPrimary(); yield return null;
            Assert.That(d.Measured(c, GearId.LaserMeasure), Is.True);
            StringAssert.StartsWith("Your laser:", d.HandsOnNotes[c.Id]);
            Assert.That(d.MenuOpen, Is.True, "back on the condition's page");
        }

        [UnityTest]
        public IEnumerator Penetrometer_HoldOnTheSoil_GivesTheReading()
        {
            yield return Load(2);
            var d = D; var c = C("tue-no-protective-system");
            d.Photograph(c); yield return null;
            d.BeginMeasure(GearId.Penetrometer); yield return null;
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Hold));
            d.HandsOn.SimAim(c.PhotoBounds.center); yield return null;
            d.HandsOn.SimHoldComplete(); yield return null;
            Assert.That(d.Measured(c, GearId.Penetrometer), Is.True);
            Assert.That(HandsOn.Active, Is.False);
        }

        [UnityTest]
        public IEnumerator Cord_Inspection_FindTheDamage()
        {
            yield return Load(1);
            var d = D; var c = C("mon-damaged-cord");
            d.Photograph(c); yield return null;
            d.BeginInspect(); yield return null;
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Inspect));
            d.HandsOn.FaceHotspot(false); yield return null;
            d.HandsOn.SimPrimary(); yield return null;
            Assert.That(d.HandsOn.InspectFalseMarks, Is.EqualTo(1), "a sound spot is not a defect");
            d.HandsOn.FaceHotspot(true); yield return null;
            yield return Frame("inspect_cord_world", c, new Vector3(2f, 1.5f, -2f));
            var view = Object.FindFirstObjectByType<SitePlayer>().View;
            var rt = new RenderTexture(1280, 720, 24); view.targetTexture = rt; view.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            System.IO.File.WriteAllBytes("Captures/t_handson/inspect_cord.png", tex.EncodeToPNG());
            RenderTexture.active = null; view.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex);
            d.HandsOn.SimPrimary(); yield return null;
            Assert.That(HandsOn.Active, Is.False);
            Assert.That(d.Inspected(c), Is.True);
            StringAssert.StartsWith("Found it", d.HandsOnNotes[c.Id]);
        }

        [UnityTest]
        public IEnumerator Barricade_RingOutsideTheSwingPath()
        {
            yield return Load(2);
            var d = D; var c = C("tue-swing-radius");
            yield return StartInstall(c);
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Barricade));
            var h = d.HandsOn; var ctr = h.SwingCenter; var r = h.SwingRadius;
            for (var i = 0; i < 3; i++) h.PlaceCone(ctr + new Vector3(Mathf.Cos(i * 2f) * r * 0.5f, 0, Mathf.Sin(i * 2f) * r * 0.5f));
            h.SimDone(); yield return null;
            Assert.That(h.Current, Is.EqualTo(HandsOn.Mode.Barricade), "a bad ring can be fixed");
            Assert.That(d.Session.GetState(c.Id), Is.Not.EqualTo(HazardState.Controlled));
            h.SimCancel(); yield return null;
            Assert.That(d.Carrying, Is.True, "cancel keeps the kit in hand");
            d.PlaceKit(c, c.PhotoBounds.center); yield return null;
            h = d.HandsOn;
            for (var i = 0; i < 6; i++) { var a = i * Mathf.PI / 3f; h.PlaceCone(ctr + new Vector3(Mathf.Cos(a) * r * 1.15f, 0, Mathf.Sin(a) * r * 1.15f)); }
            yield return Frame("barricade", c, new Vector3(0f, r * 2.2f + 4f, -r * 1.8f - 3f));
            h.SimDone(); yield return null;
            Assert.That(HandsOn.Active, Is.False);
            Assert.That(d.Session.GetState(c.Id), Is.EqualTo(HazardState.Controlled));
        }

        [UnityTest]
        public IEnumerator Wednesday_Midrail_Ladder_Cover()
        {
            yield return Load(3);
            var d = D;
            var mid = C("wed-missing-midrail");
            yield return StartInstall(mid);
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Midrail));
            d.HandsOn.SetMidrailInches(8f); d.HandsOn.SimPrimary(); yield return null;
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Midrail), "too low: try again");
            d.HandsOn.SetMidrailInches(21f);
            yield return Frame("midrail", mid, new Vector3(1.5f, 1.2f, -2.5f));
            d.HandsOn.SimPrimary(); yield return null;
            Assert.That(d.Session.GetState(mid.Id), Is.EqualTo(HazardState.Controlled));

            var ladder = C("wed-short-ladder");
            yield return StartInstall(ladder);
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Ladder));
            d.HandsOn.SetLadder(20f, 0.9f); d.HandsOn.SimPrimary(); yield return null;
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Ladder));
            d.HandsOn.SetLadder(40f, 0.9f); d.HandsOn.SimPrimary(); yield return null;
            Assert.That(d.Session.GetState(ladder.Id), Is.EqualTo(HazardState.Controlled));

            var hole = C("wed-open-hole");
            yield return StartInstall(hole);
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Cover));
            d.HandsOn.SetCover(hole.PhotoBounds.center, ""); d.HandsOn.SimPrimary(); yield return null;
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Cover), "unmarked cover fails");
            d.HandsOn.SetCover(hole.PhotoBounds.center, "HOLE");
            yield return Frame("cover", hole, new Vector3(1.8f, 2.4f, -2.2f));
            d.HandsOn.SimPrimary(); yield return null;
            Assert.That(d.Session.GetState(hole.Id), Is.EqualTo(HazardState.Controlled));
        }

        [UnityTest]
        public IEnumerator PlugInGfci_HoldAtTheOutlet()
        {
            yield return Load(1);
            var d = D; var c = C("mon-no-gfci");
            yield return StartInstall(c);
            Assert.That(d.HandsOn.Current, Is.EqualTo(HandsOn.Mode.Hold));
            d.HandsOn.SimAim(c.PhotoBounds.center); d.HandsOn.SimHoldComplete(); yield return null;
            Assert.That(d.Session.GetState(c.Id), Is.EqualTo(HazardState.Controlled));
        }
    }
}
