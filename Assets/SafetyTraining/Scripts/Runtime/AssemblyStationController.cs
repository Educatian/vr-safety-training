using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// Generalized magnetic-snap assembly station. Learners snap AssemblyParts into
    /// AssemblySockets; every snap, removal, and completion is telemetry under
    /// assembly:{stationId}. Supports three assessment patterns:
    /// - serviceable-choice: completion requires every seated part to be serviceable
    /// - prerequisite gating: a socket can require N parts of another category first
    /// - minimum-count completion: complete when N sockets are filled (capacity
    ///   problems) instead of all sockets
    /// Optionally authorizes the tower crane lift on completion.
    /// </summary>
    public sealed class AssemblyStationController : MonoBehaviour
    {
        [SerializeField] TrainingSiteId siteId = TrainingSiteId.TowerCrane;
        [SerializeField] string objectiveId = "TCR-02";
        [SerializeField] string stationId = "rigging";
        [SerializeField, Min(0)] int minimumFilledSockets;
        [SerializeField, Min(0)] int completionBonus = 25;
        [SerializeField] string completionFeedback =
            "Assembly complete\nAll components are seated and serviceable.";
        [SerializeField] string coachFeedback =
            "Clean assembly. Every component seated, checked, and controlled.";
        [SerializeField] TowerCraneAnimator craneAnimator;
        [SerializeField] List<AssemblySocket> sockets = new();

        GameObject ghost;
        int snapCount;
        bool completed;
        bool anyUnserviceableUsed;

        public bool IsComplete => completed;
        public string StationId => stationId;
        public IReadOnlyList<AssemblySocket> Sockets => sockets;

        public void Configure(TrainingSiteId site, string objective, string id,
            int minimumSockets, int bonus, string feedbackOnComplete, string coachLine,
            TowerCraneAnimator animator)
        {
            siteId = site;
            objectiveId = objective;
            stationId = id;
            minimumFilledSockets = Mathf.Max(0, minimumSockets);
            completionBonus = Mathf.Max(0, bonus);
            completionFeedback = feedbackOnComplete;
            coachFeedback = coachLine;
            craneAnimator = animator;
        }

        public void RegisterSocket(AssemblySocket socket)
        {
            if (!sockets.Contains(socket))
                sockets.Add(socket);
        }

        public int FilledCount
        {
            get
            {
                var filled = 0;
                foreach (var socket in sockets)
                    if (socket != null && socket.Occupant != null)
                        filled++;
                return filled;
            }
        }

        int FilledCountOfCategory(string category)
        {
            var filled = 0;
            foreach (var socket in sockets)
                if (socket != null && socket.Occupant != null &&
                    socket.Occupant.Category == category)
                    filled++;
            return filled;
        }

        bool PrerequisiteSatisfied(AssemblySocket socket)
        {
            return string.IsNullOrEmpty(socket.PrerequisiteCategory) ||
                   FilledCountOfCategory(socket.PrerequisiteCategory) >= socket.PrerequisiteCount;
        }

        public bool TrySnap(AssemblyPart part, string inputMode, out AssemblySocket socket)
        {
            socket = NearestFreeSocket(part, requirePrerequisite: true);
            if (socket == null)
            {
                var blocked = NearestFreeSocket(part, requirePrerequisite: false);
                if (blocked != null)
                {
                    LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                        $"assembly:{stationId}:{blocked.SocketId}:blocked", false,
                        $"part={part.PartId} blocked: needs {blocked.PrerequisiteCount} " +
                        $"x {blocked.PrerequisiteCategory} first", 0);
                    TrainingCoordinator.Instance?.SetContextFeedback(
                        $"Assembly order\nSeat {blocked.PrerequisiteCount} x " +
                        $"{blocked.PrerequisiteCategory} before this component.");
                }
                return false;
            }
            socket.SetOccupant(part);
            snapCount++;
            var serviceable = part.IsServiceable;
            if (!serviceable)
                anyUnserviceableUsed = true;
            LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                $"assembly:{stationId}:{socket.SocketId}", serviceable,
                $"part={part.PartId} category={part.Category} order={snapCount} " +
                $"serviceable={serviceable} input={inputMode}", 0);
            TrainingCoordinator.Instance?.SetContextFeedback(serviceable
                ? $"Component attached\n{part.PartId} seated at {socket.SocketId}."
                : $"Component review\n{part.PartId} is not serviceable. Replace it before finishing.");
            EvaluateCompletion();
            return true;
        }

        public void NotifyRemoved(AssemblyPart part, AssemblySocket socket)
        {
            if (socket.Occupant == part)
                socket.SetOccupant(null);
            if (completed)
            {
                completed = false;
                craneAnimator?.SetLiftAuthorized(false);
            }
            LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                $"assembly:{stationId}:{socket.SocketId}:removed", true,
                $"part={part.PartId} removed for rework", 0);
        }

        void EvaluateCompletion()
        {
            if (completed)
                return;
            var required = minimumFilledSockets > 0 ? minimumFilledSockets : sockets.Count;
            if (FilledCount < required)
                return;
            foreach (var socket in sockets)
            {
                if (socket.Occupant != null && !socket.Occupant.IsServiceable)
                {
                    TrainingCoordinator.Instance?.SetContextFeedback(
                        "Assembly on hold\nA damaged component is seated. Swap it for serviceable gear.");
                    LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                        $"assembly:{stationId}_complete", false,
                        "assembled with unserviceable component", 0);
                    return;
                }
            }
            completed = true;
            LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                $"assembly:{stationId}_complete", true,
                $"assembled with serviceable components in {snapCount} snap(s), " +
                $"{FilledCount}/{sockets.Count} sockets", completionBonus);
            if (!anyUnserviceableUsed)
                LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                    $"assembly:{stationId}_complete:first_attempt", true,
                    "no unserviceable component was ever seated", 0);
            if (completionBonus > 0)
                TrainingCoordinator.Instance?.AddHandsOnBonus(completionBonus);
            TrainingCoordinator.Instance?.SetContextFeedback(completionFeedback);
            TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(siteId, coachFeedback);
            craneAnimator?.SetLiftAuthorized(true);
        }

        AssemblySocket NearestFreeSocket(AssemblyPart part, bool requirePrerequisite)
        {
            AssemblySocket nearest = null;
            var best = float.MaxValue;
            foreach (var socket in sockets)
            {
                if (socket == null || !socket.IsFree)
                    continue;
                if (!string.IsNullOrEmpty(socket.AcceptedCategory) &&
                    socket.AcceptedCategory != part.Category)
                    continue;
                if (requirePrerequisite && !PrerequisiteSatisfied(socket))
                    continue;
                var distance = Vector3.Distance(part.transform.position, socket.AttachPosition);
                if (distance <= socket.SnapRadius && distance < best)
                {
                    best = distance;
                    nearest = socket;
                }
            }
            return nearest;
        }

        public void UpdateGhost(AssemblyPart part)
        {
            var socket = NearestFreeSocket(part, requirePrerequisite: true);
            if (socket == null)
            {
                HideGhost();
                return;
            }
            if (ghost == null)
            {
                ghost = GameObject.CreatePrimitive(PrimitiveType.Cube);
                ghost.name = "Assembly Snap Ghost";
                Destroy(ghost.GetComponent<Collider>());
                var renderer = ghost.GetComponent<Renderer>();
                renderer.material.color = new Color(0.35f, 0.95f, 0.75f, 0.4f);
            }
            ghost.transform.SetPositionAndRotation(socket.AttachPosition, socket.AttachRotation);
            ghost.transform.localScale = part.transform.lossyScale * 1.05f;
            ghost.SetActive(true);
        }

        public void HideGhost()
        {
            if (ghost != null)
                ghost.SetActive(false);
        }
    }
}
