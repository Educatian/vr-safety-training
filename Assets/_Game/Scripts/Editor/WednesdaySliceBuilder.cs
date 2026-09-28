using Jobsite.Core;
using Jobsite.Runtime;
using UnityEditor;
using UnityEngine;

namespace Jobsite.Editor
{
    // Wednesday: pump-station deck at 4.2 m (falls). Deck x 34-52, z 26-38; holes cut by JobsiteGreyboxBuilder.BuildDeck
    // at x 41-42.2 / z 30-31.2 (left open) and x 48-49.2 / z 34-35.2 (properly covered look-alike).
    public static class WednesdaySliceBuilder
    {
        const float Deck = 4.2f + 0.075f;
        const string Rocketbox = "Assets/ThirdParty/MicrosoftRocketbox/Construction_Male_01/Export/Construction_Male_01.fbx";

        public static void Add(Transform gameplay)
        {
            var root = new GameObject("Conditions_Wed").transform;
            root.SetParent(gameplay);
            root.gameObject.AddComponent<PhaseMember>().Configure(WorkDay.Wed);

            // Missing midrail module on the north edge (module k=6 placed by BuildDeck at x 47).
            var rail = Condition(root, "wed-missing-midrail", "Guardrail without midrail", true,
                new Vector3(47f, Deck + 0.55f, 37.9f), new Vector3(2.1f, 1.2f, 0.6f),
                EnergySource.Gravity, CpArea.FallProtection, 3, 5, ControlLevel.Engineering, 480f, false,
                "29 CFR 1926.502(b)(2)(i)", "Midrails sit midway between the top rail and the deck.", "21 in (mid)", true);
            var full = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/B-PROC/SM_Guardrail_service_worn.fbx");
            if (full != null)
            {
                var g = (GameObject)PrefabUtility.InstantiatePrefab(full, rail.transform.Find("Resolved"));
                g.transform.position = new Vector3(47f, Deck, 37.9f);
                g.transform.rotation = full.transform.rotation;
            }

            // Uncovered floor opening with a worker carrying material toward it.
            var hole = Condition(root, "wed-open-hole", "Uncovered floor opening", true,
                new Vector3(41.6f, Deck + 0.4f, 30.6f), new Vector3(1.6f, 1f, 1.6f),
                EnergySource.Gravity, CpArea.FallProtection, 4, 5, ControlLevel.Engineering, 420f, true,
                "29 CFR 1926.501(b)(4)(i)", "Cover or guard holes; covers secured and marked HOLE.", "Over 6 ft drop", true);
            Worker(hole.transform.Find("Unresolved"), new Vector3(43.2f, Deck, 30.6f), -90);
            Cover(hole.transform.Find("Resolved"), new Vector3(41.6f, Deck + 0.02f, 30.6f));

            // Ladder from grade to the deck that stops at the deck edge.
            var ladder = Condition(root, "wed-short-ladder", "Ladder not extended above deck", true,
                new Vector3(33.6f, 2.3f, 32f), new Vector3(1f, 4.8f, 1.2f),
                EnergySource.Gravity, CpArea.FallProtection, 3, 4, ControlLevel.Engineering, 540f, false,
                "29 CFR 1926.1053(b)(1)", "Rails extend 3 ft above the landing, or use a grab device.", "3 ft (0.9 m)", true);
            Ladder(ladder.transform.Find("Unresolved"), 4.2f);
            Ladder(ladder.transform.Find("Resolved"), 4.2f + 0.91f);

            // Look-alikes: a secured, marked cover and a worker tied off at the edge.
            var covered = Condition(root, "wed-covered-hole", "Marked hole cover", false,
                new Vector3(48.6f, Deck + 0.2f, 34.6f), new Vector3(1.6f, 0.6f, 1.6f),
                EnergySource.Gravity, CpArea.FallProtection, 1, 1, ControlLevel.Engineering, float.PositiveInfinity, false,
                "29 CFR 1926.502(i)(3)-(4)", "Covers secured against displacement and marked HOLE or COVER.", "2x load", false);
            Cover(covered.transform.Find("Unresolved"), new Vector3(48.6f, Deck + 0.02f, 34.6f));
            var tied = Condition(root, "wed-tied-off", "Worker tied off at the edge", false,
                new Vector3(51.4f, Deck + 1f, 30f), new Vector3(1f, 2f, 1f),
                EnergySource.Gravity, CpArea.FallProtection, 1, 1, ControlLevel.Ppe, float.PositiveInfinity, false,
                "29 CFR 1926.502(d)", "Personal fall arrest: harness, lanyard, anchor.", "Anchor 5,000 lb", false);
            Worker(tied.transform.Find("Unresolved"), new Vector3(51.4f, Deck, 30f), 90);
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

        static void Cover(Transform parent, Vector3 at)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.name = "PlywoodCover_HOLE";
            go.transform.SetParent(parent, true);
            go.transform.position = at;
            go.transform.localScale = new Vector3(1.5f, 0.02f, 1.5f); // overlaps the 1.2 m opening on all sides
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Textures/HF/T_PlywoodHoleCover_BaseMap.png"));
            go.GetComponent<Renderer>().sharedMaterial = m;
            foreach (var dx in new[] { -0.65f, 0.65f }) // cleats underneath stop it sliding
                MondaySliceBuilderAccess.Box(go.transform, "Cleat", at + new Vector3(dx, -0.05f, 0), new Vector3(0.04f, 0.08f, 1.1f), new Color(0.6f, 0.45f, 0.3f));
        }

        static void Ladder(Transform parent, float length)
        {
            var yellow = new Color(0.95f, 0.75f, 0.1f);
            var lean = Quaternion.Euler(0, 0, -14f); // about 4:1
            var basePt = new Vector3(33.3f, 0f, 32f);
            var holder = new GameObject("ExtensionLadder").transform;
            holder.SetParent(parent, true);
            holder.SetPositionAndRotation(basePt, lean);
            // Parts are laid out in the holder's local frame so the 4:1 lean applies to the whole ladder.
            void Part(string n, Vector3 local, Vector3 size)
            {
                var b = MondaySliceBuilderAccess.Box(holder, n, Vector3.zero, size, yellow).transform;
                b.localPosition = local; b.localRotation = Quaternion.identity;
            }
            foreach (var dz in new[] { -0.22f, 0.22f }) Part("Rail", new Vector3(0, length / 2, dz), new Vector3(0.06f, length, 0.05f));
            for (var y = 0.3f; y < length; y += 0.3f) Part("Rung", new Vector3(0, y, 0), new Vector3(0.03f, 0.03f, 0.44f));
        }

        static void Worker(Transform parent, Vector3 at, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Rocketbox);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            for (var current = skin != null ? skin.transform : null; current != null; current = current.parent)
            {
                current.gameObject.SetActive(true);
                if (current == go.transform) break;
            }
            MondaySliceBuilderAccess.RelaxArms(go);
            go.AddComponent<SafetyTraining.Runtime.NpcRelaxedPose>();
        }
    }
}
