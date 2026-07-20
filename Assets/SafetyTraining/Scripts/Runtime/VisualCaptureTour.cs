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
            yield return CaptureMissionStatus(viewer, outputDirectory);
            yield return CapturePropShowcase(viewer, outputDirectory, "02b-construction-real-props.png", constructionOrigin, 3.65f, true);
            yield return CaptureCustomPropCloseups(viewer, outputDirectory, "Construction Site", "construction");
            yield return CaptureLearningBoard(viewer, outputDirectory,
                "02c-construction-learning-objectives.png", SafetyTraining.Core.TrainingSiteId.Construction);
            PrepareGoldenEngineeringCapture();
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
                "Stage 1/6 complete: PPE secured. Next, collect four distinct field observations.");
            yield return new WaitForSecondsRealtime(0.5f);
            HideCaptureHud();
            var materialCart = GameObject.Find("Material Cart");
            var materialAction = materialCart?.GetComponent<ConstructionActionInteractable>();
            var cartStart = materialCart != null ? materialCart.transform.localPosition : Vector3.zero;
            GameObject dragPreview = null;
            if (materialCart != null && materialAction != null)
            {
                materialAction.SetCurrentStep(true);
                materialCart.transform.localPosition = Vector3.Lerp(cartStart, materialAction.TargetLocalPosition, 0.58f);
                TrainingCoordinator.Instance.SetHandsOnFeedback(
                    "DRAG ACTIVE: guide the material cart into the illuminated staging zone, then release.");
                HideCaptureHud();
                dragPreview = CreateDragPreview(materialCart.transform, materialAction.TargetLocalPosition);
            }
            if (materialCart != null && VisualCaptureFraming.TryGetBounds(materialCart, out var cartBounds))
            {
                var targetWorld = materialCart.transform.parent.TransformPoint(materialAction.TargetLocalPosition);
                var interactionBounds = cartBounds;
                if (dragPreview != null && VisualCaptureFraming.TryGetBounds(dragPreview, out var previewBounds))
                    interactionBounds.Encapsulate(previewBounds);
                interactionBounds.Encapsulate(targetWorld + Vector3.up * 0.35f);
                var dragFocus = interactionBounds.center;
                var dragDistance = Mathf.Max(4.6f, interactionBounds.extents.magnitude * 2.15f);
                var previousFieldOfView = viewer.fieldOfView;
                viewer.fieldOfView = 68f;
                yield return CaptureView(viewer, outputDirectory, "03b-construction-hands-on.png",
                    dragFocus + new Vector3(0.32f, 0.32f, -1f).normalized * dragDistance, dragFocus);
                viewer.fieldOfView = previousFieldOfView;
            }
            else
            {
                yield return CaptureView(viewer, outputDirectory, "03b-construction-hands-on.png",
                    constructionOrigin + new Vector3(0f, 2.7f, -3.45f),
                    constructionOrigin + new Vector3(0f, 0.85f, -1.2f));
            }
            if (dragPreview != null)
                Destroy(dragPreview);
            if (materialCart != null && materialAction != null)
            {
                materialCart.transform.localPosition = cartStart;
                materialAction.SetCurrentStep(false);
            }
            ShowCaptureHud();
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
            var captureDirection = coach != null && siteObjectName == "Construction Site"
                ? (coach.transform.forward + coach.transform.right * 1.2f).normalized
                : Vector3.back;
            site?.GetComponentInChildren<NpcTalkInteractable>()?.EndConversation();
            var hiddenSiteLabels = HideUnrelatedLabels(site, coach);
            if (coach != null && VisualCaptureFraming.TryGetBounds(coach.gameObject, out var idleBounds))
            {
                coach.GetComponent<NpcRelaxedPose>()?.ReturnToIdle();
                yield return new WaitForSecondsRealtime(0.4f);
                var idleDistance = Mathf.Max(3.2f, idleBounds.size.y * 1.65f);
                var idleFocus = idleBounds.center + Vector3.up * 0.65f;
                var idleFileStem = Path.GetFileNameWithoutExtension(fileName);
                yield return CaptureView(viewer, directory, $"{idleFileStem}-rest.png",
                    idleFocus + captureDirection * idleDistance,
                    idleFocus);
                var pose = coach.GetComponent<NpcRelaxedPose>();
                pose?.SetMoving(true);
                yield return new WaitForSecondsRealtime(0.3f);
                pose?.PreviewMovementPose(0.18f);
                yield return CaptureView(viewer, directory, $"{idleFileStem}-walking-a.png",
                    idleFocus + captureDirection * idleDistance,
                    idleFocus);
                yield return new WaitForSecondsRealtime(0.35f);
                pose?.PreviewMovementPose(0.68f);
                yield return CaptureView(viewer, directory, $"{idleFileStem}-walking-b.png",
                    idleFocus + captureDirection * idleDistance,
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
                    dialogueFocus + captureDirection * distance,
                    dialogueFocus);
                (NpcChatPanel.Instance ?? FindFirstObjectByType<NpcChatPanel>())?.CloseFor(coach);
                yield return new WaitForSecondsRealtime(2f);
                coach.GetComponent<NpcRelaxedPose>()?.ReturnToIdle();
                yield return new WaitForSecondsRealtime(0.4f);
                yield return CaptureView(viewer, directory, $"{fileStem}-settled.png",
                    dialogueFocus + captureDirection * distance,
                    dialogueFocus);
                RestoreRenderers(hiddenSiteLabels);
                yield break;
            }

            var origin = site != null ? site.transform.position : Vector3.zero;
            yield return CaptureView(viewer, directory, fileName,
                origin + new Vector3(0f, 2.4f, -1.4f),
                origin + new Vector3(0f, 1.5f, 2.8f));
            RestoreRenderers(hiddenSiteLabels);
        }

        static IEnumerator CapturePropShowcase(
            Camera viewer,
            string directory,
            string fileName,
            Vector3 siteOrigin,
            float cameraX = 3.65f,
            bool hideCraneEvidence = false)
        {
            HideCaptureHud();
            var craneEvidence = hideCraneEvidence
                ? GameObject.Find("Inquiry Evidence - Crane swing radius evidence")
                : null;
            var siteZone = FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None)
                .OrderBy(zone => (zone.transform.position - siteOrigin).sqrMagnitude)
                .FirstOrDefault();
            Func<Renderer, bool> belongsToCustomProp = renderer =>
                renderer.GetComponentsInParent<Transform>(true)
                    .Any(ancestor => ancestor.name.StartsWith("RealAsset - US_", StringComparison.Ordinal));
            var distractingTargets = siteZone == null
                ? Array.Empty<Renderer>()
                : siteZone.GetComponentsInChildren<InspectionTarget>(true)
                    .SelectMany(target => target.GetComponentsInChildren<Renderer>(true))
                    .Where(renderer => renderer.enabled && !belongsToCustomProp(renderer))
                    .ToArray();
            var distractingActions = siteZone == null
                ? Array.Empty<Renderer>()
                : siteZone.GetComponentsInChildren<ConstructionActionInteractable>(true)
                    .SelectMany(action => action.GetComponentsInChildren<Renderer>(true))
                    .Where(renderer => renderer.enabled && !belongsToCustomProp(renderer))
                    .ToArray();
            var distractingCoaches = siteZone == null
                ? Array.Empty<Renderer>()
                : siteZone.GetComponentsInChildren<NpcConversationAgent>(true)
                    .SelectMany(coach => coach.GetComponentsInChildren<Renderer>(true))
                    .Where(renderer => renderer.enabled && !belongsToCustomProp(renderer))
                    .ToArray();
            var portableObstructions = siteZone == null
                ? Array.Empty<Renderer>()
                : siteZone.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled && !belongsToCustomProp(renderer) &&
                        renderer.GetComponentsInParent<Transform>(true).Any(ancestor =>
                            ancestor.name.IndexOf("drum", StringComparison.OrdinalIgnoreCase) >= 0 ||
                            ancestor.name.IndexOf("ladder", StringComparison.OrdinalIgnoreCase) >= 0))
                    .ToArray();
            var hiddenRenderers = (craneEvidence == null
                    ? Enumerable.Empty<Renderer>()
                    : craneEvidence.GetComponentsInChildren<Renderer>(true).Where(renderer => renderer.enabled))
                .Concat(distractingTargets)
                .Concat(distractingActions)
                .Concat(distractingCoaches)
                .Concat(portableObstructions)
                .Distinct()
                .ToArray();
            foreach (var renderer in hiddenRenderers)
                renderer.enabled = false;
            var previousFieldOfView = viewer.fieldOfView;
            viewer.fieldOfView = 72f;
            yield return CaptureView(viewer, directory, fileName,
                siteOrigin + new Vector3(cameraX, 3.15f, -3.8f),
                siteOrigin + new Vector3(0f, 1.0f, 0.65f));
            viewer.fieldOfView = previousFieldOfView;
            RestoreRenderers(hiddenRenderers);
            ShowCaptureHud();
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
            var activeBubbles = site.GetComponentsInChildren<NpcSpeechBubbleView>(true)
                .Where(view => view.gameObject.activeSelf)
                .ToArray();
            foreach (var bubble in activeBubbles)
                bubble.gameObject.SetActive(false);
            var activeSiteCanvases = site.GetComponentsInChildren<Canvas>(true)
                .Where(canvas => canvas.gameObject.activeInHierarchy)
                .ToArray();
            foreach (var canvas in activeSiteCanvases)
                canvas.gameObject.SetActive(false);
            var props = site.GetComponentsInChildren<Transform>(true)
                .Where(item => item.name.StartsWith("RealAsset - US_", StringComparison.Ordinal))
                .GroupBy(item => item.name)
                .Select(group => group.OrderByDescending(item =>
                    item.parent != null && item.parent.name.Contains("Accessible")).First())
                .OrderBy(item => item.name)
                .ToArray();
            foreach (var prop in props)
            {
                if (!VisualCaptureFraming.TryGetBounds(prop.gameObject, out var bounds))
                    continue;
                var focus = bounds.center + Vector3.up * bounds.extents.y * 0.08f;
                var distance = Mathf.Max(1.25f, bounds.extents.magnitude * 2.2f);
                var assetSlug = prop.name.Replace("RealAsset - ", string.Empty)
                    .Replace(" ", "-").ToLowerInvariant();
                var feature = prop.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer =>
                        renderer.name.IndexOf("impalement cap", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        renderer.name.IndexOf("instruction label", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        renderer.name.IndexOf("pressure gauge", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        renderer.name.IndexOf("LOTO title", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        renderer.name.IndexOf("Emergency equipment sign", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        renderer.name.IndexOf("crash bar", StringComparison.OrdinalIgnoreCase) >= 0 ||
                        renderer.name.IndexOf("arc flash warning", StringComparison.OrdinalIgnoreCase) >= 0)
                    .OrderBy(renderer =>
                        renderer.name.IndexOf("instruction label", StringComparison.OrdinalIgnoreCase) >= 0 ? 0 :
                        renderer.name.IndexOf("pressure gauge", StringComparison.OrdinalIgnoreCase) >= 0 ? 1 : 2)
                    .ThenBy(renderer => (renderer.bounds.center - site.transform.position).sqrMagnitude)
                    .FirstOrDefault();
                var outward = feature != null ? feature.bounds.center - bounds.center : Vector3.zero;
                outward.y = 0f;
                var inward = site.transform.position - bounds.center;
                inward.y = 0f;
                var direction = outward.sqrMagnitude > 0.0025f
                    ? (outward.normalized + Vector3.up * 0.12f).normalized
                    : inward.sqrMagnitude > 0.0025f
                        ? (inward.normalized + Vector3.up * 0.12f).normalized
                        : new Vector3(0.78f, 0.38f, -1f).normalized;

                if (assetSlug.Contains("capped_rebar_bundle"))
                {
                    var rod = prop.GetComponentsInChildren<MeshFilter>(true)
                        .FirstOrDefault(filter => filter.sharedMesh != null &&
                            filter.name.IndexOf("reinforcing bar", StringComparison.OrdinalIgnoreCase) >= 0);
                    var bundleAxis = bounds.size.x >= bounds.size.z ? Vector3.right : Vector3.forward;
                    if (rod != null)
                    {
                        var rodBounds = rod.sharedMesh.bounds;
                        var localAxis = rodBounds.size.x >= rodBounds.size.y &&
                                        rodBounds.size.x >= rodBounds.size.z
                            ? Vector3.right
                            : rodBounds.size.y >= rodBounds.size.z ? Vector3.up : Vector3.forward;
                        var halfLength = Vector3.Scale(rodBounds.extents, localAxis).magnitude;
                        var endpointA = rod.transform.TransformPoint(rodBounds.center + localAxis * halfLength);
                        var endpointB = rod.transform.TransformPoint(rodBounds.center - localAxis * halfLength);
                        bundleAxis = Vector3.ProjectOnPlane(endpointB - endpointA, Vector3.up).normalized;
                    }
                    var side = Vector3.Cross(Vector3.up, bundleAxis).normalized;
                    direction = (side * 0.62f + Vector3.up * 0.78f - bundleAxis * 0.12f).normalized;
                    focus += Vector3.up * 0.04f;
                    distance = Mathf.Max(distance, bounds.extents.magnitude * 2.25f);
                }
                else if (assetSlug.Contains("abc_fire_extinguisher") && feature != null)
                {
                    var englishText = prop.GetComponentsInChildren<Renderer>(true)
                        .FirstOrDefault(renderer =>
                            renderer.name.IndexOf("English Label ABC", StringComparison.OrdinalIgnoreCase) >= 0);
                    var gauge = prop.GetComponentsInChildren<Renderer>(true)
                        .FirstOrDefault(renderer =>
                            renderer.name.IndexOf("pressure gauge", StringComparison.OrdinalIgnoreCase) >= 0);
                    var frontFeature = englishText != null ? englishText : feature;
                    focus = gauge != null
                        ? Vector3.Lerp(frontFeature.bounds.center, gauge.bounds.center, 0.42f)
                        : frontFeature.bounds.center;
                    var semanticFront = frontFeature.bounds.center - bounds.center;
                    semanticFront.y = 0f;
                    direction = semanticFront.sqrMagnitude > 0.0001f
                        ? (semanticFront.normalized + Vector3.up * 0.04f).normalized
                        : (-prop.forward + Vector3.up * 0.04f).normalized;
                    distance = Mathf.Max(1.2f, bounds.extents.magnitude * 2.25f);
                }
                else if (assetSlug.Contains("emergency_eyewash_shower"))
                {
                    var sign = prop.GetComponentsInChildren<Renderer>(true)
                        .FirstOrDefault(renderer =>
                            renderer.name.IndexOf("Emergency equipment sign",
                                StringComparison.OrdinalIgnoreCase) >= 0);
                    var label = prop.GetComponentsInChildren<Renderer>(true)
                        .FirstOrDefault(renderer =>
                            renderer.name.Equals("EYEWASH", StringComparison.OrdinalIgnoreCase));
                    var semanticFront = sign != null && label != null
                        ? label.bounds.center - sign.bounds.center
                        : -prop.forward;
                    semanticFront.y = 0f;
                    if (semanticFront.sqrMagnitude < 0.0001f)
                        semanticFront = -prop.forward;
                    var lateral = Vector3.Cross(Vector3.up, semanticFront).normalized;
                    direction = (semanticFront.normalized + lateral * 0.3f + Vector3.up * 0.12f).normalized;
                    focus = bounds.center + Vector3.up * bounds.extents.y * 0.05f;
                    distance = Mathf.Max(1.7f, bounds.extents.magnitude * 2.2f);
                }
                var hiddenContext = site.GetComponentsInChildren<Renderer>(true)
                    .Where(renderer => renderer.enabled &&
                        renderer.transform != prop && !renderer.transform.IsChildOf(prop))
                    .ToArray();
                foreach (var renderer in hiddenContext)
                    renderer.enabled = false;
                yield return CaptureView(viewer, directory,
                    $"asset-{siteSlug}-{assetSlug}.png", focus + direction * distance, focus);
                RestoreRenderers(hiddenContext);
            }
            foreach (var canvas in activeSiteCanvases)
                if (canvas != null)
                    canvas.gameObject.SetActive(true);
            foreach (var bubble in activeBubbles)
                bubble.gameObject.SetActive(true);
            ShowCaptureHud();
        }

        static GameObject CreateDragPreview(Transform item, Vector3 targetLocalPosition)
        {
            var root = new GameObject("Active Hands-On Drag Feedback");
            root.transform.position = item.position + Vector3.up * 0.92f + Vector3.left * 0.32f;
            var cyan = new Color(0.08f, 0.78f, 1f, 0.9f);
            var green = new Color(0.1f, 0.95f, 0.45f, 0.92f);
            var palm = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            palm.name = "VR Glove Grip";
            palm.transform.SetParent(root.transform, false);
            palm.transform.localScale = new Vector3(0.32f, 0.18f, 0.38f);
            palm.GetComponent<Renderer>().material.color = cyan;
            Destroy(palm.GetComponent<Collider>());
            for (var index = 0; index < 4; index++)
            {
                var finger = GameObject.CreatePrimitive(PrimitiveType.Capsule);
                finger.name = $"VR Glove Finger {index + 1}";
                finger.transform.SetParent(root.transform, false);
                finger.transform.localPosition = new Vector3((index - 1.5f) * 0.07f, -0.03f, 0.22f);
                finger.transform.localRotation = Quaternion.Euler(72f, 0f, 0f);
                finger.transform.localScale = new Vector3(0.055f, 0.13f, 0.055f);
                finger.GetComponent<Renderer>().material.color = cyan;
                Destroy(finger.GetComponent<Collider>());
            }

            var beamObject = new GameObject("VR Grab Ray");
            beamObject.transform.SetParent(root.transform, true);
            var beam = beamObject.AddComponent<LineRenderer>();
            beam.positionCount = 2;
            beam.startWidth = 0.052f;
            beam.endWidth = 0.028f;
            beam.material = new Material(Shader.Find("Sprites/Default"));
            beam.startColor = cyan;
            beam.endColor = cyan;
            beam.SetPosition(0, root.transform.position);
            beam.SetPosition(1, item.position + Vector3.up * 0.38f);

            var targetWorld = item.parent.TransformPoint(targetLocalPosition);
            var target = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            target.name = "Illuminated Material Cart Drop Target";
            target.transform.SetParent(root.transform, true);
            target.transform.position = targetWorld + Vector3.up * 0.035f;
            target.transform.localScale = new Vector3(0.72f, 0.035f, 0.72f);
            target.GetComponent<Renderer>().material.color = green;
            Destroy(target.GetComponent<Collider>());

            var routeObject = new GameObject("Drag Route To Target");
            routeObject.transform.SetParent(root.transform, true);
            var route = routeObject.AddComponent<LineRenderer>();
            route.positionCount = 2;
            route.startWidth = 0.075f;
            route.endWidth = 0.12f;
            route.material = new Material(Shader.Find("Sprites/Default"));
            route.startColor = green;
            route.endColor = green;
            route.SetPosition(0, item.position + Vector3.up * 0.12f);
            route.SetPosition(1, targetWorld + Vector3.up * 0.12f);

            var labelObject = new GameObject("Drag Active Label");
            labelObject.transform.SetParent(root.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0.38f, 0f);
            var label = labelObject.AddComponent<TextMesh>();
            label.text = "MATERIAL CART\nGRAB / DRAG ACTIVE";
            label.anchor = TextAnchor.MiddleCenter;
            label.alignment = TextAlignment.Center;
            label.characterSize = 0.075f;
            label.color = Color.white;
            labelObject.AddComponent<BillboardLabel>();
            return root;
        }
        static Renderer[] HideUnrelatedLabels(GameObject site, NpcConversationAgent coach)
        {
            if (site == null)
                return Array.Empty<Renderer>();
            var textRenderers = site.GetComponentsInChildren<TextMesh>(true)
                .Where(text => coach == null || !text.transform.IsChildOf(coach.transform))
                .Select(text => text.GetComponent<Renderer>());
            var billboardRenderers = site.GetComponentsInChildren<BillboardLabel>(true)
                .Where(label => coach == null || !label.transform.IsChildOf(coach.transform))
                .SelectMany(label => label.GetComponentsInChildren<Renderer>(true));
            var renderers = textRenderers.Concat(billboardRenderers)
                .Where(renderer => renderer != null && renderer.enabled)
                .Distinct()
                .ToArray();
            foreach (var renderer in renderers)
                renderer.enabled = false;
            return renderers;
        }

        static void RestoreRenderers(Renderer[] renderers)
        {
            foreach (var renderer in renderers)
                if (renderer != null)
                    renderer.enabled = true;
        }

        static IEnumerator CaptureMissionStatus(Camera viewer, string directory)
        {
            HideCaptureHud();
            var board = GameObject.Find("Construction Mission Status Board");
            var statusText = FindObjectsByType<TextMesh>(FindObjectsSortMode.None)
                .FirstOrDefault(text => text.text.StartsWith("MISSION STATUS", StringComparison.Ordinal))
                ?.GetComponent<Renderer>();
            var site = board?.GetComponentInParent<SiteExperienceZone>();
            var activeBubbles = site?.GetComponentsInChildren<NpcSpeechBubbleView>(true)
                .Where(view => view.gameObject.activeInHierarchy)
                .ToArray() ?? Array.Empty<NpcSpeechBubbleView>();
            foreach (var bubble in activeBubbles)
                bubble.gameObject.SetActive(false);
            (NpcChatPanel.Instance ?? FindFirstObjectByType<NpcChatPanel>())?.Close();
            var activeCanvases = FindObjectsByType<Canvas>(FindObjectsSortMode.None)
                .Where(canvas => canvas.gameObject.activeInHierarchy)
                .ToArray();
            foreach (var canvas in activeCanvases)
                canvas.gameObject.SetActive(false);
            var boardRenderer = board?.GetComponent<Renderer>();
            var hiddenWorld = FindObjectsByType<Renderer>(FindObjectsSortMode.None)
                .Where(renderer => renderer.enabled && renderer != boardRenderer && renderer != statusText)
                .ToArray();
            foreach (var renderer in hiddenWorld)
                renderer.enabled = false;
            if (board != null && VisualCaptureFraming.TryGetBounds(board, out var bounds))
            {
                var focus = bounds.center;
                yield return CaptureView(viewer, directory, "02a-construction-mission-status.png",
                    focus + Vector3.back * 3.5f + Vector3.up * 0.05f, focus);
            }
            RestoreRenderers(hiddenWorld);
            foreach (var canvas in activeCanvases)
                if (canvas != null)
                    canvas.gameObject.SetActive(true);
            foreach (var bubble in activeBubbles)
                if (bubble != null)
                    bubble.gameObject.SetActive(true);
            ShowCaptureHud();
        }

        static IEnumerator CaptureLearningBoard(Camera viewer, string directory, string fileName,
            SafetyTraining.Core.TrainingSiteId siteId)
        {
            HideCaptureHud();
            var board = FindObjectsByType<LearningObjectiveBoard>(FindObjectsSortMode.None)
                .FirstOrDefault(item => item.SiteId == siteId);
            var boardLabels = board != null
                ? board.GetComponentsInChildren<TextMesh>(true).Select(text => text.GetComponent<Renderer>()).ToHashSet()
                : new System.Collections.Generic.HashSet<Renderer>();
            var hiddenLabels = FindObjectsByType<TextMesh>(FindObjectsSortMode.None)
                .Select(text => text.GetComponent<Renderer>())
                .Where(renderer => renderer != null && renderer.enabled && !boardLabels.Contains(renderer))
                .ToArray();
            foreach (var renderer in hiddenLabels)
                renderer.enabled = false;
            if (board != null && VisualCaptureFraming.TryGetBounds(board.gameObject, out var bounds))
            {
                var focus = bounds.center;
                yield return CaptureView(viewer, directory, fileName,
                    focus - board.transform.forward * 3.8f + Vector3.up * 0.1f, focus);
            }
            RestoreRenderers(hiddenLabels);
            ShowCaptureHud();
        }

        static void PrepareGoldenEngineeringCapture()
        {
            var golden = ConstructionGoldenModuleController.Instance;
            if (golden == null)
                return;
            golden.Begin();
            golden.NotifyPracticalStep(0, "PPE check");
            foreach (var id in new[] { "fall-edge-gap", "crane-swing-radius", "material-staging", "formwork-access" })
                golden.TryCollectEvidence(id, true);
        }

        static IEnumerator CaptureEngineeringStations(Camera viewer, string directory)
        {
            var captureOccluders = new[] { "Inspection Clipboard", "Material Cart", "Barricade Gate", "Guardrail Kit" }
                .Select(GameObject.Find)
                .Where(item => item != null);
            var hiddenActionRenderers = FindObjectsByType<ConstructionActionInteractable>(FindObjectsSortMode.None)
                .SelectMany(action => action.GetComponentsInChildren<Renderer>(true))
                .Concat(captureOccluders.SelectMany(item => item.GetComponentsInChildren<Renderer>(true)))
                .Where(renderer => renderer.enabled)
                .Distinct()
                .ToArray();
            foreach (var renderer in hiddenActionRenderers)
                renderer.enabled = false;
            var stations = FindObjectsByType<EngineeringDecisionStation>(FindObjectsSortMode.None)
                .OrderBy(item => item.transform.position.z).ToArray();
            for (var index = 0; index < stations.Length; index++)
            {
                HideCaptureHud();
                var station = stations[index];
                if (VisualCaptureFraming.TryGetBounds(station.gameObject, out var bounds))
                {
                    var focus = bounds.center + Vector3.up * 0.05f;
                    var isFormwork = station.DecisionId == "formwork-capacity";
                    var distance = isFormwork ? 2.7f : 4.0f;
                    var sideOffset = isFormwork ? 0.45f : 0.9f;
                    yield return CaptureView(viewer, directory,
                        $"02{(char)('d' + index)}-construction-{station.DecisionId}.png",
                        focus - station.transform.forward * distance + station.transform.right * sideOffset, focus);
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
                    focus - formwork.transform.forward * 2.7f + formwork.transform.right * 0.45f, focus);
                options.FirstOrDefault(item => item.IsCorrect)?.Select();
                yield return new WaitForSecondsRealtime(0.35f);
                ShowCaptureHud();
                yield return CaptureView(viewer, directory, "02h-formwork-verified-hud.png",
                    focus - formwork.transform.forward * 2.7f + formwork.transform.right * 0.45f, focus);
            }
            RestoreRenderers(hiddenActionRenderers);
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
