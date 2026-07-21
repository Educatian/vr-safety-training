using System.Linq;
using NUnit.Framework;
using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class RiggingAssemblyTests
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";

        [SetUp]
        public void OpenTrainingScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        static AssemblyStationController Station(string stationId = "rigging") =>
            Object.FindObjectsByType<AssemblyStationController>(FindObjectsInactive.Include,
                    FindObjectsSortMode.None)
                .Single(item => item.StationId == stationId);

        static AssemblyPart Part(string id) =>
            Object.FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.PartId == id);

        [Test]
        public void Scene_ContainsFourAssemblyStationsWithExpectedInventory()
        {
            var stations = Object.FindObjectsByType<AssemblyStationController>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.That(stations.Select(item => item.StationId).ToArray(),
                Is.EquivalentTo(new[] { "rigging", "guardrail", "shore-frames", "loto" }));
            Assert.That(Station("rigging").Sockets.Count, Is.EqualTo(3));
            Assert.That(Station("guardrail").Sockets.Count, Is.EqualTo(4));
            Assert.That(Station("shore-frames").Sockets.Count, Is.EqualTo(6));
            Assert.That(Station("loto").Sockets.Count, Is.EqualTo(3));
            var parts = Object.FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(parts, Has.Length.EqualTo(20));
            Assert.That(parts.Count(part => !part.IsServiceable), Is.EqualTo(4));
        }

        [Test]
        public void GuardrailSocket_RejectsRailUntilBothPostsAreSeated()
        {
            var station = Station("guardrail");
            var topRail = Part("top-rail-section");
            var railSocket = station.Sockets.Single(socket => socket.SocketId == "top-rail");
            topRail.transform.position = railSocket.AttachPosition;
            Assert.That(station.TrySnap(topRail, "Test", out _), Is.False);

            var postA = Part("post-a");
            postA.transform.position = station.Sockets.Single(s => s.SocketId == "post-left").AttachPosition;
            station.TrySnap(postA, "Test", out _);
            var postB = Part("post-b");
            postB.transform.position = station.Sockets.Single(s => s.SocketId == "post-right").AttachPosition;
            station.TrySnap(postB, "Test", out _);

            topRail.transform.position = railSocket.AttachPosition;
            Assert.That(station.TrySnap(topRail, "Test", out var seated), Is.True);
            Assert.That(seated.SocketId, Is.EqualTo("top-rail"));
        }

        [Test]
        public void ShoreFrames_CompleteAtFiveOfSixBays()
        {
            var station = Station("shore-frames");
            for (var index = 1; index <= 5; index++)
            {
                var frame = Part($"shore-frame-{index}");
                frame.transform.position = station.Sockets[index - 1].AttachPosition;
                Assert.That(station.TrySnap(frame, "Test", out _), Is.True, $"frame {index}");
            }
            Assert.That(station.IsComplete, Is.True);
        }

        [Test]
        public void CraneLift_StartsUnauthorizedUntilRigIsAssembled()
        {
            var animator = Object.FindFirstObjectByType<TowerCraneAnimator>(FindObjectsInactive.Include);
            Assert.That(animator, Is.Not.Null);
            Assert.That(animator.LiftAuthorized, Is.False);
        }

        [Test]
        public void Snap_WithinRadiusOccupiesSocket_AndServiceableRigAuthorizesLift()
        {
            var station = Station();
            var animator = Object.FindFirstObjectByType<TowerCraneAnimator>(FindObjectsInactive.Include);
            var sockets = station.Sockets;

            var slingA = Part("tagged-sling-a");
            slingA.transform.position = sockets[0].AttachPosition;
            Assert.That(station.TrySnap(slingA, "Test", out var first), Is.True);
            Assert.That(first.Occupant, Is.SameAs(slingA));
            Assert.That(station.IsComplete, Is.False);

            var slingB = Part("tagged-sling-b");
            slingB.transform.position = sockets[1].AttachPosition;
            Assert.That(station.TrySnap(slingB, "Test", out _), Is.True);

            var tagline = Part("tagline-coil");
            tagline.transform.position = sockets[2].AttachPosition;
            Assert.That(station.TrySnap(tagline, "Test", out _), Is.True);

            Assert.That(station.IsComplete, Is.True);
            Assert.That(animator.LiftAuthorized, Is.True);
        }

        [Test]
        public void Snap_WithDamagedSlingFillsSocketButDoesNotAuthorizeLift()
        {
            var station = Station();
            var animator = Object.FindFirstObjectByType<TowerCraneAnimator>(FindObjectsInactive.Include);
            var sockets = station.Sockets;

            var frayed = Part("frayed-sling-a");
            frayed.transform.position = sockets[0].AttachPosition;
            Assert.That(station.TrySnap(frayed, "Test", out _), Is.True);
            var slingB = Part("tagged-sling-b");
            slingB.transform.position = sockets[1].AttachPosition;
            station.TrySnap(slingB, "Test", out _);
            var tagline = Part("tagline-coil");
            tagline.transform.position = sockets[2].AttachPosition;
            station.TrySnap(tagline, "Test", out _);

            Assert.That(station.IsComplete, Is.False);
            Assert.That(animator.LiftAuthorized, Is.False);
        }

        [Test]
        public void Snap_FailsOutsideSnapRadius()
        {
            var station = Station();
            var sling = Part("tagged-sling-a");
            sling.transform.position = station.Sockets[0].AttachPosition + new Vector3(5f, 0f, 0f);

            Assert.That(station.TrySnap(sling, "Test", out var socket), Is.False);
            Assert.That(socket, Is.Null);
        }
    }
}
