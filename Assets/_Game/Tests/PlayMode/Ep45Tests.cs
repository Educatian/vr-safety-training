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
    // EP4 (crane + roof via stair tower) and EP5 (boom under the line, backing mixer, rebar). Captures/t_ep45/.
    public sealed class Ep45Tests
    {
        static void Shot(Camera cam, string name)
        {
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_ep45"); System.IO.File.WriteAllBytes($"Captures/t_ep45/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt);
        }

        static IEnumerator Shift(int ep)
        {
            EpisodeDirector.Selected = Episodes.Get(ep); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            Object.FindFirstObjectByType<ShiftDirector>().Begin();
            yield return null;
        }

        static void Stand(SitePlayer p, Vector3 at, Vector3 look)
        {
            var cc = p.GetComponent<CharacterController>(); cc.enabled = false;
            p.transform.position = at; p.transform.LookAt(new Vector3(look.x, at.y, look.z)); cc.enabled = true;
            var eye = at + Vector3.up * 1.6f;
            var pitch = -Mathf.Atan2(look.y - eye.y, new Vector2(look.x - eye.x, look.z - eye.z).magnitude) * Mathf.Rad2Deg;
            p.View.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        [UnityTest]
        public IEnumerator Ep4_CraneAndRoof_Ep5_UnderTheLine()
        {
            yield return Shift(4);
            var d = Object.FindFirstObjectByType<ShiftDirector>();
            var p = Object.FindFirstObjectByType<SitePlayer>();
            Assert.That(d.Conditions.Count(c => c.IsHazard), Is.EqualTo(6));
            Stand(p, new Vector3(38f, 0.05f, 36f), new Vector3(48f, 3f, 50f)); yield return null; Shot(p.View, "01_ep4_crane");
            Stand(p, new Vector3(56.5f, 0.05f, 52f), new Vector3(53.2f, 0.2f, 49.6f)); yield return null; Shot(p.View, "02_ep4_outrigger");
            Stand(p, new Vector3(43f, 0.05f, 61f), new Vector3(47f, 2.4f, 57f)); yield return null; Shot(p.View, "03_ep4_under_load");

            // Stair tower to the roof.
            Stand(p, new Vector3(85.6f, 0.05f, 24f), new Vector3(87.4f, 1.2f, 24f)); yield return null;
            d.Interact(); yield return null;
            Assert.That(p.transform.position.y, Is.GreaterThan(15f), "stair tower takes you to the roof: " + d.Notice);
            Stand(p, new Vector3(104f, 16.15f, 33.5f), new Vector3(106f, 16.8f, 37.3f)); yield return null; Shot(p.View, "04_ep4_roof_edge");
            Stand(p, new Vector3(99.5f, 16.15f, 31f), new Vector3(96f, 16.3f, 34f)); yield return null; Shot(p.View, "05_ep4_skylight");

            yield return Shift(5);
            d = Object.FindFirstObjectByType<ShiftDirector>();
            p = Object.FindFirstObjectByType<SitePlayer>();
            Assert.That(d.Conditions.Count(c => c.IsHazard), Is.EqualTo(3));
            Stand(p, new Vector3(42.5f, 0.05f, 70f), new Vector3(40f, 7.9f, 75.2f)); yield return null;
            var boom = d.Conditions.First(c => c.Id == "fri-boom-near-line");
            Assert.That(d.PhotoValid(boom), Is.True, "boom photographable from the ground");
            d.Interact(); yield return null;
            Assert.That(d.Selected, Is.EqualTo(boom), d.Notice);
            Shot(p.View, "06_ep5_boom_photo");
            d.ToggleTablet(); yield return null;
            Stand(p, new Vector3(41f, 0.05f, 60f), new Vector3(46.2f, 1f, 66f)); yield return null; Shot(p.View, "07_ep5_mixer");
            Stand(p, new Vector3(28.4f, 0.05f, 26f), new Vector3(31.4f, 0.4f, 28.5f)); yield return null; Shot(p.View, "08_ep5_rebar");
        }
    }
}
