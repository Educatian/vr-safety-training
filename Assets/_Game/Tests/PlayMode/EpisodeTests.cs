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
    // Episode select -> intro cinematic -> the topic's day loads with its own hazards and quizzes.
    public sealed class EpisodeTests
    {
        static void Shot(Camera cam, string name)
        {
            var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_episodes"); System.IO.File.WriteAllBytes($"Captures/t_episodes/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt);
        }

        [UnityTest]
        public IEnumerator GearLocker_BuyTester_ThenReadingAndHintOnSite()
        {
            CareerStore.Save(new Career(lifetimeXp: 1200, points: 900));
            EpisodeDirector.Selected = null; ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = false;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var eps = Object.FindFirstObjectByType<EpisodeDirector>();
            eps.ShowTab(EpisodeDirector.Tab.Gear);
            Assert.That(eps.Buy(GearId.GfciTester), Is.EqualTo(BuyResult.Bought));
            Assert.That(eps.Buy(GearId.LaserMeasure), Is.EqualTo(BuyResult.Bought));
            yield return null;
            Shot(eps.CinematicCamera, "07_gear_locker");
            eps.ShowTab(EpisodeDirector.Tab.Crew);
            yield return null;
            Shot(eps.CinematicCamera, "08_crew");

            EpisodeDirector.SkipIntro = true;
            EpisodeDirector.Play(Episodes.Get(1));
            yield return null; yield return null;
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.That(director.Career.Has(GearId.GfciTester), Is.True, "purchase persists across the scene load");
            var outlet = director.Conditions.First(c => c.Id == "mon-no-gfci");
            Assert.That(outlet.Reading(GearId.GfciTester), Does.Contain("not GFCI"));
            director.Begin();
            var tokens = director.Hints.Tokens;
            director.UseHint();
            Assert.That(director.Hints.Tokens, Is.EqualTo(tokens - 1));
            Assert.That(director.Notice, Does.StartWith("Dolores:"));
            // Walk up to Dolores: name tag over her head + minimap in the corner; then the full-site map.
            var dolores = Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None).First(c => c.DisplayName == "Dolores");
            var player = Object.FindFirstObjectByType<SitePlayer>();
            player.GetComponent<CharacterController>().enabled = false;
            player.transform.position = dolores.transform.position + dolores.transform.forward * 4.5f + Vector3.right * 1.2f;
            player.transform.LookAt(new Vector3(dolores.transform.position.x, player.transform.position.y, dolores.transform.position.z));
            player.View.transform.localRotation = Quaternion.Euler(4, 0, 0);
            for (var i = 0; i < 3; i++) yield return null;
            Shot(player.View, "09_nametag_minimap");
            var map = Object.FindFirstObjectByType<Minimap>();
            map.Toggle(); yield return null;
            Shot(player.View, "10_full_map");
            map.Toggle();
            CareerStore.Reset();
        }

        [UnityTest]
        public IEnumerator Menu_ThenEpisode2Intro_LoadsTrenchDay()
        {
            EpisodeDirector.Selected = null; ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = false;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var eps = Object.FindFirstObjectByType<EpisodeDirector>();
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Menu));
            yield return new WaitForSeconds(0.5f);
            Shot(eps.CinematicCamera, "01_episode_select");

            EpisodeDirector.Play(Episodes.Get(2));
            yield return null; yield return null;
            eps = Object.FindFirstObjectByType<EpisodeDirector>();
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Intro));
            yield return new WaitForSeconds(1.5f);
            Shot(eps.CinematicCamera, "02_title_card");
            yield return new WaitForSeconds(6f);
            Shot(eps.CinematicCamera, "03_intro_shot1");
            yield return new WaitForSeconds(6f);
            Shot(eps.CinematicCamera, "04_intro_shot2");
            yield return new WaitForSeconds(5f);
            Shot(eps.CinematicCamera, "05_intro_shot3");
            eps.Skip();
            yield return null; yield return null;
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Playing));

            var director = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.That(Object.FindFirstObjectByType<SitePhaseController>().Day, Is.EqualTo(WorkDay.Tue));
            Assert.That(director.Conditions.Any(c => c.Id.StartsWith("tue-")), Is.True);
            Assert.That(director.Conditions.Any(c => c.Id.StartsWith("mon-")), Is.False);
            foreach (CheckInStation.Kind k in System.Enum.GetValues(typeof(CheckInStation.Kind))) director.CheckIn(k);
            Assert.That(director.Quiz.Items[0].Id, Is.EqualTo("tue-tb-ladder"));
            Shot(Object.FindFirstObjectByType<SitePlayer>().View, "06_ep2_gate");
        }
    }
}
