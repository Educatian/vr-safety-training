using System.Collections;
using System.Linq;
using System.Reflection;
using Jobsite.Core;
using Jobsite.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jobsite.PlayTests
{
    // Machine rigging and crew body language, checked by eye (Captures/t_rig/): the excavator at each key of its dig
    // cycle from the side and from a walking eye, the crane boom at two luff angles, and gesture strips (6 frames over
    // each gesture) to see the ease in / ease out and the per-person rhythm. Explicit: run on demand.
    [Explicit("Rig / gesture captures: run by hand (Logs/claude/run_grip.bat)")]
    public sealed class RigCaptureTests
    {
        static Camera cam;
        const int W = 960, H = 600;

        static void Shot(string name, Vector3 from, Vector3 at, float fov = 40f, int w = W, int h = H)
        {
            if (cam == null) { cam = new GameObject("RigCam").AddComponent<Camera>(); cam.enabled = false; }
            cam.fieldOfView = fov; cam.nearClipPlane = 0.05f; cam.farClipPlane = 600f;
            cam.transform.position = from; cam.transform.LookAt(at);
            var rt = new RenderTexture(w, h, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_rig"); System.IO.File.WriteAllBytes($"Captures/t_rig/{name}.jpg", tex.EncodeToJPG(88));
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex);
        }

        static IEnumerator Load(int episode)
        {
            ArcadeMode.Active = true; ArcadeMode.Daily = false; ArcadeMode.Seed = 3;
            EpisodeDirector.Selected = Episodes.Get(episode); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null; yield return null;
            cam = null;
        }

        [TearDown] public void Reset() => ArcadeMode.Exit();

        [UnityTest]
        public IEnumerator Excavator_DigCycle_Keys()
        {
            yield return Load(2);
            var rig = Object.FindFirstObjectByType<ExcavatorRig>();
            Assert.That(rig, Is.Not.Null);
            rig.Running = false;
            var up = rig.transform.up;
            var house = rig.House.position;
            var boomFwd = Vector3.ProjectOnPlane(rig.Boom.GetComponentInChildren<Renderer>().bounds.center - house, up).normalized;
            var across = Vector3.Cross(up, boomFwd);
            var mid = house + boomFwd * 3.5f + up * 1.5f;
            var keys = new[] { 0f, 0.12f, 0.2f, 0.3f, 0.4f, 0.48f, 0.56f, 0.7f, 0.82f };
            foreach (var u in keys)
            {
                rig.SetCycle(u); yield return null;
                Shot($"exc_side_{u:0.00}", mid + across * 16f + up * 1.0f, mid, 42f);
                Shot($"exc_eye_{u:0.00}", house - across * 9f + boomFwd * 9f + up * 0.6f, mid - up * 1f, 55f);
            }
            // Running: a few seconds of the cycle as a strip (swing smoothing, bite shake).
            rig.Running = true;
            for (var i = 0; i < 8; i++) { yield return new WaitForSeconds(1.5f); Shot($"exc_run_{i}", house - across * 12f - boomFwd * 4f + up * 6f, mid, 50f); }
        }

        [UnityTest]
        public IEnumerator Crane_Luff_And_Cylinder()
        {
            yield return Load(4);
            var rig = Object.FindFirstObjectByType<CraneRig>();
            Assert.That(rig, Is.Not.Null);
            yield return new WaitForSeconds(0.5f);
            // Thursday's pick as the shift opens: boom over the beam bundle, hook at the sling apex.
            var load = GameObject.Find("SuspendedBeams");
            if (load != null)
            {
                var lp = load.transform.position;
                Shot("crane_pick_wide", lp + new Vector3(-22f, 6f, -14f), (lp + rig.transform.position) / 2f + Vector3.up * 4f, 50f);
                Shot("crane_pick_hook", lp + new Vector3(-7f, 2f, -5f), lp + Vector3.up * 2.5f, 50f);
                Debug.Log($"[RigCapture] hook {rig.HookPosition} load {lp} boom {rig.BoomAngle:F1} line {rig.LineLength:F1}");
            }
            var f = typeof(CraneRig).GetField("boomAngle", BindingFlags.NonPublic | BindingFlags.Instance);
            var up = rig.transform.up;
            var b = rig.Boom.GetComponentInChildren<Renderer>().bounds;
            var fwd = Vector3.ProjectOnPlane(b.center - rig.Slew.position, up).normalized;
            var across = Vector3.Cross(up, fwd);
            foreach (var a in new[] { 0f, 10f, 35f, 60f })
            {
                f.SetValue(rig, a); yield return null; yield return null;
                var c = rig.Slew.position + fwd * 5f + up * 3f;
                Shot($"crane_side_{a:00}", c + across * 26f + up * 2f, c, 45f);
                Shot($"crane_cyl_{a:00}", rig.Slew.position + across * 6f + fwd * 2f + up * 2.5f, rig.Slew.position + fwd * 2.5f + up * 2.5f, 50f);
            }
            Assert.That(rig.Boom.position.y, Is.GreaterThan(0f));
        }

        [UnityTest]
        public IEnumerator Gesture_Strips()
        {
            yield return Load(1);
            var all = Object.FindObjectsByType<CrewGestures>(FindObjectsSortMode.None);
            var g = all.First(x => x.name.Contains("Dolores"));
            yield return new WaitForSeconds(0.4f);
            var who = g.transform;
            var target = who.position + Vector3.up * 1.1f;
            var eye = target + who.forward * 3.4f + who.right * 0.7f + Vector3.up * 0.15f;
            Shot("g_idle_a", eye, target, 40f, 520, 640);
            yield return new WaitForSeconds(4f);
            Shot("g_idle_b", eye, target, 40f, 520, 640);
            string[] names = { "wave", "explain", "point", "greet", "puzzled", "workstopped", "nearmiss", "listening" };
            for (var k = 0; k < names.Length; k++)
            {
                switch (k)
                {
                    case 0: g.Wave(2.4f); break;
                    case 1: g.Explain(3f); break;
                    case 2: g.Point(who.position + who.right * 8f + Vector3.up, 2.4f); break;
                    case 3: g.React(CrewGestures.Situation.Greet); break;
                    case 4: g.React(CrewGestures.Situation.Puzzled); break;
                    case 5: g.React(CrewGestures.Situation.WorkStopped); break;
                    case 6: g.React(CrewGestures.Situation.NearMiss, who.position + who.forward * 5f); break;
                    case 7: g.React(CrewGestures.Situation.Listening); break;
                }
                for (var i = 0; i < 6; i++) { yield return new WaitForSeconds(0.45f); Shot($"g_{k}_{names[k]}_{i}", eye, target, 40f, 520, 640); }
                yield return new WaitForSeconds(1.2f);
            }
        }
    }
}
