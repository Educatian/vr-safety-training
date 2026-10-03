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
    // The game's opening: plays on request (first visit in a browser), runs every narrated beat, can be skipped, and
    // always lands on the menu. Frames every 0.5 s in Captures/t_prologue/ for review.
    public sealed class PrologueTests
    {
        [TearDown] public void Reset() { EpisodeDirector.ForcePrologue = false; ArcadeMode.Exit(); }

        static IEnumerator LoadMenu(bool prologue)
        {
            ArcadeMode.Exit();
            EpisodeDirector.Selected = null; EpisodeDirector.SkipIntro = false; EpisodeDirector.ForcePrologue = prologue;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Prologue_PlaysEveryBeat_ThenTheMenu()
        {
            yield return LoadMenu(true);
            var eps = Object.FindFirstObjectByType<EpisodeDirector>();
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Intro), "the opening plays before the menu");
            var seen = new System.Collections.Generic.HashSet<string>();
            var cam = eps.CinematicCamera;
            var shot = 0;
            for (var t = 0f; t < 120f && eps.Current == EpisodeDirector.State.Intro; t += 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                if (!string.IsNullOrEmpty(eps.Caption)) seen.Add(eps.Caption);
                if (cam == null) continue;
                var rt = new RenderTexture(960, 540, 24); cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(960, 540, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
                System.IO.Directory.CreateDirectory("Captures/t_prologue"); System.IO.File.WriteAllBytes($"Captures/t_prologue/p{shot++:D3}.jpg", tex.EncodeToJPG(85));
                RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex);
            }
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Menu), "the opening hands over to the menu");
            Assert.That(seen.Count, Is.EqualTo(Prologue.Beats.Count), "every narrated beat was shown");
            Assert.That(GameObject.Find("WatchIntro"), Is.Not.Null, "the menu offers the intro again");
        }

        [UnityTest]
        public IEnumerator Prologue_CanBeSkipped()
        {
            yield return LoadMenu(true);
            var eps = Object.FindFirstObjectByType<EpisodeDirector>();
            yield return new WaitForSeconds(1f);
            eps.Skip();
            yield return null; yield return null;
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Menu));
        }

        [UnityTest]
        public IEnumerator AutomatedRuns_GoStraightToTheMenu()
        {
            yield return LoadMenu(false);
            Assert.That(Object.FindFirstObjectByType<EpisodeDirector>().Current, Is.EqualTo(EpisodeDirector.State.Menu));
        }
    }
}
