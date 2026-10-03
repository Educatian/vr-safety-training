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
    // Tripo cast in place: each named NPC is the photo-matched model with a face rig, scaled like a person, facing
    // its post, and plays a situational combo (gesture + expression). Captures/t_npc/.
    public sealed class NpcCaptureTests
    {
        static void Shot(Camera cam, string name)
        {
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_npc"); System.IO.File.WriteAllBytes($"Captures/t_npc/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt);
        }

        [UnityTest]
        public IEnumerator Cast_IsTripo_WithFace_AndReacts()
        {
            var cases = new (int ep, string name, CrewGestures.Situation s)[]
            {
                (1, "Dolores", CrewGestures.Situation.Hint), (1, "Ray", CrewGestures.Situation.ForemanPressure),
                (2, "Marcus", CrewGestures.Situation.WorkStopped), (2, "Luis", CrewGestures.Situation.Acknowledge),
                (3, "Tasha", CrewGestures.Situation.ControlInstalled), (4, "Kiara", CrewGestures.Situation.NearMiss),
                (5, "Dale", CrewGestures.Situation.WeatherTurn),
            };
            foreach (var (ep, name, s) in cases)
            {
                EpisodeDirector.Selected = Episodes.Get(ep); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
                yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
                yield return null;
                var d = Object.FindFirstObjectByType<ShiftDirector>(); d.Begin();
                var p = Object.FindFirstObjectByType<SitePlayer>();
                var g = CrewGestures.Named(name);
                Assert.That(g, Is.Not.Null, name + " on site in EP" + ep);
                var face = g.GetComponent<NpcFace>();
                Assert.That(face != null && face.HasFace, name + " has the Tripo face rig");
                var body = g.GetComponentInChildren<SkinnedMeshRenderer>().bounds;
                Assert.That(body.size.y, Is.InRange(1.5f, 2.3f), name + " is person-sized");

                // Stand 3 m in front of them (their forward), at their feet level, and look at the face.
                var front = g.transform.position + g.transform.forward * 3f;
                var cc = p.GetComponent<CharacterController>(); cc.enabled = false;
                p.transform.position = new Vector3(front.x, g.transform.position.y + 0.05f, front.z);
                p.transform.LookAt(new Vector3(g.transform.position.x, p.transform.position.y, g.transform.position.z)); cc.enabled = true;
                p.View.transform.localRotation = Quaternion.Euler(4f, 0, 0);
                g.React(s, g.transform.position + g.transform.right * 4f + Vector3.up);
                yield return new WaitForSeconds(0.9f);
                Assert.That(g.Busy, name + " plays the " + s + " combo");
                Shot(p.View, $"{ep}_{name}_{s}");
            }
        }
    }
}
