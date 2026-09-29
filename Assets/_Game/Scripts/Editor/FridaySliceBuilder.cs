using Jobsite.Core;
using Jobsite.Runtime;
using UnityEngine;

namespace Jobsite.Editor
{
    // Friday (EP5 "Under the Line", capstone): the pump-station pour with a boom pump set up under the 13 kV
    // distribution line (z 78, conductors 9.1 m), ready-mix trucks backing in, and rebar at the slab edge.
    public static class FridaySliceBuilder
    {
        const float LineZ = 78f, LineY = 9.1f;

        public static void Add(Transform gameplay)
        {
            var root = new GameObject("Conditions_Fri").transform;
            root.SetParent(gameplay);
            root.gameObject.AddComponent<PhaseMember>().Configure(WorkDay.Fri);
            var red = new Color(0.72f, 0.1f, 0.08f); var orange = new Color(0.95f, 0.45f, 0.05f); var rebar = new Color(0.3f, 0.18f, 0.12f);

            // The boom pump (TR-3D) behind the pump station, outriggers down.
            SliceKit.LowCollider(SliceKit.Model(root, "TR-3D/SM_ConcretePumpTruck.fbx", new Vector3(40f, 0f, 66f), 90f), 2.4f);

            // Capstone: boom unfolded toward the line, tip about 2 m from the conductor (inside 10 ft).
            var boom = SliceKit.Condition(root, "fri-boom-near-line", "Pump boom inside 10 ft of the power line", true,
                new Vector3(40f, 7.9f, 75.2f), new Vector3(2f, 2.2f, 3.2f),
                EnergySource.Electrical, CpArea.Electrical, 4, 5, ControlLevel.Engineering, 330f, true,
                "29 CFR 1926.600(a)(6); 1926.1408 Table A", "Keep equipment at least 10 ft from lines up to 50 kV (or have the utility de-energize and ground them); use a dedicated spotter.", "10 ft up to 50 kV", true);
            boom.SetPhotoRange(15f);   // photographed from the ground, looking up
            Boom(SliceKit.Before(boom), new[] { new Vector3(40f, 3.6f, 67.5f), new Vector3(40f, 7.4f, 70.5f), new Vector3(40f, 8.6f, 74.6f), new Vector3(40f, 7.2f, 76.2f) }, red);
            Boom(SliceKit.After(boom), new[] { new Vector3(40f, 3.6f, 67.5f), new Vector3(40f, 5.8f, 64.5f), new Vector3(40f, 6.1f, 60f), new Vector3(40f, 4.2f, 57.5f) }, red);
            SliceKit.Worker(SliceKit.Before(boom), new Vector3(37.6f, 0f, 67f), 60f, "Dale Pruitt", "Pump operator", CrewGestures.Activity.Crew);
            SliceKit.Worker(SliceKit.After(boom), new Vector3(37.6f, 0f, 67f), 60f, "Dale Pruitt", "Pump operator", CrewGestures.Activity.Crew);
            // Resolved: dedicated spotter and a 10 ft limit flagged on the ground under the line.
            SliceKit.Worker(SliceKit.After(boom), new Vector3(44f, 0f, 70f), 320f, null, null, CrewGestures.Activity.Idle);
            for (var x = 30f; x <= 50f; x += 2.5f) SliceKit.Model(SliceKit.After(boom), "TR-3D/SM_TrafficCone28in.fbx", new Vector3(x, 0f, LineZ - 3.05f), 0f);

            // Ready-mix truck backing to the pump hopper with a laborer walking behind it; resolved: a spotter.
            SliceKit.LowCollider(SliceKit.Model(root, "TR-3D/SM_MixerTruck.fbx", new Vector3(51f, 0f, 66f), 0f), 2.4f);   // rear toward the pump hopper
            var backing = SliceKit.Condition(root, "fri-backing-mixer", "Laborer behind a backing mixer, no spotter", true,
                new Vector3(46.2f, 1f, 66f), new Vector3(2.4f, 2f, 5f),
                EnergySource.Motion, CpArea.StruckBy, 4, 5, ControlLevel.Administrative, 400f, false,
                "29 CFR 1926.601(b)(4)", "A vehicle with an obstructed rear view backs only with a working backup alarm or an observer signaling it's safe.", "Alarm or observer", true);
            SliceKit.Worker(SliceKit.Before(backing), new Vector3(46.2f, 0f, 64f), 0f, null, null, CrewGestures.Activity.Walk, false,
                new Vector3(46.2f, 0f, 63.8f), new Vector3(46.2f, 0f, 68.4f));
            SliceKit.Worker(SliceKit.After(backing), new Vector3(46.5f, 0f, 70.8f), 200f, null, null, CrewGestures.Activity.Crew);

            // Rebar dowels sticking up at the pump-station slab edge where the crew steps down; resolved: impalement caps.
            var dowels = SliceKit.Condition(root, "fri-rebar-impalement", "Uncapped rebar at the slab edge", true,
                new Vector3(31.4f, 0.45f, 28.5f), new Vector3(1f, 0.9f, 3.4f),
                EnergySource.Mechanical, CpArea.General, 3, 4, ControlLevel.Engineering, 460f, false,
                "29 CFR 1926.701(b)", "Guard protruding rebar that workers could fall onto or into (impalement caps or troughs).", "Any protruding bar", true);
            for (var z = 27f; z <= 30f; z += 0.5f)
            {
                SliceKit.Box(SliceKit.Before(dowels), "Dowel", new Vector3(31.4f, 0.35f, z), new Vector3(0.025f, 0.7f, 0.025f), rebar);
                SliceKit.Box(SliceKit.After(dowels), "Dowel", new Vector3(31.4f, 0.35f, z), new Vector3(0.025f, 0.7f, 0.025f), rebar);
            }
            SliceKit.Box(SliceKit.After(dowels), "ImpalementTrough", new Vector3(31.4f, 0.72f, 28.5f), new Vector3(0.14f, 0.08f, 3.3f), orange);

            // Look-alikes: capped dowels on the other side, and the power-line warning sign with a ground marker line.
            var capped = SliceKit.Condition(root, "fri-rebar-capped", "Rebar with impalement caps", false,
                new Vector3(31.4f, 0.45f, 35f), new Vector3(1f, 0.9f, 2.4f),
                EnergySource.Mechanical, CpArea.General, 1, 1, ControlLevel.Engineering, float.PositiveInfinity, false,
                "29 CFR 1926.701(b)", "Rated caps or troughs guard the bars.", "Guarded", true);
            for (var z = 34f; z <= 36f; z += 0.5f)
            {
                SliceKit.Box(SliceKit.Before(capped), "Dowel", new Vector3(31.4f, 0.35f, z), new Vector3(0.025f, 0.7f, 0.025f), rebar);
                SliceKit.Box(SliceKit.Before(capped), "Cap", new Vector3(31.4f, 0.72f, z), new Vector3(0.1f, 0.06f, 0.1f), orange);
            }
            var sign = SliceKit.Condition(root, "fri-line-sign", "Overhead line warning sign", false,
                new Vector3(58f, 1.2f, LineZ - 3.5f), new Vector3(1.2f, 2.4f, 0.6f),
                EnergySource.Electrical, CpArea.Electrical, 1, 1, ControlLevel.Administrative, float.PositiveInfinity, false,
                "29 CFR 1926.1408(a)", "Mark the work zone and the 10 ft limit; warn operators of overhead lines.", "Posted", true);
            SliceKit.Box(SliceKit.Before(sign), "SignPost", new Vector3(58f, 1f, LineZ - 3.5f), new Vector3(0.08f, 2f, 0.08f), new Color(0.5f, 0.4f, 0.3f));
            SliceKit.Box(SliceKit.Before(sign), "Sign_DangerOverheadLines", new Vector3(58f, 2.1f, LineZ - 3.55f), new Vector3(0.9f, 0.6f, 0.03f), new Color(0.95f, 0.8f, 0.1f));
        }

        static void Boom(Transform parent, Vector3[] joints, Color red)
        {
            for (var i = 0; i < joints.Length - 1; i++)
                SliceKit.Line(parent, "BoomSection", joints[i], joints[i + 1], 0.42f - i * 0.07f, red);
            SliceKit.Line(parent, "EndHose", joints[joints.Length - 1], joints[joints.Length - 1] + Vector3.down * 2.5f, 0.13f, new Color(0.1f, 0.1f, 0.1f));
        }
    }
}
