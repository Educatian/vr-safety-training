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
    // Photographs every crew gesture so pose directions can be checked by eye (Captures/t_gestures/).
    public sealed class GestureTests
    {
        static Camera cam;

        static void Shot(Transform who, string name, float dist = 3.2f, float side = 0.6f, float up = 0.2f)
        {
            if (cam == null) { cam = new GameObject("GestureCam").AddComponent<Camera>(); cam.fieldOfView = 40; }
            var target = who.position + Vector3.up * 1.1f;
            cam.transform.position = target + who.forward * dist + who.right * side + Vector3.up * up;
            cam.transform.LookAt(target);
            var rt = new RenderTexture(800, 900, 24); cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(800, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 800, 900), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_gestures"); System.IO.File.WriteAllBytes($"Captures/t_gestures/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt);
        }

        static IEnumerator Load(int episode)
        {
            EpisodeDirector.Selected = Episodes.Get(episode); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            cam = null;
        }

        [UnityTest]
        public IEnumerator CrewAndWorkers_Gestures()
        {
            yield return Load(1);
            var dolores = Object.FindObjectsByType<CrewGestures>(FindObjectsSortMode.None).First(g => g.name.Contains("Dolores"));
            yield return new WaitForSeconds(0.3f);
            Shot(dolores.transform, "01_dolores_idle");
            dolores.Wave(5); yield return new WaitForSeconds(0.4f); Shot(dolores.transform, "02_dolores_wave");
            dolores.Explain(5); yield return new WaitForSeconds(0.4f); Shot(dolores.transform, "03_dolores_explain");
            dolores.Point(dolores.transform.position + dolores.transform.right * 8f + Vector3.up, 5); yield return new WaitForSeconds(0.4f);
            Shot(dolores.transform, "04_dolores_point");

            yield return Load(2);
            yield return new WaitForSeconds(0.5f);
            var all = Object.FindObjectsByType<CrewGestures>(FindObjectsSortMode.None);
            var marcus = all.First(g => g.GetComponent<NameTag>()?.DisplayName == "Marcus Bell");
            var luis = all.First(g => g.GetComponent<NameTag>()?.DisplayName == "Luis Ortega");
            Shot(marcus.transform, "05_marcus_dig_a", 0.4f, 2.6f, 2.6f);
            yield return new WaitForSeconds(0.7f);
            Shot(marcus.transform, "06_marcus_dig_b", 0.4f, 2.6f, 2.6f);
            Shot(luis.transform, "07_luis_saw", 1.0f, 3.2f);
            var walker = all.First(g => g.transform.position.x > 57f && g.transform.position.x < 64f && g.transform.position.y > -0.5f && g.GetComponent<NameTag>() == null);
            var p0 = walker.transform.position;
            // Up to 3 s: a frame hitch or a turn at a waypoint can eat a fixed 0.8 s window (flaky on a loaded machine).
            for (var t = 0f; t < 3f && Vector3.Distance(p0, walker.transform.position) <= 0.3f; t += 0.2f) yield return new WaitForSeconds(0.2f);
            Shot(walker.transform, "08_walker", 0.5f, -4.5f, 0.4f);
            Assert.That(Vector3.Distance(p0, walker.transform.position), Is.GreaterThan(0.3f), "the swing-radius walker should move");
        }
    }
}
