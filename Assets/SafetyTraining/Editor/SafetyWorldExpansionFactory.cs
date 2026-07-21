using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEngine;

namespace SafetyTraining.Editor
{
    internal static class SafetyWorldExpansionFactory
    {
        const float Width = 30f;
        const float Depth = 26f;
        const float ZoneRadius = 24f;
        const float AnnexDepth = 10f;
        const float AnnexCenterZ = 16.6f;

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
                AddRearAnnex(site, zone.SiteId);
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
            var sideSpan = Depth / 2f + 0.3f + AnnexCenterZ + AnnexDepth / 2f;
            var sideCenterZ = (AnnexCenterZ + AnnexDepth / 2f - (Depth / 2f + 0.3f)) / 2f;
            SetCollider(site, "Isolation Collider Left", new Vector3(-15.15f, 2.1f, sideCenterZ),
                new Vector3(0.25f, 4.2f, sideSpan));
            SetCollider(site, "Isolation Collider Right", new Vector3(15.15f, 2.1f, sideCenterZ),
                new Vector3(0.25f, 4.2f, sideSpan));
            SetCollider(site, "Isolation Collider Rear",
                new Vector3(0f, 2.1f, AnnexCenterZ + AnnexDepth / 2f + 0.15f),
                new Vector3(Width, 4.2f, 0.25f));
            SetCollider(site, "Isolation Collider Entry Left", new Vector3(-9.4f, 2.1f, -13.15f),
                new Vector3(11.2f, 4.2f, 0.25f));
            SetCollider(site, "Isolation Collider Entry Right", new Vector3(9.4f, 2.1f, -13.15f),
                new Vector3(11.2f, 4.2f, 0.25f));
            SetCollider(site, "Isolation Collider Entry Center", new Vector3(0f, 2.1f, -13.15f),
                new Vector3(5.1f, 4.2f, 0.25f));
            CreateEntryGates(site);
            CreatePerimeterHoarding(site, sideCenterZ, sideSpan);

            SafetyWorldAssetPainter.AddModel(site, "North Chainlink Perimeter", "modular_chainlink_fence_1k.fbx",
                new Vector3(-7.5f, 0f, 21.4f), 8.5f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "North Chainlink Perimeter B", "modular_chainlink_fence_1k.fbx",
                site.name == "Construction Site" ? new Vector3(40f, 0f, 40f) : new Vector3(7.5f, 0f, 21.4f),
                8.5f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Left Chainlink Perimeter", "modular_chainlink_fence_1k.fbx",
                new Vector3(-14.2f, 0f, 1f), 10f, Vector3.zero);
            SafetyWorldAssetPainter.AddModel(site, "Right Chainlink Perimeter", "modular_chainlink_fence_1k.fbx",
                site.name == "Construction Site" ? new Vector3(40f, 0f, -40f) : new Vector3(14.2f, 0f, 1f),
                10f, Vector3.zero);
            SafetyWorldAssetPainter.AddModel(site, "Annex Left Chainlink", "modular_chainlink_fence_1k.fbx",
                new Vector3(-14.2f, 0f, AnnexCenterZ), 10f, Vector3.zero);
            SafetyWorldAssetPainter.AddModel(site, "Annex Right Chainlink", "modular_chainlink_fence_1k.fbx",
                new Vector3(14.2f, 0f, AnnexCenterZ), 10f, Vector3.zero);
        }

        // Rear annex yard: walkable expansion behind each module with its own
        // analytics zones so movement data keeps spatial resolution, plus staged
        // laydown/drill areas reserved for future item pairs.
        static void AddRearAnnex(Transform site, TrainingSiteId siteId)
        {
            var slab = SafetyScenePrimitives.Primitive(PrimitiveType.Cube,
                "WorldExpansion - Annex Yard Slab", site,
                new Vector3(0f, 0.008f, AnnexCenterZ),
                new Vector3(Width - 1.6f, 0.016f, AnnexDepth), Color.white);
            slab.GetComponent<Renderer>().sharedMaterial = RealEnvironmentMaterials.DamagedConcrete;
            AddPath(site, "Annex Spine", new Vector3(0f, 0.05f, AnnexCenterZ - 1f),
                new Vector3(2.4f, 0.035f, AnnexDepth + 2.2f));

            var prefix = siteId.ToString().ToLowerInvariant();
            CreateAnalyticsZone(site, siteId, $"{prefix}_annex_laydown", "Annex Laydown Yard",
                new Vector3(-7f, 1.2f, AnnexCenterZ), new Vector3(13.5f, 2.4f, AnnexDepth - 1f));
            CreateAnalyticsZone(site, siteId, $"{prefix}_annex_drill", "Annex Drill Yard",
                new Vector3(7f, 1.2f, AnnexCenterZ), new Vector3(13.5f, 2.4f, AnnexDepth - 1f));

            AddWaypoint(site, "05 Annex Yard", new Vector3(0f, 0.12f, AnnexCenterZ - 2.4f), siteId);
            AddSubzone(site, "Annex Laydown Yard", new Vector3(-7f, 0f, AnnexCenterZ + 1.6f),
                "plastic_crate_02_1k.fbx", 1.5f, new Vector3(0f, 12f, 0f), ColorFor(siteId));
            AddSubzone(site, "Annex Drill Yard", new Vector3(7f, 0f, AnnexCenterZ + 1.6f),
                "concrete_road_barrier_1k.fbx", 1.6f, new Vector3(0f, -95f, 0f), ColorFor(siteId));
            SafetyWorldAssetPainter.AddModel(site, "Annex Hand Truck", "hand_truck_1k.fbx",
                new Vector3(-3.4f, 0f, AnnexCenterZ - 2.2f), 1.2f, new Vector3(0f, 205f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Annex Generator", "portable_generator_1k.fbx",
                new Vector3(3.6f, 0f, AnnexCenterZ + 3f), 1.5f, new Vector3(0f, 40f, 0f));
            foreach (var x in new[] { -7f, 7f })
            {
                var lightObject = new GameObject("Annex Work Light");
                lightObject.transform.SetParent(site, false);
                lightObject.transform.localPosition = new Vector3(x, 3.4f, AnnexCenterZ);
                var light = lightObject.AddComponent<Light>();
                light.type = LightType.Point;
                light.color = new Color(1f, 0.92f, 0.76f);
                light.intensity = 1.3f;
                light.range = 9f;
                light.shadows = LightShadows.None;
            }
        }

        static void CreateAnalyticsZone(Transform site, TrainingSiteId siteId, string id,
            string title, Vector3 position, Vector3 size)
        {
            var zone = new GameObject($"Analytics Zone - {title}");
            zone.transform.SetParent(site, false);
            zone.transform.localPosition = position;
            var collider = zone.AddComponent<BoxCollider>();
            collider.size = size;
            zone.AddComponent<SpatialAnalyticsZone>().Configure(siteId, id, title);
        }

        // The world expansion enlarged the walkable envelope to +-15m but left the
        // opaque hoarding at the original compact perimeter, so the outer edge was
        // invisible colliders with a bare skybox horizon behind them - the
        // "cliff-like edges" testers flagged in HMD. Wrap the true boundary in the
        // same corrugated hoarding so every site edge reads as a closed jobsite.
        static void CreatePerimeterHoarding(Transform site, float sideCenterZ, float sideSpan)
        {
            RealEnvironmentDresser.CreateCorrugatedHoarding(site, "Expansion Hoarding Entry",
                new Vector3(0f, 0f, -13.32f), 30.7f, 4.2f, 0f);
            RealEnvironmentDresser.CreateCorrugatedHoarding(site, "Expansion Hoarding Rear",
                new Vector3(0f, 0f, sideCenterZ + sideSpan / 2f + 0.17f), 30.7f, 4.2f, 180f);
            RealEnvironmentDresser.CreateCorrugatedHoarding(site, "Expansion Hoarding Left",
                new Vector3(-15.32f, 0f, sideCenterZ), sideSpan + 0.4f, 4.2f, 90f);
            RealEnvironmentDresser.CreateCorrugatedHoarding(site, "Expansion Hoarding Right",
                new Vector3(15.32f, 0f, sideCenterZ), sideSpan + 0.4f, 4.2f, -90f);
        }

        // The entry wall segments leave two 1.25m doorway openings that let the
        // learner walk (and see) straight out into the void beyond the site slab.
        // Seal both with solid gate panels: matching metal sheet, hazard stripe,
        // and a full-height collider.
        static void CreateEntryGates(Transform site)
        {
            foreach (var x in new[] { -3.175f, 3.175f })
            {
                var suffix = x < 0f ? "Left" : "Right";
                var gate = SafetyScenePrimitives.Primitive(PrimitiveType.Cube,
                    $"Entry Gate {suffix}", site,
                    new Vector3(x, 2.1f, -13.15f), new Vector3(1.35f, 4.2f, 0.22f), Color.white);
                gate.GetComponent<Renderer>().sharedMaterial = RealEnvironmentMaterials.MetalSheet;
                var stripe = SafetyScenePrimitives.Primitive(PrimitiveType.Cube,
                    $"Entry Gate Stripe {suffix}", site,
                    new Vector3(x, 1.05f, -13.02f), new Vector3(1.35f, 0.14f, 0.03f),
                    new Color(0.98f, 0.68f, 0.08f));
                Object.DestroyImmediate(stripe.GetComponent<Collider>());
            }
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
            if (siteId == TrainingSiteId.TowerCrane)
            {
                return new[]
                {
                    new AnalyticsSpec($"{prefix}_entry", "Entry Orientation", new Vector3(0f, 1.2f, -7.6f),
                        new Vector3(7f, 2.4f, 6f)),
                    new AnalyticsSpec($"{prefix}_left_evidence", "Left Evidence Run", new Vector3(-5.2f, 1.2f, 0.8f),
                        new Vector3(6f, 2.4f, 12f)),
                    new AnalyticsSpec($"{prefix}_lift_corridor", "Lift Corridor Watch", new Vector3(0f, 1.2f, 0.5f),
                        new Vector3(4f, 2.4f, 7.4f)),
                    new AnalyticsSpec($"{prefix}_landing_zone", "Panel Landing Review", new Vector3(2.6f, 1.2f, -2.6f),
                        new Vector3(3.4f, 2.4f, 3f)),
                    new AnalyticsSpec($"{prefix}_crane_base", "Crane Base and Rigging", new Vector3(3f, 1.2f, 8.6f),
                        new Vector3(9f, 2.4f, 6f)),
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
                position + new Vector3(0f, 0.42f, 0f), 0.05f);
            text.color = new Color(1f, 1f, 1f, 0.75f);
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
                case TrainingSiteId.TowerCrane:
                    SafetyWorldAssetPainter.AddModel(site, "Facade Skeleton Tower Wing", "modular_factory_facade_1k.fbx",
                        new Vector3(-7f, 0f, 6.5f), 6.2f, Vector3.zero);
                    SafetyWorldAssetPainter.AddModel(site, "Panel Laydown Rows", "cement_bag_1k.fbx",
                        new Vector3(8.2f, 0f, -5.6f), 1.6f, new Vector3(0f, 40f, 0f));
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
                case TrainingSiteId.TowerCrane:
                    AddSubzone(site, "Panel Casting Yard", new Vector3(-7.5f, 0f, -6.4f),
                        "cement_bag_1k.fbx", 1.5f, new Vector3(0f, 18f, 0f), ColorFor(siteId));
                    AddSubzone(site, "Rigging Loft", new Vector3(7.5f, 0f, 6.3f),
                        "metal_toolbox_1k.fbx", 1.15f, new Vector3(0f, -20f, 0f), ColorFor(siteId));
                    AddSubzone(site, "Operator Briefing", new Vector3(7.5f, 0f, -6.2f),
                        "clipboard_1k.fbx", 0.95f, new Vector3(0f, 12f, 0f), ColorFor(siteId));
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
                TrainingSiteId.TowerCrane => new Color(0.85f, 0.4f, 0.12f),
                _ => Color.white
            };
        }
    }
}
