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
    // Face close-ups (Captures/t_face/): every crew member's head from the front, plus a 2.5 m shot through the
    // player's view (HUD included), to check eyes, blinks and anything drawn over the face. Explicit, on demand.
    [Explicit]
    public sealed class FaceCaptureTests
    {
        static void Shot(Camera cam, string name, int w = 960, int h = 720)
        {
            var rt = new RenderTexture(w, h, 24); var old = cam.targetTexture; cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_face"); System.IO.File.WriteAllBytes($"Captures/t_face/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = old; Object.Destroy(rt); Object.Destroy(tex);
        }

        static Transform Head(Transform root) =>
            root.GetComponentsInChildren<Transform>(true).FirstOrDefault(t => t.name.EndsWith("Head") && !t.name.Contains("Top"));

        [UnityTest]
        public IEnumerator Crew_Faces()
        {
            EpisodeDirector.Selected = Episodes.Get(1); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var d = Object.FindFirstObjectByType<ShiftDirector>();
            foreach (CheckInStation.Kind k in System.Enum.GetValues(typeof(CheckInStation.Kind))) d.CheckIn(k);
            d.SubmitHierarchy(HierarchyOrdering.Correct);
            while (!d.Quiz.Done) d.AnswerQuiz(d.Quiz.Current.Correct);
            d.Begin(); yield return new WaitForSeconds(0.5f);
            var p = Object.FindFirstObjectByType<SitePlayer>();

            var camGo = new GameObject("FaceCam", typeof(Camera));
            var cam = camGo.GetComponent<Camera>(); cam.fieldOfView = 30f; cam.nearClipPlane = 0.02f; cam.enabled = false;
            var log = new System.Text.StringBuilder();
            var i = 0;
            foreach (var crew in Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None).OrderBy(c => c.DisplayName))
            {
                var head = Head(crew.transform);
                var face = crew.GetComponentInChildren<NpcFace>();
                var smr = crew.GetComponentsInChildren<SkinnedMeshRenderer>(true).FirstOrDefault(r => r.sharedMesh != null && r.sharedMesh.GetBlendShapeIndex("blink_L") >= 0);
                log.AppendLine($"{crew.DisplayName}: head={(head != null ? head.name : "none")} face={(face != null && face.HasFace)} eyes={(face != null ? face.EyeCount : 0)} {(face != null ? face.EyeDebug : "")} faceObj={(face != null ? face.name + " fwd " + face.transform.forward : "")} root fwd={crew.transform.forward} blendshapes={(smr != null ? smr.sharedMesh.blendShapeCount : 0)} " +
                               $"renderers={string.Join(",", crew.GetComponentsInChildren<Renderer>(true).Select(r => r.name + (r.enabled && r.gameObject.activeInHierarchy ? "" : "(off)")))}");
                var c = head != null ? head.position + Vector3.up * 0.05f : crew.transform.position + Vector3.up * 1.62f;
                var fwd = crew.transform.forward; fwd.y = 0; fwd.Normalize();
                cam.transform.position = c + fwd * 0.75f; cam.transform.LookAt(c);
                var slug = crew.DisplayName.Split(' ')[0];
                Shot(cam, $"{i:00}_{slug}_face");
                if (smr != null)
                {   // eyes shut (blink shape) to see what the lids do
                    int bl = smr.sharedMesh.GetBlendShapeIndex("blink_L"), br = smr.sharedMesh.GetBlendShapeIndex("blink_R");
                    if (face != null) face.enabled = false;
                    smr.SetBlendShapeWeight(bl, 100); smr.SetBlendShapeWeight(br, 100);
                    foreach (var eo in crew.GetComponentsInChildren<Transform>(true).Where(t => t.name == "EyeOverlay")) eo.gameObject.SetActive(false);
                    yield return null;                          // skinning (blend shapes) is applied once per frame
                    Shot(cam, $"{i:00}_{slug}_blink");
                    smr.SetBlendShapeWeight(bl, 0); smr.SetBlendShapeWeight(br, 0);
                    if (face != null) face.enabled = true;
                    yield return null;
                }
                // Through the player's eyes, 2.5 m away (HUD, name tag and any marker over the face).
                var cc = p.GetComponent<CharacterController>(); cc.enabled = false;
                var at = crew.transform.position + fwd * 2.5f; at.y = crew.transform.position.y + 0.05f;
                p.transform.position = at; p.transform.LookAt(new Vector3(crew.transform.position.x, at.y, crew.transform.position.z)); cc.enabled = true;
                p.View.transform.localRotation = Quaternion.Euler(-2f, 0, 0);
                yield return null; yield return null;
                Shot(p.View, $"{i:00}_{slug}_view");
                i++;
            }
            System.IO.File.WriteAllText("Captures/t_face/report.txt", log.ToString());
            Object.Destroy(camGo);
            Assert.Greater(i, 0, "crew found");
        }
    }
}
