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
    // Background crew variety (bodies, hard hats, skin tones) and the world-space interaction FX.
    // Captures: Captures/t_variety/ (crew close-ups, FX frame) for visual review.
    public sealed class VarietyTests
    {
        static IEnumerator Load(int episode)
        {
            EpisodeDirector.Selected = Episodes.Get(episode); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
        }

        static void Shot(Camera cam, string name)
        {
            var rt = new RenderTexture(1280, 720, 24); var old = cam.targetTexture; cam.targetTexture = rt;
            cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_variety"); System.IO.File.WriteAllBytes($"Captures/t_variety/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = old; Object.Destroy(rt); Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator BackgroundCrew_AreVaried_AndStayRigged()
        {
            yield return Load(2);
            yield return null;
            var bg = Object.FindObjectsByType<CrewGestures>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(CrewVariety.IsBackgroundRocketbox).ToList();
            Assert.That(bg.Count, Is.GreaterThanOrEqualTo(3), "background crew found");
            Assert.That(CrewVariety.Varied, Is.EqualTo(bg.Count), "every background worker was varied");
            Assert.That(CrewVariety.Females, Is.GreaterThan(0), "some background crew use the female body");
            foreach (var g in bg)
            {
                // Inactive episode roots have not run Awake yet; their skeleton must still be complete.
                if (g.isActiveAndEnabled) Assert.That(g.Rigged, Is.True, g.name + " bones found after the swap");
                Assert.That(g.GetComponentsInChildren<Transform>(true).Count(t => t.name == "Bip01 R Hand" || t.name == "Bip01 L Hand" || t.name == "Bip01 Head"), Is.EqualTo(3), g.name + " has one head and two hands");
                Assert.That(g.transform.Cast<Transform>().Count(c => c.name == "Bip01"), Is.EqualTo(1), g.name + " has exactly one skeleton");
            }
            Color HatOf(CrewGestures g) => g.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.name.ToLowerInvariant().Contains("helmet")).Select(CrewVariety.TintOf).DefaultIfEmpty(Color.clear).First();
            Color SkinOf(CrewGestures g) => g.GetComponentsInChildren<SkinnedMeshRenderer>(true).SelectMany(r => r.sharedMaterials)
                .Where(m => m != null && m.name.ToLowerInvariant().Contains("head")).Select(CrewVariety.TintOf).DefaultIfEmpty(Color.clear).First();
            Assert.That(bg.Select(HatOf).Distinct().Count(), Is.GreaterThanOrEqualTo(3), "at least three hard-hat colours");
            Assert.That(bg.Select(SkinOf).Distinct().Count(), Is.GreaterThanOrEqualTo(2), "at least two skin tones");

            // Close-ups of the first visible workers for review.
            var view = Object.FindFirstObjectByType<SitePlayer>().View;
            var cam = new GameObject("VarietyCam").AddComponent<Camera>(); cam.CopyFrom(view); cam.enabled = false;
            var i = 0;
            foreach (var g in bg.Where(g => g.isActiveAndEnabled).Take(6))
            {
                var fwd = g.transform.forward; fwd.y = 0; fwd.Normalize();
                cam.transform.SetPositionAndRotation(g.transform.position + fwd * 2.6f + Vector3.up * 1.6f, Quaternion.LookRotation(-fwd + Vector3.down * 0.12f));
                Shot(cam, $"crew_{i++}_{g.name}");
                yield return null;
            }
        }

        [UnityTest]
        public IEnumerator InteractionFx_SpawnInTheWorld_AndToast()
        {
            yield return Load(1);
            yield return new WaitForSeconds(0.3f);
            var cam = Object.FindFirstObjectByType<SitePlayer>().View;
            var at = cam.transform.position + cam.transform.forward * 3f + Vector3.down * 0.6f;
            var before = SiteFx.Bursts;
            SiteFx.Pulse(at, new Color(0.2f, 0.9f, 1f));
            SiteFx.Burst(at + Vector3.up * 0.3f, new Color(0.3f, 1f, 0.4f), 24);
            SiteFx.Check(at + Vector3.up * 0.5f);
            SiteFx.Toast("Crew request done: Dolores");
            yield return new WaitForSeconds(0.25f);
            Assert.That(SiteFx.Bursts, Is.EqualTo(before + 1));
            Assert.That(SiteFx.LastToast, Does.Contain("Crew request done"));
            Assert.That(Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Any(p => p.particleCount > 0), "burst particles alive");
            Shot(cam, "fx_frame");
        }

        [UnityTest]
        public IEnumerator FlatColourCubes_GetChamferedEdges_FacingOutward()
        {
            yield return Load(1);
            var (prims, chamfered, total) = PrimitivePolish.Census(SceneManager.GetActiveScene());
            System.IO.Directory.CreateDirectory("Captures/t_variety");
            System.IO.File.WriteAllText("Captures/t_variety/primitives.txt",
                $"renderers={total} untreated_primitives={prims} ({(float)prims / Mathf.Max(1, total):P0}) chamfered={chamfered}\n");
            Debug.Log($"[PrimitivePolish] renderers={total} primitives={prims} chamfered={chamfered}");
            Assert.That(chamfered, Is.GreaterThan(20), "flat-colour cubes were chamfered");
            var mesh = Object.FindObjectsByType<MeshFilter>(FindObjectsSortMode.None).Select(f => f.sharedMesh).First(m => m != null && m.name == "ChamferBox");
            var v = mesh.vertices; var t = mesh.triangles;
            Assert.That(t.Length / 3, Is.EqualTo(6 * 2 + 12 * 2 + 8), "6 faces, 12 chamfers, 8 corners");
            for (var k = 0; k < t.Length; k += 3)
            {
                Vector3 a = v[t[k]], b = v[t[k + 1]], c = v[t[k + 2]];
                Assert.That(Vector3.Dot((a + b + c) / 3f, Vector3.Cross(b - a, c - a)), Is.GreaterThan(0f), "triangle " + k / 3 + " faces outward");
            }
            var cam = Object.FindFirstObjectByType<SitePlayer>().View;
            yield return null;
            Shot(cam, "ep1_view_chamfered");
        }

        [UnityTest]
        public IEnumerator Props_ReadAsObjects_NotBoxes()
        {
            yield return Load(2);
            yield return null;
            Assert.That(PropDetail.Built, Is.GreaterThan(60), "container / tank / trench box detail built");
            var view = Object.FindFirstObjectByType<SitePlayer>().View;
            var cam = new GameObject("PropCam").AddComponent<Camera>(); cam.CopyFrom(view); cam.enabled = false; cam.fieldOfView = 50f;
            void Frame(string name, Vector3 target, Vector3 offset)
            {
                cam.transform.position = target + offset;
                cam.transform.rotation = Quaternion.LookRotation(target - cam.transform.position);
                Shot(cam, name);
            }
            var all = Object.FindObjectsByType<MeshRenderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            var conex = all.FirstOrDefault(r => r.name == "conex");
            if (conex != null) Frame("prop_conex", conex.transform.position, new Vector3(5.5f, 1.2f, -4.5f));
            var fuel = all.FirstOrDefault(r => r.name == "fuel");
            if (fuel != null)
            {
                Assert.That(fuel.enabled, Is.False, "the fuel box is replaced by the tank");
                Frame("prop_fuel", fuel.transform.position, new Vector3(3.2f, 1.4f, -3.2f));
            }
            var wall = all.FirstOrDefault(r => r.name == "TrenchBox_WallW" && r.gameObject.activeInHierarchy);
            if (wall != null) Frame("prop_trenchbox", wall.transform.position + new Vector3(0.6f, 0, 0), new Vector3(0.3f, 3.2f, -3.6f));
            Assert.That(all.Where(r => r.name == "Spreader" && r.gameObject.activeInHierarchy).All(r => !r.enabled), "spreaders drawn as pipes");
            Object.Destroy(cam.gameObject);
        }
    }
}
