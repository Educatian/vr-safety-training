using UnityEngine;

namespace SafetyTraining.Editor
{
    internal static class USConstructionContextFactory
    {
        public static void AddOptimizedProps(Transform site)
        {
            AddMaterialYard(site);
            AddAccessAndFormwork(site);
            AddStructuralFrame(site);
            AddInspectionLaydown(site);
            AddPermitBoard(site);
            AddInquiryRouteCueProps(site);
        }

        static void AddMaterialYard(Transform site)
        {
            SafetyWorldAssetPainter.AddModel(site, "US Concrete Barrier A",
                "concrete_road_barrier_1k.fbx", new Vector3(-9.1f, 0f, -7.1f),
                2.4f, new Vector3(0f, 8f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "US Concrete Barrier B",
                "concrete_road_barrier_1k.fbx", new Vector3(-8.1f, 0f, -5.6f),
                2.4f, new Vector3(0f, -7f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Stacked Cement Bags North",
                "cement_bag_1k.fbx", new Vector3(-5.7f, 0f, -6.9f),
                1.6f, new Vector3(0f, 12f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Stacked Cement Bags South",
                "cement_bag_1k.fbx", new Vector3(-4.4f, 0f, -6.2f),
                1.45f, new Vector3(0f, -18f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Cardboard Hardware Delivery",
                "cardboard_box_01_1k.fbx", new Vector3(-7.9f, 0f, -1.7f),
                1.7f, new Vector3(0f, 21f, 0f));
        }

        static void AddAccessAndFormwork(Transform site)
        {
            SafetyWorldAssetPainter.AddModel(site, "Leaning Section Ladder",
                "ladder_sectioned_01_1k.fbx", new Vector3(-10.4f, 0f, 3.6f),
                3.8f, new Vector3(0f, 0f, -18f));
            SafetyWorldAssetPainter.AddModel(site, "Formwork Panel Rack A",
                "modular_factory_facade_1k.fbx", new Vector3(-9.2f, 0f, 7.4f),
                3.9f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Formwork Panel Rack B",
                "modular_factory_facade_1k.fbx", new Vector3(-11.2f, 0f, 8.4f),
                3.4f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Temporary Work Light",
                "caged_hanging_light_1k.fbx", new Vector3(-3.2f, 2.6f, 6.8f),
                1.1f, Vector3.zero);
        }

        static void AddStructuralFrame(Transform site)
        {
            SafetyWorldAssetPainter.AddModel(site, "Two Story Steel Bay Front",
                "modular_factory_facade_1k.fbx", new Vector3(0.8f, 0f, 6.9f),
                7.6f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Two Story Steel Bay Rear",
                "modular_factory_facade_1k.fbx", new Vector3(0.8f, 0f, 10.7f),
                7.4f, new Vector3(0f, 90f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Second Level Deck Reference",
                "modular_factory_facade_1k.fbx", new Vector3(2.6f, 0f, 8.5f),
                5.3f, new Vector3(0f, 180f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Perimeter Access Gate",
                "modular_chainlink_fence_1k.fbx", new Vector3(0f, 0f, -12.35f),
                3.2f, Vector3.zero);
        }

        static void AddInspectionLaydown(Transform site)
        {
            SafetyWorldAssetPainter.AddModel(site, "Formwork Hardware Crate",
                "old_military_crate_1k.fbx", new Vector3(-10.7f, 0f, -1.2f),
                1.7f, new Vector3(0f, 14f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Lift Plan Toolbox",
                "metal_toolbox_1k.fbx", new Vector3(6.9f, 0f, -8.7f),
                1.25f, new Vector3(0f, -18f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Survey Clipboard Station",
                "clipboard_1k.fbx", new Vector3(7.8f, 0.24f, -7.1f),
                1.15f, new Vector3(0f, 34f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Portable Power Separation",
                "concrete_road_barrier_1k.fbx", new Vector3(9.7f, 0f, -3.8f),
                2.35f, new Vector3(0f, 82f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Temporary Utility Box",
                "utility_box_01_1k.fbx", new Vector3(10.8f, 0f, -5.7f),
                1.7f, new Vector3(0f, -35f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Tool Hand Truck",
                "hand_truck_1k.fbx", new Vector3(8.9f, 0f, -6.7f),
                1.45f, new Vector3(0f, 138f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Crew Drill Near Permit Board",
                "Drill_01_1k.fbx", new Vector3(7.1f, 0.28f, -6.25f),
                0.75f, new Vector3(0f, -22f, 0f));
        }

        static void AddPermitBoard(Transform site)
        {
            var board = SafetyScenePrimitives.Primitive(PrimitiveType.Cube,
                "US Site Permit Board", site, new Vector3(5.8f, 1.1f, -9.9f),
                new Vector3(1.9f, 1.3f, 0.12f), new Color(0.08f, 0.1f, 0.12f));
            Object.DestroyImmediate(board.GetComponent<Collider>());
            var label = SafetyScenePrimitives.Label("OSHA NOTICE\nPPE REQUIRED\nFALL PROTECTION",
                site, new Vector3(5.8f, 1.25f, -9.8f), 0.085f);
            label.color = new Color(1f, 0.92f, 0.55f);
        }

        static void AddInquiryRouteCueProps(Transform site)
        {
            SafetyWorldAssetPainter.AddModel(site, "Stacked Formwork Access Pinch Point",
                "modular_factory_facade_1k.fbx", new Vector3(-12.1f, 0f, 5.7f),
                3.1f, new Vector3(0f, 92f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Guardrail Material Bundle",
                "ladder_sectioned_01_1k.fbx", new Vector3(-2.3f, 0f, 10.9f),
                3.2f, new Vector3(0f, 84f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Rigging Hardware Crate",
                "old_military_crate_1k.fbx", new Vector3(2.9f, 0f, 6.2f),
                1.45f, new Vector3(0f, -16f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Permit Review Clipboard",
                "clipboard_1k.fbx", new Vector3(6.4f, 0.24f, -9.25f),
                1f, new Vector3(0f, -20f, 0f));
            SafetyWorldAssetPainter.AddModel(site, "Pedestrian Exclusion Barrier",
                "concrete_road_barrier_1k.fbx", new Vector3(4.2f, 0f, 2.8f),
                2.5f, new Vector3(0f, 88f, 0f));
        }
    }
}
