using System.IO;
using Jobsite.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Jobsite.Editor
{
    // Bakes a top-down "drone survey" photo of the real scene per work day (zero runtime cost on the web).
    // Covers X -4..124, Z -4..84 m, same window as the plan minimap. Tools/minimap/compose.py adds labels + north arrow.
    public static class MinimapBaker
    {
        const float X0 = -4, Z0 = -4, W = 128, H = 88;
        const int Px = 2048, Py = 1408;   // 16 px/m

        public static void Bake()
        {
            EditorSceneManager.OpenScene("Assets/_Game/Scenes/Jobsite.unity");
            Directory.CreateDirectory("Tools/minimap/raw");
            var cam = new GameObject("MinimapBakeCam").AddComponent<Camera>();
            cam.orthographic = true; cam.orthographicSize = H / 2; cam.aspect = W / H;
            cam.transform.SetPositionAndRotation(new Vector3(X0 + W / 2, 150, Z0 + H / 2), Quaternion.Euler(90, 0, 0));
            cam.nearClipPlane = 1; cam.farClipPlane = 400;
            var rt = new RenderTexture(Px, Py, 24) { antiAliasing = 4 };
            cam.targetTexture = rt;
            foreach (var day in new[] { WorkDay.Mon, WorkDay.Tue, WorkDay.Wed, WorkDay.Thu, WorkDay.Fri })
            {
                SitePhaseController.Apply(day);
                cam.Render();
                RenderTexture.active = rt;
                var tex = new Texture2D(Px, Py, TextureFormat.RGB24, false);
                tex.ReadPixels(new Rect(0, 0, Px, Py), 0, 0); tex.Apply();
                File.WriteAllBytes($"Tools/minimap/raw/{day}.png", tex.EncodeToPNG());
                Object.DestroyImmediate(tex);
            }
            RenderTexture.active = null; cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(cam.gameObject);
            Debug.Log("[Minimap] baked 5 days");
        }
    }
}
