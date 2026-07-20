using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Editor
{
    internal static class SafetyInquiryContentFactory
    {
        readonly struct EvidenceSpec
        {
            public EvidenceSpec(string id, string title, string hazardType, string note,
                string asset, Vector3 position, bool distractor)
            {
                Id = id;
                Title = title;
                HazardType = hazardType;
                Note = note;
                Asset = asset;
                Position = position;
                IsDistractor = distractor;
            }

            public string Id { get; }
            public string Title { get; }
            public string HazardType { get; }
            public string Note { get; }
            public string Asset { get; }
            public Vector3 Position { get; }
            public bool IsDistractor { get; }
        }

        public static void AddInquiryContent(Transform site, TrainingSiteId siteId)
        {
            foreach (var spec in SpecsFor(siteId))
                AddEvidence(site, siteId, spec);
            AddDecisionStation(site, siteId);
        }

        static void AddEvidence(Transform site, TrainingSiteId siteId, EvidenceSpec spec)
        {
            var anchor = new GameObject($"Inquiry Evidence - {spec.Title}");
            anchor.transform.SetParent(site, false);
            anchor.transform.localPosition = spec.Position;
            var collider = anchor.AddComponent<SphereCollider>();
            collider.radius = 0.75f;
            collider.isTrigger = true;
            anchor.AddComponent<XRSimpleInteractable>();
            anchor.AddComponent<InteractiveHoverFeedback>();
            anchor.AddComponent<EvidenceObject>().Configure(siteId, spec.Id, spec.Title,
                spec.HazardType, spec.Note, spec.IsDistractor);
            SafetyWorldAssetPainter.AddModel(anchor.transform, spec.Title, spec.Asset,
                Vector3.zero, 1.35f, Vector3.zero);
            var label = SafetyScenePrimitives.Label(LabelFor(spec.Title), anchor.transform,
                new Vector3(0f, 1.1f, 0f), 0.07f);
            label.color = spec.IsDistractor
                ? new Color(0.78f, 0.8f, 0.84f)
                : new Color(0.55f, 0.94f, 1f);
        }

        static void AddDecisionStation(Transform site, TrainingSiteId siteId)
        {
            var station = SafetyScenePrimitives.Primitive(PrimitiveType.Cube,
                "Inquiry Decision Station", site, new Vector3(5.2f, 0.55f, -6.8f),
                new Vector3(1.8f, 1.1f, 0.16f), new Color(0.06f, 0.1f, 0.14f));
            station.AddComponent<XRSimpleInteractable>();
            station.AddComponent<InteractiveHoverFeedback>();
            var requiredEvidence = siteId == TrainingSiteId.Construction
                ? ConstructionGoldenModuleProgress.RequiredRelevantEvidence
                : InquirySessionController.DefaultMinimumEvidenceForReport;
            station.AddComponent<InquiryDecisionStation>().Configure(siteId,
                HypothesisFor(siteId), ExplanationFor(siteId), requiredEvidence);
            var labelText = siteId == TrainingSiteId.Construction
                ? "FINAL REPORT (MISSION GATED)"
                : "SUBMIT REPORT (3+ EVIDENCE)";
            var label = SafetyScenePrimitives.Label(labelText, site,
                new Vector3(5.2f, 1.45f, -6.72f), 0.105f);
            label.color = new Color(0.7f, 0.92f, 1f);
        }

        static string LabelFor(string title)
        {
            return title.ToUpperInvariant()
                .Replace(" EVIDENCE", string.Empty)
                .Replace(" CLUE", string.Empty)
                .Replace(" SAMPLE", string.Empty)
                .Replace(" REFERENCE", string.Empty);
        }

        static string HypothesisFor(TrainingSiteId siteId)
        {
            return siteId switch
            {
                TrainingSiteId.Construction => "Uncontrolled access and suspended-load exposure are the priority risks.",
                TrainingSiteId.Warehouse => "Route conflict and unstable storage are the priority risks.",
                TrainingSiteId.FireResponse => "Blocked egress and extinguisher access control the emergency decision.",
                TrainingSiteId.ChemicalProcessing => "HazCom evidence and exposure path determine the spill response.",
                TrainingSiteId.ElectricalMaintenance => "Energized-source control and LOTO verification are required first.",
                _ => "Collected evidence identifies the safest corrective action."
            };
        }

        static string ExplanationFor(TrainingSiteId siteId)
        {
            return siteId switch
            {
                TrainingSiteId.Construction => "Controls should isolate the crane radius, protect edges, and restore access.",
                TrainingSiteId.Warehouse => "Controls should separate pedestrian flow, secure loads, and clear egress.",
                TrainingSiteId.FireResponse => "Controls should keep exits and extinguishers clear before close approach.",
                TrainingSiteId.ChemicalProcessing => "Controls should quarantine unknown chemicals and stage spill response safely.",
                TrainingSiteId.ElectricalMaintenance => "Controls should isolate energy, verify absence of voltage, and remove damaged equipment.",
                _ => "Controls should match the observed evidence."
            };
        }

        static EvidenceSpec[] SpecsFor(TrainingSiteId siteId)
        {
            return siteId switch
            {
                TrainingSiteId.Construction => ConstructionSpecs(),
                TrainingSiteId.Warehouse => WarehouseSpecs(),
                TrainingSiteId.FireResponse => FireSpecs(),
                TrainingSiteId.ChemicalProcessing => ChemicalSpecs(),
                TrainingSiteId.ElectricalMaintenance => ElectricalSpecs(),
                _ => new EvidenceSpec[0]
            };
        }

        static EvidenceSpec[] ConstructionSpecs()
        {
            return new[]
            {
                Spec("crane-swing-radius", "Crane swing radius evidence", "Struck-by",
                    "Suspended-load area needs a controlled exclusion boundary.",
                    "overhead_crane_1k.fbx", new Vector3(8f, 0.2f, 11f)),
                Spec("fall-edge-gap", "Open edge sightline", "Fall",
                    "Second-level work needs guardrail or approved fall arrest.",
                    "modular_factory_facade_1k.fbx", new Vector3(-7.6f, 0.2f, 5.8f)),
                Spec("material-staging", "Material staging clue", "Housekeeping",
                    "Stored materials should not reduce safe access width.",
                    "cement_bag_1k.fbx", new Vector3(-6.4f, 0.2f, -4.8f)),
                Spec("generator-clearance", "Generator clearance clue", "Equipment",
                    "Generator access needs clearance and visible work boundary.",
                    "portable_generator_1k.fbx", new Vector3(-8.8f, 0.2f, -3.2f)),
                Spec("formwork-access", "Formwork access evidence", "Access",
                    "Stored formwork panels should not block ladder or scaffold access.",
                    "modular_factory_facade_1k.fbx", new Vector3(-9.6f, 0.2f, 6.8f)),
                Spec("permit-board", "Permit board clue", "Pre-task planning",
                    "The crew should verify permit, lift plan, and PPE controls before entry.",
                    "clipboard_1k.fbx", new Vector3(6.4f, 0.2f, -9.0f)),
                Spec("clean-reference", "Controlled reference sample", "Distractor",
                    "This controlled sample helps compare safe staging against hazards.",
                    "clipboard_1k.fbx", new Vector3(5.3f, 0.2f, 3.6f), true)
            };
        }

        static EvidenceSpec[] WarehouseSpecs()
        {
            return new[]
            {
                Spec("blind-corner", "Blind corner evidence", "Struck-by",
                    "Pedestrian route and vehicle route conflict at this corner.",
                    "industrial_storage_cart_1k.fbx", new Vector3(-6.4f, 0.2f, -4.4f)),
                Spec("dock-egress", "Dock egress clue", "Egress",
                    "Exit travel needs a clear path through the dock zone.",
                    "rollershutter_door_1k.fbx", new Vector3(0f, 0.2f, 11.6f)),
                Spec("unstable-load", "Unstable load evidence", "Material handling",
                    "Stacked containers show a possible falling-object hazard.",
                    "plastic_crate_02_1k.fbx", new Vector3(6.6f, 0.2f, 7.1f)),
                Spec("route-barrier", "Route barrier clue", "Traffic control",
                    "Barrier placement should separate people from vehicle movement.",
                    "concrete_road_barrier_1k.fbx", new Vector3(5.1f, 0.2f, -2.6f)),
                Spec("pedestrian-walkway", "Pedestrian walkway evidence", "Traffic control",
                    "Marked pedestrian travel should remain physically separated from vehicle staging.",
                    "concrete_road_barrier_1k.fbx", new Vector3(2.8f, 0.2f, -2.8f)),
                Spec("shipping-paperwork", "Routine paperwork sample", "Distractor",
                    "Documentation alone is not a physical route hazard.",
                    "clipboard_1k.fbx", new Vector3(-2.8f, 0.2f, 4.5f), true)
            };
        }

        static EvidenceSpec[] FireSpecs()
        {
            return new[]
            {
                Spec("extinguisher-access", "Extinguisher access evidence", "Fire response",
                    "Portable extinguishers need visible and unobstructed access.",
                    "korean_fire_extinguisher_01_1k.fbx", new Vector3(-6.4f, 0.2f, -3.6f)),
                Spec("exit-obstruction", "Exit obstruction clue", "Egress",
                    "Stored objects can delay evacuation under smoke conditions.",
                    "old_military_crate_1k.fbx", new Vector3(6f, 0.2f, 6.4f)),
                Spec("smoke-origin", "Smoke origin evidence", "Emergency response",
                    "Smoke source location changes whether to fight or evacuate.",
                    "portable_generator_1k.fbx", new Vector3(0f, 0.2f, 8.6f)),
                Spec("alarm-route", "Alarm route clue", "Emergency response",
                    "Alarm and exit route should be verified before close approach.",
                    "rollershutter_door_1k.fbx", new Vector3(4.7f, 0.2f, -5.8f)),
                Spec("keep-clear-line", "Keep clear line evidence", "Egress",
                    "Emergency travel paths should stay clear even during extinguisher practice.",
                    "concrete_road_barrier_1k.fbx", new Vector3(1.8f, 0.2f, 7.6f)),
                Spec("clean-extinguisher", "Accessible reference extinguisher", "Distractor",
                    "This accessible extinguisher is a controlled comparison point.",
                    "korean_fire_extinguisher_01_1k.fbx", new Vector3(-1.8f, 0.2f, 2.5f), true)
            };
        }

        static EvidenceSpec[] ChemicalSpecs()
        {
            return new[]
            {
                Spec("unlabeled-container", "Unlabeled container clue", "HazCom",
                    "Unlabeled containers need quarantine and reporting before handling.",
                    "Barrel_01_1k.fbx", new Vector3(-6.8f, 0.2f, -4.5f)),
                Spec("sds-missing", "SDS lookup clue", "HazCom",
                    "The learner should check written hazard information before action.",
                    "clipboard_1k.fbx", new Vector3(-1.8f, 0.2f, -6.2f)),
                Spec("vapor-path", "Vapor path evidence", "Chemical exposure",
                    "Air movement and low vapor cues indicate isolation distance.",
                    "modular_industrial_pipes_01_1k.fbx", new Vector3(0f, 0.2f, 10.2f)),
                Spec("spill-kit-route", "Spill kit route clue", "Containment",
                    "Spill response equipment must be reachable without exposure.",
                    "hand_truck_1k.fbx", new Vector3(6.4f, 0.2f, 5.8f)),
                Spec("secondary-containment", "Secondary containment evidence", "Containment",
                    "Containment boundary should limit spread before cleanup begins.",
                    "concrete_road_barrier_1k.fbx", new Vector3(3.8f, 0.2f, -2.9f)),
                Spec("sealed-drum", "Sealed reference drum", "Distractor",
                    "A sealed labeled container is useful as a safe comparison.",
                    "Barrel_01_1k.fbx", new Vector3(4.2f, 0.2f, -2.7f), true)
            };
        }

        static EvidenceSpec[] ElectricalSpecs()
        {
            return new[]
            {
                Spec("open-panel", "Open panel evidence", "Electrical",
                    "Exposed energized parts need qualified-person isolation.",
                    "utility_box_01_1k.fbx", new Vector3(-5.8f, 0.2f, 9.2f)),
                Spec("loto-point", "LOTO point clue", "Lockout tagout",
                    "Energy isolation needs lock, tag, and verification before work.",
                    "metal_toolbox_1k.fbx", new Vector3(6.4f, 0.2f, -4.4f)),
                Spec("damaged-cord", "Damaged cord evidence", "Electrical",
                    "Damaged flexible cord should be removed from service.",
                    "Drill_01_1k.fbx", new Vector3(-4.5f, 0.2f, -3.4f)),
                Spec("wet-floor-interface", "Wet floor interface clue", "Electrical",
                    "Water near powered equipment changes the control priority.",
                    "concrete_road_barrier_1k.fbx", new Vector3(1.8f, 0.2f, 5.4f)),
                Spec("arc-flash-boundary", "Arc flash boundary evidence", "Electrical",
                    "Approach boundary should be visible before any energized-panel interaction.",
                    "concrete_road_barrier_1k.fbx", new Vector3(-1.8f, 0.2f, 5.8f)),
                Spec("closed-panel", "Closed panel reference", "Distractor",
                    "A closed controlled panel is a comparison point, not the issue.",
                    "utility_box_01_1k.fbx", new Vector3(3.8f, 0.2f, 8.2f), true)
            };
        }

        static EvidenceSpec Spec(string id, string title, string hazardType, string note,
            string asset, Vector3 position, bool distractor = false)
        {
            return new EvidenceSpec(id, title, hazardType, note, asset, position, distractor);
        }
    }
}
