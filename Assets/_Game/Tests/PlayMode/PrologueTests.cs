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
    // The game's opening is the first mission's cold open: it plays when Episode 1 starts, runs every narrated beat,
    // can be skipped, and hands over to the episode's own title card and intro. The menu's WATCH INTRO replays it and
    // lands back on the menu. Frames every 0.5 s in Captures/t_prologue/ for review.
    public sealed class PrologueTests
    {
        [TearDown] public void Reset() { EpisodeDirector.ForcePrologue = false; EpisodeDirector.Selected = null; ArcadeMode.Exit(); }

        static IEnumerator Load(int episode, bool prologue)
        {
            ArcadeMode.Exit();
            EpisodeDirector.Selected = episode > 0 ? Episodes.Get(episode) : null;
            EpisodeDirector.SkipIntro = false; EpisodeDirector.ForcePrologue = prologue;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
        }

        [UnityTest]
        public IEnumerator Prologue_BeforeMonday_PlaysEveryBeat_ThenTheEpisodeIntro()
        {
            yield return Load(1, true);
            var eps = Object.FindFirstObjectByType<EpisodeDirector>();
            Assert.That(eps.InPrologue, Is.True, "the opening plays as the mission starts");
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Intro));
            var seen = new System.Collections.Generic.HashSet<string>();
            var cam = eps.CinematicCamera;
            var shot = 0;
            for (var t = 0f; t < 120f && eps.InPrologue; t += 0.5f)
            {
                yield return new WaitForSeconds(0.5f);
                if (eps.InPrologue && !string.IsNullOrEmpty(eps.Caption)) seen.Add(eps.Caption);
                if (cam == null) continue;
                var rt = new RenderTexture(960, 540, 24); cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
                var tex = new Texture2D(960, 540, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 960, 540), 0, 0); tex.Apply();
                System.IO.Directory.CreateDirectory("Captures/t_prologue"); System.IO.File.WriteAllBytes($"Captures/t_prologue/p{shot++:D3}.jpg", tex.EncodeToJPG(85));
                RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex);
            }
            Assert.That(eps.InPrologue, Is.False);
            Assert.That(seen.Count, Is.EqualTo(Prologue.Beats.Count), "every narrated beat was shown");
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Intro), "the opening hands over to Episode 1's intro");
            yield return new WaitForSeconds(1f);
            Assert.That(GameObject.Find("TitleCard"), Is.Not.Null, "Episode 1's title card follows the opening");
            eps.Skip();
            yield return null; yield return null;
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Playing), "then the shift starts");
        }

        [UnityTest]
        public IEnumerator Prologue_CanBeSkipped_IntoTheEpisode()
        {
            yield return Load(1, true);
            var eps = Object.FindFirstObjectByType<EpisodeDirector>();
            yield return new WaitForSeconds(1f);
            eps.Skip();
            yield return null; yield return null; yield return null;
            Assert.That(eps.InPrologue, Is.False);
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Intro), "skipping the opening keeps the episode's title card");
            eps.Skip();
            yield return null; yield return null;
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Playing));
        }

        [UnityTest]
        public IEnumerator MenuReplay_LandsBackOnTheMenu()
        {
            yield return Load(0, true);
            var eps = Object.FindFirstObjectByType<EpisodeDirector>();
            Assert.That(eps.InPrologue, Is.True);
            yield return new WaitForSeconds(1f);
            eps.Skip();
            yield return null; yield return null;
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Menu));
            Assert.That(GameObject.Find("WatchIntro"), Is.Not.Null, "the menu offers the opening again");
        }

        [UnityTest]
        public IEnumerator Menu_And_OtherEpisodes_HaveNoOpening()
        {
            yield return Load(0, false);
            Assert.That(Object.FindFirstObjectByType<EpisodeDirector>().Current, Is.EqualTo(EpisodeDirector.State.Menu));
            yield return Load(2, false);
            var eps = Object.FindFirstObjectByType<EpisodeDirector>();
            Assert.That(eps.InPrologue, Is.False, "the opening belongs to the first mission only");
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Intro), "Tuesday opens on its own intro");
        }
    }
}
