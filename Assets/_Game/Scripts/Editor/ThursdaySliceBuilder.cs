using Jobsite.Core;
using Jobsite.Runtime;
using UnityEngine;

namespace Jobsite.Editor
{
    // Thursday (EP4 "The Pick"): RT crane on the crane pad (x 44-52, z 42-50) setting roof steel, and Building B's
    // 16 m low-slope roof (x 92-116, z 20-38) reached by the stair tower on its west side.
    public static class ThursdaySliceBuilder
    {
        const float Roof = 16.1f;

        public static void Add(Transform gameplay)
        {
            var root = new GameObject("Conditions_Thu").transform;
            root.SetParent(gameplay);
            root.gameObject.AddComponent<PhaseMember>().Configure(WorkDay.Thu);
            var mat = new Color(0.36f, 0.26f, 0.17f); var steel = new Color(0.35f, 0.33f, 0.3f); var clay = new Color(0.45f, 0.2f, 0.12f);

            // --- crane ---
            var outrigger = SliceKit.Condition(root, "thu-outrigger-no-mat", "Outrigger float on bare clay", true,
                new Vector3(53.2f, 0.4f, 49.6f), new Vector3(1.6f, 1f, 1.6f),
                EnergySource.Gravity, CpArea.StruckBy, 3, 5, ControlLevel.Engineering, 420f, true,
                "29 CFR 1926.1402(b)", "Ground under the crane is firm, drained and graded; use mats or blocking so the crane stays level.", "Firm, drained, graded", true);
            Float(SliceKit.Before(outrigger), new Vector3(53.2f, 0f, 49.6f), steel, clay, false, mat);
            Float(SliceKit.After(outrigger), new Vector3(53.2f, 0f, 49.6f), steel, clay, true, mat);

            var swing = SliceKit.Condition(root, "thu-swing-radius", "No barricade at the counterweight swing", true,
                new Vector3(48f, 1f, 40.8f), new Vector3(7f, 2f, 2.2f),
                EnergySource.Motion, CpArea.StruckBy, 4, 5, ControlLevel.Engineering, 380f, false,
                "29 CFR 1926.1424(a)(2)", "Barricade the swing radius of the counterweight so no one can be struck or pinned.", "Full swing radius", true);
            SliceKit.Worker(SliceKit.Before(swing), new Vector3(45f, 0f, 40.6f), 90, null, null, CrewGestures.Activity.Walk, false,
                new Vector3(44.5f, 0f, 40.6f), new Vector3(51.5f, 0f, 40.6f));
            for (var x = 44.5f; x <= 51.5f; x += 3.5f) SliceKit.Model(SliceKit.After(swing), "TR-3D/SM_BarricadeType3.fbx", new Vector3(x, 0f, 40.2f), 0f);

            var under = SliceKit.Condition(root, "thu-under-load", "Worker under the suspended load", true,
                new Vector3(47f, 2.4f, 57f), new Vector3(3.5f, 4.8f, 2.4f),
                EnergySource.Gravity, CpArea.StruckBy, 4, 5, ControlLevel.Administrative, 360f, true,
                "29 CFR 1926.1425(b)", "Nobody in the fall zone of a suspended load except workers hooking, unhooking or guiding it.", "Fall zone", true);
            // Roof beam bundle flown on the hook (both states), a tag line in the resolved state.
            var bundle = SliceKit.Box(root, "SuspendedBeams", new Vector3(47f, 4.4f, 57f), new Vector3(6f, 0.45f, 0.8f), steel).transform;
            SliceKit.Line(root, "HoistLine", new Vector3(47f, 4.7f, 57f), new Vector3(47f, 21f, 57f), 0.04f, new Color(0.25f, 0.25f, 0.25f));
            SliceKit.Line(root, "Sling_L", new Vector3(45f, 4.65f, 57f), new Vector3(47f, 6.2f, 57f), 0.05f, new Color(0.45f, 0.2f, 0.6f));
            SliceKit.Line(root, "Sling_R", new Vector3(49f, 4.65f, 57f), new Vector3(47f, 6.2f, 57f), 0.05f, new Color(0.45f, 0.2f, 0.6f));
            SliceKit.Worker(SliceKit.Before(under), new Vector3(47.2f, 0f, 57.2f), 200f);
            SliceKit.Line(SliceKit.After(under), "TagLine", new Vector3(50f, 4.4f, 57f), new Vector3(53f, 1.1f, 59f), 0.02f, new Color(0.95f, 0.8f, 0.2f));
            SliceKit.Worker(SliceKit.After(under), new Vector3(53.3f, 0f, 59.3f), 250f);

            var sling = SliceKit.Condition(root, "thu-frayed-sling", "Synthetic sling cut and frayed", true,
                new Vector3(41.5f, 0.4f, 53f), new Vector3(1.8f, 0.8f, 1.6f),
                EnergySource.Gravity, CpArea.StruckBy, 3, 5, ControlLevel.Engineering, 480f, false,
                "29 CFR 1926.251(a)(1)", "Inspect rigging before each shift; remove damaged slings from service.", "Before each shift", true);
            SliceKit.Box(SliceKit.Before(sling), "RoundSling", new Vector3(41.5f, 0.05f, 53f), new Vector3(1.4f, 0.06f, 0.18f), new Color(0.45f, 0.2f, 0.6f));
            SliceKit.Box(SliceKit.Before(sling), "Cut_Fray", new Vector3(41.2f, 0.09f, 53f), new Vector3(0.2f, 0.04f, 0.2f), new Color(0.9f, 0.85f, 0.75f));
            SliceKit.Worker(SliceKit.Before(sling), new Vector3(42.3f, 0f, 53.6f), 230f, null, null, CrewGestures.Activity.Idle);
            SliceKit.Box(SliceKit.After(sling), "RoundSling_Tagged", new Vector3(41.5f, 0.05f, 53f), new Vector3(1.4f, 0.06f, 0.18f), new Color(0.45f, 0.2f, 0.6f));
            SliceKit.Box(SliceKit.After(sling), "Tag_DoNotUse", new Vector3(41.5f, 0.12f, 53f), new Vector3(0.18f, 0.02f, 0.25f), new Color(0.9f, 0.1f, 0.08f));

            // Look-alikes: the other outrigger on mats, and a qualified signal person with a radio.
            var matted = SliceKit.Condition(root, "thu-outrigger-matted", "Outrigger on crane mats", false,
                new Vector3(42.8f, 0.4f, 49.6f), new Vector3(1.6f, 1f, 1.6f),
                EnergySource.Gravity, CpArea.StruckBy, 1, 1, ControlLevel.Engineering, float.PositiveInfinity, false,
                "29 CFR 1926.1402(b)", "Mats spread the load on firm, graded ground.", "Firm, drained, graded", true);
            Float(SliceKit.Before(matted), new Vector3(42.8f, 0f, 49.6f), steel, clay, true, mat);
            var signal = SliceKit.Condition(root, "thu-signal-person", "Signal person with radio", false,
                new Vector3(44.8f, 1f, 53.5f), new Vector3(1f, 2f, 1f),
                EnergySource.Motion, CpArea.StruckBy, 1, 1, ControlLevel.Administrative, float.PositiveInfinity, false,
                "29 CFR 1926.1428", "A qualified signal person directs the pick when the operator's view is obstructed.", "Qualified", true);
            SliceKit.Worker(SliceKit.Before(signal), new Vector3(44.8f, 0f, 53.5f), 60f, "Kiara Wells", "Signal person", CrewGestures.Activity.Signal, true,
                new Vector3(47f, 6f, 57f));   // hand signals, eyes on the load

            // --- roof (via the stair tower) ---
            var access = new GameObject("StairTower_Access"); access.transform.SetParent(root, false); access.transform.position = new Vector3(87.4f, 1f, 24f);
            access.AddComponent<BoxCollider>().size = new Vector3(0.8f, 2f, 3f);
            access.AddComponent<AccessPoint>().Configure("Climb the stair tower to the roof", new Vector3(94.5f, Roof + 0.05f, 24f), 90f);
            var down = new GameObject("Roof_Access_Down"); down.transform.SetParent(root, false); down.transform.position = new Vector3(92.6f, Roof + 1f, 24f);
            down.AddComponent<BoxCollider>().size = new Vector3(0.6f, 2f, 2f);
            down.AddComponent<AccessPoint>().Configure("Climb down the stair tower", new Vector3(86.6f, 0.05f, 24f), 270f);
            // Keep the learner inside the warning line and off the shaft/stair openings (they only inspect from here).
            const float x0 = 92f, z0 = 20f, w = 24f, d = 18f, inset = 1.83f;
            SliceKit.Blocker(root, new Vector3(x0 + w / 2, Roof + 1f, z0 + inset - 0.1f), new Vector3(w, 2f, 0.2f));
            SliceKit.Blocker(root, new Vector3(x0 + w / 2, Roof + 1f, z0 + d - inset + 0.1f), new Vector3(w, 2f, 0.2f));
            SliceKit.Blocker(root, new Vector3(x0 + w - inset + 0.1f, Roof + 1f, z0 + d / 2), new Vector3(0.2f, 2f, d));
            SliceKit.Blocker(root, new Vector3(x0 + 7.2f, Roof + 1f, z0 + 8.5f), new Vector3(9.4f, 2f, 5.2f));   // shaft + stair openings

            var edge = SliceKit.Condition(root, "thu-roof-edge", "Roofer outside the warning line, no fall protection", true,
                new Vector3(106f, Roof + 1f, 37.1f), new Vector3(2f, 2f, 1.4f),
                EnergySource.Gravity, CpArea.FallProtection, 4, 5, ControlLevel.Engineering, 400f, true,
                "29 CFR 1926.501(b)(10); 1926.502(f)", "Between a warning line and the edge, workers need guardrails, a safety net, PFAS or a monitor system.", "Line 6 ft from edge", true);
            SliceKit.Worker(SliceKit.Before(edge), new Vector3(106f, Roof, 37.3f), 180f);
            SliceKit.Model(SliceKit.After(edge), "TR-3D/SM_SrlRoofAnchor.fbx", new Vector3(104f, Roof, 34.5f), 0f);
            SliceKit.Worker(SliceKit.After(edge), new Vector3(106f, Roof, 37.3f), 180f, null, null, CrewGestures.Activity.TiedOff);
            SliceKit.Line(SliceKit.After(edge), "SRL_Lifeline", new Vector3(104f, Roof + 0.3f, 34.5f), new Vector3(106f, Roof + 1.1f, 37.2f), 0.02f, new Color(0.95f, 0.6f, 0.05f));

            var sky = SliceKit.Condition(root, "thu-open-skylight", "Skylight with no screen or cover", true,
                new Vector3(96f, Roof + 0.4f, 34f), new Vector3(1.8f, 0.8f, 3f),
                EnergySource.Gravity, CpArea.FallProtection, 3, 5, ControlLevel.Engineering, 500f, false,
                "29 CFR 1926.501(b)(4)(i)", "Skylights are holes: cover or guard them.", "Any skylight", true);
            SliceKit.Worker(SliceKit.Before(sky), new Vector3(97.4f, Roof, 33f), 250f);
            Screen(SliceKit.After(sky), new Vector3(96f, Roof + 0.45f, 34f));
            var screened = SliceKit.Condition(root, "thu-skylight-screened", "Skylight with a screen", false,
                new Vector3(112f, Roof + 0.4f, 34f), new Vector3(1.8f, 0.8f, 3f),
                EnergySource.Gravity, CpArea.FallProtection, 1, 1, ControlLevel.Engineering, float.PositiveInfinity, false,
                "29 CFR 1926.501(b)(4)(i)", "A secured screen over the skylight guards it.", "Covered", true);
            Screen(SliceKit.Before(screened), new Vector3(112f, Roof + 0.45f, 34f));
        }

        // Outrigger jack + float; on bare clay it sinks (dark ring), on mats it sits level.
        static void Float(Transform parent, Vector3 at, Color steel, Color clay, bool onMats, Color mat)
        {
            if (onMats) for (var i = -1; i <= 1; i++) SliceKit.Box(parent, "CraneMat", at + new Vector3(i * 0.42f, 0.1f, 0), new Vector3(0.4f, 0.2f, 1.6f), mat);
            var baseY = onMats ? 0.2f : -0.06f;
            SliceKit.Box(parent, "Float", at + new Vector3(0, baseY + 0.04f, 0), new Vector3(0.6f, 0.08f, 0.6f), steel);
            SliceKit.Box(parent, "Jack", at + new Vector3(0, baseY + 0.55f, 0), new Vector3(0.22f, 1f, 0.22f), steel);
            if (!onMats) SliceKit.Box(parent, "Rut", at + new Vector3(0, 0.005f, 0), new Vector3(0.95f, 0.01f, 0.95f), clay * 0.6f);
        }

        // Galvanized skylight screen with bolts.
        static void Screen(Transform parent, Vector3 at)
        {
            var g = new Color(0.62f, 0.64f, 0.65f);
            for (var i = -3; i <= 3; i++) SliceKit.Box(parent, "ScreenBar_X", at + new Vector3(i * 0.2f, 0, 0), new Vector3(0.02f, 0.02f, 2.6f), g);
            for (var j = -6; j <= 6; j++) SliceKit.Box(parent, "ScreenBar_Z", at + new Vector3(0, 0.01f, j * 0.2f), new Vector3(1.4f, 0.02f, 0.02f), g);
        }
    }
}
