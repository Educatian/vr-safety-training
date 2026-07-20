using NUnit.Framework;
using SafetyTraining.Core;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class ConstructionGoldenModuleProgressTests
    {
        [Test]
        public void NewLearner_CompletesEvidenceCenteredMissionInRequiredOrder()
        {
            var mission = new ConstructionGoldenModuleProgress();

            Assert.That(mission.Begin().Stage, Is.EqualTo(ConstructionGoldenStage.PpeEntry));
            Assert.That(mission.RecordPpePlacement().Stage, Is.EqualTo(ConstructionGoldenStage.EvidenceInvestigation));
            for (var index = 0; index < 4; index++)
                mission.RecordEvidence($"evidence-{index}", true);
            Assert.That(mission.Stage, Is.EqualTo(ConstructionGoldenStage.EngineeringDecisions));
            foreach (var id in new[] { "formwork-capacity", "crane-radius", "trench-system" })
                mission.RecordEngineeringDecision(id, true);
            Assert.That(mission.Stage, Is.EqualTo(ConstructionGoldenStage.ControlInstallation));
            for (var step = 1; step <= 4; step++)
                mission.RecordControlStep(step);
            Assert.That(mission.Stage, Is.EqualTo(ConstructionGoldenStage.CoachDebrief));
            Assert.That(mission.RecordCoachExplanation(
                "The fall risk requires a guardrail control before second floor access.").Accepted, Is.True);
            Assert.That(mission.Stage, Is.EqualTo(ConstructionGoldenStage.FinalReport));
            Assert.That(mission.RecordFinalReport().Stage, Is.EqualTo(ConstructionGoldenStage.Complete));
        }

        [Test]
        public void SkilledLearner_CannotSkipEvidenceEngineeringControlsOrDebrief()
        {
            var mission = new ConstructionGoldenModuleProgress();
            mission.Begin();

            Assert.That(mission.RecordEngineeringDecision("formwork-capacity", true).Accepted, Is.False);
            Assert.That(mission.RecordControlStep(1).Accepted, Is.False);
            Assert.That(mission.RecordFinalReport().Accepted, Is.False);
            Assert.That(mission.Stage, Is.EqualTo(ConstructionGoldenStage.PpeEntry));
        }

        [Test]
        public void Abuse_DuplicateEvidenceAndDecisionsDoNotAdvanceCounts()
        {
            var mission = new ConstructionGoldenModuleProgress();
            mission.Begin();
            mission.RecordPpePlacement();

            Assert.That(mission.RecordEvidence("edge", true).Accepted, Is.True);
            Assert.That(mission.RecordEvidence("edge", true).Accepted, Is.False);
            Assert.That(mission.RelevantEvidenceCount, Is.EqualTo(1));
            mission.RecordEvidence("crane", true);
            mission.RecordEvidence("access", true);
            mission.RecordEvidence("formwork", true);

            Assert.That(mission.RecordEngineeringDecision("invented-station", true).Accepted, Is.False);
            Assert.That(mission.RecordEngineeringDecision("crane-radius", true).Accepted, Is.True);
            Assert.That(mission.RecordEngineeringDecision("crane-radius", true).Accepted, Is.False);
            Assert.That(mission.EngineeringDecisionCount, Is.EqualTo(1));
        }

        [Test]
        public void Stress_WrongEngineeringAttemptsRemainRecoverable()
        {
            var mission = ReadyForEngineering();

            for (var index = 0; index < 3; index++)
                Assert.That(mission.RecordEngineeringDecision("formwork-capacity", false).Accepted, Is.True);
            Assert.That(mission.EngineeringDecisionCount, Is.Zero);
            Assert.That(mission.Stage, Is.EqualTo(ConstructionGoldenStage.EngineeringDecisions));
            Assert.That(mission.RecordEngineeringDecision("formwork-capacity", true).Accepted, Is.True);
            Assert.That(mission.EngineeringDecisionCount, Is.EqualTo(1));
        }

        [TestCase("")]
        [TestCase("Guardrail.")]
        [TestCase("I saw several things and I think the site looks unsafe today.")]
        [TestCase("Install all controls because controls are generally important for workers.")]
        public void Debrief_RejectsEmptyShortOrUnconnectedExplanations(string explanation)
        {
            var mission = ReadyForDebrief();

            Assert.That(mission.RecordCoachExplanation(explanation).Accepted, Is.False);
            Assert.That(mission.Stage, Is.EqualTo(ConstructionGoldenStage.CoachDebrief));
        }

        [Test]
        public void ReadableDebrief_AcceptsRiskToControlConnection()
        {
            var mission = ReadyForDebrief();

            var update = mission.RecordCoachExplanation(
                "The crane load creates struck-by risk, so isolate the swing radius with a barricade.");

            Assert.That(update.Accepted, Is.True);
            Assert.That(update.StageChanged, Is.True);
            Assert.That(update.Stage, Is.EqualTo(ConstructionGoldenStage.FinalReport));
        }

        static ConstructionGoldenModuleProgress ReadyForEngineering()
        {
            var mission = new ConstructionGoldenModuleProgress();
            mission.Begin();
            mission.RecordPpePlacement();
            foreach (var id in new[] { "edge", "crane", "access", "formwork" })
                mission.RecordEvidence(id, true);
            return mission;
        }

        static ConstructionGoldenModuleProgress ReadyForDebrief()
        {
            var mission = ReadyForEngineering();
            foreach (var id in new[] { "formwork-capacity", "crane-radius", "trench-system" })
                mission.RecordEngineeringDecision(id, true);
            for (var step = 1; step <= 4; step++)
                mission.RecordControlStep(step);
            return mission;
        }
    }
}
