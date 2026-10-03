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
    // Launch stills (docs/AwesomeAiGames_Plan.md): every episode's establishing and close intro framings, in the
    // shipped lighting and a low warm "golden hour" sun, plus an in-round frame with the Hazard Hunt HUD.
    // Captures/t_hero/ -> pick one, downscale to 512 px WebP for the list entry. Lighting changes are capture-only.
    public sealed class HeroShotTests
    {
        static void Render(Camera cam, string name, int w = 1600, int h = 900)
        {
            var rt = new RenderTexture(w, h, 24); var old = cam.targetTexture; cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(w, h, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, w, h), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_hero"); System.IO.File.WriteAllBytes($"Captures/t_hero/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = old; Object.Destroy(rt); Object.Destroy(tex);
        }

        static Vector3 V(float[] v) => new Vector3(v[0], v[1], v[2]);

        [TearDown] public void Reset() => ArcadeMode.Exit();

        [UnityTest]
        public IEnumerator EveryEpisode_EstablishingAndCloseFramings_TwoLights()
        {
            var shots = 0;
            foreach (var ep in Episodes.All.Where(e => e.Playable && e.Shots.Count >= 3))
            {
                ArcadeMode.Active = true; ArcadeMode.Daily = false; ArcadeMode.Seed = 1;
                EpisodeDirector.Selected = ep; ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
                yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
                yield return new WaitForSeconds(0.4f);   // crews settle into their working poses
                var view = Object.FindFirstObjectByType<SitePlayer>().View;
                var cam = new GameObject("HeroCam").AddComponent<Camera>(); cam.CopyFrom(view); cam.enabled = false; cam.fieldOfView = 42f;
                var sun = Object.FindObjectsByType<Light>(FindObjectsSortMode.None).FirstOrDefault(l => l.type == LightType.Directional);
                foreach (var i in new[] { 0, 2 })
                {
                    var s = ep.Shots[i];
                    cam.transform.position = Vector3.Lerp(V(s.From), V(s.To), 0.6f);
                    cam.transform.rotation = Quaternion.LookRotation(V(s.LookAt) - cam.transform.position);
                    Render(cam, $"ep{ep.Number}_shot{i}_day"); shots++;
                    if (sun == null) continue;
                    var (rot, col, inten) = (sun.transform.rotation, sun.color, sun.intensity);
                    // Low sun from behind the camera's left shoulder: long shadows, warm rim on the crews.
                    sun.transform.rotation = Quaternion.Euler(11f, cam.transform.eulerAngles.y + 140f, 0f);
                    sun.color = new Color(1f, .76f, .52f); sun.intensity = inten * 1.15f;
                    Render(cam, $"ep{ep.Number}_shot{i}_golden"); shots++;
                    sun.transform.rotation = rot; sun.color = col; sun.intensity = inten;
                }
                // In-round frame with the HUD: the player stands at the close framing and looks at the same spot.
                var player = Object.FindFirstObjectByType<SitePlayer>();
                var close = ep.Shots[2];
                player.GetComponent<CharacterController>().enabled = false;
                player.transform.position = new Vector3(close.From[0], player.transform.position.y, close.From[2]);
                var look = V(close.LookAt) - view.transform.position; look.y = 0;
                if (look.sqrMagnitude > 0.01f) player.transform.rotation = Quaternion.LookRotation(look);
                yield return new WaitForSeconds(0.3f);
                Render(view, $"ep{ep.Number}_round_hud"); shots++;
                Object.Destroy(cam.gameObject);
            }
            Assert.That(shots, Is.GreaterThanOrEqualTo(10));
        }
    }
}
