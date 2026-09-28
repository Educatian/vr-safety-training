using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jobsite.Editor
{
    // Headless progress screenshots from fixed viewpoints (TestPlan: critic viewpoints).
    // Unity.exe -batchmode -quit -executeMethod Jobsite.Editor.ShotCapture.Run -shots Tools/shots/<set>.json
    public static class ShotCapture
    {
        [Serializable] sealed class Shot { public string name; public float[] pos; public float[] lookAt; public string target; public float[] offset; public float fov = 60f; public string day; }
        [Serializable] sealed class ShotSet { public string scene; public string outDir; public int width = 1600; public int height = 900; public Shot[] shots; }

        public static void Run()
        {
            var args = Environment.GetCommandLineArgs();
            var file = args[Array.IndexOf(args, "-shots") + 1];
            var set = JsonUtility.FromJson<ShotSet>(File.ReadAllText(file));
            EditorSceneManager.OpenScene(set.scene, OpenSceneMode.Single);
            Directory.CreateDirectory(set.outDir);

            var go = new GameObject("__ShotCamera");
            var cam = go.AddComponent<Camera>();
            var rt = new RenderTexture(set.width, set.height, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            var tex = new Texture2D(set.width, set.height, TextureFormat.RGB24, false);

            foreach (var shot in set.shots)
            {
                cam.fieldOfView = shot.fov;
                if (!string.IsNullOrEmpty(shot.day))
                    Jobsite.Runtime.SitePhaseController.Apply((Jobsite.Runtime.WorkDay)Enum.Parse(typeof(Jobsite.Runtime.WorkDay), shot.day));
                if (!string.IsNullOrEmpty(shot.target))
                {
                    // Frame a named object: camera sits at bounds centre + offset * bounds radius.
                    var bounds = BoundsOf(GameObject.Find(shot.target));
                    var radius = Mathf.Max(bounds.extents.magnitude, 1f);
                    go.transform.position = bounds.center + V(shot.offset ?? new[] { 0.6f, 0.45f, -1f }).normalized * radius * 1.6f;
                    go.transform.LookAt(bounds.center);
                }
                else
                {
                    go.transform.position = V(shot.pos);
                    go.transform.LookAt(V(shot.lookAt));
                }
                // Edit-mode capture: particles do not tick, so pre-warm them (e.g. silica dust).
                foreach (var ps in UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None))
                    ps.Simulate(3f, true, true);
                cam.Render();
                RenderTexture.active = rt;
                tex.ReadPixels(new Rect(0, 0, set.width, set.height), 0, 0);
                tex.Apply();
                File.WriteAllBytes(Path.Combine(set.outDir, shot.name + ".png"), tex.EncodeToPNG());
                Debug.Log($"[ShotCapture] {shot.name}");
            }

            RenderTexture.active = null;
            UnityEngine.Object.DestroyImmediate(go);
        }

        static Bounds BoundsOf(GameObject root)
        {
            if (root == null)
                throw new ArgumentException("Shot target not found in scene.");
            var renderers = root.GetComponentsInChildren<Renderer>();
            var b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(root.transform.position, Vector3.one);
            foreach (var r in renderers) b.Encapsulate(r.bounds);
            return b;
        }

        static Vector3 V(float[] a) => new Vector3(a[0], a[1], a[2]);
    }
}
