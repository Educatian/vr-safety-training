using System.Collections.Generic;
using SafetyTraining.Core;
using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// Magnetic-snap rigging assembly: the learner assembles slings and a tagline
    /// onto the lift jig. Every snap is telemetry (part choice, socket, order,
    /// serviceability); the crane's trolley travel stays locked until the rig is
    /// assembled entirely from serviceable, tagged components.
    /// </summary>
    public sealed class RiggingAssemblyStation : MonoBehaviour
    {
        [SerializeField] TrainingSiteId siteId = TrainingSiteId.TowerCrane;
        [SerializeField] string objectiveId = "TCR-02";
        [SerializeField] TowerCraneAnimator craneAnimator;
        [SerializeField] List<AssemblySocket> sockets = new();

        GameObject ghost;
        int snapCount;
        bool completed;
        bool anyUnserviceableUsed;

        public bool IsComplete => completed;
        public IReadOnlyList<AssemblySocket> Sockets => sockets;

        public void Configure(TrainingSiteId site, string objective, TowerCraneAnimator animator)
        {
            siteId = site;
            objectiveId = objective;
            craneAnimator = animator;
        }

        public void RegisterSocket(AssemblySocket socket)
        {
            if (!sockets.Contains(socket))
                sockets.Add(socket);
        }

        public bool TrySnap(AssemblyPart part, string inputMode, out AssemblySocket socket)
        {
            socket = NearestFreeSocket(part);
            if (socket == null)
                return false;
            socket.SetOccupant(part);
            snapCount++;
            var serviceable = part.IsServiceable;
            if (!serviceable)
                anyUnserviceableUsed = true;
            LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                $"assembly:{socket.SocketId}", serviceable,
                $"part={part.PartId} category={part.Category} order={snapCount} " +
                $"serviceable={serviceable} input={inputMode}", 0);
            TrainingCoordinator.Instance?.SetContextFeedback(serviceable
                ? $"Rigging component attached\n{part.PartId} seated at {socket.SocketId}."
                : $"Rigging review\n{part.PartId} shows damage. Inspect the tags and replace it before the lift.");
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
                $"assembly:{socket.SocketId}:removed", true,
                $"part={part.PartId} removed for rework", 0);
        }

        void EvaluateCompletion()
        {
            foreach (var socket in sockets)
            {
                if (socket.Occupant == null)
                    return;
            }
            foreach (var socket in sockets)
            {
                if (!socket.Occupant.IsServiceable)
                {
                    TrainingCoordinator.Instance?.SetContextFeedback(
                        "Lift on hold\nA damaged component is rigged. Swap it for tagged, inspected gear.");
                    LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                        "assembly:rigging_complete", false,
                        "rig assembled with unserviceable component", 0);
                    return;
                }
            }
            completed = true;
            LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                "assembly:rigging_complete", true,
                $"rig assembled with inspected components in {snapCount} snap(s)", 25);
            if (!anyUnserviceableUsed)
                LearningOutcomeTracker.Instance?.Record(siteId, objectiveId,
                    "assembly:rigging_complete:first_attempt", true,
                    "no damaged component was ever rigged", 0);
            TrainingCoordinator.Instance?.AddHandsOnBonus(25);
            TrainingCoordinator.Instance?.SetContextFeedback(
                "Lift authorized\nRigging verified with inspected components. The trolley may travel.");
            TrainingCoordinator.Instance?.PresentPracticalCoachFeedback(siteId,
                "Clean rig. Tagged slings, tagline on, load path controlled - that is how a lift starts.");
            craneAnimator?.SetLiftAuthorized(true);
        }

        AssemblySocket NearestFreeSocket(AssemblyPart part)
        {
            AssemblySocket nearest = null;
            var best = float.MaxValue;
            foreach (var socket in sockets)
            {
                if (socket == null || !socket.IsFree)
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
            var socket = NearestFreeSocket(part);
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
