using System.Linq;
using NUnit.Framework;
using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class OshaScenarioCatalogTests
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";

        [SetUp]
        public void OpenTrainingScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [Test]
        public void EveryAuthoredInspectionTarget_HasCitationActionAndAuthority()
        {
            var targets = Object.FindObjectsByType<InspectionTarget>(FindObjectsSortMode.None);

            Assert.That(targets, Is.Not.Empty);
            foreach (var target in targets)
            {
                var definition = target.Compliance;
                Assert.That(definition.Citation, Does.StartWith("1926."), target.TargetId);
                Assert.That(definition.LearnerAction, Is.Not.Empty, target.TargetId);
            }
        }

        [Test]
        public void ElectricalIsolation_RequiresQualifiedPersonEscalation()
        {
            var target = Object.FindObjectsByType<InspectionTarget>(FindObjectsSortMode.None)
                .Single(item => item.TargetId == "open-panel");

            Assert.That(target.Compliance.Authority, Is.EqualTo(SafetyAuthority.QualifiedPerson));
            Assert.That(target.Compliance.LearnerAction, Does.Contain("escalate"));
        }

        [Test]
        public void CoachBrief_DoesNotClaimCertification()
        {
            var brief = OshaScenarioCatalog.GetSiteCoachBrief(TrainingSiteId.Construction);

            Assert.That(brief, Does.Contain("not a qualification or compliance certificate"));
            Assert.That(brief, Does.Contain("OSHA 1926.501"));
        }
    }
}
