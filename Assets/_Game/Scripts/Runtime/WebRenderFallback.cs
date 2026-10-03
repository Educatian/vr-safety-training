using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Jobsite.Runtime
{
    // WebGL render switches read from the page URL (?pp=0&vol=0&depth=0&opaque=0), for diagnosing GPU/driver issues
    // without a rebuild. Applied to every camera, including cameras created later (cinematic).
    public sealed class WebRenderFallback : MonoBehaviour
    {
        static bool pp = true, vol = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            var url = Application.absoluteURL ?? "";
            bool Off(string key) => url.Contains(key + "=0");
            pp = !Off("pp"); vol = !Off("vol");
            if (GraphicsSettings.currentRenderPipeline is UniversalRenderPipelineAsset urp)
            {
                if (Off("depth")) urp.supportsCameraDepthTexture = false;
                if (Off("opaque")) urp.supportsCameraOpaqueTexture = false;
            }
            if (Application.platform == RuntimePlatform.WebGLPlayer)
                Debug.Log($"[WebRender] pp={pp} vol={vol} url={url}");
            var go = new GameObject("WebRenderFallback"); DontDestroyOnLoad(go); go.AddComponent<WebRenderFallback>();
        }

        private float next;
        private void Update()
        {
            if (Time.unscaledTime < next) return;
            next = Time.unscaledTime + 1f;
            foreach (var cam in Camera.allCameras)
            {
                var data = cam.GetUniversalAdditionalCameraData();
                if (data != null && data.renderPostProcessing != pp) data.renderPostProcessing = pp;
            }
            if (!vol) foreach (var v in FindObjectsByType<Volume>(FindObjectsSortMode.None).Where(v => v.enabled)) v.enabled = false;
        }
    }
}
