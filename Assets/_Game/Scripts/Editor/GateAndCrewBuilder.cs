using Jobsite.Runtime;
using SafetyTraining.Runtime;
using UnityEditor;
using UnityEngine;

namespace Jobsite.Editor
{
    // Gate check-in (sign-in board + PPE rack) and talkable named crew (GDD §14), wired to OpenRouter.
    public static class GateAndCrewBuilder
    {
        const string ConfigPath = "Assets/_Game/Settings/LLM_OpenRouter.asset";
        const string Male = "Assets/ThirdParty/MicrosoftRocketbox/Construction_Male_01/Export/Construction_Male_01.fbx";
        const string Female = "Assets/ThirdParty/MicrosoftRocketbox/Construction_Female_01/Export/Construction_Female_01.fbx";

        public static void Add(Transform gameplay)
        {
            var gate = new GameObject("GateCheckIn").transform;
            gate.SetParent(gameplay);

            // Sign-in board on a post just inside the gate.
            Station(gate, CheckInStation.Kind.SignIn, "SignInBoard", new Vector3(14.6f, 1.2f, 1.6f), new Vector3(0.6f, 0.8f, 0.05f), new Color(0.95f, 0.95f, 0.92f));
            MondaySliceBuilderAccess.Box(gate, "SignInPost", new Vector3(14.6f, 0.45f, 1.62f), new Vector3(0.08f, 0.9f, 0.08f), new Color(0.5f, 0.4f, 0.3f));

            // PPE rack: a table with a hard hat, vest, glasses and gloves to take one by one.
            MondaySliceBuilderAccess.Box(gate, "PpeTable", new Vector3(9.6f, 0.75f, 2.4f), new Vector3(2.2f, 0.06f, 0.7f), new Color(0.35f, 0.35f, 0.36f));
            Station(gate, CheckInStation.Kind.HardHat, "HardHat", new Vector3(8.8f, 0.88f, 2.4f), new Vector3(0.3f, 0.16f, 0.26f), new Color(1f, 0.85f, 0.1f));
            Station(gate, CheckInStation.Kind.Vest, "HiVisVest", new Vector3(9.35f, 0.8f, 2.4f), new Vector3(0.45f, 0.04f, 0.4f), new Color(1f, 0.45f, 0.05f));
            Station(gate, CheckInStation.Kind.Glasses, "SafetyGlasses", new Vector3(9.9f, 0.8f, 2.4f), new Vector3(0.16f, 0.05f, 0.06f), new Color(0.15f, 0.15f, 0.18f));
            Station(gate, CheckInStation.Kind.Gloves, "Gloves", new Vector3(10.4f, 0.8f, 2.4f), new Vector3(0.22f, 0.05f, 0.14f), new Color(0.75f, 0.6f, 0.35f));

            var config = EnsureConfig();
            Crew(gameplay, Female, new Vector3(12.8f, 0f, 3.6f), 200f, "Dolores",
                "veteran site safety manager and the learner's mentor; calm, practical, bilingual EN/ES",
                "Construction fall protection starts at 6 ft (29 CFR 1926.501(b)(1)). Hierarchy of controls: eliminate, substitute, engineer, administrate, PPE last. " +
                "GFCI or assured grounding on temporary power (1926.404(b)(1)). Trenches 5 ft or deeper need a protective system (1926.652(a)(1)); " +
                "spoil at least 2 ft back (1926.651(j)(2)); ladder within 25 ft (1926.651(c)(2)). Anyone may stop work for a hazard.", config);
            Crew(gameplay, Male, new Vector3(21.5f, 0f, 20.5f), 260f, "Ray",
                "general foreman under schedule pressure; blunt, wants the pour on time, but backs down when a hazard is named clearly",
                "The schedule is tight this week. Stop-work is allowed for real hazards; name the hazard and the fix. " +
                "Materials and kits are at the supply rack by the trailers.", config);
        }

        static void Station(Transform parent, CheckInStation.Kind kind, string name, Vector3 at, Vector3 size, Color color)
        {
            var go = MondaySliceBuilderAccess.Box(parent, name, at, size, color);
            var col = go.AddComponent<BoxCollider>();
            col.size = Vector3.one + new Vector3(0.4f / Mathf.Max(size.x, 0.01f), 0.6f / Mathf.Max(size.y, 0.01f), 0.4f / Mathf.Max(size.z, 0.01f)); // generous E target
            go.AddComponent<CheckInStation>().Configure(kind, go);
        }

        static void Crew(Transform parent, string model, Vector3 at, float yaw, string name, string role, string facts, LlmEndpointConfig config)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(model);
            if (prefab == null) return;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.name = "Crew_" + name;
            go.transform.SetPositionAndRotation(at, Quaternion.Euler(0, yaw, 0));
            var skin = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
            for (var c = skin != null ? skin.transform : null; c != null; c = c.parent) { c.gameObject.SetActive(true); if (c == go.transform) break; }
            MondaySliceBuilderAccess.RelaxArms(go);
            go.AddComponent<NpcRelaxedPose>();                 // idle weight shift + explain/point gestures
            var capsule = go.AddComponent<CapsuleCollider>();  // E ray target
            capsule.center = new Vector3(0, 0.9f, 0); capsule.height = 1.8f; capsule.radius = 0.35f;
            go.AddComponent<CrewMember>().Configure(name, role, facts, config);
        }

        static LlmEndpointConfig EnsureConfig()
        {
            var config = AssetDatabase.LoadAssetAtPath<LlmEndpointConfig>(ConfigPath);
            if (config == null) { config = ScriptableObject.CreateInstance<LlmEndpointConfig>(); AssetDatabase.CreateAsset(config, ConfigPath); }
            // Editor/desktop read the key from the gitignored project .env; web builds must use the proxy (Tools/proxy).
            config.ConfigureOpenRouter("anthropic/claude-haiku-4.5", "OPENROUTER_API_KEY", ".env", config.WebProxyEndpoint);
            EditorUtility.SetDirty(config);
            AssetDatabase.SaveAssets();
            return config;
        }
    }
}
