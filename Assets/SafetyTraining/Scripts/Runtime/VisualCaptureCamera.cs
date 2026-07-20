using System;
using System.IO;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    internal static class VisualCaptureCamera
    {
        const int CaptureWidth = 1168;
        const int CaptureHeight = 692;

        public static void ConfigureForCapture(Camera viewer)
        {
            foreach (var behaviour in viewer.transform.root.GetComponentsInChildren<Behaviour>(true))
                if (behaviour is DesktopExplorerController ||
                    behaviour.GetType().Name.Contains("TrackedPoseDriver", StringComparison.Ordinal))
                    behaviour.enabled = false;
            viewer.rect = new Rect(0f, 0f, 1f, 1f);
            viewer.targetTexture = null;
            viewer.stereoTargetEye = StereoTargetEyeMask.None;
            viewer.clearFlags = CameraClearFlags.Skybox;
            HideNonHudRigRenderers(viewer);
        }

        public static void HideNonHudRigRenderers(Camera viewer)
        {
            foreach (var rigRenderer in viewer.transform.root.GetComponentsInChildren<Renderer>(true))
                if (rigRenderer.GetComponentInParent<TrainingHud>() == null)
                    rigRenderer.enabled = false;
            foreach (var rigCanvas in viewer.transform.root.GetComponentsInChildren<Canvas>(true))
                if (rigCanvas.GetComponentInParent<TrainingHud>() == null &&
                    rigCanvas.GetComponentInParent<NpcChatPanel>() == null)
                    rigCanvas.enabled = false;
        }

        public static void CapturePng(Camera viewer, string path)
        {
            HideNonHudRigRenderers(viewer);
            var previousActive = RenderTexture.active;
            var target = new RenderTexture(CaptureWidth, CaptureHeight, 24, RenderTextureFormat.ARGB32);
            var image = new Texture2D(CaptureWidth, CaptureHeight, TextureFormat.RGB24, false);
            var captureObject = new GameObject("Visual QA Capture Camera");
            var capture = captureObject.AddComponent<Camera>();
            var canvases = UnityEngine.Object.FindObjectsByType<Canvas>(FindObjectsSortMode.None);
            var previousModes = new RenderMode[canvases.Length];
            var previousCameras = new Camera[canvases.Length];
            var previousPlaneDistances = new float[canvases.Length];
            var renderWithCapture = new bool[canvases.Length];
            try
            {
                capture.enabled = false;
                capture.stereoTargetEye = StereoTargetEyeMask.None;
                capture.clearFlags = CameraClearFlags.Skybox;
                capture.backgroundColor = viewer.backgroundColor;
                capture.cullingMask = viewer.cullingMask;
                capture.fieldOfView = viewer.fieldOfView;
                capture.nearClipPlane = viewer.nearClipPlane;
                capture.farClipPlane = viewer.farClipPlane;
                capture.allowHDR = viewer.allowHDR;
                capture.allowMSAA = viewer.allowMSAA;
                capture.targetTexture = target;
                capture.aspect = (float)CaptureWidth / CaptureHeight;
                capture.transform.SetPositionAndRotation(viewer.transform.position, viewer.transform.rotation);
                capture.rect = new Rect(0f, 0f, 1f, 1f);
                capture.pixelRect = new Rect(0f, 0f, CaptureWidth, CaptureHeight);
                for (var index = 0; index < canvases.Length; index++)
                {
                    var canvas = canvases[index];
                    if (canvas == null || !canvas.enabled || !canvas.gameObject.activeInHierarchy ||
                        canvas.renderMode != RenderMode.ScreenSpaceOverlay)
                        continue;
                    previousModes[index] = canvas.renderMode;
                    previousCameras[index] = canvas.worldCamera;
                    previousPlaneDistances[index] = canvas.planeDistance;
                    renderWithCapture[index] = true;
                    canvas.renderMode = RenderMode.ScreenSpaceCamera;
                    canvas.worldCamera = capture;
                    canvas.planeDistance = Mathf.Max(capture.nearClipPlane + 0.15f, 0.25f);
                }
                RenderTexture.active = target;
                Canvas.ForceUpdateCanvases();
                GL.Clear(true, true, new Color(0.47f, 0.62f, 0.8f));
                capture.Render();
                RenderTexture.active = target;
                image.ReadPixels(new Rect(0f, 0f, CaptureWidth, CaptureHeight), 0, 0);
                image.Apply();
                File.WriteAllBytes(path, image.EncodeToPNG());
            }
            finally
            {
                for (var index = 0; index < canvases.Length; index++)
                {
                    if (!renderWithCapture[index] || canvases[index] == null)
                        continue;
                    canvases[index].renderMode = previousModes[index];
                    canvases[index].worldCamera = previousCameras[index];
                    canvases[index].planeDistance = previousPlaneDistances[index];
                }
                RenderTexture.active = previousActive;
                UnityEngine.Object.Destroy(captureObject);
                UnityEngine.Object.Destroy(target);
                UnityEngine.Object.Destroy(image);
            }
        }
    }
}
