using System.Linq;
using NUnit.Framework;
using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace SafetyTraining.Tests.EditMode
{
    public sealed class WorldExpansionTests
    {
        const string ScenePath = "Assets/SafetyTraining/Scenes/SafetyTrainingExplorer.unity";

        [SetUp]
        public void OpenTrainingScene()
        {
            EditorSceneManager.OpenScene(ScenePath);
        }

        [Test]
        public void ExpandedSitesHaveRoomForExplorationWithoutLeavingTrainingState()
        {
            var zones = Object.FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None);

            Assert.That(zones, Has.Length.EqualTo(5));
            foreach (var zone in zones)
            {
                Assert.That(zone.InfluenceRadius, Is.GreaterThanOrEqualTo(19f), zone.name);
                var floor = zone.transform.Find("Floor");
                Assert.That(floor, Is.Not.Null, zone.name);
                Assert.That(floor.localScale.x, Is.GreaterThanOrEqualTo(30f), zone.name);
                Assert.That(floor.localScale.z, Is.GreaterThanOrEqualTo(26f), zone.name);
            }
        }

        [Test]
        public void EveryModuleContainsAOneWayExplorationLoop()
        {
            foreach (var site in Object.FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None))
            {
                var routeNames = site.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.StartsWith("WorldExpansion - Route -"))
                    .Select(item => item.name).ToArray();

                Assert.That(routeNames, Has.Length.EqualTo(4), site.name);
                Assert.That(routeNames, Does.Contain("WorldExpansion - Route - Entry Spine"), site.name);
                Assert.That(routeNames, Does.Contain("WorldExpansion - Route - Left Evidence Loop"), site.name);
                Assert.That(routeNames, Does.Contain("WorldExpansion - Route - Rear Traverse"), site.name);
                Assert.That(routeNames, Does.Contain("WorldExpansion - Route - Right Return Loop"), site.name);
            }
        }

        [Test]
        public void SpatialAnalyticsZonesCoverEachModuleRouteStage()
        {
            var analyticsZones = Object.FindObjectsByType<SpatialAnalyticsZone>(FindObjectsSortMode.None);

            Assert.That(analyticsZones, Has.Length.EqualTo(31));
            foreach (var site in (TrainingSiteId[])System.Enum.GetValues(typeof(TrainingSiteId)))
            {
                var siteZones = analyticsZones.Where(zone => zone.SiteId == site).ToArray();
                var expectedCount = site == TrainingSiteId.Construction ? 7 : 6;
                Assert.That(siteZones, Has.Length.EqualTo(expectedCount), site.ToString());
                Assert.That(siteZones.Select(zone => zone.ZoneId).Distinct().ToArray(),
                    Has.Length.EqualTo(expectedCount), site.ToString());
            }
        }

        [Test]
        public void InquiryEvidenceObjectsSupportInvestigationInEveryModule()
        {
            var evidenceObjects = Object.FindObjectsByType<EvidenceObject>(FindObjectsSortMode.None);

            Assert.That(evidenceObjects, Has.Length.EqualTo(31));
            foreach (var site in (TrainingSiteId[])System.Enum.GetValues(typeof(TrainingSiteId)))
            {
                var siteEvidence = evidenceObjects.Where(item => item.SiteId == site).ToArray();
                var expectedCount = site == TrainingSiteId.Construction ? 7 : 6;
                Assert.That(siteEvidence, Has.Length.EqualTo(expectedCount), site.ToString());
                Assert.That(siteEvidence.Count(item => item.IsDistractor), Is.EqualTo(1), site.ToString());
                Assert.That(siteEvidence.Select(item => item.EvidenceId).Distinct().ToArray(),
                    Has.Length.EqualTo(expectedCount), site.ToString());
            }
        }

        [Test]
        public void InquiryFlowHasControllerAndFinalReportStations()
        {
            Assert.That(Object.FindFirstObjectByType<InquirySessionController>(), Is.Not.Null);
            var stations = Object.FindObjectsByType<InquiryDecisionStation>(FindObjectsSortMode.None);

            Assert.That(stations, Has.Length.EqualTo(5));
            foreach (var station in stations)
                Assert.That(station.GetComponent<Collider>(), Is.Not.Null, station.name);
        }

        [Test]
        public void InquiryReportRequiresRelevantEvidenceInsteadOfDistractors()
        {
            var controller = Object.FindFirstObjectByType<InquirySessionController>();
            if (InquirySessionController.Instance != controller)
            {
                typeof(InquirySessionController)
                    .GetMethod("Awake", System.Reflection.BindingFlags.Instance |
                                         System.Reflection.BindingFlags.NonPublic)
                    .Invoke(controller, null);
            }
            controller.StartInquiry(TrainingSiteId.Construction,
                InquirySessionController.PromptFor(TrainingSiteId.Construction));
            var constructionEvidence = Object.FindObjectsByType<EvidenceObject>(FindObjectsSortMode.None)
                .Where(item => item.SiteId == TrainingSiteId.Construction).ToArray();
            var distractor = constructionEvidence.Single(item => item.IsDistractor);
            var relevant = constructionEvidence.Where(item => !item.IsDistractor).Take(3).ToArray();

            distractor.Collect();
            relevant[0].Collect();
            relevant[1].Collect();

            Assert.That(controller.CollectedItemCount(TrainingSiteId.Construction), Is.EqualTo(3));
            Assert.That(controller.EvidenceCount(TrainingSiteId.Construction), Is.EqualTo(2));
            Assert.That(controller.DistractorCount(TrainingSiteId.Construction), Is.EqualTo(1));
            Assert.That(controller.CanSubmitReport(TrainingSiteId.Construction, 3), Is.False);

            relevant[2].Collect();

            Assert.That(controller.EvidenceCount(TrainingSiteId.Construction), Is.EqualTo(3));
            Assert.That(controller.CanSubmitReport(TrainingSiteId.Construction, 3), Is.True);
        }

        [Test]
        public void RealAssetsAndParticleCuesStayWithinVrBudget()
        {
            foreach (var site in Object.FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None))
            {
                Assert.That(site.GetComponentsInChildren<Transform>(true)
                    .Count(item => item.name.StartsWith("WorldExpansion - RealAsset -")),
                    Is.GreaterThanOrEqualTo(3), site.name);
            }

            var effects = Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None)
                .Where(system => system.name.StartsWith("WorldExpansion -")).ToArray();
            Assert.That(effects, Has.Length.GreaterThanOrEqualTo(5));
            foreach (var effect in effects)
            {
                Assert.That(effect.main.maxParticles, Is.LessThanOrEqualTo(80), effect.name);
                Assert.That(effect.collision.enabled, Is.False, effect.name);
                Assert.That(effect.main.cullingMode, Is.EqualTo(ParticleSystemCullingMode.Automatic), effect.name);
            }
        }

        [Test]
        public void RealAssetsUseDistanceCullingLodsForVrBudget()
        {
            var realAssets = Object.FindObjectsByType<Transform>(FindObjectsSortMode.None)
                .Where(item => item.name.StartsWith("WorldExpansion - RealAsset -")).ToArray();

            Assert.That(realAssets, Has.Length.GreaterThanOrEqualTo(45));
            foreach (var asset in realAssets)
            {
                var group = asset.GetComponent<LODGroup>();
                Assert.That(group, Is.Not.Null, asset.name);
                Assert.That(group.GetLODs(), Has.Length.EqualTo(1), asset.name);
                Assert.That(group.GetLODs()[0].screenRelativeTransitionHeight, Is.LessThanOrEqualTo(0.006f),
                    asset.name);
                Assert.That((int)(GameObjectUtility.GetStaticEditorFlags(asset.gameObject) & StaticEditorFlags.BatchingStatic),
                    Is.EqualTo(0), asset.name);
            }
        }

        [Test]
        public void ConstructionSiteReadsAsAUsMultiLevelJobsite()
        {
            var construction = Object.FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None)
                .Single(site => site.SiteId == TrainingSiteId.Construction);
            var names = construction.GetComponentsInChildren<Transform>(true)
                .Select(item => item.name).ToArray();

            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Two Story Steel Bay Front"));
            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Two Story Steel Bay Rear"));
            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Second Level Deck Reference"));
            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Formwork Hardware Crate"));
            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Lift Plan Toolbox"));
            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Survey Clipboard Station"));
            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Stacked Formwork Access Pinch Point"));
            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Guardrail Material Bundle"));
            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Rigging Hardware Crate"));
            Assert.That(names, Does.Contain("WorldExpansion - RealAsset - Pedestrian Exclusion Barrier"));
            Assert.That(names, Does.Contain("US Site Permit Board"));
        }

        [Test]
        public void ExpandedIsolationCollidersMatchTheLargerSiteFootprint()
        {
            foreach (var site in Object.FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None))
            {
                var left = site.transform.Find("Isolation Collider Left").GetComponent<BoxCollider>();
                var right = site.transform.Find("Isolation Collider Right").GetComponent<BoxCollider>();
                var rear = site.transform.Find("Isolation Collider Rear").GetComponent<BoxCollider>();

                Assert.That(left.size.z, Is.GreaterThanOrEqualTo(26f), site.name);
                Assert.That(right.size.z, Is.GreaterThanOrEqualTo(26f), site.name);
                Assert.That(rear.size.x, Is.GreaterThanOrEqualTo(30f), site.name);
            }
        }

        [Test]
        public void NonConstructionSitesExposeThreeDistinctOperationalSubzones()
        {
            var sites = Object.FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None)
                .Where(item => item.SiteId != TrainingSiteId.Construction).ToArray();

            Assert.That(sites, Has.Length.EqualTo(4));
            foreach (var site in sites)
            {
                var subzones = site.GetComponentsInChildren<Transform>(true)
                    .Where(item => item.name.StartsWith("Operational Subzone - ")).ToArray();
                Assert.That(subzones, Has.Length.EqualTo(3), site.SiteId.ToString());
                Assert.That(subzones.Select(item => item.name).Distinct().Count(), Is.EqualTo(3));
            }
        }

        [Test]
        public void NpcSituationContextIncludesLearnerSubzoneAndInquiryState()
        {
            var warehouse = Object.FindObjectsByType<SiteExperienceZone>(FindObjectsSortMode.None)
                .Single(item => item.SiteId == TrainingSiteId.Warehouse);
            var coach = warehouse.GetComponentInChildren<NpcConversationAgent>(true);
            var zone = warehouse.GetComponentsInChildren<SpatialAnalyticsZone>(true).First();
            var context = coach.BuildSituationContext(zone.GetComponent<BoxCollider>().bounds.center);

            Assert.That(context, Does.Contain(zone.DisplayName));
            Assert.That(context, Does.Contain("evidence").IgnoreCase);
            Assert.That(context, Does.Contain("Hypothesis").IgnoreCase);
        }
    }
}
