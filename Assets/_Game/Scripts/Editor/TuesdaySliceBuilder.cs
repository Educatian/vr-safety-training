using Jobsite.Core;
using Jobsite.Runtime;
using UnityEditor;
using UnityEngine;

namespace Jobsite.Editor
{
    // Tuesday: storm-line trench (GDD 4B / 16). Trench x 65.3-66.7, 1.83 m (6 ft) deep, runs along z.
    // Hazards sit inside the real work; each has a compliant twin nearby (look-alike parity rule).
    public static class TuesdaySliceBuilder
    {
        const float Depth = 1.83f, X0 = 65.3f, X1 = 66.7f;
        const string Rocketbox = "Assets/ThirdParty/MicrosoftRocketbox/Construction_Male_01/Export/Construction_Male_01.fbx";

        public static void Add(Transform gameplay)
        {
            var root = new GameObject("Conditions_Tue").transform;
            root.SetParent(gameplay);
            root.gameObject.AddComponent<PhaseMember>().Configure(WorkDay.Tue);

            // Trench box modules (8 ft) along the dug run; one module missing at z 24-28.8.
            for (var z = 10f; z < 40f; z += 2.44f)
                if (z < 24f || z > 28.8f) TrenchBox(root, z);

            var gap = Condition(root, "tue-no-protective-system", "Worker in unshored 6 ft trench", true,
                new Vector3((X0 + X1) / 2, -Depth / 2, 26.4f), new Vector3(1.6f, Depth + 0.6f, 4.8f),
                EnergySource.Gravity, CpArea.Excavation, 4, 5, ControlLevel.Engineering, 420f, true,
                "29 CFR 1926.652(a)(1)", "Protect workers in excavations 5 ft or deeper from cave-ins.", "5 ft", true);
            Worker(gap.transform.Find("Unresolved"), new Vector3(66f, -Depth, 26.4f), 0, "Marcus Bell", "Pipe layer", CrewGestures.Activity.Dig);
            TrenchBox(gap.transform.Find("Resolved"), 24.4f);

            // Spoil dumped at the edge on one stretch (the rest of the spoil line is set back more than 2 ft).
            var spoil = Condition(root, "tue-spoil-at-edge", "Spoil at the trench edge", true,
                new Vector3(67.3f, 0.5f, 36f), new Vector3(1.6f, 1.2f, 4f),
                EnergySource.Gravity, CpArea.Excavation, 3, 4, ControlLevel.Engineering, 520f, false,
                "29 CFR 1926.651(j)(2)", "Keep spoil and equipment at least 2 ft from the edge.", "2 ft (0.61 m)", true);
            Mound(spoil.transform.Find("Unresolved"), new Vector3(67.1f, 0f, 36f), new Vector3(1.2f, 1.4f, 3.6f));

            // Worker far from any ladder (the only ladder is at z 12).
            var egress = Condition(root, "tue-no-egress", "No ladder near worker in trench", true,
                new Vector3((X0 + X1) / 2, -Depth / 2, 44f), new Vector3(1.6f, Depth + 0.6f, 3f),
                EnergySource.Gravity, CpArea.Excavation, 3, 4, ControlLevel.Engineering, 560f, false,
                "29 CFR 1926.651(c)(2)", "Trenches 4 ft or deeper need a ladder within 25 ft of travel.", "25 ft", true);
            Worker(egress.transform.Find("Unresolved"), new Vector3(66f, -Depth, 44f), 180, null, null, CrewGestures.Activity.Dig);
            Ladder(root, 12f);
            Ladder(egress.transform.Find("Resolved"), 44.8f);

            // Swing radius of the NPC excavator (cab at x 62.5, z 30) open to foot traffic.
            var swing = Condition(root, "tue-swing-radius", "Walkway through excavator swing radius", true,
                new Vector3(60.5f, 1f, 30f), new Vector3(4f, 2f, 6f),
                EnergySource.Motion, CpArea.StruckBy, 4, 5, ControlLevel.Engineering, 480f, true,
                "Swing-radius barricading (excavator rule: SME check)",
                "Barricade the counterweight swing area; stop work if someone is inside.", "Full swing radius", false);
            Worker(swing.transform.Find("Unresolved"), new Vector3(60.2f, 0f, 31f), 90, null, null, CrewGestures.Activity.Walk, new Vector3(58.6f, 0f, 31f), new Vector3(62.2f, 0f, 31f));
            for (var a = 0f; a < 360f; a += 30f)
                MondaySliceBuilderAccess.Cone(swing.transform.Find("Resolved"),
                    new Vector3(62.5f, 0, 30f) + Quaternion.Euler(0, a, 0) * Vector3.forward * 5.5f);

            // Dry-cutting concrete pipe at the laydown: visible silica dust.
            var silica = Condition(root, "tue-dry-cutting", "Dry cutting concrete pipe", true,
                new Vector3(55.6f, 0.9f, 15f), new Vector3(2f, 1.8f, 2f),
                EnergySource.Chemical, CpArea.General, 4, 4, ControlLevel.Engineering, 540f, false,
                "29 CFR 1926.1153 Table 1", "Use integrated water delivery when cutting concrete.", "Wet method", true);
            Worker(silica.transform.Find("Unresolved"), new Vector3(55.2f, 0f, 15f), 90, "Luis Ortega", "Saw operator", CrewGestures.Activity.Saw); // at the pipe end, not inside the stack
            Dust(silica.transform.Find("Unresolved"), new Vector3(55.9f, 0.5f, 15f));

            // Look-alikes: a correctly placed box and a competent-person inspection board.
            Condition(root, "tue-box-ok", "Trench box in place", false,
                new Vector3((X0 + X1) / 2, -Depth / 2, 13.7f), new Vector3(1.6f, Depth + 0.6f, 2.4f),
                EnergySource.Gravity, CpArea.Excavation, 1, 1, ControlLevel.Engineering, float.PositiveInfinity, false,
                "29 CFR 1926.652(g)", "Shield installed; workers stay inside the protected zone.", "", false);
            var board = Condition(root, "tue-cp-inspection", "Daily inspection board signed", false,
                new Vector3(64.2f, 1f, 18f), new Vector3(0.8f, 1.6f, 0.4f),
                EnergySource.Gravity, CpArea.Excavation, 1, 1, ControlLevel.Administrative, float.PositiveInfinity, false,
                "29 CFR 1926.651(k)(1)", "A competent person inspects excavations daily.", "Daily", false);
            MondaySliceBuilderAccess.Box(board.transform.Find("Unresolved"), "InspectionBoard", new Vector3(64.2f, 1.1f, 18f),
                new Vector3(0.6f, 0.8f, 0.04f), new Color(0.95f, 0.95f, 0.9f));
        }

        static SiteCondition Condition(Transform root, string id, string title, bool hazard, Vector3 center, Vector3 size,
            EnergySource energy, CpArea area, int p, int s, ControlLevel best, float trigger, bool stop,
            string cfr, string requirement, string threshold, bool verified)
        {
            var go = new GameObject(id);
            go.transform.SetParent(root);
            go.transform.position = center;
            var before = new GameObject("Unresolved").transform; before.SetParent(go.transform, false);
            var after = new GameObject("Resolved").transform; after.SetParent(go.transform, false);
            var col = go.AddComponent<BoxCollider>(); col.size = size; col.isTrigger = true;
            var c = go.AddComponent<SiteCondition>();
            c.Configure(id, title, requirement, hazard, energy, area, p, s, before.gameObject, after.gameObject);
            c.SetControlKey(best, trigger, stop);
            c.SetStandard(cfr, requirement, threshold, verified);
            return c;
        }

        static void TrenchBox(Transform parent, float z)
        {
            var alu = new Color(0.72f, 0.74f, 0.76f);
            MondaySliceBuilderAccess.Box(parent, "TrenchBox_WallW", new Vector3(X0 + 0.08f, -Depth / 2 + 0.2f, z + 1.2f), new Vector3(0.1f, Depth + 0.4f, 2.4f), alu);
            MondaySliceBuilderAccess.Box(parent, "TrenchBox_WallE", new Vector3(X1 - 0.08f, -Depth / 2 + 0.2f, z + 1.2f), new Vector3(0.1f, Depth + 0.4f, 2.4f), alu);
            foreach (var dz in new[] { 0.4f, 2.0f })
                foreach (var y in new[] { -0.5f, -1.3f })
                    MondaySliceBuilderAccess.Box(parent, "Spreader", new Vector3((X0 + X1) / 2, y, z + dz), new Vector3(X1 - X0 - 0.2f, 0.1f, 0.1f), new Color(0.2f, 0.35f, 0.7f));
        }

        // Extends 3 ft (0.91 m) above the trench edge.
        static void Ladder(Transform parent, float z)
        {
            var yellow = new Color(0.95f, 0.75f, 0.1f);
            var len = Depth + 0.91f;
            foreach (var dx in new[] { -0.22f, 0.22f })
                MondaySliceBuilderAccess.Box(parent, "LadderRail", new Vector3(66f + dx, -Depth + len / 2, z), new Vector3(0.05f, len, 0.08f), yellow);
            for (var y = -Depth + 0.3f; y < 0.9f; y += 0.3f)
                MondaySliceBuilderAccess.Box(parent, "Rung", new Vector3(66f, y, z), new Vector3(0.44f, 0.03f, 0.03f), yellow);
        }

        static void Mound(Transform parent, Vector3 at, Vector3 size)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "SpoilAtEdge"; go.transform.SetParent(parent, true);
            go.transform.position = at; go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = AssetDatabase.LoadAssetAtPath<Material>("Assets/_Game/Art/Materials/M_RedClayCut.mat");
        }

        static void Worker(Transform parent, Vector3 at, float yaw, string name = null, string trade = null,
            CrewGestures.Activity activity = CrewGestures.Activity.Idle, Vector3 walkA = default, Vector3 walkB = default) =>
            SliceKit.Worker(parent, at, yaw, name, trade, activity, name != null && name.StartsWith("Tasha"), walkA, walkB);

        static void Dust(Transform parent, Vector3 at)
        {
            var go = new GameObject("SilicaDust");
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.startLifetime = 3.5f; main.startSpeed = 0.6f; main.startSize = new ParticleSystem.MinMaxCurve(0.4f, 1.2f);
            main.startColor = new Color(0.86f, 0.84f, 0.8f, 0.45f); main.maxParticles = 400; main.gravityModifier = -0.02f;
            var emission = ps.emission; emission.rateOverTime = 60f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 35f; shape.radius = 0.1f;
            var vel = ps.velocityOverLifetime; vel.enabled = true;
            vel.x = new ParticleSystem.MinMaxCurve(0.4f, 0.9f); vel.y = new ParticleSystem.MinMaxCurve(0f, 0f); vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var sz = ps.sizeOverLifetime; sz.enabled = true; sz.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0, 0.5f, 1, 2.5f));
            var r = go.GetComponent<ParticleSystemRenderer>();
            // Built-in soft round particle material: an untextured quad renders as a hard beige square.
            r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
        }
    }
}
