using Jobsite.Core;
using Jobsite.Runtime;
using UnityEditor;
using UnityEngine;

namespace Jobsite.Editor
{
    // Shared builders for day slices (Thu/Fri): conditions with Unresolved/Resolved states, crew, props.
    public static class SliceKit
    {
        public const string Male = "Assets/ThirdParty/MicrosoftRocketbox/Construction_Male_01/Export/Construction_Male_01.fbx";
        public const string Female = "Assets/ThirdParty/MicrosoftRocketbox/Construction_Female_01/Export/Construction_Female_01.fbx";

        public static SiteCondition Condition(Transform root, string id, string title, bool hazard, Vector3 center, Vector3 size,
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

        public static Transform Before(SiteCondition c) => c.transform.Find("Unresolved");
        public static Transform After(SiteCondition c) => c.transform.Find("Resolved");

        public static GameObject Worker(Transform parent, Vector3 at, float yaw, string name = null, string trade = null,
            CrewGestures.Activity activity = CrewGestures.Activity.Idle, bool female = false, Vector3 walkA = default, Vector3 walkB = default)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(female ? Female : Male);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            for (var t = skin != null ? skin.transform : null; t != null; t = t.parent)
            {
                t.gameObject.SetActive(true);
                if (t == go.transform) break;
            }
            MondaySliceBuilderAccess.RelaxArms(go);
            if (name != null) go.AddComponent<NameTag>().Configure(name, trade, at.y + 2.12f > 2.2f ? 2.12f : 2.12f);
            go.AddComponent<CrewGestures>().Configure(activity, walkA, walkB);
            return go;
        }

        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Color color) =>
            MondaySliceBuilderAccess.Box(parent, name, center, size, color);

        public static GameObject Model(Transform parent, string rel, Vector3 at, float yaw) =>
            JobsiteGreyboxBuilder.ModelAt(parent, "Assets/_Game/Art/Models/" + rel, at, yaw);

        // Swap a model's colliders for one world-aligned body box capped at `maxHeight`, so tall parts (a folded boom)
        // don't block photos taken past them while the body still blocks walking.
        public static void LowCollider(GameObject go, float maxHeight)
        {
            if (go == null) return;
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return;
            var b = rs[0].bounds; foreach (var r in rs) b.Encapsulate(r.bounds);
            foreach (var c in go.GetComponentsInChildren<Collider>()) Object.DestroyImmediate(c);
            var box = new GameObject("BodyCollider"); box.transform.SetParent(go.transform, true);
            box.transform.SetPositionAndRotation(new Vector3(b.center.x, Mathf.Min(maxHeight, b.size.y) / 2, b.center.z), Quaternion.identity);
            box.AddComponent<BoxCollider>().size = new Vector3(b.size.x, Mathf.Min(maxHeight, b.size.y), b.size.z);
        }

        // Thin rope/cable between two points.
        public static GameObject Line(Transform parent, string name, Vector3 a, Vector3 b, float thickness, Color color)
        {
            var go = Box(parent, name, (a + b) / 2, new Vector3(thickness, thickness, Vector3.Distance(a, b)), color);
            go.transform.rotation = Quaternion.LookRotation(b - a);
            return go;
        }

        // Invisible wall the player cannot pass (roof openings, warning lines).
        public static void Blocker(Transform parent, Vector3 center, Vector3 size)
        {
            var go = new GameObject("PlayerBlocker"); go.transform.SetParent(parent, false); go.transform.position = center;
            go.AddComponent<BoxCollider>().size = size;
        }
    }
}
