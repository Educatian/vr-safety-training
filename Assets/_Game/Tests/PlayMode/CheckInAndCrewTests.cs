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
    // Gate flow (sign in, don PPE, hierarchy drag-drop, toolbox quiz) and a live crew conversation.
    public sealed class CheckInAndCrewTests
    {
        static void Shot(string name)
        {
            var cam = Object.FindFirstObjectByType<SitePlayer>().View; var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_gate"); System.IO.File.WriteAllBytes($"Captures/t_gate/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt);
        }

        [UnityTest]
        public IEnumerator Gate_CheckInBriefingQuiz_ThenShift()
        {
            EpisodeDirector.Selected = Episodes.Get(1); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.That(director.Current, Is.EqualTo(ShiftDirector.Phase.CheckIn));

            foreach (var s in Object.FindObjectsByType<CheckInStation>(FindObjectsSortMode.None)) s.Use(director);
            Assert.That(director.CheckInComplete, Is.True);
            Assert.That(director.Current, Is.EqualTo(ShiftDirector.Phase.Briefing));
            yield return null;

            var board = Object.FindFirstObjectByType<HierarchyBoard>();
            Assert.That(board, Is.Not.Null, "hierarchy drag-drop board should be on the tablet");
            Shot("01_hierarchy_dragdrop");
            board.PlaceAll(new[] { "PPE", "Substitution", "Engineering", "Administrative", "Elimination" });
            director.SubmitHierarchy(board.Order());
            Assert.That(director.HierarchyScore, Is.EqualTo(3));
            director.SubmitHierarchy(HierarchyOrdering.Correct);
            Assert.That(director.HierarchyScore, Is.EqualTo(5));
            yield return null;
            Shot("02_toolbox_quiz");

            while (!director.Quiz.Done) director.AnswerQuiz(director.Quiz.Current.Correct);   // options are shuffled per session
            Assert.That(director.Quiz.Done, Is.True);
            Assert.That(director.Quiz.CorrectCount, Is.EqualTo(3));
            director.Begin();
            Assert.That(director.Current, Is.EqualTo(ShiftDirector.Phase.Shift));
            Assert.That(director.Xp, Is.EqualTo(50 + 75));
        }

        [UnityTest]
        public IEnumerator Crew_Dolores_AnswersAQuestion()
        {
            EpisodeDirector.Selected = Episodes.Get(1); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true; GameSettings.AiConsent = 1;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var dolores = Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None).First(c => c.DisplayName == "Dolores");
            dolores.BeginTalk(Camera.main.transform);
            var task = dolores.Ask("At what height do I need fall protection on this deck?", "");
            var t0 = Time.realtimeSinceStartup;
            while (!task.IsCompleted && Time.realtimeSinceStartup - t0 < 30f) yield return null;
            Assert.That(task.IsCompleted, Is.True, "reply timed out");
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var player = Object.FindFirstObjectByType<SitePlayer>();
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.position = dolores.transform.position + dolores.transform.forward * 2f;
            player.transform.rotation = Quaternion.LookRotation(-dolores.transform.forward);
            player.View.transform.LookAt(dolores.transform.position + Vector3.up * 1.6f);
            director.SendMessage("StartTalk", dolores, SendMessageOptions.DontRequireReceiver);
            for (var i = 0; i < 30; i++) yield return null;
            Shot("03_dolores_chat");
            var reply = dolores.Transcript.Last();
            Debug.Log("[CrewTest] " + reply);
            Assert.That(reply, Does.StartWith("Dolores:"));
            Assert.That(reply.Length, Is.GreaterThan(20));
        }
    }
}
