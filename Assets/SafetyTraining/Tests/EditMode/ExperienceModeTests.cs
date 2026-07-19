using NUnit.Framework;
using SafetyTraining.Runtime;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class ExperienceModeTests
    {
        [Test]
        public void AutoModeUsesDesktopWithoutRunningXrDisplay()
        {
            Assert.That(ExperienceModeController.ResolveStartupMode(
                TrainingExperienceMode.Auto, RuntimePlatform.WindowsPlayer, false),
                Is.EqualTo(TrainingExperienceMode.Desktop));
        }

        [Test]
        public void AutoModeUsesImmersiveVrWithRunningXrDisplay()
        {
            Assert.That(ExperienceModeController.ResolveStartupMode(
                TrainingExperienceMode.Auto, RuntimePlatform.WindowsPlayer, true),
                Is.EqualTo(TrainingExperienceMode.ImmersiveVr));
        }

        [Test]
        public void QuestStandaloneAlwaysUsesImmersiveVr()
        {
            Assert.That(ExperienceModeController.ResolveStartupMode(
                TrainingExperienceMode.Desktop, RuntimePlatform.Android, false),
                Is.EqualTo(TrainingExperienceMode.ImmersiveVr));
        }
    }
}
