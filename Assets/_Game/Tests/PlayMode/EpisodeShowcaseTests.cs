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
    // Per-episode showcase (Captures/t_show/): the mission's first on-site target with its cue, the tablet after a
    // report (feedback + KSA line + standard), and the closing KSA profile.
    public sealed class EpisodeShowcaseTests
    {
        static void Shot(Camera cam, string name)
        {
            int w = Screen.width, h = Screen.height;
            var rt = new RenderTexture(w, h, 24); cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_show"); System.IO.File.WriteAllBytes($"Captures/t_show/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt);
        }

        // A standing spot ~dist m from the target with ground under it and a clear line of sight.
        static bool Vantage(Bounds b, float dist, out Vector3 at)
        {
            var c = b.center;
            for (var i = 0; i < 16; i++)
            {
                var a = i * Mathf.PI * 2 / 16 + 0.3f;
                var p = new Vector3(c.x + Mathf.Cos(a) * dist, b.min.y + 3f, c.z + Mathf.Sin(a) * dist);
                if (!Physics.Raycast(p, Vector3.down, out var g, 6f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (g.point.y < b.min.y - 0.5f || g.point.y > b.min.y + 3.5f || g.point.y > 1.5f && b.min.y < 1f) continue;   // ground, not on top of a truck
                var eye = g.point + Vector3.up * 1.6f;
                if (Physics.CheckSphere(g.point + Vector3.up * 1f, 0.4f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (Physics.Linecast(eye, c, out var hit, ~0, QueryTriggerInteraction.Ignore) && Vector3.Distance(hit.point, c) > b.extents.magnitude + 0.5f) continue;
                at = g.point + Vector3.up * 0.05f; return true;
            }
            at = default; return false;
        }

        static void Stand(SitePlayer p, Vector3 at, Vector3 look)
        {
            var cc = p.GetComponent<CharacterController>(); cc.enabled = false;
            p.transform.position = at; p.transform.LookAt(new Vector3(look.x, at.y, look.z)); cc.enabled = true;
            var eye = at + Vector3.up * 1.6f;
            p.View.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(look.y - eye.y, new Vector2(look.x - eye.x, look.z - eye.z).magnitude) * Mathf.Rad2Deg, 0, 0);
        }

        [UnityTest]
        public IEnumerator Showcase_Ep1_to_Ep5()
        {
            GameSettings.Guidance = ScaffoldCues.Full;
            Debug.Log($"[Showcase] screen {Screen.width}x{Screen.height}");
            for (var ep = 1; ep <= 5; ep++)
            {
                EpisodeDirector.Selected = Episodes.Get(ep); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
                yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
                yield return null;
                var d = Object.FindFirstObjectByType<ShiftDirector>();
                var p = Object.FindFirstObjectByType<SitePlayer>();
                foreach (CheckInStation.Kind k in System.Enum.GetValues(typeof(CheckInStation.Kind))) d.CheckIn(k);
                d.SubmitHierarchy(HierarchyOrdering.Correct);
                while (!d.Quiz.Done) d.AnswerQuiz(d.Quiz.Current.Correct);
                d.Begin(); yield return null;

                // 1) Site: first mission step that has an on-site hazard target, with the mission cue over it.
                var target = d.Mission.Mission.Steps.SelectMany(s => s.Targets)
                    .Select(id => d.Conditions.FirstOrDefault(c => c != null && c.Id == id && c.isActiveAndEnabled && c.IsHazard))
                    .FirstOrDefault(c => c != null) ?? d.Conditions.First(c => c.IsHazard);
                var b = target.PhotoBounds;
                Vector3 at, look = b.center + Vector3.up * 0.8f;
                if (target.Id == "fri-boom-near-line") { at = new Vector3(42.5f, 0.05f, 70f); look = new Vector3(40f, 7.9f, 75.2f); }   // boom tip and the line overhead
                else if (!Vantage(b, 7f, out at) && !Vantage(b, 5f, out at)) at = b.center + new Vector3(0, -b.extents.y, -6f);
                Stand(p, at, look);
                yield return new WaitForSeconds(0.3f);
                Shot(p.View, $"ep{ep}_1_site");

                // 2) Tablet after a report: feedback, KSA line, OSHA chip, controls.
                d.Photograph(target);
                d.Report(target.Spec.Energy, Mathf.Clamp(target.Spec.Probability - 1, 1, 5), target.Spec.Severity);
                yield return null;
                Shot(p.View, $"ep{ep}_2_tablet");
                if (ep == 1)
                {   // hands only (UI hidden) to check the grip
                    var canvas = Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None).First(c => c.name == "TabletCanvas");
                    canvas.enabled = false; Shot(p.View, "ep1_hands"); canvas.enabled = true;
                }
                if (target.Spec.RequiresStopWork) d.StopWork();
                d.Control(target.Spec.BestFeasibleControl);
                if (d.PendingInstall == target.Id) { d.PickUpKit(); d.SetKitDown(target, b.center); }
                if (d.MenuOpen) d.ToggleTablet();

                // 3) Closing: KSA profile.
                d.EndShift();
                while (!d.Quiz.Done) d.AnswerQuiz(d.Quiz.Current.Correct);
                d.ExplainBack(0);
                yield return null;
                Shot(p.View, $"ep{ep}_3_profile");
                Debug.Log($"[Showcase] EP{ep} target={target.Id} at={at}");
            }
            GameSettings.Guidance = -1;
        }
    }
}
