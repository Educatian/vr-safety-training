using System;
using System.Collections;
using System.IO;
using System.Linq;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    public sealed class VisualCaptureTour : MonoBehaviour
    {
        const string EnabledVariable = "SAFETY_CAPTURE_TOUR";
        const string DirectoryVariable = "SAFETY_CAPTURE_DIR";
        const string StartDelayVariable = "SAFETY_CAPTURE_START_DELAY";
        const string HoldVariable = "SAFETY_CAPTURE_HOLD_SECONDS";

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void StartWhenRequested()
        {
            if (!string.Equals(Environment.GetEnvironmentVariable(EnabledVariable), "1", StringComparison.Ordinal))
                return;

            new GameObject("Visual Capture Tour").AddComponent<VisualCaptureTour>();
        }

        IEnumerator Start()
        {
            var outputDirectory = Environment.GetEnvironmentVariable(DirectoryVariable);
            if (string.IsNullOrWhiteSpace(outputDirectory))
                outputDirectory = Path.GetFullPath(Path.Combine(Application.dataPath, "..", "Captures"));
            Directory.CreateDirectory(outputDirectory);

            var startDelay = ReadSeconds(StartDelayVariable, 0f);
            if (startDelay > 0f)
                yield return new WaitForSecondsRealtime(startDelay);
            yield return WaitForSceneReady();
            var viewer = Camera.main;
            if (viewer == null)
            {
                Debug.LogError("Visual capture tour could not find the Main Camera.");
                yield break;
            }

            UnityEngine.XR.XRSettings.enabled = false;
            VisualCaptureCamera.ConfigureForCapture(viewer);
            yield return CaptureView(viewer, outputDirectory, "01-campus-hub.png",
                new Vector3(0f, 2.2f, -10.2f), new Vector3(0f, 0.9f, -3.5f));

            PrepareSiteCapture(SafetyTraining.Core.TrainingSiteId.Construction);
            var constructionOrigin = SiteOrigin("Construction Site");
            yield return CaptureView(viewer, outputDirectory, "02-construction-overview.png",
                constructionOrigin + new Vector3(0f, 2.9f, -3.5f), constructionOrigin + new Vector3(0f, 1f, 0.4f));
            yield return CapturePropShowcase(viewer, outputDirectory, "02b-construction-real-props.png", constructionOrigin, 0f, true);
            yield return CaptureCustomPropCloseups(viewer, outputDirectory, "Construction Site", "construction");
            yield return CaptureLearningBoard(viewer, outputDirectory,
                "02c-construction-learning-objectives.png", SafetyTraining.Core.TrainingSiteId.Construction);
            yield return CaptureEngineeringStations(viewer, outputDirectory);
            var constructionHazard = FindObjectsByType<InspectionTarget>(FindObjectsSortMode.None)
                .FirstOrDefault(target => target.TargetId == "fall-edge");
            if (constructionHazard != null)
                TrainingCoordinator.Instance.Inspect(constructionHazard);
            yield return new WaitForSecondsRealtime(0.8f);
            ShowCaptureHud();
            yield return CaptureView(viewer, outputDirectory, "03-construction-feedback.png",
                constructionOrigin + new Vector3(0f, 2.7f, -3.45f), constructionOrigin + new Vector3(0f, 0.85f, -1.2f));
            TrainingCoordinator.Instance.SetHandsOnFeedback(
                "Hands-on 1/5: PPE secured. Next, place the exclusion barricade.");
            yield return new WaitForSecondsRealtime(0.5f);
            ShowCaptureHud();
            yield return CaptureView(viewer, outputDirectory, "03b-construction-hands-on.png",
                constructionOrigin + new Vector3(0f, 2.7f, -3.45f), constructionOrigin + new Vector3(0f, 0.85f, -1.2f));
            yield return CaptureNpc(viewer, outputDirectory, "Construction Site", "04-construction-npc.png");

            PrepareSiteCapture(SafetyTraining.Core.TrainingSiteId.Warehouse);
            var warehouseOrigin = SiteOrigin("Warehouse");
            yield return CaptureView(viewer, outputDirectory, "05-warehouse-overview.png",
                warehouseOrigin + new Vector3(0f, 2.9f, -3.5f), warehouseOrigin + new Vector3(0f, 1f, 0.4f));
            yield return CapturePropShowcase(viewer, outputDirectory, "05b-warehouse-real-props.png", warehouseOrigin, 0f);
            yield return CaptureCustomPropCloseups(viewer, outputDirectory, "Warehouse", "warehouse");
            yield return CaptureNpc(viewer, outputDirectory, "Warehouse", "06-warehouse-npc.png");

            PrepareSiteCapture(SafetyTraining.Core.TrainingSiteId.FireResponse);
            var fireOrigin = SiteOrigin("Fire Response");
            yield return CaptureView(viewer, outputDirectory, "07-fire-response-overview.png",
                fireOrigin + new Vector3(0f, 2.9f, -3.5f), fireOrigin + new Vector3(0f, 1f, 0.4f));
            yield return CapturePropShowcase(viewer, outputDirectory, "07b-fire-response-real-props.png", fireOrigin, 0f);
            yield return CaptureCustomPropCloseups(viewer, outputDirectory, "Fire Response", "fire");
            yield return CaptureNpc(viewer, outputDirectory, "Fire Response", "08-fire-response-npc.png");

            PrepareSiteCapture(SafetyTraining.Core.TrainingSiteId.ChemicalProcessing);
            var chemicalOrigin = SiteOrigin("Chemical Processing");
            yield return CaptureView(viewer, outputDirectory, "09-chemical-overview.png",
                chemicalOrigin + new Vector3(0f, 2.9f, -3.5f), chemicalOrigin + new Vector3(0f, 1f, 0.4f));
            yield return CapturePropShowcase(viewer, outputDirectory, "09b-chemical-real-props.png",
                chemicalOrigin);
            yield return CaptureCustomPropCloseups(viewer, outputDirectory, "Chemical Processing", "chemical");
            yield return CaptureNpc(viewer, outputDirectory, "Chemical Processing", "10-chemical-npc.png");

            PrepareSiteCapture(SafetyTraining.Core.TrainingSiteId.ElectricalMaintenance);
            var electricalOrigin = SiteOrigin("Electrical Maintenance");
            yield return CaptureView(viewer, outputDirectory, "11-electrical-overview.png",
                electricalOrigin + new Vector3(0f, 2.9f, -3.5f), electricalOrigin + new Vector3(0f, 1f, 0.4f));
            yield return CapturePropShowcase(viewer, outputDirectory, "11b-electrical-real-props.png", electricalOrigin, -3.65f);
            yield return CaptureCustomPropCloseups(viewer, outputDirectory, "Electrical Maintenance", "electrical");
            yield return CaptureNpc(viewer, outputDirectory, "Electrical Maintenance", "12-electrical-npc.png");

            File.WriteAllText(Path.Combine(outputDirectory, "tour-complete.txt"),
                DateTime.UtcNow.ToString("O"));
            Debug.Log($"Visual capture tour completed: {outputDirectory}");
            if (Application.isBatchMode)
                Application.Quit(0);
        }

        static IEnumerator WaitForSceneReady()
        {
            var deadline = Time.realtimeSinceStartup + 12f;
            while ((Camera.main == null || TrainingCoordinator.Instance == null) &&
                   Time.realtimeSinceStartup < deadline)
                yield return null;
            yield return new WaitForSecondsRealtime(1f);
        }

        static float ReadSeconds(string variable, float fallback)
        {
            return float.TryParse(Environment.GetEnvironmentVariable(variable),
                System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var seconds)
                ? Mathf.Max(0f, seconds)
                : fallback;
        }

        static Vector3 SiteOrigin(string siteName)
        {
            var site = GameObject.Find(siteName);
            return site != null ? site.transform.position : Vector3.zero;
        }

        public static void PrepareSiteCapture(SafetyTraining.Core.TrainingSiteId siteId)
        {
            (NpcChatPanel.Instance ?? FindFirstObjectByType<NpcChatPanel>())?.Close();
            var isolation = SiteIsolationController.Instance ?? FindFirstObjectByType<SiteIsolationController>();
            isolation?.ShowSite(siteId);
            var director = FindFirstObjectByType<SiteExperienceDirector>();
            if (director != null)
                director.enabled = false;
            var coordinator = TrainingCoordinator.Instance ?? FindFirstObjectByType<TrainingCoordinator>();
            coordinator?.EnterSite(siteId);
            ShowCaptureHud();
        }

        static void ShowCaptureHud()
        {
            var hud = FindObjectsByType<TrainingHud>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();
            if (hud != null)
            {
                hud.gameObject.SetActive(true);
                hud.SetVisible(true);
            }
        }

        static void HideCaptureHud()
        {
            var hud = FindObjectsByType<TrainingHud>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .FirstOrDefault();
            if (hud != null)
                hud.SetVisible(false);
        }

        static IEnumerator CaptureNpc(
            Camera viewer,
            string directory,
            string siteObjectName,
            string fileName)
        {
            HideCaptureHud();
            var site = GameObject.Find(siteObjectName);
            var coach = site != null ? site.GetComponentInChildren<NpcConversationAgent>() : null;
            if (coach != null && VisualCaptureFraming.TryGetBounds(coach.gameObject, out var idleBounds))
            {
                coach.GetComponent<NpcRelaxedPose>()?.ReturnToIdle();
                yield return new WaitForSecondsRealtime(0.4f);
                var idleDistance = Mathf.Max(3.2f, idleBounds.size.y * 1.65f);
                var idleFocus = idleBounds.center + Vector3.up * 0.65f;
                var idleFileStem = Path.GetFileNameWithoutExtension(fileName);
                yield return CaptureView(viewer, directory, $"{idleFileStem}-rest.png",
                    idleFocus + Vector3.back * idleDistance,
                    idleFocus);
                var pose = coach.GetComponent<NpcRelaxedPose>();
                pose?.SetMoving(true);
                yield return new WaitForSecondsRealtime(0.3f);
                yield return CaptureView(viewer, directory, $"{idleFileStem}-walking-a.png",
                    idleFocus + Vector3.back * idleDistance,
                    idleFocus);
                yield return new WaitForSecondsRealtime(0.35f);
                yield return CaptureView(viewer, directory, $"{idleFileStem}-walking-b.png",
                    idleFocus + Vector3.back * idleDistance,
                    idleFocus);
                pose?.SetMoving(false);
                yield return new WaitForSecondsRealtime(0.35f);
            }
            var initialReply = coach?.LastReply;
            site?.GetComponentInChildren<NpcTalkInteractable>()?.Ask();
            var replyDeadline = Time.realtimeSinceStartup + 15f;
            while (coach != null && coach.LastReply == initialReply &&
                   Time.realtimeSinceStartup < replyDeadline)
                yield return null;

            if (coach != null && VisualCaptureFraming.TryGetBounds(coach.gameObject, out var bounds))
            {
                var distance = Mathf.Max(3.2f, bounds.size.y * 1.65f);
                var dialogueFocus = bounds.center + Vector3.up * 0.65f;
                var fileStem = Path.GetFileNameWithoutExtension(fileName);
                coach.GetComponent<NpcRelaxedPose>()?.PlayEncouragement(false);
                yield return new WaitForSecondsRealtime(1.3f);
                yield return CaptureView(viewer, directory, fileName,
                    dialogueFocus + Vector3.back * distance,
                    dialogueFocus);
                (NpcChatPanel.Instance ?? FindFirstObjectByType<NpcChatPanel>())?.CloseFor(coach);
                yield return new WaitForSecondsRealtime(2f);
                coach.GetComponent<NpcRelaxedPose>()?.ReturnToIdle();
                yield return new WaitForSecondsRealtime(0.4f);
                yield return CaptureView(viewer, directory, $"{fileStem}-settled.png",
                    dialogueFocus + Vector3.back * distance,
                    dialogueFocus);
                yield break;
            }

            var origin = site != null ? site.transform.position : Vector3.zero;
            yield return CaptureView(viewer, directory, fileName,
                origin + new Vector3(0f, 2.4f, -1.4f),
                origin + new Vector3(0f, 1.5f, 2.8f));
        }

        static IEnumerator CapturePropShowcase(
            Camera viewer,
            string directory,
            string fileName,
            Vector3 siteOrigin,
            float cameraX = 3.65f,
            bool hideCraneEvidence = false)
        {
            var hud = FindFirstObjectByType<TrainingHud>();
            if (hud != null)
                hud.SetVisible(false);
            var craneEvidence = hideCraneEvidence
                ? GameObject.Find("Inquiry Evidence - Crane swing radius evidence")
                : null;
            var hiddenRenderers = craneEvidence == null
                ? Array.Empty<Renderer>()
                : craneEvidence.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled).ToArray();
            foreach (var renderer in hiddenRenderers)
                renderer.enabled = false;
            var previousFieldOfView = viewer.fieldOfView;
            viewer.fieldOfView = 80f;
            yield return CaptureView(viewer, directory, fileName,
                siteOrigin + new Vector3(cameraX, 3.35f, -4.05f),
                siteOrigin + new Vector3(0f, 0.9f, 0.35f));
            viewer.fieldOfView = previousFieldOfView;
            foreach (var renderer in hiddenRenderers)
                renderer.enabled = true;
            if (hud != null)
                hud.SetVisible(true);
        }
        static IEnumerator CaptureCustomPropCloseups(
            Camera viewer,
            string directory,
            string siteObjectName,
            string siteSlug)
        {
            HideCaptureHud();
            var site = GameObject.Find(siteObjectName);
            if (site == null)
                yield break;
            var props = site.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith("RealAsset - US_", StringComparison.Ordinal))
                .OrderBy(item => item.name)
                .ToArray();
            foreach (var prop in props)
            {
                if (!VisualCaptureFraming.TryGetBounds(prop.gameObject, out var bounds))
                    continue;
                var focus = bounds.center + Vector3.up * bounds.extents.y * 0.08f;
                var distance = Mathf.Max(2.4f, bounds.extents.magnitude * 2.35f);
                var direction = new Vector3(0.78f, 0.38f, -1f).normalized;
                var assetSlug = prop.name.Replace("RealAsset - ", string.Empty)
                    .Replace(" ", "-").ToLowerInvariant();
                yield return CaptureView(viewer, directory,
                    $"asset-{siteSlug}-{assetSlug}.png", focus + direction * distance, focus);
            }
            ShowCaptureHud();
        }

        static IEnumerator CaptureLearningBoard(Camera viewer, string directory, string fileName,
            SafetyTraining.Core.TrainingSiteId siteId)
        {
            HideCaptureHud();
            var board = FindObjectsByType<LearningObjectiveBoard>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.SiteId == siteId);
            if (board != null && VisualCaptureFraming.TryGetBounds(board.gameObject, out var bounds))
            {
                var focus = bounds.center;
                yield return CaptureView(viewer, directory, fileName,
                    focus - board.transform.forward * 3.8f + Vector3.up * 0.1f, focus);
            }
            ShowCaptureHud();
        }

        static IEnumerator CaptureEngineeringStations(Camera viewer, string directory)
        {
            var stations = FindObjectsByType<EngineeringDecisionStation>(FindObjectsSortMode.None)
                .OrderBy(item => item.transform.position.z).ToArray();
            for (var index = 0; index < stations.Length; index++)
            {
                HideCaptureHud();
                var station = stations[index];
                if (VisualCaptureFraming.TryGetBounds(station.gameObject, out var bounds))
                {
                    var focus = bounds.center + Vector3.up * 0.05f;
                    yield return CaptureView(viewer, directory,
                        $"02{(char)('d' + index)}-construction-{station.DecisionId}.png",
                        focus - station.transform.forward * 4.0f, focus);
                }
            }

            var formwork = stations.FirstOrDefault(item => item.DecisionId == "formwork-capacity");
            if (formwork != null && VisualCaptureFraming.TryGetBounds(formwork.gameObject, out var formworkBounds))
            {
                var options = formwork.GetComponentsInChildren<EngineeringDecisionOption>(true);
                options.FirstOrDefault(item => !item.IsCorrect)?.Select();
                yield return new WaitForSecondsRealtime(0.25f);
                HideCaptureHud();
                var focus = formworkBounds.center;
                yield return CaptureView(viewer, directory, "02g-formwork-diagnostic-feedback.png",
                    focus - formwork.transform.forward * 4.0f, focus);
                options.FirstOrDefault(item => item.IsCorrect)?.Select();
                yield return new WaitForSecondsRealtime(0.35f);
                ShowCaptureHud();
                yield return CaptureView(viewer, directory, "02h-formwork-verified-hud.png",
                    focus - formwork.transform.forward * 4.0f, focus);
            }
            ShowCaptureHud();
        }

        static IEnumerator CaptureView(
            Camera viewer,
            string directory,
            string fileName,
            Vector3 position,
            Vector3 focus)
        {
            VisualCaptureCamera.HideNonHudRigRenderers(viewer);
            viewer.rect = new Rect(0f, 0f, 1f, 1f);
            viewer.targetTexture = null;
            viewer.transform.SetPositionAndRotation(position, Quaternion.LookRotation(focus - position));
            yield return null;
            VisualCaptureCamera.CapturePng(viewer, Path.Combine(directory, fileName));
            yield return new WaitForSecondsRealtime(ReadSeconds(HoldVariable, 1f));
        }
    }
}
