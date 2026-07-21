using SafetyTraining.Core;
using SafetyTraining.Runtime;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace SafetyTraining.Editor
{
    internal static class SafetySiteFactory
    {
        static readonly Color Steel = new Color(0.36f, 0.42f, 0.48f);
        static readonly Color Concrete = new Color(0.38f, 0.4f, 0.43f);
        static readonly Color Timber = new Color(0.55f, 0.34f, 0.16f);
        static readonly Color SafetyYellow = new Color(0.95f, 0.68f, 0.08f);
        static readonly Color ExtinguisherRed = new Color(0.72f, 0.08f, 0.05f);

        public static Transform Construction(Vector3 origin)
        {
            var root = SiteRoot("Construction Site", origin, new Color(0.22f, 0.25f, 0.28f));
            AddLane(root, "Marked Access Route", new Vector3(2.5f, 0.02f, 1.35f),
                new Vector2(2.25f, 4.7f), new Color(0.26f, 0.28f, 0.3f), SafetyYellow);
            AddBay(root, "Materials Staging Zone", new Vector3(2.6f, 0.035f, -2.7f),
                new Vector2(2.5f, 1.9f), SafetyYellow);

            var openEdge = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Open Work Platform",
                root, new Vector3(-2.7f, 1.1f, 0.5f), new Vector3(3.2f, 0.2f, 2.2f), Concrete);
            SafetyScenePrimitives.Target(openEdge, TrainingSiteId.Construction, "fall-edge", true,
                "Unprotected elevated edge", "The work platform has an exposed edge without a guardrail.",
                "Install compliant top and mid rails or require approved fall arrest before access.");
            AddSupports(openEdge.transform, 1f);
            AddScaffoldTower(root, new Vector3(-2.7f, 0f, 1.8f));

            var crates = CrateStack(root, "Crates in Access Route", new Vector3(2.5f, 0f, 0.5f), 3);
            SafetyScenePrimitives.Target(crates, TrainingSiteId.Construction, "blocked-access", true,
                "Blocked access route", "Materials obstruct the marked route used to reach the work area.",
                "Move materials into the designated staging zone and restore the full route width.");

            var guarded = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Guarded Platform",
                root, new Vector3(-2.7f, 0.55f, -2.7f), new Vector3(3.2f, 0.18f, 1.5f), Concrete);
            AddGuardrail(guarded.transform);
            SafetyScenePrimitives.Target(guarded, TrainingSiteId.Construction, "guarded-edge", false,
                "Guarded work platform", "Top rails and mid rails control the fall exposure at this platform.",
                "Maintain the barrier and report damaged or missing sections.");

            var stored = CrateStack(root, "Stored Materials", new Vector3(2.6f, 0f, -2.7f), 2);
            SafetyScenePrimitives.Target(stored, TrainingSiteId.Construction, "stored-materials", false,
                "Materials in staging zone", "The materials are stable and outside the marked access route.",
                "Keep the stack stable and within the designated boundary.");

            var damagedLadder = Ladder(root, "Damaged Access Ladder", new Vector3(-0.2f, 0f, 2.55f), true);
            SafetyScenePrimitives.Target(damagedLadder, TrainingSiteId.Construction, "damaged-ladder", true,
                "Damaged access ladder", "A rung is missing and the ladder leans without a top tie-off.",
                "Tag the ladder out of service and replace it with an inspected ladder.");

            var securedLadder = Ladder(root, "Secured Access Ladder", new Vector3(1.35f, 0f, 2.55f), false);
            SafetyScenePrimitives.Target(securedLadder, TrainingSiteId.Construction, "secured-ladder", false,
                "Secured access ladder", "The ladder is complete, footed on a firm surface, and tied off at the top.",
                "Inspect it before each shift and keep the tie-off intact.");
            CreateConstructionHandsOn(root);
            GuardrailAssembly(root);
            ShoreFrameAssembly(root);
            return root;
        }

        static GameObject Ladder(Transform parent, string name, Vector3 position, bool damaged)
        {
            var ladder = AssemblyRoot(parent, name, position, new Vector3(1.1f, 2.9f, 0.9f),
                new Vector3(0f, 1.4f, 0f));
            var rail = damaged ? new Color(0.52f, 0.5f, 0.46f) : new Color(0.72f, 0.72f, 0.68f);
            foreach (var x in new[] { -0.32f, 0.32f })
                DecorativePrimitive(PrimitiveType.Cylinder, "Ladder Rail", ladder.transform,
                    new Vector3(x, 1.35f, 0f), new Vector3(0.06f, 1.35f, 0.06f), rail, 0.4f, 0.5f);
            for (var rung = 0; rung < 6; rung++)
            {
                if (damaged && rung == 3)
                    continue;
                DecorativePrimitive(PrimitiveType.Cylinder, "Ladder Rung", ladder.transform,
                        new Vector3(0f, 0.35f + rung * 0.45f, 0f), new Vector3(0.045f, 0.34f, 0.045f),
                        rail * 1.05f, 0.4f, 0.5f)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            if (damaged)
            {
                ladder.transform.localRotation = Quaternion.Euler(0f, 0f, 9f);
                DecorativePrimitive(PrimitiveType.Cube, "Broken Rung Stub", ladder.transform,
                    new Vector3(-0.24f, 0.35f + 3 * 0.45f, 0f), new Vector3(0.12f, 0.05f, 0.05f),
                    new Color(0.4f, 0.32f, 0.26f));
            }
            else
            {
                DecorativePrimitive(PrimitiveType.Cube, "Top Tie-Off Strap", ladder.transform,
                    new Vector3(0f, 2.62f, -0.08f), new Vector3(0.72f, 0.08f, 0.1f), SafetyYellow);
            }
            return ladder;
        }

        static void CreateConstructionHandsOn(Transform root)
        {
            var golden = root.gameObject.AddComponent<ConstructionGoldenModuleController>();
            CreateConstructionMissionStatus(root, golden);
            var practical = root.gameObject.AddComponent<ConstructionHandsOnController>();
            var ppe = HandsOnProp(root, "PPE Kit - Helmet and Vest", new Vector3(-1.1f, 0.65f, 1.5f),
                new Vector3(0.9f, 0.55f, 0.7f), new Color(0.95f, 0.72f, 0.08f));
            AddPpeCaseDetail(ppe.transform);
            SafetyScenePrimitives.Label("PPE", ppe.transform, new Vector3(0f, 0.45f, -0.38f), 0.08f);
            ConfigureAction(ppe, practical, 0, "PPE check",
                "Pick up the kit and stage helmet plus vest at the entry check point.",
                new Vector3(0.15f, 0.65f, 2.05f), Vector3.zero, 0.85f);

            var barricade = HandsOnProp(root, "Barricade Gate", new Vector3(0.7f, 0.65f, -1.35f),
                new Vector3(1.6f, 0.18f, 0.18f), SafetyYellow);
            AddBarricadeDetail(barricade.transform);
            SafetyScenePrimitives.Label("BARRICADE", barricade.transform, new Vector3(0f, 0.22f, -0.18f), 0.06f);
            ConfigureAction(barricade, practical, 3, "Set exclusion barricade",
                "Place the barrier around the open-edge work zone.",
                new Vector3(-0.1f, 0.65f, -2.05f), Vector3.zero, 1.1f);

            var railKit = HandsOnProp(root, "Guardrail Kit", new Vector3(-2.3f, 0.35f, 2.6f),
                new Vector3(1.4f, 0.16f, 0.16f), new Color(0.94f, 0.94f, 0.88f));
            AddRailKitDetail(railKit.transform);
            SafetyScenePrimitives.Label("TOP RAIL", railKit.transform, new Vector3(0f, 0.22f, -0.18f), 0.06f);
            ConfigureAction(railKit, practical, 2, "Install guardrail kit",
                "Pick up the rail kit and fit top and mid rails to the platform edge.",
                new Vector3(-2.65f, 0.35f, -1.75f), Vector3.zero, 1.1f);

            var cart = HandsOnProp(root, "Material Cart", new Vector3(2.8f, 0.55f, 1.9f),
                new Vector3(1.0f, 0.55f, 0.72f), Timber);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Cart Handle", cart.transform,
                new Vector3(0f, 0.72f, 0.38f), new Vector3(0.8f, 0.12f, 0.12f), Steel);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Cart Wheel A", cart.transform,
                new Vector3(-0.55f, -0.55f, -0.35f), new Vector3(0.24f, 0.12f, 0.24f), Steel);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Cart Wheel B", cart.transform,
                new Vector3(0.55f, -0.55f, -0.35f), new Vector3(0.24f, 0.12f, 0.24f), Steel);
            SafetyScenePrimitives.Label("MOVE", cart.transform, new Vector3(0f, 0.55f, -0.38f), 0.07f);
            ConfigureAction(cart, practical, 1, "Move material cart",
                "Grab the cart and return it to the marked staging zone.",
                new Vector3(2.45f, 0.55f, -2.25f), new Vector3(0f, 180f, 0f), 1.1f);

            var clipboard = HandsOnProp(root, "Inspection Clipboard", new Vector3(5.8f, 0.7f, -4.3f),
                new Vector3(0.55f, 0.08f, 0.78f), new Color(0.82f, 0.82f, 0.76f));
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Clipboard Clip", clipboard.transform,
                new Vector3(0f, 0.09f, 0.28f), new Vector3(0.25f, 0.04f, 0.12f), Steel);
            SafetyScenePrimitives.Label("FINAL CHECK", clipboard.transform, new Vector3(0f, 0.18f, -0.42f), 0.06f);
            ConfigureAction(clipboard, practical, 4, "Complete final walkdown",
                "Pick up the clipboard and deliver the completed check at the site control point.",
                new Vector3(0f, 0.7f, -0.25f), Vector3.zero, 0.85f);
        }

        static void CreateConstructionMissionStatus(Transform root, ConstructionGoldenModuleController golden)
        {
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Construction Mission Status Board",
                root, new Vector3(2.35f, 1.65f, -7.75f), new Vector3(4.2f, 1.7f, 0.12f),
                new Color(0.035f, 0.065f, 0.085f));
            var status = SafetyScenePrimitives.Label(
                "MISSION STATUS  READY\nEvidence 0/4   Engineering 0/3\nControls 0/4   Debrief PENDING",
                root, new Vector3(2.35f, 1.65f, -7.66f), 0.065f);
            status.color = new Color(0.72f, 0.94f, 1f);
            status.anchor = TextAnchor.MiddleCenter;
            status.alignment = TextAlignment.Center;
            status.lineSpacing = 1.25f;
            golden.Configure(status);
        }

        static GameObject HandsOnProp(Transform parent, string name, Vector3 position, Vector3 scale, Color color)
        {
            var item = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, name, parent, position, scale, color);
            item.AddComponent<XRGrabInteractable>();
            return item;
        }

        static void ConfigureAction(GameObject item, ConstructionHandsOnController controller,
            int step, string title, string instruction, Vector3 destination,
            Vector3 destinationEulerAngles, float acceptanceRadius)
        {
            var action = item.AddComponent<ConstructionActionInteractable>();
            item.AddComponent<InteractiveHoverFeedback>();
            var target = PlacementTargetFactory.Create(item.transform.parent, item.name, destination,
                title, acceptanceRadius);
            action.Configure(controller, step, title, instruction, destination,
                destinationEulerAngles, acceptanceRadius, target);
        }

        static void AddPpeCaseDetail(Transform caseRoot)
        {
            SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Hard Hat", caseRoot,
                new Vector3(-0.2f, 0.36f, 0f), new Vector3(0.28f, 0.08f, 0.28f), new Color(0.98f, 0.78f, 0.08f));
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Vest Fold", caseRoot,
                new Vector3(0.24f, 0.36f, 0f), new Vector3(0.32f, 0.08f, 0.42f), new Color(0.96f, 0.44f, 0.05f));
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Case Latch", caseRoot,
                new Vector3(0f, -0.3f, -0.37f), new Vector3(0.18f, 0.08f, 0.04f), Steel);
        }

        static void AddBarricadeDetail(Transform gate)
        {
            foreach (var x in new[] { -0.72f, 0.72f })
            {
                SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Barricade Foot", gate,
                    new Vector3(x, -0.55f, 0f), new Vector3(0.22f, 0.08f, 0.22f), Steel);
                SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Barricade Post", gate,
                    new Vector3(x, -0.22f, 0f), new Vector3(0.08f, 0.35f, 0.08f), SafetyYellow);
            }
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Barricade Stripe", gate,
                new Vector3(0f, 0f, -0.03f), new Vector3(1.35f, 0.05f, 0.04f), new Color(0.88f, 0.16f, 0.06f));
        }

        static void AddRailKitDetail(Transform kit)
        {
            foreach (var x in new[] { -0.52f, 0.52f })
            {
                SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Rail Base Plate", kit,
                    new Vector3(x, -0.18f, 0f), new Vector3(0.16f, 0.04f, 0.16f), Steel);
                SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Rail Upright", kit,
                    new Vector3(x, 0.18f, 0f), new Vector3(0.06f, 0.35f, 0.06f), SafetyYellow);
            }
        }

        static void AddScaffoldTower(Transform root, Vector3 origin)
        {
            var scaffold = new GameObject("Scaffold Tower").transform;
            scaffold.SetParent(root, false);
            scaffold.localPosition = origin;
            foreach (var x in new[] { -1.25f, 1.25f })
                foreach (var z in new[] { -0.6f, 0.6f })
                {
                    SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Scaffold Upright", scaffold,
                        new Vector3(x, 1.3f, z), new Vector3(0.06f, 1.3f, 0.06f), Steel);
                    SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Base Jack", scaffold,
                        new Vector3(x, 0.06f, z), new Vector3(0.14f, 0.06f, 0.14f), SafetyYellow);
                }
            foreach (var y in new[] { 0.7f, 1.8f, 2.8f })
            {
                SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Scaffold Crossbar", scaffold,
                    new Vector3(0f, y, -0.6f), new Vector3(2.6f, 0.06f, 0.06f), Steel);
                SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Scaffold Deck", scaffold,
                    new Vector3(0f, y, 0f), new Vector3(2.5f, 0.06f, 1.3f), new Color(0.42f, 0.44f, 0.46f));
            }
            SafetyScenePrimitives.Label("SCAFFOLD", scaffold, new Vector3(0f, 3.15f, -0.6f), 0.08f);
        }

        public static Transform Warehouse(Vector3 origin)
        {
            var root = SiteRoot("Warehouse", origin, new Color(0.18f, 0.28f, 0.38f));
            AddLane(root, "Pedestrian Aisle", new Vector3(-2.4f, 0.02f, 0.55f),
                new Vector2(2.2f, 5.2f), new Color(0.12f, 0.34f, 0.28f), Color.white);
            AddLane(root, "Vehicle Lane", new Vector3(2.4f, 0.02f, 0.75f),
                new Vector2(2.4f, 4.8f), new Color(0.24f, 0.27f, 0.3f), SafetyYellow);
            AddBay(root, "Cargo Staging Bay", new Vector3(2.4f, 0.04f, -2.6f),
                new Vector2(2.8f, 2f), Color.white);
            AddBay(root, "Maintenance Boundary", new Vector3(-2.5f, 0.04f, -2.6f),
                new Vector2(2.4f, 2f), SafetyYellow);

            var spill = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Floor Spill",
                root, new Vector3(-2.4f, 0.03f, 0.4f), new Vector3(1.2f, 0.03f, 0.75f), new Color(0.08f, 0.22f, 0.32f));
            SafetyScenePrimitives.Target(spill, TrainingSiteId.Warehouse, "floor-spill", true,
                "Uncontrolled floor spill", "Liquid is present in an active pedestrian aisle without isolation.",
                "Isolate the area, use the spill procedure, and reopen only after the surface is safe.");

            var lanePallet = Pallet(root, "Pallet in Vehicle Lane", new Vector3(2.4f, 0f, 0.6f));
            SafetyScenePrimitives.Target(lanePallet, TrainingSiteId.Warehouse, "vehicle-route", true,
                "Obstruction in vehicle lane", "Cargo extends into the powered industrial truck route.",
                "Stop traffic and relocate the pallet to a designated staging or rack position.");

            var cone = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Maintenance Cone",
                root, new Vector3(-2.5f, 0.45f, -2.6f), new Vector3(0.45f, 0.45f, 0.45f), SafetyYellow);
            SafetyScenePrimitives.Target(cone, TrainingSiteId.Warehouse, "marked-maintenance", false,
                "Marked maintenance area", "The temporary work area is visibly isolated from normal travel.",
                "Keep the boundary intact until maintenance releases the area.");

            var rackCargo = Pallet(root, "Cargo in Staging Bay", new Vector3(2.4f, 0f, -2.6f));
            SafetyScenePrimitives.Target(rackCargo, TrainingSiteId.Warehouse, "racked-cargo", false,
                "Cargo in staging bay", "The pallet is stable and fully contained inside the marked bay.",
                "Maintain aisle clearance and verify the load remains stable.");

            var damagedUpright = RackUpright(root, "Damaged Rack Upright", new Vector3(-0.7f, 0f, 2.35f), true);
            SafetyScenePrimitives.Target(damagedUpright, TrainingSiteId.Warehouse, "damaged-rack-upright", true,
                "Damaged rack upright", "The loaded rack column is visibly bent at impact height with no protector.",
                "Unload the bay and have the upright assessed before it carries load again.");

            var guardedUpright = RackUpright(root, "Guarded Rack Upright", new Vector3(1.1f, 0f, 2.35f), false);
            SafetyScenePrimitives.Target(guardedUpright, TrainingSiteId.Warehouse, "guarded-rack-upright", false,
                "Guarded rack upright", "The identical column is straight and fitted with a base protector.",
                "Keep the protector seated and report any vehicle impact.");
            return root;
        }

        static GameObject RackUpright(Transform parent, string name, Vector3 position, bool damaged)
        {
            var rack = AssemblyRoot(parent, name, position, new Vector3(1.4f, 2.6f, 1f),
                new Vector3(0f, 1.25f, 0f));
            var column = new Color(0.24f, 0.4f, 0.62f);
            if (damaged)
            {
                DecorativePrimitive(PrimitiveType.Cube, "Upright Lower", rack.transform,
                    new Vector3(0f, 0.35f, 0f), new Vector3(0.12f, 0.35f, 0.12f), column, 0.55f, 0.42f);
                var bent = DecorativePrimitive(PrimitiveType.Cube, "Upright Bent Section", rack.transform,
                    new Vector3(0.12f, 0.92f, 0f), new Vector3(0.12f, 0.3f, 0.12f), column * 0.82f,
                    0.55f, 0.42f);
                bent.transform.localRotation = Quaternion.Euler(0f, 0f, -24f);
                DecorativePrimitive(PrimitiveType.Cube, "Upright Upper", rack.transform,
                    new Vector3(0.04f, 1.75f, 0f), new Vector3(0.12f, 0.62f, 0.12f), column, 0.55f, 0.42f);
            }
            else
            {
                DecorativePrimitive(PrimitiveType.Cube, "Upright Column", rack.transform,
                    new Vector3(0f, 1.2f, 0f), new Vector3(0.12f, 1.2f, 0.12f), column, 0.55f, 0.42f);
                DecorativePrimitive(PrimitiveType.Cube, "Column Protector", rack.transform,
                    new Vector3(0f, 0.25f, 0f), new Vector3(0.3f, 0.25f, 0.3f), SafetyYellow);
            }
            DecorativePrimitive(PrimitiveType.Cube, "Rack Beam", rack.transform,
                new Vector3(0.55f, 2.15f, 0f), new Vector3(1.2f, 0.1f, 0.12f),
                new Color(0.85f, 0.42f, 0.08f), 0.45f, 0.4f);
            DecorativePrimitive(PrimitiveType.Cube, "Beam Load", rack.transform,
                new Vector3(0.55f, 2.5f, 0f), new Vector3(0.9f, 0.55f, 0.7f), Timber);
            return rack;
        }

        public static Transform FireResponse(Vector3 origin)
        {
            var root = SiteRoot("Fire Response", origin, new Color(0.34f, 0.2f, 0.18f));
            AddLane(root, "Emergency Egress Lane", new Vector3(2.5f, 0.02f, 0.2f),
                new Vector2(2.5f, 6.2f), new Color(0.08f, 0.36f, 0.18f), Color.white);

            var blockedExtinguisher = Extinguisher(root, "Blocked Extinguisher", new Vector3(-2.6f, 0f, 0.9f));
            CrateStack(root, "Obstruction", new Vector3(-2.6f, 0f, 0.1f), 2);
            SafetyScenePrimitives.Target(blockedExtinguisher, TrainingSiteId.FireResponse, "blocked-extinguisher", true,
                "Blocked fire extinguisher", "Stored material prevents immediate access to the extinguisher.",
                "Remove the obstruction and preserve the required clear access area.");

            var exitBlock = Pallet(root, "Pallet at Emergency Exit", new Vector3(2.6f, 0f, 0.7f));
            Marking(root, "Exit Sign Backplate", new Vector3(3.45f, 1.7f, 4.05f),
                new Vector3(1.55f, 0.55f, 0.08f), new Color(0.03f, 0.38f, 0.18f));
            SafetyScenePrimitives.Label("EXIT", root, new Vector3(3.45f, 1.7f, 3.95f), 0.22f);
            SafetyScenePrimitives.Target(exitBlock, TrainingSiteId.FireResponse, "blocked-exit", true,
                "Blocked emergency exit", "A pallet obstructs the direct egress path to the marked exit.",
                "Remove the pallet immediately and keep the egress route continuously clear.");

            var clearExtinguisher = Extinguisher(root, "Accessible Extinguisher", new Vector3(-2.5f, 0f, -2.6f));
            SafetyScenePrimitives.Target(clearExtinguisher, TrainingSiteId.FireResponse, "clear-extinguisher", false,
                "Accessible fire extinguisher", "The extinguisher is visible and has clear approach space.",
                "Continue routine inspection and preserve clear access.");

            var clearRoute = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Clear Egress Route",
                root, new Vector3(2.5f, 0.02f, -2.6f), new Vector3(2.4f, 0.04f, 1.2f), new Color(0.12f, 0.45f, 0.2f));
            SafetyScenePrimitives.Target(clearRoute, TrainingSiteId.FireResponse, "clear-egress", false,
                "Clear egress route", "The marked evacuation route is unobstructed and easy to follow.",
                "Keep temporary storage and equipment outside the route.");

            var proppedDoor = FireDoor(root, "Propped Fire Door", new Vector3(-1f, 0f, 3.45f), true);
            SafetyScenePrimitives.Target(proppedDoor, TrainingSiteId.FireResponse, "propped-fire-door", true,
                "Propped-open fire door", "A wedge holds the self-closing fire door open on the egress route.",
                "Remove the prop and let the door close to protect the escape path.");

            var closedDoor = FireDoor(root, "Closed Fire Door", new Vector3(1.1f, 0f, 3.45f), false);
            SafetyScenePrimitives.Target(closedDoor, TrainingSiteId.FireResponse, "closed-fire-door", false,
                "Closed self-closing fire door", "The identical fire door is fully closed and latched.",
                "Keep the closer unobstructed and report latch damage.");
            return root;
        }

        static GameObject FireDoor(Transform parent, string name, Vector3 position, bool propped)
        {
            var doorway = AssemblyRoot(parent, name, position, new Vector3(1.6f, 2.4f, 1f),
                new Vector3(0f, 1.15f, 0f));
            var frame = new Color(0.4f, 0.12f, 0.1f);
            foreach (var x in new[] { -0.62f, 0.62f })
                DecorativePrimitive(PrimitiveType.Cube, "Door Jamb", doorway.transform,
                    new Vector3(x, 1.1f, 0f), new Vector3(0.12f, 1.1f, 0.16f), frame);
            DecorativePrimitive(PrimitiveType.Cube, "Door Header", doorway.transform,
                new Vector3(0f, 2.24f, 0f), new Vector3(1.36f, 0.12f, 0.16f), frame);
            var slab = DecorativePrimitive(PrimitiveType.Cube, "Fire Door Slab", doorway.transform,
                propped ? new Vector3(-0.28f, 1.08f, -0.42f) : new Vector3(0f, 1.08f, 0f),
                new Vector3(1.1f, 2.05f, 0.08f), new Color(0.62f, 0.2f, 0.14f));
            if (propped)
            {
                slab.transform.localRotation = Quaternion.Euler(0f, 55f, 0f);
                DecorativePrimitive(PrimitiveType.Cube, "Door Wedge", doorway.transform,
                    new Vector3(-0.62f, 0.08f, -0.72f), new Vector3(0.22f, 0.16f, 0.3f), Timber);
            }
            else
            {
                DecorativePrimitive(PrimitiveType.Cube, "Door Closer Arm", doorway.transform,
                    new Vector3(0.3f, 2.05f, -0.1f), new Vector3(0.5f, 0.06f, 0.06f), Steel);
            }
            SafetyScenePrimitives.Label("FIRE DOOR\nKEEP CLOSED", doorway.transform,
                new Vector3(0f, 1.55f, -0.12f), 0.055f);
            return doorway;
        }

        public static Transform ChemicalProcessing(Vector3 origin)
        {
            var root = SiteRoot("Chemical Processing", origin, new Color(0.12f, 0.28f, 0.25f));
            AddLane(root, "Chemical Transfer Lane", new Vector3(2.5f, 0.02f, 0.7f),
                new Vector2(2.4f, 5.5f), new Color(0.14f, 0.3f, 0.25f), Color.white);
            AddBay(root, "Compatible Storage Bay", new Vector3(2.5f, 0.04f, -2.6f),
                new Vector2(2.8f, 2f), new Color(0.35f, 0.8f, 0.56f));

            var leakingDrum = Drum(root, "Leaking Solvent Drum", new Vector3(-2.5f, 0f, 0.6f),
                new Color(0.14f, 0.22f, 0.3f), new Color(0.95f, 0.68f, 0.08f));
            SafetyScenePrimitives.Target(leakingDrum, TrainingSiteId.ChemicalProcessing, "chemical-leak", true,
                "Leaking solvent drum", "A drum is visibly leaking into the transfer lane.",
                "Stop the transfer, isolate the area, and follow the site spill response procedure.");

            var unlabeledDrum = Drum(root, "Unlabeled Chemical Drum", new Vector3(-2.5f, 0f, -1.8f),
                new Color(0.68f, 0.68f, 0.7f), new Color(0.88f, 0.2f, 0.12f));
            SafetyScenePrimitives.Target(unlabeledDrum, TrainingSiteId.ChemicalProcessing, "unlabeled-drum", true,
                "Unlabeled chemical container", "The container lacks a legible identity and hazard label.",
                "Quarantine the container and restore the approved label before handling.");

            var labeledDrum = Drum(root, "Labeled Compatible Drum", new Vector3(2.5f, 0f, -2.6f),
                new Color(0.18f, 0.4f, 0.55f), new Color(0.2f, 0.8f, 0.42f));
            SafetyScenePrimitives.Target(labeledDrum, TrainingSiteId.ChemicalProcessing, "labeled-drum", false,
                "Labeled compatible container", "The container is labeled and inside the compatible storage bay.",
                "Keep the label legible and maintain segregation by compatibility.");

            var eyewash = Eyewash(root, "Emergency Eyewash", new Vector3(2.5f, 0f, 0.1f));
            SafetyScenePrimitives.Target(eyewash, TrainingSiteId.ChemicalProcessing, "clear-eyewash", false,
                "Accessible emergency eyewash", "The eyewash station has a clear approach from the transfer lane.",
                "Keep the approach clear and test the station on the site schedule.");

            var incompatible = DrumPair(root, "Incompatible Storage Pair", new Vector3(-0.9f, 0f, 3.2f), false);
            SafetyScenePrimitives.Target(incompatible, TrainingSiteId.ChemicalProcessing, "incompatible-storage", true,
                "Incompatible chemicals stored together", "An oxidizer drum sits directly against a flammable-solvent drum with no separation.",
                "Separate the containers per the SDS segregation plan before any transfer.");

            var segregated = DrumPair(root, "Segregated Storage Pair", new Vector3(1.5f, 0f, 3.2f), true);
            SafetyScenePrimitives.Target(segregated, TrainingSiteId.ChemicalProcessing, "segregated-storage", false,
                "Segregated compatible storage", "The same two chemical classes are separated by a rated divider wall.",
                "Maintain the divider and keep both labels legible.");
            return root;
        }

        static GameObject DrumPair(Transform parent, string name, Vector3 position, bool divided)
        {
            var pair = AssemblyRoot(parent, name, position, new Vector3(2f, 1.6f, 1.1f),
                new Vector3(0f, 0.75f, 0f));
            var spacing = divided ? 0.62f : 0.4f;
            Drum(pair, $"{name} Oxidizer", new Vector3(-spacing, 0f, 0f),
                new Color(0.75f, 0.62f, 0.16f), new Color(0.95f, 0.85f, 0.1f));
            Drum(pair, $"{name} Solvent", new Vector3(spacing, 0f, 0f),
                new Color(0.5f, 0.16f, 0.1f), new Color(0.88f, 0.2f, 0.12f));
            if (divided)
                DecorativePrimitive(PrimitiveType.Cube, "Segregation Divider", pair.transform,
                    new Vector3(0f, 0.7f, 0f), new Vector3(0.1f, 0.7f, 0.95f),
                    new Color(0.78f, 0.8f, 0.82f));
            return pair.gameObject;
        }

        static void Drum(GameObject parent, string name, Vector3 position, Color body, Color band)
        {
            Drum(parent.transform, name, position, body, band);
        }

        public static Transform ElectricalMaintenance(Vector3 origin)
        {
            var root = SiteRoot("Electrical Maintenance", origin, new Color(0.16f, 0.2f, 0.34f));
            AddLane(root, "Protected Cable Crossing", new Vector3(2.5f, 0.02f, 0.9f),
                new Vector2(2.3f, 5.8f), new Color(0.18f, 0.22f, 0.34f), new Color(0.98f, 0.76f, 0.12f));
            AddBay(root, "Lockout Staging", new Vector3(2.5f, 0.04f, -2.6f),
                new Vector2(2.8f, 1.9f), new Color(0.62f, 0.7f, 0.95f));

            var openPanel = ElectricalPanel(root, "Open Electrical Panel", new Vector3(-2.5f, 0f, 0.8f), false);
            SafetyScenePrimitives.Target(openPanel, TrainingSiteId.ElectricalMaintenance, "open-panel", true,
                "Open energized panel", "The panel is open without a visible lockout boundary or verified isolation.",
                "Stop work, isolate the source, apply lockout/tagout, and verify zero energy.");

            var looseCable = CableBundle(root, "Loose Cable Crossing", new Vector3(-2.5f, 0f, -1.8f), false);
            SafetyScenePrimitives.Target(looseCable, TrainingSiteId.ElectricalMaintenance, "loose-cable", true,
                "Unprotected cable crossing", "A temporary cable crosses the walking route without a ramp or overhead support.",
                "Reroute the cable or install a rated cable protector before access resumes.");

            var lockedPanel = ElectricalPanel(root, "Locked Isolated Panel", new Vector3(3.55f, 0f, 0.25f), true);
            SafetyScenePrimitives.Target(lockedPanel, TrainingSiteId.ElectricalMaintenance, "locked-panel", false,
                "Locked and tagged panel", "The panel is closed with a visible lockout tag inside the staging boundary.",
                "Preserve the isolation and verify the tag remains legible.");

            var protectedCable = CableBundle(root, "Protected Cable Ramp", new Vector3(2.5f, 0f, 0.6f), true);
            SafetyScenePrimitives.Target(protectedCable, TrainingSiteId.ElectricalMaintenance, "protected-cable", false,
                "Protected cable crossing", "The cable crossing is covered by a high-visibility floor protector.",
                "Inspect the ramp before use and keep its edges seated.");

            var damagedCord = ExtensionCord(root, "Spliced Extension Cord", new Vector3(-0.8f, 0f, 3.3f), true);
            SafetyScenePrimitives.Target(damagedCord, TrainingSiteId.ElectricalMaintenance, "damaged-cord", true,
                "Damaged extension cord", "The cord shows a taped field splice with exposed conductor strands.",
                "Remove the cord from service and replace it before energizing equipment.");

            var elevatedCord = ExtensionCord(root, "Elevated Extension Cord", new Vector3(1.2f, 0f, 3.3f), false);
            SafetyScenePrimitives.Target(elevatedCord, TrainingSiteId.ElectricalMaintenance, "elevated-cord", false,
                "Elevated intact cord", "The identical cord is intact and routed on hooks above the walking surface.",
                "Keep the cord elevated and inspect the jacket before each use.");
            LotoAssembly(root);
            return root;
        }

        static GameObject ExtensionCord(Transform parent, string name, Vector3 position, bool damaged)
        {
            var cord = AssemblyRoot(parent, name, position, new Vector3(1.9f, damaged ? 0.5f : 1.6f, 0.8f),
                new Vector3(0f, damaged ? 0.2f : 0.85f, 0f));
            var jacket = new Color(0.16f, 0.38f, 0.78f);
            var height = damaged ? 0.08f : 1.25f;
            var run = DecorativePrimitive(PrimitiveType.Cylinder, "Cord Run", cord.transform,
                new Vector3(0f, height, 0f), new Vector3(0.04f, 0.85f, 0.04f), jacket);
            run.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            if (damaged)
            {
                DecorativePrimitive(PrimitiveType.Cylinder, "Field Splice Tape", cord.transform,
                    new Vector3(0.1f, height, 0f), new Vector3(0.09f, 0.14f, 0.09f),
                    new Color(0.12f, 0.12f, 0.14f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                DecorativePrimitive(PrimitiveType.Cylinder, "Exposed Strands", cord.transform,
                    new Vector3(0.28f, height, 0f), new Vector3(0.05f, 0.06f, 0.05f),
                    new Color(0.85f, 0.6f, 0.3f)).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            else
            {
                foreach (var x in new[] { -0.62f, 0.62f })
                {
                    DecorativePrimitive(PrimitiveType.Cylinder, "Cord Hook Post", cord.transform,
                        new Vector3(x, 0.62f, 0f), new Vector3(0.05f, 0.62f, 0.05f), Steel);
                    DecorativePrimitive(PrimitiveType.Cube, "Cord Hook", cord.transform,
                        new Vector3(x, 1.25f, 0f), new Vector3(0.1f, 0.06f, 0.14f), SafetyYellow);
                }
            }
            return cord;
        }

        public static Transform TowerCrane(Vector3 origin)
        {
            var root = SiteRoot("Tower Crane", origin, new Color(0.3f, 0.27f, 0.24f));
            AddLane(root, "Lift Corridor", new Vector3(0f, 0.02f, 0.5f),
                new Vector2(3.2f, 6.6f), new Color(0.3f, 0.3f, 0.3f), SafetyYellow);
            AddBay(root, "Panel Landing Zone", new Vector3(2.6f, 0.04f, -2.6f),
                new Vector2(2.6f, 2f), SafetyYellow);
            ApartmentFrame(root, new Vector3(-2.55f, 0f, 2.3f));
            var craneAnimator = TowerCraneRig(root, new Vector3(3.5f, 0f, 3.4f));
            RiggingAssembly(root, craneAnimator);

            var fallZone = AssemblyRoot(root, "Uncontrolled Fall Zone Walkway", new Vector3(0.1f, 0f, 3.3f),
                new Vector3(2.6f, 3f, 1.8f), new Vector3(0f, 1.4f, 0f));
            DecorativePrimitive(PrimitiveType.Cube, "Walkway Tool Crate", fallZone.transform,
                new Vector3(-0.6f, 0.25f, 0.3f), new Vector3(0.6f, 0.5f, 0.45f), Timber);
            DecorativePrimitive(PrimitiveType.Cube, "Dropped Panel Shim", fallZone.transform,
                new Vector3(0.7f, 0.03f, -0.5f), new Vector3(0.8f, 0.06f, 0.25f),
                new Color(0.62f, 0.6f, 0.57f));
            SafetyScenePrimitives.Target(fallZone.gameObject, TrainingSiteId.TowerCrane, "uncontrolled-fall-zone", true,
                "Unbarricaded fall zone", "The suspended panel travels directly over an open walkway with no barricade.",
                "Stop the lift, clear the route, and barricade the fall zone before loads travel overhead.");

            var landed = AssemblyRoot(root, "Barricaded Landing Zone", new Vector3(2.6f, 0f, -2.6f),
                new Vector3(2.6f, 2f, 2f), new Vector3(0f, 0.9f, 0f));
            DecorativePrimitive(PrimitiveType.Cube, "Landed Precast Panel", landed.transform,
                new Vector3(0f, 0.55f, 0f), new Vector3(2.2f, 0.9f, 0.18f), new Color(0.66f, 0.64f, 0.6f));
            foreach (var dx in new[] { -0.8f, 0.8f })
                DecorativePrimitive(PrimitiveType.Cube, "Landing Dunnage", landed.transform,
                    new Vector3(dx, 0.06f, 0f), new Vector3(0.25f, 0.12f, 0.6f), Timber);
            foreach (var dz in new[] { -0.85f, 0.85f })
            {
                DecorativePrimitive(PrimitiveType.Cube, "Landing Barricade Rail", landed.transform,
                    new Vector3(0f, 0.5f, dz), new Vector3(2.4f, 0.07f, 0.06f), SafetyYellow);
                foreach (var dx in new[] { -1.1f, 1.1f })
                    DecorativePrimitive(PrimitiveType.Cylinder, "Landing Barricade Post", landed.transform,
                        new Vector3(dx, 0.25f, dz), new Vector3(0.06f, 0.25f, 0.06f), SafetyYellow);
            }
            SafetyScenePrimitives.Target(landed.gameObject, TrainingSiteId.TowerCrane, "barricaded-fall-zone", false,
                "Barricaded landing zone", "The identical panel path ends inside a barricaded landing zone on dunnage.",
                "Keep the barricade line intact while loads travel overhead.");

            var barePole = PowerPole(root, "Unmarked Power Line", new Vector3(-2.7f, 0f, -1.6f), false);
            SafetyScenePrimitives.Target(barePole, TrainingSiteId.TowerCrane, "powerline-encroachment", true,
                "Power line inside swing path", "The energized line crosses the jib swing path with no warning line or spotter.",
                "Stop work and establish the minimum clearance, warning line, and dedicated spotter.");

            var markedPole = PowerPole(root, "Controlled Power Line", new Vector3(-0.8f, 0f, -3.6f), true);
            SafetyScenePrimitives.Target(markedPole, TrainingSiteId.TowerCrane, "cleared-powerline-plan", false,
                "Controlled power line crossing", "The same line type is flagged with a warning line and a staffed spotter post.",
                "Maintain the warning line and keep the spotter post staffed.");

            var badSling = SlingStation(root, "Frayed Sling Station", new Vector3(1.2f, 0f, 2.2f), true);
            SafetyScenePrimitives.Target(badSling, TrainingSiteId.TowerCrane, "damaged-sling", true,
                "Damaged synthetic sling", "The staged sling shows cut strands at the wear point and no inspection tag.",
                "Remove the sling from service and rig with tagged, inspected gear.");

            var goodSling = SlingStation(root, "Inspected Rigging Rack", new Vector3(2.7f, 0f, 0.9f), false);
            SafetyScenePrimitives.Target(goodSling, TrainingSiteId.TowerCrane, "inspected-rigging", false,
                "Inspected rigging rack", "Identical slings hang tagged, dated, and clear of the ground.",
                "Keep the inspection tags legible and stage rigging off the ground.");
            return root;
        }

        // Magnetic-snap rigging assembly in the Rigging Loft: the learner chooses
        // between tagged and damaged slings, snaps two sling legs and a tagline
        // onto the lift jig, and only a fully serviceable rig authorizes trolley
        // travel on the tower crane.
        static void RiggingAssembly(Transform root, TowerCraneAnimator craneAnimator)
        {
            craneAnimator.SetLiftAuthorized(false);
            var stationRoot = new GameObject("Rigging Assembly Station").transform;
            stationRoot.SetParent(root, false);
            stationRoot.localPosition = new Vector3(7.5f, 0f, 7.6f);
            var station = stationRoot.gameObject.AddComponent<AssemblyStationController>();
            station.Configure(TrainingSiteId.TowerCrane, "TCR-02", "rigging", 0, 25,
                "Lift authorized\nRigging verified with inspected components. The trolley may travel.",
                "Clean rig. Tagged slings, tagline on, load path controlled - that is how a lift starts.",
                craneAnimator);

            foreach (var x in new[] { -1.1f, 1.1f })
            {
                DecorativePrimitive(PrimitiveType.Cube, "Jig A-Post", stationRoot,
                    new Vector3(x, 0.75f, 0f), new Vector3(0.14f, 0.75f, 0.14f), Steel);
            }
            DecorativePrimitive(PrimitiveType.Cube, "Spreader Bar", stationRoot,
                new Vector3(0f, 1.52f, 0f), new Vector3(2.6f, 0.12f, 0.16f), SafetyYellow);
            DecorativePrimitive(PrimitiveType.Cube, "Mock Panel Load", stationRoot,
                new Vector3(0f, 0.45f, 0.55f), new Vector3(2.2f, 0.9f, 0.14f),
                new Color(0.66f, 0.64f, 0.6f));
            var jigLabel = SafetyScenePrimitives.Label("RIG THE LIFT\nSLING + SLING + TAGLINE", stationRoot,
                new Vector3(0f, 2.05f, -0.1f), 0.07f);
            jigLabel.color = new Color(0.72f, 0.88f, 0.96f);

            CreateAssemblySocket(station, stationRoot, "sling-shackle-left",
                "sling", new Vector3(-0.7f, 1.2f, 0f));
            CreateAssemblySocket(station, stationRoot, "sling-shackle-right",
                "sling", new Vector3(0.7f, 1.2f, 0f));
            CreateAssemblySocket(station, stationRoot, "tagline-anchor",
                "tagline", new Vector3(0f, 0.5f, 0.75f));

            CreateAssemblyPart(station, stationRoot, "tagged-sling-a", "sling", true,
                new Vector3(-2.4f, 0.55f, -1.6f), new Color(0.2f, 0.35f, 0.7f));
            CreateAssemblyPart(station, stationRoot, "tagged-sling-b", "sling", true,
                new Vector3(-1.7f, 0.55f, -1.9f), new Color(0.2f, 0.35f, 0.7f));
            CreateAssemblyPart(station, stationRoot, "frayed-sling-a", "sling", false,
                new Vector3(1.7f, 0.55f, -1.9f), new Color(0.5f, 0.42f, 0.2f));
            CreateAssemblyPart(station, stationRoot, "frayed-sling-b", "sling", false,
                new Vector3(2.4f, 0.55f, -1.6f), new Color(0.5f, 0.42f, 0.2f));
            CreateAssemblyPart(station, stationRoot, "tagline-coil", "tagline", true,
                new Vector3(0f, 0.35f, -2.1f), new Color(0.9f, 0.55f, 0.12f));
            var partsLabel = SafetyScenePrimitives.Label("TAGGED            FRAYED", stationRoot,
                new Vector3(0f, 1.25f, -1.85f), 0.06f);
            partsLabel.color = new Color(0.72f, 0.88f, 0.96f);
        }

        // Guardrail kit assembly (annex laydown yard): posts must be seated before
        // either rail will accept - the 1926.502 build order becomes the mechanic.
        static void GuardrailAssembly(Transform root)
        {
            var stationRoot = new GameObject("Guardrail Assembly Station").transform;
            stationRoot.SetParent(root, false);
            stationRoot.localPosition = new Vector3(-7f, 0f, 15.6f);
            var station = stationRoot.gameObject.AddComponent<AssemblyStationController>();
            station.Configure(TrainingSiteId.Construction, "CON-03", "guardrail", 0, 25,
                "Guardrail complete\nPosts, top rail, and mid rail are seated in a compliant sequence.",
                "Posts first, then rails - that is a 1926.502 guardrail done right.", null);
            DecorativePrimitive(PrimitiveType.Cube, "Guardrail Base Curb", stationRoot,
                new Vector3(0f, 0.08f, 0f), new Vector3(2.8f, 0.16f, 0.4f), Concrete);
            var label = SafetyScenePrimitives.Label("BUILD THE GUARDRAIL\nPOSTS FIRST, THEN RAILS", stationRoot,
                new Vector3(0f, 1.9f, -0.3f), 0.065f);
            label.color = new Color(0.72f, 0.88f, 0.96f);

            CreateAssemblySocket(station, stationRoot, "post-left", "post",
                new Vector3(-1f, 0.7f, 0f));
            CreateAssemblySocket(station, stationRoot, "post-right", "post",
                new Vector3(1f, 0.7f, 0f));
            CreateAssemblySocket(station, stationRoot, "top-rail", "rail-top",
                new Vector3(0f, 1.35f, 0f), "post", 2);
            CreateAssemblySocket(station, stationRoot, "mid-rail", "rail-mid",
                new Vector3(0f, 0.85f, 0f), "post", 2);

            CreateAssemblyPart(station, stationRoot, "post-a", "post", true,
                new Vector3(-2.2f, 0.35f, -1.4f), SafetyYellow);
            CreateAssemblyPart(station, stationRoot, "post-b", "post", true,
                new Vector3(-1.5f, 0.35f, -1.7f), SafetyYellow);
            CreateAssemblyPart(station, stationRoot, "bent-post", "post", false,
                new Vector3(-0.6f, 0.35f, -1.9f), new Color(0.62f, 0.5f, 0.14f));
            CreateAssemblyPart(station, stationRoot, "top-rail-section", "rail-top", true,
                new Vector3(1.1f, 0.35f, -1.7f), new Color(0.94f, 0.94f, 0.88f));
            CreateAssemblyPart(station, stationRoot, "mid-rail-section", "rail-mid", true,
                new Vector3(1.9f, 0.35f, -1.4f), new Color(0.82f, 0.82f, 0.76f));
        }

        // Shore frame capacity assembly (annex drill yard): the physical version of
        // the formwork calculation - seat frames until rated capacity covers the
        // documented demand (5 x 4,500 lb >= 19,000 lb).
        static void ShoreFrameAssembly(Transform root)
        {
            var stationRoot = new GameObject("Shore Frame Assembly Station").transform;
            stationRoot.SetParent(root, false);
            stationRoot.localPosition = new Vector3(7f, 0f, 15.6f);
            var station = stationRoot.gameObject.AddComponent<AssemblyStationController>();
            station.Configure(TrainingSiteId.Construction, "CON-02", "shore-frames", 5, 25,
                "Capacity verified\n5 x 4,500 lb = 22,500 lb rated support covers the 19,000 lb demand.",
                "You proved the number with your hands: capacity above demand before any pour.", null);
            DecorativePrimitive(PrimitiveType.Cube, "Mock Slab Soffit", stationRoot,
                new Vector3(0f, 1.75f, 0f), new Vector3(3.4f, 0.14f, 1.6f), Concrete);
            var label = SafetyScenePrimitives.Label(
                "SHORE THE POUR\nDEMAND 19,000 LB  FRAMES 4,500 LB EACH", stationRoot,
                new Vector3(0f, 2.25f, -0.85f), 0.06f);
            label.color = new Color(0.72f, 0.88f, 0.96f);

            for (var bay = 0; bay < 6; bay++)
                CreateAssemblySocket(station, stationRoot, $"shore-bay-{bay + 1}", "shore",
                    new Vector3(-1.25f + bay * 0.5f, 0.85f, 0f));
            for (var frame = 0; frame < 6; frame++)
                CreateAssemblyPart(station, stationRoot, $"shore-frame-{frame + 1}", "shore", true,
                    new Vector3(-1.5f + frame * 0.6f, 0.35f, -1.7f), Steel);
        }

        // Lockout/tagout assembly (annex laydown yard): hasp, then lock, then tag -
        // the 1926.417 sequence enforced by socket prerequisites, with an expired
        // tag as the serviceability foil.
        static void LotoAssembly(Transform root)
        {
            var stationRoot = new GameObject("LOTO Assembly Station").transform;
            stationRoot.SetParent(root, false);
            stationRoot.localPosition = new Vector3(-7f, 0f, 15.6f);
            var station = stationRoot.gameObject.AddComponent<AssemblyStationController>();
            station.Configure(TrainingSiteId.ElectricalMaintenance, "ELE-02", "loto", 0, 25,
                "Isolation secured\nHasp, personal lock, and current danger tag are applied in sequence.",
                "Hasp, lock, tag - your energy control is on and documented.", null);
            DecorativePrimitive(PrimitiveType.Cube, "Mock Disconnect Switch", stationRoot,
                new Vector3(0f, 1.15f, 0.1f), new Vector3(0.9f, 1.3f, 0.3f),
                new Color(0.31f, 0.36f, 0.43f));
            var label = SafetyScenePrimitives.Label("APPLY LOCKOUT\nHASP > LOCK > TAG", stationRoot,
                new Vector3(0f, 2.05f, -0.15f), 0.065f);
            label.color = new Color(0.72f, 0.88f, 0.96f);

            CreateAssemblySocket(station, stationRoot, "hasp-point", "hasp",
                new Vector3(0f, 1.05f, -0.15f));
            CreateAssemblySocket(station, stationRoot, "lock-point", "lock",
                new Vector3(0f, 0.75f, -0.2f), "hasp", 1);
            CreateAssemblySocket(station, stationRoot, "tag-point", "tag",
                new Vector3(0.35f, 0.75f, -0.2f), "lock", 1);

            CreateAssemblyPart(station, stationRoot, "group-hasp", "hasp", true,
                new Vector3(-1.6f, 0.35f, -1.3f), Steel);
            CreateAssemblyPart(station, stationRoot, "personal-lock", "lock", true,
                new Vector3(-0.9f, 0.35f, -1.6f), new Color(0.85f, 0.12f, 0.1f));
            CreateAssemblyPart(station, stationRoot, "danger-tag", "tag", true,
                new Vector3(0.9f, 0.35f, -1.6f), SafetyYellow);
            CreateAssemblyPart(station, stationRoot, "expired-tag", "tag", false,
                new Vector3(1.6f, 0.35f, -1.3f), new Color(0.6f, 0.58f, 0.5f));
        }

        static void CreateAssemblySocket(AssemblyStationController station, Transform parent,
            string id, string category, Vector3 localPosition,
            string prerequisiteCategory = null, int prerequisiteCount = 0)
        {
            var socketObject = new GameObject($"Assembly Socket - {id}");
            socketObject.transform.SetParent(parent, false);
            socketObject.transform.localPosition = localPosition;
            var marker = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Socket Marker",
                socketObject.transform, Vector3.zero, new Vector3(0.16f, 0.16f, 0.16f),
                new Color(0.16f, 0.5f, 0.42f));
            Object.DestroyImmediate(marker.GetComponent<Collider>());
            var socket = socketObject.AddComponent<AssemblySocket>();
            socket.Configure(id, category, 0.55f);
            if (!string.IsNullOrEmpty(prerequisiteCategory))
                socket.RequirePrerequisite(prerequisiteCategory, prerequisiteCount);
            station.RegisterSocket(socket);
        }

        static void CreateAssemblyPart(AssemblyStationController station, Transform parent,
            string id, string category, bool serviceable, Vector3 localPosition, Color color)
        {
            var part = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, $"Assembly Part - {id}",
                parent, localPosition, new Vector3(0.09f, 0.32f, 0.09f), color);
            part.AddComponent<Rigidbody>();
            part.AddComponent<XRGrabInteractable>();
            part.AddComponent<InteractiveHoverFeedback>();
            part.AddComponent<AssemblyPart>().Configure(station, id, category, serviceable);
            if (serviceable)
            {
                var tag = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Inspection Tag",
                    part.transform, new Vector3(0.1f, 0.55f, 0f), new Vector3(0.55f, 0.28f, 0.1f),
                    SafetyYellow);
                Object.DestroyImmediate(tag.GetComponent<Collider>());
            }
        }

        static void ApartmentFrame(Transform parent, Vector3 position)
        {
            var frame = new GameObject("Apartment Frame Under Construction").transform;
            frame.SetParent(parent, false);
            frame.localPosition = position;
            var concrete = new Color(0.62f, 0.61f, 0.58f);
            for (var floor = 0; floor < 5; floor++)
            {
                var y = 0.95f * (floor + 1);
                var slab = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, $"Apartment Slab L{floor + 1}",
                    frame, new Vector3(0f, y, 0f), new Vector3(4.1f, 0.14f, 3.4f), concrete);
                Object.DestroyImmediate(slab.GetComponent<Collider>());
                foreach (var x in new[] { -1.8f, 0f, 1.8f })
                    foreach (var z in new[] { -1.45f, 1.45f })
                    {
                        var column = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Apartment Column",
                            frame, new Vector3(x, y - 0.48f, z), new Vector3(0.22f, 0.82f, 0.22f), concrete * 0.94f);
                        Object.DestroyImmediate(column.GetComponent<Collider>());
                    }
            }
            foreach (var x in new[] { -1.6f, 0.4f, 1.9f })
            {
                var rebar = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Column Rebar Cage",
                    frame, new Vector3(x, 5.15f, 1.45f), new Vector3(0.1f, 0.35f, 0.1f),
                    new Color(0.45f, 0.3f, 0.2f));
                Object.DestroyImmediate(rebar.GetComponent<Collider>());
            }
            ApplyMetalFinish(frame, 0.05f, 0.24f);
        }

        static TowerCraneAnimator TowerCraneRig(Transform parent, Vector3 position)
        {
            var rig = new GameObject("Fixed Tower Crane").transform;
            rig.SetParent(parent, false);
            rig.localPosition = position;
            var craneYellow = new Color(0.92f, 0.62f, 0.1f);
            var pad = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Crane Foundation Pad",
                rig, new Vector3(0f, 0.25f, 0f), new Vector3(2.8f, 0.5f, 2.8f), Concrete);
            Object.DestroyImmediate(pad.GetComponent<Collider>());
            const int mastSegments = 11;
            const float segmentHeight = 1.3f;
            for (var segment = 0; segment < mastSegments; segment++)
            {
                var y = 0.5f + segmentHeight * segment + segmentHeight / 2f;
                foreach (var x in new[] { -0.55f, 0.55f })
                    foreach (var z in new[] { -0.55f, 0.55f })
                    {
                        var chord = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Mast Chord",
                            rig, new Vector3(x, y, z), new Vector3(0.09f, segmentHeight / 2f, 0.09f), craneYellow);
                        Object.DestroyImmediate(chord.GetComponent<Collider>());
                    }
                var band = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Mast Lattice Band",
                    rig, new Vector3(0f, y + segmentHeight / 2f - 0.05f, 0f),
                    new Vector3(1.25f, 0.1f, 1.25f), craneYellow * 0.92f);
                Object.DestroyImmediate(band.GetComponent<Collider>());
            }
            var slewY = 0.5f + segmentHeight * mastSegments + 0.25f;

            var slewingUnit = new GameObject("Slewing Unit").transform;
            slewingUnit.SetParent(rig, false);
            slewingUnit.localPosition = new Vector3(0f, slewY, 0f);
            void SlewPart(GameObject item) => Object.DestroyImmediate(item.GetComponent<Collider>());

            SlewPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Slew Ring", slewingUnit,
                new Vector3(0f, 0.12f, 0f), new Vector3(1.35f, 0.24f, 1.35f), craneYellow * 0.85f));
            SlewPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Operator Cab", slewingUnit,
                new Vector3(-0.95f, 0.62f, 0f), new Vector3(0.95f, 0.95f, 0.9f),
                new Color(0.2f, 0.24f, 0.3f)));
            SlewPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Main Jib", slewingUnit,
                new Vector3(-6.4f, 1.05f, 0f), new Vector3(12.6f, 0.2f, 0.55f), craneYellow));
            SlewPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Counter Jib", slewingUnit,
                new Vector3(2.9f, 1.05f, 0f), new Vector3(4.6f, 0.2f, 0.55f), craneYellow));
            SlewPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Counterweight Stack", slewingUnit,
                new Vector3(4.85f, 0.5f, 0f), new Vector3(0.95f, 1.1f, 1.1f), Concrete * 0.85f));
            SlewPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Apex Pylon", slewingUnit,
                new Vector3(0f, 2.1f, 0f), new Vector3(0.08f, 1.1f, 0.08f), craneYellow));
            Strut(slewingUnit, "Jib Tie Front", new Vector3(0f, 3.15f, 0f),
                new Vector3(-12.5f, 1.15f, 0f), 0.045f, craneYellow * 0.9f);
            Strut(slewingUnit, "Jib Tie Mid", new Vector3(0f, 3.15f, 0f),
                new Vector3(-6.4f, 1.15f, 0f), 0.04f, craneYellow * 0.9f);
            Strut(slewingUnit, "Counter Tie", new Vector3(0f, 3.15f, 0f),
                new Vector3(4.9f, 1.15f, 0f), 0.045f, craneYellow * 0.9f);

            var trolleyAssembly = new GameObject("Trolley Assembly").transform;
            trolleyAssembly.SetParent(slewingUnit, false);
            trolleyAssembly.localPosition = new Vector3(-9.2f, 0.85f, 0f);
            void TrolleyPart(GameObject item) => Object.DestroyImmediate(item.GetComponent<Collider>());
            TrolleyPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Jib Trolley", trolleyAssembly,
                Vector3.zero, new Vector3(0.6f, 0.28f, 0.68f), new Color(0.25f, 0.28f, 0.32f)));
            var lineLength = slewY + 0.85f - 6.5f;
            TrolleyPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Hoist Line", trolleyAssembly,
                new Vector3(0f, -lineLength / 2f, 0f), new Vector3(0.055f, lineLength / 2f, 0.055f),
                new Color(0.15f, 0.15f, 0.16f)));
            TrolleyPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Hook Block", trolleyAssembly,
                new Vector3(0f, -lineLength, 0f), new Vector3(0.28f, 0.45f, 0.28f), SafetyYellow));
            TrolleyPart(SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Suspended Precast Panel",
                trolleyAssembly, new Vector3(0f, -lineLength - 0.95f, 0f),
                new Vector3(2.6f, 1.3f, 0.2f), new Color(0.66f, 0.64f, 0.6f)));
            foreach (var dx in new[] { -0.9f, 0.9f })
            {
                var sling = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Panel Sling Leg",
                    trolleyAssembly, new Vector3(dx / 2f, -lineLength - 0.35f, 0f),
                    new Vector3(0.028f, 0.4f, 0.028f), new Color(0.2f, 0.35f, 0.7f));
                sling.transform.localRotation = Quaternion.Euler(0f, 0f, dx > 0f ? -30f : 30f);
                Object.DestroyImmediate(sling.GetComponent<Collider>());
            }

            var animator = rig.gameObject.AddComponent<TowerCraneAnimator>();
            animator.Configure(slewingUnit, trolleyAssembly);
            ApplyMetalFinish(rig, 0.55f, 0.42f);
            return animator;
        }

        // Unifies primitive-built assemblies with the PBR props around them by
        // swapping default materials for cached metal-finish materials.
        static void ApplyMetalFinish(Transform root, float metallic, float smoothness)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                var color = renderer.sharedMaterial != null
                    ? renderer.sharedMaterial.color : Color.gray;
                renderer.sharedMaterial = SafetyEquipmentMeshFactory.Material(color, metallic, smoothness);
            }
        }

        static void Strut(Transform parent, string name, Vector3 from, Vector3 to,
            float radius, Color color)
        {
            var strut = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, name, parent,
                (from + to) / 2f, new Vector3(radius, Vector3.Distance(from, to) / 2f, radius), color);
            strut.transform.localRotation = Quaternion.FromToRotation(Vector3.up, to - from);
            Object.DestroyImmediate(strut.GetComponent<Collider>());
        }

        static GameObject PowerPole(Transform parent, string name, Vector3 position, bool controlled)
        {
            var pole = AssemblyRoot(parent, name, position, new Vector3(1.6f, 4.6f, 1.3f),
                new Vector3(0f, 2.2f, 0f));
            DecorativePrimitive(PrimitiveType.Cylinder, "Utility Pole", pole.transform,
                new Vector3(0f, 2.1f, 0f), new Vector3(0.14f, 2.1f, 0.14f), Timber * 0.8f);
            DecorativePrimitive(PrimitiveType.Cube, "Crossarm", pole.transform,
                new Vector3(0f, 4f, 0f), new Vector3(1.4f, 0.09f, 0.09f), Timber * 0.7f);
            foreach (var x in new[] { -0.55f, 0.55f })
                foreach (var direction in new[] { 1f, -1f })
                {
                    var span = DecorativePrimitive(PrimitiveType.Cylinder, "Energized Span", pole.transform,
                        new Vector3(x, 3.72f, direction * 1.02f), new Vector3(0.032f, 1.08f, 0.032f),
                        new Color(0.1f, 0.1f, 0.12f));
                    span.transform.localRotation = Quaternion.Euler(direction * 79f, 0f, 0f);
                }
            if (controlled)
            {
                for (var flag = 0; flag < 4; flag++)
                    DecorativePrimitive(PrimitiveType.Cube, "Warning Line Flag", pole.transform,
                        new Vector3(-0.9f + flag * 0.6f, 2.5f, 0.9f), new Vector3(0.16f, 0.22f, 0.02f),
                        new Color(0.95f, 0.4f, 0.05f));
                DecorativePrimitive(PrimitiveType.Cube, "Spotter Post Sign", pole.transform,
                    new Vector3(0.75f, 1.2f, 0.55f), new Vector3(0.55f, 0.55f, 0.05f), SafetyYellow);
                SafetyScenePrimitives.Label("DEDICATED\nSPOTTER", pole.transform,
                    new Vector3(0.75f, 1.2f, 0.5f), 0.05f);
            }
            return pole;
        }

        static GameObject SlingStation(Transform parent, string name, Vector3 position, bool damaged)
        {
            var rack = AssemblyRoot(parent, name, position, new Vector3(1.6f, 2f, 1f),
                new Vector3(0f, 0.95f, 0f));
            foreach (var x in new[] { -0.6f, 0.6f })
                DecorativePrimitive(PrimitiveType.Cylinder, "Rack Post", rack.transform,
                    new Vector3(x, 0.85f, 0f), new Vector3(0.06f, 0.85f, 0.06f), Steel);
            DecorativePrimitive(PrimitiveType.Cylinder, "Rack Rail", rack.transform,
                    new Vector3(0f, 1.68f, 0f), new Vector3(0.05f, 0.66f, 0.05f), Steel)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            var slingColor = damaged ? new Color(0.5f, 0.42f, 0.2f) : new Color(0.2f, 0.35f, 0.7f);
            foreach (var x in new[] { -0.3f, 0.1f })
            {
                var sling = DecorativePrimitive(PrimitiveType.Cylinder, "Staged Sling", rack.transform,
                    new Vector3(x, damaged ? 1.05f : 1.25f, 0f), new Vector3(0.035f, 0.42f, 0.035f), slingColor);
                if (damaged)
                    sling.transform.localRotation = Quaternion.Euler(0f, 0f, x < 0f ? 14f : -9f);
            }
            if (damaged)
            {
                DecorativePrimitive(PrimitiveType.Cylinder, "Cut Strand Stub", rack.transform,
                    new Vector3(-0.28f, 0.66f, 0.05f), new Vector3(0.015f, 0.09f, 0.015f),
                    new Color(0.75f, 0.68f, 0.5f));
                DecorativePrimitive(PrimitiveType.Cylinder, "Sling Tail On Ground", rack.transform,
                        new Vector3(0.35f, 0.05f, 0.15f), new Vector3(0.03f, 0.3f, 0.03f), slingColor)
                    .transform.localRotation = Quaternion.Euler(0f, 30f, 90f);
            }
            else
            {
                foreach (var x in new[] { -0.3f, 0.1f })
                    DecorativePrimitive(PrimitiveType.Cube, "Inspection Tag", rack.transform,
                        new Vector3(x + 0.06f, 0.86f, 0.03f), new Vector3(0.08f, 0.12f, 0.01f), SafetyYellow);
            }
            return rack;
        }

        static Transform SiteRoot(string title, Vector3 origin, Color color)
        {
            var root = new GameObject(title).transform;
            root.position = origin;
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Floor", root,
                new Vector3(0f, -0.15f, 0f), new Vector3(10f, 0.3f, 9f), color);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Floor Safety Border", root,
                new Vector3(0f, 0.03f, -4.2f), new Vector3(9.4f, 0.04f, 0.08f), SafetyYellow);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Back Wall", root,
                new Vector3(0f, 1.5f, 4.25f), new Vector3(10f, 3f, 0.25f), color * 1.35f);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Worklight Header", root,
                new Vector3(0f, 3.1f, 3.95f), new Vector3(7.4f, 0.08f, 0.08f), new Color(0.92f, 0.94f, 0.85f));
            SafetyScenePrimitives.Label(title, root, new Vector3(0f, 2.7f, 3.95f), 0.1f);
            return root;
        }

        static GameObject Drum(Transform parent, string name, Vector3 position, Color body, Color band)
        {
            var drum = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, name, parent,
                position + new Vector3(0f, 0.65f, 0f), new Vector3(0.72f, 0.65f, 0.72f), body);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, "Drum Top", drum.transform,
                new Vector3(0f, 0.94f, 0f), new Vector3(0.68f, 0.05f, 0.68f), body * 1.12f);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Hazard Band", drum.transform,
                new Vector3(0f, 0.15f, 0f), new Vector3(0.75f, 0.08f, 0.75f), band);
            SafetyScenePrimitives.Label("CHEM", drum.transform,
                new Vector3(0f, 0.65f, -0.38f), 0.08f);
            return drum;
        }

        static GameObject Eyewash(Transform parent, string name, Vector3 position)
        {
            var station = AssemblyRoot(parent, name, position, new Vector3(1.4f, 2.2f, 1.1f),
                new Vector3(0f, 1.05f, 0f));
            var green = new Color(0.08f, 0.48f, 0.24f);
            var stainless = new Color(0.58f, 0.64f, 0.65f);
            DecorativePrimitive(PrimitiveType.Cylinder, "Pedestal", station.transform,
                new Vector3(0f, 0.58f, 0f), new Vector3(0.16f, 0.58f, 0.16f), stainless, 0.35f, 0.6f);
            DecorativePrimitive(PrimitiveType.Cylinder, "Floor Flange", station.transform,
                new Vector3(0f, 0.07f, 0f), new Vector3(0.34f, 0.07f, 0.34f), stainless * 0.78f,
                0.35f, 0.55f);
            DecorativePrimitive(PrimitiveType.Cylinder, "Drain Collar", station.transform,
                new Vector3(0f, 0.35f, 0f), new Vector3(0.24f, 0.05f, 0.24f), stainless * 0.82f,
                0.35f, 0.55f);
            SafetyEquipmentMeshFactory.CreateEyewashBowl(station.transform, new Vector3(0f, 1.16f, 0f), green);
            foreach (var x in new[] { -0.22f, 0.22f })
            {
                DecorativePrimitive(PrimitiveType.Cylinder, "Nozzle Stem", station.transform,
                    new Vector3(x, 1.28f, 0f), new Vector3(0.045f, 0.11f, 0.045f), stainless, 0.35f, 0.62f);
                DecorativePrimitive(PrimitiveType.Sphere, "Nozzle Head", station.transform,
                    new Vector3(x, 1.4f, -0.03f), new Vector3(0.12f, 0.08f, 0.12f), green * 1.2f);
            }
            DecorativePrimitive(PrimitiveType.Cube, "Activation Paddle", station.transform,
                new Vector3(0.45f, 0.72f, -0.02f), new Vector3(0.46f, 0.08f, 0.14f), green * 1.2f);
            DecorativePrimitive(PrimitiveType.Cube, "Eyewash Sign", station.transform,
                new Vector3(0f, 1.92f, 0.28f), new Vector3(1.15f, 0.62f, 0.06f), green);
            SafetyScenePrimitives.Label("EMERGENCY\nEYEWASH", station.transform,
                new Vector3(0f, 1.92f, 0.2f), 0.065f);
            return station;
        }

        static GameObject ElectricalPanel(Transform parent, string name, Vector3 position, bool locked)
        {
            var panel = AssemblyRoot(parent, name, position, new Vector3(1.7f, 2.55f, 1.1f),
                new Vector3(0f, 1.25f, 0f));
            var cabinet = locked ? new Color(0.31f, 0.36f, 0.43f) : new Color(0.16f, 0.18f, 0.2f);
            SafetyEquipmentMeshFactory.CreateElectricalCabinetShell(panel.transform,
                new Vector3(0f, 1.25f, 0.08f), cabinet);
            DecorativePrimitive(PrimitiveType.Cube, "Panel Plinth", panel.transform,
                new Vector3(0f, 0.08f, 0f), new Vector3(1.48f, 0.16f, 0.52f), cabinet * 0.72f, 0.72f, 0.34f);
            DecorativePrimitive(PrimitiveType.Cube, "Panel Rain Cap", panel.transform,
                new Vector3(0f, 2.43f, 0f), new Vector3(1.42f, 0.1f, 0.48f), cabinet * 1.18f,
                0.72f, 0.34f);

            if (locked)
            {
                DecorativePrimitive(PrimitiveType.Cube, "Closed Panel Door", panel.transform,
                    new Vector3(0f, 1.25f, -0.19f), new Vector3(1.12f, 2.08f, 0.06f), cabinet * 1.28f);
                DecorativePrimitive(PrimitiveType.Cube, "Lock Body", panel.transform,
                    new Vector3(0.34f, 0.58f, -0.29f), new Vector3(0.18f, 0.24f, 0.12f), SafetyYellow);
                DecorativePrimitive(PrimitiveType.Cylinder, "Lock Shackle", panel.transform,
                    new Vector3(0.34f, 0.78f, -0.27f), new Vector3(0.08f, 0.13f, 0.08f), Steel);
            }
            else
            {
                DecorativePrimitive(PrimitiveType.Cube, "Panel Interior", panel.transform,
                    new Vector3(0f, 1.25f, -0.18f), new Vector3(1.06f, 1.96f, 0.06f), new Color(0.035f, 0.04f, 0.045f));
                var door = DecorativePrimitive(PrimitiveType.Cube, "Open Panel Door", panel.transform,
                    new Vector3(-0.72f, 1.25f, -0.62f), new Vector3(1.06f, 2.08f, 0.055f), cabinet * 1.22f);
                door.transform.localRotation = Quaternion.Euler(0f, 78f, 0f);
                foreach (var x in new[] { -0.3f, 0f, 0.3f })
                    DecorativePrimitive(PrimitiveType.Cube, "Insulated Bus Bar", panel.transform,
                        new Vector3(x, 1.5f, -0.24f), new Vector3(0.075f, 1.1f, 0.04f),
                        x < 0f ? new Color(0.72f, 0.18f, 0.12f) : x > 0f ? new Color(0.12f, 0.34f, 0.72f) : SafetyYellow);
                foreach (var y in new[] { 0.72f, 1.05f })
                    DecorativePrimitive(PrimitiveType.Cube, "Breaker Row", panel.transform,
                        new Vector3(0f, y, -0.29f), new Vector3(0.78f, 0.18f, 0.08f), Steel * 0.62f);
            }

            foreach (var y in new[] { 0.65f, 1.85f })
                DecorativePrimitive(PrimitiveType.Cylinder, "Door Hinge", panel.transform,
                    new Vector3(-0.56f, y, -0.24f), new Vector3(0.05f, 0.12f, 0.05f), Steel);
            DecorativePrimitive(PrimitiveType.Cube, "Door Handle", panel.transform,
                new Vector3(0.42f, 1.25f, -0.29f), new Vector3(0.08f, 0.34f, 0.1f), Steel * 0.7f);
            DecorativePrimitive(PrimitiveType.Cube, locked ? "Lockout Tag" : "Danger Placard", panel.transform,
                new Vector3(0f, 1.92f, -0.3f), new Vector3(0.58f, 0.34f, 0.05f),
                locked ? SafetyYellow : new Color(0.85f, 0.12f, 0.08f));
            SafetyScenePrimitives.Label(locked ? "LOCKED OUT" : "DANGER\nLIVE PARTS", panel.transform,
                new Vector3(0f, 1.92f, -0.36f), 0.055f);
            return panel;
        }

        static GameObject CableBundle(Transform parent, string name, Vector3 position, bool protectedRoute)
        {
            var cable = AssemblyRoot(parent, name, position, new Vector3(4.8f, 0.45f, 0.95f),
                new Vector3(0f, 0.18f, 0f));
            var cableColors = new[]
            {
                new Color(0.025f, 0.03f, 0.035f),
                new Color(0.42f, 0.055f, 0.035f),
                new Color(0.035f, 0.16f, 0.42f)
            };
            for (var index = 0; index < cableColors.Length; index++)
            {
                var conduit = DecorativePrimitive(PrimitiveType.Cylinder, "Flexible Power Cable", cable.transform,
                    new Vector3(0f, 0.09f, (index - 1) * 0.16f), new Vector3(0.038f, 2.15f, 0.038f),
                    cableColors[index]);
                conduit.transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
            if (protectedRoute)
            {
                SafetyEquipmentMeshFactory.CreateCableProtectorShell(cable.transform, Vector3.zero,
                    new Color(0.12f, 0.13f, 0.14f));
                foreach (var z in new[] { -0.34f, 0.34f })
                    DecorativePrimitive(PrimitiveType.Cube, "High Visibility Edge", cable.transform,
                        new Vector3(0f, 0.2f, z), new Vector3(4.72f, 0.12f, 0.08f), SafetyYellow);
                foreach (var x in new[] { -1.5f, 0f, 1.5f })
                    DecorativePrimitive(PrimitiveType.Cube, "Protector Grip Rib", cable.transform,
                        new Vector3(x, 0.3f, 0f), new Vector3(0.08f, 0.045f, 0.62f), Steel * 0.55f);
            }
            else
            {
                foreach (var x in new[] { -1.25f, 0f, 1.25f })
                    DecorativePrimitive(PrimitiveType.Cube, "Loose Cable Tie", cable.transform,
                        new Vector3(x, 0.11f, 0f), new Vector3(0.1f, 0.045f, 0.48f), SafetyYellow);
            }
            return cable;
        }

        static GameObject AssemblyRoot(Transform parent, string name, Vector3 position, Vector3 colliderSize,
            Vector3 colliderCenter)
        {
            var root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.localPosition = position;
            var collider = root.AddComponent<BoxCollider>();
            collider.size = colliderSize;
            collider.center = colliderCenter;
            return root;
        }

        static GameObject DecorativePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 position, Vector3 scale, Color color, float metallic = 0f, float smoothness = 0.28f)
        {
            var item = SafetyScenePrimitives.Primitive(type, name, parent, position, scale, color);
            item.GetComponent<Renderer>().sharedMaterial =
                SafetyEquipmentMeshFactory.Material(color, metallic, smoothness);
            Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }

        static GameObject CrateStack(Transform parent, string name, Vector3 position, int count)
        {
            var root = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, name, parent,
                position + new Vector3(0f, 0.4f, 0f), new Vector3(1.5f, 0.8f, 1.1f), Timber);
            AddCrateDetail(root.transform, new Vector3(1.5f, 0.8f, 1.1f));
            for (var index = 1; index < count; index++)
            {
                var crate = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, $"Crate {index + 1}", root.transform,
                    new Vector3(0f, index, 0f), Vector3.one, Timber * (1f - index * 0.06f));
                AddCrateDetail(crate.transform, Vector3.one);
            }
            return root;
        }

        static void AddCrateDetail(Transform crate, Vector3 size)
        {
            var wood = new Color(0.7f, 0.45f, 0.22f);
            foreach (var x in new[] { -0.44f, 0.44f })
                foreach (var z in new[] { -0.38f, 0.38f })
                    SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Crate Corner Batten", crate,
                        new Vector3(x * size.x, 0f, z * size.z),
                        new Vector3(0.08f * size.x, 0.95f * size.y, 0.08f * size.z), wood);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Crate Top Slat", crate,
                new Vector3(0f, 0.48f * size.y, 0f), new Vector3(0.9f * size.x, 0.05f * size.y, 0.85f * size.z), wood);
        }

        static GameObject Pallet(Transform parent, string name, Vector3 position)
        {
            var pallet = SafetyScenePrimitives.Primitive(PrimitiveType.Cube, name, parent,
                position + new Vector3(0f, 0.12f, 0f), new Vector3(2.1f, 0.24f, 1.4f), Timber);
            foreach (var x in new[] { -0.75f, 0f, 0.75f })
                SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Pallet Slat", pallet.transform,
                    new Vector3(x, 0.14f, 0f), new Vector3(0.18f, 0.08f, 1.25f), Timber * 1.15f);
            foreach (var z in new[] { -0.46f, 0.46f })
                SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Pallet Runner", pallet.transform,
                    new Vector3(0f, -0.12f, z), new Vector3(1.85f, 0.12f, 0.14f), Timber * 0.85f);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Load", pallet.transform,
                new Vector3(0f, 2.1f, 0f), new Vector3(0.8f, 3.4f, 0.75f), Steel);
            return pallet;
        }

        static GameObject Extinguisher(Transform parent, string name, Vector3 position)
        {
            var body = SafetyScenePrimitives.Primitive(PrimitiveType.Cylinder, name, parent,
                position + new Vector3(0f, 0.65f, 0f), new Vector3(0.3f, 0.65f, 0.3f), ExtinguisherRed);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Handle", body.transform,
                new Vector3(0f, 1.15f, 0f), new Vector3(0.6f, 0.15f, 0.18f), Steel);
            return body;
        }

        static void AddSupports(Transform platform, float height)
        {
            foreach (var x in new[] { -0.42f, 0.42f })
                SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Support", platform,
                    new Vector3(x, -height * 2.7f, 0f), new Vector3(0.12f, height * 5f, 0.12f), Steel);
        }

        static void AddGuardrail(Transform platform)
        {
            foreach (var x in new[] { -0.47f, 0.47f })
                SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Rail Post", platform,
                    new Vector3(x, 3f, -0.45f), new Vector3(0.05f, 6f, 0.08f), SafetyYellow);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Top Rail", platform,
                new Vector3(0f, 5.5f, -0.45f), new Vector3(1f, 0.25f, 0.08f), SafetyYellow);
            SafetyScenePrimitives.Primitive(PrimitiveType.Cube, "Mid Rail", platform,
                new Vector3(0f, 3f, -0.45f), new Vector3(1f, 0.2f, 0.08f), SafetyYellow);
        }

        static void AddLane(
            Transform parent,
            string name,
            Vector3 center,
            Vector2 size,
            Color fill,
            Color edge)
        {
            Marking(parent, name, center, new Vector3(size.x, 0.025f, size.y), fill);
            var edgeOffset = size.x * 0.5f;
            Marking(parent, $"{name} Left Edge", center + Vector3.left * edgeOffset,
                new Vector3(0.08f, 0.04f, size.y), edge);
            Marking(parent, $"{name} Right Edge", center + Vector3.right * edgeOffset,
                new Vector3(0.08f, 0.04f, size.y), edge);
        }

        static void AddBay(Transform parent, string name, Vector3 center, Vector2 size, Color edge)
        {
            var halfX = size.x * 0.5f;
            var halfZ = size.y * 0.5f;
            Marking(parent, $"{name} Front", center + Vector3.back * halfZ,
                new Vector3(size.x, 0.045f, 0.08f), edge);
            Marking(parent, $"{name} Back", center + Vector3.forward * halfZ,
                new Vector3(size.x, 0.045f, 0.08f), edge);
            Marking(parent, $"{name} Left", center + Vector3.left * halfX,
                new Vector3(0.08f, 0.045f, size.y), edge);
            Marking(parent, $"{name} Right", center + Vector3.right * halfX,
                new Vector3(0.08f, 0.045f, size.y), edge);
        }

        static GameObject Marking(
            Transform parent,
            string name,
            Vector3 position,
            Vector3 scale,
            Color color)
        {
            var item = SafetyScenePrimitives.Primitive(
                PrimitiveType.Cube, name, parent, position, scale, color);
            Object.DestroyImmediate(item.GetComponent<Collider>());
            return item;
        }
    }
}
