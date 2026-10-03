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
    // Grip tuning sheet (Captures/t_grip/): the tablet raised at 16:9 with a set of pinch poses, hands-only and with
    // the UI, plus where the thumb / index / middle tips sit across the tablet's thickness. Explicit: run on demand
    // with -testFilter Jobsite.PlayTests.GripTuneTests.
    [Explicit]
    public sealed class GripTuneTests
    {
        // (pose, auto-search). Auto keeps EdgeY / Others from the pose and searches the rest.
        static readonly (FirstPersonTablet.GripPose pose, bool auto)[] Poses =
        {
            (FirstPersonTablet.DefaultGrip, true),
            (new FirstPersonTablet.GripPose(20, 0, 30, 15, 75, -0.15f, 0.6f), true),
            (new FirstPersonTablet.GripPose(20, 0, 30, 15, 75, -0.55f, 0.6f), true),
            (new FirstPersonTablet.GripPose(20, 0, 30, 15, 60, -0.35f, 0.6f), true),
            (new FirstPersonTablet.GripPose(20, 0, 30, 15, 90, -0.35f, 0.6f), true),
            (new FirstPersonTablet.GripPose(20, 10, 0, 10, 75, -0.35f, 0.6f), false),
            (new FirstPersonTablet.GripPose(10, 15, -15, 10, 75, -0.35f, 0.4f), false),
            (new FirstPersonTablet.GripPose(30, 20, 15, 10, 75, -0.35f, 0.8f), false),
        };

        static void Shot(Camera cam, RenderTexture rt, string name)
        {
            Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(rt.width, rt.height, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, rt.width, rt.height), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_grip"); System.IO.File.WriteAllBytes($"Captures/t_grip/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator Grip_Variants()
        {
            GameSettings.Guidance = ScaffoldCues.Full;
            EpisodeDirector.Selected = Episodes.Get(1); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var d = Object.FindFirstObjectByType<ShiftDirector>();
            var p = Object.FindFirstObjectByType<SitePlayer>();
            foreach (CheckInStation.Kind k in System.Enum.GetValues(typeof(CheckInStation.Kind))) d.CheckIn(k);
            d.SubmitHierarchy(HierarchyOrdering.Correct);
            while (!d.Quiz.Done) d.AnswerQuiz(d.Quiz.Current.Correct);
            d.Begin(); yield return null;
            var target = d.Conditions.First(c => c != null && c.isActiveAndEnabled && c.IsHazard);
            d.Photograph(target);
            d.Report(target.Spec.Energy, Mathf.Clamp(target.Spec.Probability - 1, 1, 5), target.Spec.Severity);
            yield return new WaitForSecondsRealtime(0.35f);

            var fp = p.View.GetComponent<FirstPersonTablet>();
            if (fp == null) fp = Object.FindFirstObjectByType<FirstPersonTablet>();
            Assert.IsNotNull(fp, "FirstPersonTablet on the view camera");
            var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.name == "TabletCanvas");
            var log = new System.Text.StringBuilder();
            foreach (var size in new[] { new Vector2Int(1280, 720), new Vector2Int(960, 720) })
            {
                var rt = new RenderTexture(size.x, size.y, 24);
                p.View.targetTexture = rt;
                yield return null; yield return null;   // FirstPersonTablet refits to the new pixel size
                if (size.x == 1280)
                    for (var i = 0; i < Poses.Length; i++)
                    {
                        fp.Regrip(Poses[i].pose, Poses[i].auto); yield return null;
                        canvas.enabled = false; Shot(p.View, rt, $"v{i:00}_hands"); canvas.enabled = true;
                        Shot(p.View, rt, $"v{i:00}_ui");
                        log.AppendLine($"v{i:00}  {(Poses[i].auto ? "auto" : "manual")}  {fp.LastGripReport}");
                    }
                fp.Regrip(FirstPersonTablet.DefaultGrip); yield return null;
                Shot(p.View, rt, $"default_{size.x}x{size.y}");
                FirstPersonTablet.FullView = true; yield return null; yield return null;
                Shot(p.View, rt, $"fullview_{size.x}x{size.y}");
                Assert.IsFalse(GameObject.Find("FP_TabletHands") != null && GameObject.Find("FP_TabletHands").activeInHierarchy, "the 3D device hides in full view");
                FirstPersonTablet.ToggleFullView(); yield return null; yield return null;
                Assert.IsFalse(FirstPersonTablet.FullView);
                Shot(p.View, rt, $"handheld_again_{size.x}x{size.y}");
                p.View.targetTexture = null; rt.Release(); Object.Destroy(rt);
                yield return null;
            }
            System.IO.File.WriteAllText("Captures/t_grip/report.txt", log.ToString());
            Assert.IsNotEmpty(fp.LastGripReport, "the right glove found its thumb and index bones");
        }
    }
}
