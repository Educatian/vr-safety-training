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

        static RiggingAssemblyStation Station() =>
            Object.FindFirstObjectByType<RiggingAssemblyStation>(FindObjectsInactive.Include);

        static AssemblyPart Part(string id) =>
            Object.FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Single(item => item.PartId == id);

        [Test]
        public void Scene_ContainsRiggingStationWithThreeSocketsAndFiveParts()
        {
            var station = Station();
            Assert.That(station, Is.Not.Null);
            Assert.That(station.Sockets.Count, Is.EqualTo(3));
            var parts = Object.FindObjectsByType<AssemblyPart>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
            Assert.That(parts, Has.Length.EqualTo(5));
            Assert.That(parts.Count(part => part.IsServiceable), Is.EqualTo(3));
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
