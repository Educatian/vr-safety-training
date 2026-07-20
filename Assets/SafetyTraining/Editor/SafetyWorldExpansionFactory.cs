using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEngine;

namespace SafetyTraining.Editor
{
    internal static class SafetyWorldExpansionFactory
    {
        const float Width = 30f;
        const float Depth = 26f;
        const float ZoneRadius = 19f;

        readonly struct AnalyticsSpec
        {
            public AnalyticsSpec(string id, string title, Vector3 position, Vector3 size)
            {
                Id = id;
                Title = title;
                Position = position;
                Size = size;
            }

            public string Id { get; }
            public string Title { get; }
            public Vector3 Position { get; }
            public Vector3 Size { get; }
        }

        public static void ExpandAll(params Transform[] sites)
        {
            foreach (var site in sites)
            {
                var zone = site.GetComponent<SiteExperienceZone>();
                if (zone == null)
                    continue;

                site.GetComponent<SiteExperienceZone>().Configure(zone.SiteId, ZoneRadius);
                ExpandBase(site);
                ExpandBoundaries(site);
                AddRouteLoop(site, zone.SiteId);
                AddAnalyticsZones(site, zone.SiteId);
                AddModuleDressing(site, zone.SiteId);
                AddOperationalSubzones(site, zone.SiteId);
                SafetyInquiryContentFactory.AddInquiryContent(site, zone.SiteId);
                SafetyWorldAssetPainter.MarkStaticEnvironment(site);
            }
        }

        static void ExpandBase(Transform site)
        {
            var floor = site.Find("Floor");
            if (floor != null)
                floor.localScale = new Vector3(Width, 0.3f, Depth);

            var groundPatch = SafetyScenePrimitives.Primitive(PrimitiveType.Cube,
                "WorldExpansion - Rough Concrete Apron", site, new Vector3(0f, 0.012f, 0f),
                new Vector3(Width - 1.2f, 0.024f, Depth - 1.2f), Color.white);
            groundPatch.GetComponent<Renderer>().sharedMaterial = RealEnvironmentMaterials.DamagedConcrete;

            AddPath(site, "Entry Spine", new Vector3(0f, 0.04f, -6.4f), new Vector3(2.4f, 0.035f, 10.4f));
            AddPath(site, "Left Evidence Loop", new Vector3(-5.2f, 0.045f, 1.4f), new Vector3(2.2f, 0.035f, 15.8f));
            AddPath(site, "Rear Traverse", new Vector3(0f, 0.05f, 8.8f), new Vector3(13.4f, 0.035f, 2.2f));
            AddPath(site, "Right Return Loop", new Vector3(5.2f, 0.045f, 1.4f), new Vector3(2.2f, 0.035f, 15.8f));
        }

        static void AddPath(Transform site, string name, Vector3 position, Vector3 scale)
        {
            var path = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, $"WorldExpansion - Route - {name}", site,
                position, scale, Color.white);
            path.GetComponent<Renderer>().sharedMaterial = RealEnvironmentMaterials.Asphalt;
            Object.DestroyImmediate(path.GetComponent<Collider>());
        }

        static void ExpandBoundaries(Transform site)
        {
            SetCollider(site, "Isolation Collider Left", new Vector3(-15.15f, 2.1f, 0f),
                new Vector3(0.25f, 4.2f, Depth));
            SetCollider(site, "Isolation Collider Right", new Vector3(15.15f, 2.1f, 0f),
                new Vector3(0.25f, 4.2f, Depth));
            SetCollider(site, "Isolation Collider Rear", new Vector3(0f, 2.1f, 13.15f),
                new Vector3(Width, 4.2f, 0.25f));
            SetCollider(site, "Isolation Collider Entry Left", new Vector3(-9.4f, 2.1f, -13.15f),
                new Vector3(11.2f, 4.2f, 0.25f));
            SetCollider(site, "Isolation Collider Entry Right", new Vector3(9.4f, 2.1f, -13.15f),
                new Vector3(11.2f, 4.2f, 0.25f));
            SetCollider(site, "Isolation Collider Entry Center", new Vector3(0f, 2.1f, -13.15f),
                new Vector3(5.1f, 4.2f, 0.25f));

            SafetyWorldAssetPainter.AddModel(site, "North Chainlink Perimeter", "modular_chainlink_fence_1k.fbx",
                new Vector3(-7.5f, 0f, 12.4f), 8.5f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "North Chainlink Perimeter B", "modular_chainlink_fence_1k.fbx",
                site.name == "Construction Site" ? new Vector3(40f, 0f, 40f) : new Vector3(7.5f, 0f, 12.4f),
                8.5f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Left Chainlink Perimeter", "modular_chainlink_fence_1k.fbx",
                new Vector3(-14.2f, 0f, 1f), 10f, Vector3.zero);
            SafetyWorldAssetPainter.AddModel(site, "Right Chainlink Perimeter", "modular_chainlink_fence_1k.fbx",
                site.name == "Construction Site" ? new Vector3(40f, 0f, -40f) : new Vector3(14.2f, 0f, 1f),
                10f, Vector3.zero);
        }

        static void SetCollider(Transform site, string name, Vector3 center, Vector3 size)
        {
            var item = site.Find(name);
            if (item == null || !item.TryGetComponent<BoxCollider>(out var collider))
                return;
            item.localPosition = center;
            collider.size = size;
        }

        static void AddAnalyticsZones(Transform site, TrainingSiteId siteId)
        {
            foreach (var spec in SpecsFor(siteId))
            {
                var zone = new GameObject($"Analytics Zone - {spec.Title}");
                zone.transform.SetParent(site, false);
                zone.transform.localPosition = spec.Position;
                var collider = zone.AddComponent<BoxCollider>();
                collider.size = spec.Size;
                zone.AddComponent<SpatialAnalyticsZone>().Configure(siteId, spec.Id, spec.Title);
            }
        }

        static AnalyticsSpec[] SpecsFor(TrainingSiteId siteId)
        {
            var prefix = siteId.ToString().ToLowerInvariant();
            if (siteId == TrainingSiteId.Construction)
            {
                return new[]
                {
                    new AnalyticsSpec($"{prefix}_entry", "Entry Orientation", new Vector3(0f, 1.2f, -7.6f),
                        new Vector3(7f, 2.4f, 6f)),
                    new AnalyticsSpec($"{prefix}_material_yard", "Material Yard Evidence", new Vector3(-6.4f, 1.2f, -5.4f),
                        new Vector3(7.5f, 2.4f, 6.4f)),
                    new AnalyticsSpec($"{prefix}_left_evidence", "Left Evidence Run", new Vector3(-5.2f, 1.2f, 0.8f),
                        new Vector3(5.8f, 2.4f, 5.8f)),
                    new AnalyticsSpec($"{prefix}_formwork_access", "Formwork Access Check", new Vector3(-9.6f, 1.2f, 6.2f),
                        new Vector3(6.2f, 2.4f, 8.4f)),
                    new AnalyticsSpec($"{prefix}_crane_bay", "Crane and Steel Bay", new Vector3(1.5f, 1.2f, 8.8f),
                        new Vector3(11f, 2.4f, 7.2f)),
                    new AnalyticsSpec($"{prefix}_utility_permit", "Utility and Permit Review", new Vector3(8.2f, 1.2f, -6.3f),
                        new Vector3(7.2f, 2.4f, 6.8f)),
                    new AnalyticsSpec($"{prefix}_debrief_return", "Debrief Return Route", new Vector3(5.2f, 1.2f, 0.6f),
                        new Vector3(6f, 2.4f, 9.2f))
                };
            }
            if (siteId == TrainingSiteId.Warehouse)
            {
                return new[]
                {
                    new AnalyticsSpec($"{prefix}_entry", "Entry Orientation", new Vector3(0f, 1.2f, -7.6f),
                        new Vector3(7f, 2.4f, 6f)),
                    new AnalyticsSpec($"{prefix}_left_evidence", "Left Evidence Run", new Vector3(-5.2f, 1.2f, 0.8f),
                        new Vector3(6f, 2.4f, 12f)),
                    new AnalyticsSpec($"{prefix}_dock_staging", "Dock Staging Review", new Vector3(0f, 1.2f, 9.4f),
                        new Vector3(10f, 2.4f, 6.2f)),
                    new AnalyticsSpec($"{prefix}_load_stability", "Load Stability Check", new Vector3(6.5f, 1.2f, 6.6f),
                        new Vector3(6.2f, 2.4f, 6.2f)),
                    new AnalyticsSpec($"{prefix}_traffic_control", "Traffic Control Decision", new Vector3(5.2f, 1.2f, -2.8f),
                        new Vector3(6f, 2.4f, 7f)),
                    new AnalyticsSpec($"{prefix}_right_return", "Right Return Route", new Vector3(5.2f, 1.2f, 0.8f),
                        new Vector3(6f, 2.4f, 12f))
                };
            }
            if (siteId == TrainingSiteId.FireResponse)
            {
                return new[]
                {
                    new AnalyticsSpec($"{prefix}_entry", "Entry Orientation", new Vector3(0f, 1.2f, -7.6f),
                        new Vector3(7f, 2.4f, 6f)),
                    new AnalyticsSpec($"{prefix}_left_evidence", "Left Evidence Run", new Vector3(-5.2f, 1.2f, 0.8f),
                        new Vector3(6f, 2.4f, 12f)),
                    new AnalyticsSpec($"{prefix}_extinguisher_access", "Extinguisher Access Check", new Vector3(-6.2f, 1.2f, -3.5f),
                        new Vector3(5.6f, 2.4f, 5.4f)),
                    new AnalyticsSpec($"{prefix}_egress_door", "Egress Door Review", new Vector3(0f, 1.2f, 10.2f),
                        new Vector3(10f, 2.4f, 5.8f)),
                    new AnalyticsSpec($"{prefix}_alarm_route", "Alarm Route Decision", new Vector3(4.8f, 1.2f, -5.8f),
                        new Vector3(5.4f, 2.4f, 5.8f)),
                    new AnalyticsSpec($"{prefix}_right_return", "Right Return Route", new Vector3(5.2f, 1.2f, 0.8f),
                        new Vector3(6f, 2.4f, 12f))
                };
            }
            if (siteId == TrainingSiteId.ChemicalProcessing)
            {
                return new[]
                {
                    new AnalyticsSpec($"{prefix}_entry", "Entry Orientation", new Vector3(0f, 1.2f, -7.6f),
                        new Vector3(7f, 2.4f, 6f)),
                    new AnalyticsSpec($"{prefix}_left_evidence", "Left Evidence Run", new Vector3(-5.2f, 1.2f, 0.8f),
                        new Vector3(6f, 2.4f, 12f)),
                    new AnalyticsSpec($"{prefix}_container_label", "Container Label Check", new Vector3(-6.8f, 1.2f, -4.5f),
                        new Vector3(5.8f, 2.4f, 5.8f)),
                    new AnalyticsSpec($"{prefix}_sds_station", "SDS Lookup Station", new Vector3(-1.8f, 1.2f, -6.2f),
                        new Vector3(5f, 2.4f, 5f)),
                    new AnalyticsSpec($"{prefix}_vapor_path", "Vapor Path Review", new Vector3(0f, 1.2f, 9.8f),
                        new Vector3(11f, 2.4f, 6f)),
                    new AnalyticsSpec($"{prefix}_right_return", "Right Return Route", new Vector3(5.2f, 1.2f, 0.8f),
                        new Vector3(6f, 2.4f, 12f))
                };
            }
            if (siteId == TrainingSiteId.ElectricalMaintenance)
            {
                return new[]
                {
                    new AnalyticsSpec($"{prefix}_entry", "Entry Orientation", new Vector3(0f, 1.2f, -7.6f),
                        new Vector3(7f, 2.4f, 6f)),
                    new AnalyticsSpec($"{prefix}_left_evidence", "Left Evidence Run", new Vector3(-5.2f, 1.2f, 0.8f),
                        new Vector3(6f, 2.4f, 12f)),
                    new AnalyticsSpec($"{prefix}_loto_station", "LOTO Station Check", new Vector3(6.4f, 1.2f, -4.4f),
                        new Vector3(5.8f, 2.4f, 5.8f)),
                    new AnalyticsSpec($"{prefix}_panel_row", "Panel Row Review", new Vector3(-2f, 1.2f, 9.2f),
                        new Vector3(10f, 2.4f, 5.8f)),
                    new AnalyticsSpec($"{prefix}_cord_hazard", "Cord Hazard Decision", new Vector3(-4.5f, 1.2f, -3.4f),
                        new Vector3(5.6f, 2.4f, 5.6f)),
                    new AnalyticsSpec($"{prefix}_right_return", "Right Return Route", new Vector3(5.2f, 1.2f, 0.8f),
                        new Vector3(6f, 2.4f, 12f))
                };
            }
            return new[]
            {
                new AnalyticsSpec($"{prefix}_entry", "Entry Orientation", new Vector3(0f, 1.2f, -7.6f),
                    new Vector3(7f, 2.4f, 6f)),
                new AnalyticsSpec($"{prefix}_left_evidence", "Left Evidence Run", new Vector3(-5.2f, 1.2f, 0.8f),
                    new Vector3(6f, 2.4f, 12f)),
                new AnalyticsSpec($"{prefix}_rear_task", "Rear Task Bay", new Vector3(0f, 1.2f, 8.6f),
                    new Vector3(12f, 2.4f, 6f)),
                new AnalyticsSpec($"{prefix}_right_return", "Right Return Route", new Vector3(5.2f, 1.2f, 0.8f),
                    new Vector3(6f, 2.4f, 12f))
            };
        }

        static void AddRouteLoop(Transform site, TrainingSiteId siteId)
        {
            AddWaypoint(site, "01 Evidence Gate", new Vector3(0f, 0.12f, -10.2f), siteId);
            AddWaypoint(site, "02 Side Investigation", new Vector3(-5.2f, 0.12f, -3.4f), siteId);
            AddWaypoint(site, "03 High-Value Task Bay", new Vector3(0f, 0.12f, 8.8f), siteId);
            AddWaypoint(site, "04 Debrief Return", new Vector3(5.2f, 0.12f, -3.4f), siteId);
        }

        static void AddWaypoint(Transform site, string label, Vector3 position, TrainingSiteId siteId)
        {
            var marker = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder,
                $"WorldExpansion - Route Marker - {label}", site, position,
                new Vector3(0.42f, 0.04f, 0.42f), ColorFor(siteId));
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var text = SafetyScenePrimitives.Label(label.ToUpperInvariant(), site,
                position + new Vector3(0f, 0.58f, 0f), 0.08f);
            text.color = Color.white;
        }

        static void AddModuleDressing(Transform site, TrainingSiteId siteId)
        {
            switch (siteId)
            {
                case TrainingSiteId.Construction:
                    AddConstruction(site);
                    break;
                case TrainingSiteId.Warehouse:
                    AddWarehouse(site);
                    break;
                case TrainingSiteId.FireResponse:
                    AddFire(site);
                    break;
                case TrainingSiteId.ChemicalProcessing:
                    AddChemical(site);
                    break;
                case TrainingSiteId.ElectricalMaintenance:
                    AddElectrical(site);
                    break;
            }
        }

        static void AddConstruction(Transform site)
        {

            SafetyWorldAssetPainter.AddModel(site, "Facade Skeleton Wing A", "modular_factory_facade_1k.fbx",
                new Vector3(-7f, 0f, 6.5f), 6.2f, Vector3.zero);
            SafetyWorldAssetPainter.AddModel(site, "Generator Dust Source", "portable_generator_1k.fbx",
                new Vector3(-11.2f, 0f, -4.2f), 2.2f, new Vector3(0f, 135f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Material Evidence Stack", "cement_bag_1k.fbx",
                new Vector3(-6.8f, 0f, -5f), 1.8f, new Vector3(0f, -10f, 0f));
            SafetyWorldAssetPainter.AddAmbientDust(site, "Construction Dust Plume",
                new Vector3(0f, 0.45f, 8f), new Color(0.74f, 0.68f, 0.56f, 0.55f), 52);
            USConstructionContextFactory.AddOptimizedProps(site);
        }

        static void AddWarehouse(Transform site)
        {
            SafetyWorldAssetPainter.AddModel(site, "Dock Door Rear", "rollershutter_door_1k.fbx",
                new Vector3(0f, 0f, 12.15f), 5.4f, new Vector3(0f, 180f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Receiving Cart", "industrial_storage_cart_1k.fbx",
                new Vector3(-6.5f, 0f, -4.3f), 1.7f, new Vector3(0f, 170f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Pallet Evidence Stack", "plastic_crate_02_1k.fbx",
                new Vector3(6.6f, 0f, 7.2f), 1.6f, new Vector3(0f, 12f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Pedestrian Walkway Barrier", "concrete_road_barrier_1k.fbx",
                new Vector3(2.8f, 0f, -2.8f), 2.2f, new Vector3(0f, 84f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Receiving Paperwork Stand", "clipboard_1k.fbx",
                new Vector3(-2.4f, 0.24f, 4.2f), 1f, new Vector3(0f, 18f, 0f));
            SafetyWorldAssetPainter.AddAmbientDust(site, "Dock Dust Beam",
                new Vector3(0f, 1.1f, 9.4f), new Color(0.62f, 0.68f, 0.7f, 0.42f), 36);
        }

        static void AddFire(Transform site)
        {
            SafetyWorldAssetPainter.AddModel(site, "Egress Door Rear", "rollershutter_door_1k.fbx",
                new Vector3(0f, 0f, 12.15f), 5.2f, new Vector3(0f, 180f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Extinguisher Practice Stand", "korean_fire_extinguisher_01_1k.fbx",
                new Vector3(-6.4f, 0f, -3.6f), 1.7f, new Vector3(-90f, 0f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Obstruction Evidence Stack", "old_military_crate_1k.fbx",
                new Vector3(6f, 0f, 6.6f), 1.8f, new Vector3(0f, -8f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Alarm Route Marker", "clipboard_1k.fbx",
                new Vector3(4.8f, 0.24f, -5.8f), 1f, new Vector3(0f, -16f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Egress Keep Clear Barrier", "concrete_road_barrier_1k.fbx",
                new Vector3(1.8f, 0f, 7.6f), 2.15f, new Vector3(0f, 78f, 0f));
            SafetyWorldAssetPainter.AddAmbientDust(site, "Training Smoke Cue",
                new Vector3(0f, 0.65f, 8.8f), new Color(0.44f, 0.46f, 0.48f, 0.5f), 48);
        }

        static void AddChemical(Transform site)
        {
            SafetyWorldAssetPainter.AddModel(site, "Pipe Rack Rear", "modular_industrial_pipes_01_1k.fbx",
                new Vector3(-3.6f, 0f, 3.35f), 3.2f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Transfer Drum Cluster", "Barrel_01_1k.fbx",
                new Vector3(-6.8f, 0f, -4.5f), 1.8f, new Vector3(90f, 0f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Waste Staging Cart", "hand_truck_1k.fbx",
                new Vector3(6.4f, 0f, 5.9f), 1.4f, new Vector3(0f, 180f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "SDS Binder Station", "clipboard_1k.fbx",
                new Vector3(-1.8f, 0.24f, -6.2f), 1f, new Vector3(0f, 22f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Secondary Containment Barrier", "concrete_road_barrier_1k.fbx",
                new Vector3(3.8f, 0f, -2.9f), 2f, new Vector3(0f, 82f, 0f));
            SafetyWorldAssetPainter.AddAmbientDust(site, "Low Vapor Leak Cue",
                new Vector3(-5.8f, 0.5f, -3.9f), new Color(0.42f, 0.68f, 0.6f, 0.42f), 32);
        }

        static void AddElectrical(Transform site)
        {
            SafetyWorldAssetPainter.AddModel(site, "Panel Row Rear", "utility_box_01_1k.fbx",
                new Vector3(-5.8f, 0f, 9.2f), 2.4f, Vector3.zero);
            SafetyWorldAssetPainter.AddModel(site, "Panel Row Rear B", "utility_box_01_1k.fbx",
                new Vector3(0f, 0f, 9.2f), 2.4f, Vector3.zero);
            SafetyWorldAssetPainter.AddModel(site, "Verification Toolbox", "metal_toolbox_1k.fbx",
                new Vector3(6.4f, 0f, -4.4f), 1.2f, new Vector3(0f, 18f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Arc Flash Boundary Barrier", "concrete_road_barrier_1k.fbx",
                new Vector3(-1.8f, 0f, 5.8f), 2.25f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Damaged Cord Drill", "Drill_01_1k.fbx",
                new Vector3(-4.4f, 0.26f, -3.4f), 0.7f, new Vector3(0f, -20f, 0f));
            SafetyWorldAssetPainter.AddSparkCue(site, new Vector3(-2.9f, 1.45f, 9.1f));
        }

        static void AddOperationalSubzones(Transform site, TrainingSiteId siteId)
        {
            switch (siteId)
            {
                case TrainingSiteId.Warehouse:
                    AddSubzone(site, "Receiving Dock", new Vector3(-7.5f, 0f, -6.8f),
                        "hand_truck_1k.fbx", 1.25f, new Vector3(0f, 24f, 0f), ColorFor(siteId));
                    AddSubzone(site, "High-Bay Storage Aisle", new Vector3(-7.5f, 0f, 6.3f),
                        "plastic_crate_02_1k.fbx", 1.55f, new Vector3(0f, -8f, 0f), ColorFor(siteId));
                    AddSubzone(site, "Forklift Battery Service", new Vector3(7.5f, 0f, 6.3f),
                        "portable_generator_1k.fbx", 1.65f, new Vector3(0f, 145f, 0f), ColorFor(siteId));
                    break;
                case TrainingSiteId.FireResponse:
                    AddSubzone(site, "Hot-Work Origin Bay", new Vector3(-7.5f, 0f, 6.2f),
                        "portable_generator_1k.fbx", 1.7f, new Vector3(0f, 128f, 0f), ColorFor(siteId));
                    AddSubzone(site, "Protected Egress Corridor", new Vector3(0f, 0f, 10.2f),
                        "rollershutter_door_1k.fbx", 3.6f, new Vector3(0f, 180f, 0f), ColorFor(siteId));
                    AddSubzone(site, "Incident Command Muster", new Vector3(7.5f, 0f, -6.4f),
                        "clipboard_1k.fbx", 0.95f, new Vector3(0f, -18f, 0f), ColorFor(siteId));
                    break;
                case TrainingSiteId.ChemicalProcessing:
                    AddSubzone(site, "Unloading and Transfer", new Vector3(-7.6f, 0f, -6.6f),
                        "Barrel_01_1k.fbx", 1.55f, new Vector3(90f, 0f, 0f), ColorFor(siteId));
                    AddSubzone(site, "Flammable Storage Process", new Vector3(-6.8f, 0f, 7.3f),
                        "modular_industrial_pipes_01_1k.fbx", 2.7f, new Vector3(0f, 90f, 0f), ColorFor(siteId));
                    AddSubzone(site, "Eyewash and Decon", new Vector3(7.5f, 0f, 6.2f),
                        "hand_truck_1k.fbx", 1.25f, new Vector3(0f, 180f, 0f), ColorFor(siteId));
                    break;
                case TrainingSiteId.ElectricalMaintenance:
                    AddSubzone(site, "MCC and Panel Room", new Vector3(0f, 0f, 9.6f),
                        "utility_box_01_1k.fbx", 2.15f, Vector3.zero, ColorFor(siteId));
                    AddSubzone(site, "LOTO Preparation", new Vector3(7.5f, 0f, -6.2f),
                        "metal_toolbox_1k.fbx", 1.15f, new Vector3(0f, 16f, 0f), ColorFor(siteId));
                    AddSubzone(site, "Cable Trench Service", new Vector3(-7.5f, 0f, 4.2f),
                        "Drill_01_1k.fbx", 0.72f, new Vector3(0f, -25f, 0f), ColorFor(siteId));
                    break;
            }
        }

        static void AddSubzone(Transform site, string title, Vector3 position, string asset,
            float assetSize, Vector3 assetRotation, Color color)
        {
            var root = new GameObject($"Operational Subzone - {title}");
            root.transform.SetParent(site, false);
            root.transform.localPosition = position;

            foreach (var x in new[] { -2.05f, 2.05f })
            {
                var post = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder,
                    $"Subzone Post - {title}", root.transform, new Vector3(x, 1.05f, 0f),
                    new Vector3(0.075f, 1.05f, 0.075f), color);
                Object.DestroyImmediate(post.GetComponent<Collider>());
            }
            var header = SafetyScenePrimitives.Primitive(PrimitiveType.Cube,
                $"Subzone Header - {title}", root.transform, new Vector3(0f, 2.08f, 0f),
                new Vector3(4.25f, 0.18f, 0.12f), color);
            Object.DestroyImmediate(header.GetComponent<Collider>());
            var label = SafetyScenePrimitives.Label(title.ToUpperInvariant(), root.transform,
                new Vector3(0f, 2.38f, 0f), 0.085f);
            label.color = Color.white;

            SafetyWorldAssetPainter.AddModel(site, $"{title} Equipment Anchor", asset,
                position + new Vector3(0f, 0f, 1.35f), assetSize, assetRotation);
        }
        static Color ColorFor(TrainingSiteId siteId)
        {
            return siteId switch
            {
                TrainingSiteId.Construction => new Color(0.95f, 0.55f, 0.08f),
                TrainingSiteId.Warehouse => new Color(0.12f, 0.48f, 0.85f),
                TrainingSiteId.FireResponse => new Color(0.78f, 0.16f, 0.1f),
                TrainingSiteId.ChemicalProcessing => new Color(0.26f, 0.65f, 0.52f),
                TrainingSiteId.ElectricalMaintenance => new Color(0.32f, 0.38f, 0.72f),
                _ => Color.white
            };
        }
    }
}
