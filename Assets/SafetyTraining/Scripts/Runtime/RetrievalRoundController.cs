using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// After certification, re-presents the conditions the learner misjudged
    /// (false-positived look-alikes) mixed with one real hazard per affected site,
    /// as an explicit hazard/controlled reclassification pass at the original
    /// locations. Score-neutral: every judgment is telemetry, nothing changes the
    /// deterministic score or completion state.
    /// </summary>
    public sealed class RetrievalRoundController : MonoBehaviour
    {
        RetrievalRoundPlan plan;
        readonly Dictionary<string, GameObject> plateRoots = new();
        bool started;
        bool finished;
        float nextCheckAt;

        void Update()
        {
            if (finished || Time.unscaledTime < nextCheckAt)
                return;
            nextCheckAt = Time.unscaledTime + 1f;
            var coordinator = TrainingCoordinator.Instance;
            if (coordinator == null || started || !coordinator.CertificationComplete)
                return;
            started = true;
            var items = coordinator.BuildRetrievalItems();
            if (items.Length == 0)
            {
                finished = true;
                RecordRoundOutcome(TrainingSiteId.Construction,
                    "clean sweep: no misjudged conditions to recertify");
                return;
            }
            plan = new RetrievalRoundPlan(items);
            foreach (var item in plan.Items)
                CreatePlates(item);
            coordinator.SetContextFeedback(
                $"Recertification patrol\nRevisit {plan.ItemCount} flagged condition(s) and reclassify each " +
                "as HAZARD or CONTROLLED at its location.");
        }

        public void SubmitJudgment(RetrievalJudgmentPlate plate)
        {
            if (plan == null)
                return;
            var judgment = plan.Judge(plate.TargetId, plate.JudgesHazard);
            if (judgment == null)
                return;
            var coordinator = TrainingCoordinator.Instance;
            LearningOutcomeTracker.Instance?.Record(plate.SiteId,
                LearningObjectiveCatalog.ObjectiveAt(plate.SiteId, 0).Id,
                $"retrieval:{plate.TargetId}", judgment.Correct,
                $"judged={(plate.JudgesHazard ? "hazard" : "controlled")} " +
                $"first={judgment.FirstAttempt} remaining={judgment.RemainingItems}", 0);
            if (judgment.Correct)
            {
                RemovePlates(plate.TargetId);
                coordinator?.SetContextFeedback(judgment.RemainingItems == 0
                    ? "Recertification patrol complete\nEvery flagged condition was reclassified correctly."
                    : $"Reclassification verified\n{judgment.RemainingItems} flagged condition(s) remaining.");
            }
            else
            {
                coordinator?.SetContextFeedback(
                    "Reclassification review\nCompare this condition against its look-alike, then judge again.");
            }
            if (plan.IsComplete && !finished)
            {
                finished = true;
                RecordRoundOutcome(plate.SiteId,
                    $"resolved {plan.ResolvedCount}/{plan.ItemCount} flagged conditions");
            }
        }

        void RecordRoundOutcome(TrainingSiteId site, string detail)
        {
            LearningOutcomeTracker.Instance?.Record(site,
                LearningObjectiveCatalog.ObjectiveAt(site, 0).Id,
                "retrieval_round", true, detail, 0);
        }

        void CreatePlates(RetrievalItemSpec item)
        {
            var anchor = FindTargetTransform(item);
            if (anchor == null)
                return;
            var root = new GameObject($"Retrieval Plates - {item.TargetId}");
            // Parent to the target so the plates follow site isolation: they appear
            // when the learner re-enters the site, even though they are created
            // while the site is still hidden at the hub.
            root.transform.SetParent(anchor, false);
            root.transform.position = anchor.position + Vector3.up * 1.9f;
            plateRoots[item.TargetId] = root;
            CreatePlate(root.transform, item, judgesHazard: true, new Vector3(-0.55f, 0f, 0f), "HAZARD");
            CreatePlate(root.transform, item, judgesHazard: false, new Vector3(0.55f, 0f, 0f), "CONTROLLED");
        }

        void CreatePlate(Transform parent, RetrievalItemSpec item, bool judgesHazard,
            Vector3 localPosition, string caption)
        {
            var plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = $"Retrieval {caption} - {item.TargetId}";
            plate.transform.SetParent(parent, false);
            plate.transform.localPosition = localPosition;
            plate.transform.localScale = new Vector3(0.95f, 0.3f, 0.08f);
            plate.GetComponent<Renderer>().material.color = new Color(0.07f, 0.11f, 0.16f);
            plate.AddComponent<XRSimpleInteractable>();
            plate.AddComponent<InteractiveHoverFeedback>();
            plate.AddComponent<RetrievalJudgmentPlate>().Configure(this, item.SiteId,
                item.TargetId, judgesHazard);

            var labelObject = new GameObject("Label");
            labelObject.transform.SetParent(plate.transform, false);
            labelObject.transform.localPosition = new Vector3(0f, 0f, -0.6f);
            labelObject.transform.localScale = new Vector3(
                1f / plate.transform.localScale.x,
                1f / plate.transform.localScale.y,
                1f / plate.transform.localScale.z);
            var text = labelObject.AddComponent<TextMesh>();
            text.text = caption;
            text.characterSize = 0.055f;
            text.fontSize = 64;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.color = new Color(0.72f, 0.88f, 0.96f);
            labelObject.AddComponent<BillboardLabel>();
        }

        void RemovePlates(string targetId)
        {
            if (!plateRoots.TryGetValue(targetId, out var root) || root == null)
                return;
            plateRoots.Remove(targetId);
            Destroy(root);
        }

        static Transform FindTargetTransform(RetrievalItemSpec item)
        {
            foreach (var target in FindObjectsByType<InspectionTarget>(
                         FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (target.SiteId == item.SiteId && target.TargetId == item.TargetId)
                    return target.transform;
            }
            return null;
        }
    }

    [RequireComponent(typeof(Collider), typeof(XRSimpleInteractable))]
    public sealed class RetrievalJudgmentPlate : MonoBehaviour
    {
        RetrievalRoundController controller;
        XRSimpleInteractable interactable;
        InteractiveHoverFeedback hoverFeedback;

        public TrainingSiteId SiteId { get; private set; }
        public string TargetId { get; private set; } = string.Empty;
        public bool JudgesHazard { get; private set; }

        public void Configure(RetrievalRoundController owner, TrainingSiteId siteId,
            string targetId, bool judgesHazard)
        {
            controller = owner;
            SiteId = siteId;
            TargetId = targetId;
            JudgesHazard = judgesHazard;
        }

        void Awake()
        {
            interactable = GetComponent<XRSimpleInteractable>();
            hoverFeedback = GetComponent<InteractiveHoverFeedback>();
            if (hoverFeedback == null)
                hoverFeedback = gameObject.AddComponent<InteractiveHoverFeedback>();
        }

        void OnEnable()
        {
            if (interactable == null)
                interactable = GetComponent<XRSimpleInteractable>();
            interactable.selectEntered.AddListener(OnSelected);
            interactable.hoverEntered.AddListener(OnHoverEntered);
            interactable.hoverExited.AddListener(OnHoverExited);
        }

        void OnDisable()
        {
            if (interactable == null)
                return;
            interactable.selectEntered.RemoveListener(OnSelected);
            interactable.hoverEntered.RemoveListener(OnHoverEntered);
            interactable.hoverExited.RemoveListener(OnHoverExited);
        }

        void OnMouseEnter() => hoverFeedback?.SetHovered(true);
        void OnMouseExit() => hoverFeedback?.SetHovered(false);

        void OnMouseDown()
        {
            if (DesktopPointerInputGate.CanUseWorldPointer)
                controller?.SubmitJudgment(this);
        }

        void OnSelected(SelectEnterEventArgs _) => controller?.SubmitJudgment(this);
        void OnHoverEntered(HoverEnterEventArgs _) => hoverFeedback?.SetHovered(true);
        void OnHoverExited(HoverExitEventArgs _) => hoverFeedback?.SetHovered(false);
    }
}
