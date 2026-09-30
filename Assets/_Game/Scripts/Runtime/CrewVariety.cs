using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jobsite.Runtime
{
    // Background crew variety (quality review area 7: 13 identical male clones). On every scene load, before any
    // Start runs, each unnamed Rocketbox worker gets a stable look from its position: about a third switch to the
    // female body, hard hats vary (white / yellow / orange / blue, as on real sites), skin tone varies (head and
    // hands together), and height varies a few percent. Named cast (Tripo models with faces) are never touched.
    public static class CrewVariety
    {
        public static readonly Color[] HardHats =
        {
            new Color(0.95f, 0.95f, 0.93f), new Color(1f, 0.82f, 0.12f), new Color(1f, 0.5f, 0.1f), new Color(0.2f, 0.42f, 0.85f),
        };
        // Multipliers on the (light) source skin; clothes on the same texture darken slightly with them.
        public static readonly Color[] SkinTones =
        {
            Color.white, new Color(0.86f, 0.74f, 0.62f), new Color(0.66f, 0.5f, 0.38f), new Color(0.5f, 0.36f, 0.27f), new Color(0.78f, 0.64f, 0.52f),
        };

        public static int Varied { get; private set; }
        public static int Females { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }

        private static void OnLoaded(Scene scene, LoadSceneMode mode) => Apply(scene);

        public static bool IsBackgroundRocketbox(CrewGestures g)
        {
            if (g == null || g.GetComponent<NameTag>() != null || g.GetComponent<CrewMember>() != null || g.GetComponent<NpcFace>() != null) return false;
            var smr = g.GetComponentInChildren<SkinnedMeshRenderer>(true);
            return smr != null && smr.sharedMesh != null && (smr.sharedMesh.name.Contains("m111") || smr.sharedMesh.name.Contains("f101"));
        }

        public static void Apply(Scene scene)
        {
            Varied = 0; Females = 0;
            var workers = new List<CrewGestures>();
            foreach (var root in scene.GetRootGameObjects())
                workers.AddRange(root.GetComponentsInChildren<CrewGestures>(true).Where(IsBackgroundRocketbox));
            // Stable order (by position) so the same worker always gets the same look.
            workers = workers.OrderBy(w => Mathf.Round(w.transform.position.x * 10)).ThenBy(w => Mathf.Round(w.transform.position.z * 10)).ToList();
            var female = CrewBodies.Female();
            for (var i = 0; i < workers.Count; i++)
            {
                var w = workers[i];
                var h = Hash(w.transform.position);
                if (female != null && i % 3 == 1 && SwapBody(w, female)) Females++;
                Dress(w, HardHats[(i + (int)(h % 2)) % HardHats.Length], SkinTones[(int)(h / 7 % (uint)SkinTones.Length)]);
                w.transform.localScale *= 0.96f + (h % 9) * 0.01f;
                Varied++;
            }
        }

        // Replace the visual hierarchy (mesh + Bip01 skeleton) under the worker root; components on the root stay.
        private static bool SwapBody(CrewGestures w, GameObject prefab)
        {
            var old = w.GetComponentInChildren<SkinnedMeshRenderer>(true);
            if (old == null || old.sharedMesh.name.Contains("f101")) return false;
            var inst = Object.Instantiate(prefab);
            if (inst.transform.Find("Bip01") == null) { Object.Destroy(inst); return false; }
            var remove = new List<Transform>();
            foreach (Transform c in w.transform)
                if (c.name == "Bip01" || c.GetComponentInChildren<SkinnedMeshRenderer>(true) != null) remove.Add(c);
            foreach (var c in remove) { c.SetParent(null, false); c.gameObject.SetActive(false); Object.Destroy(c.gameObject); }
            var kids = new List<Transform>();
            foreach (Transform c in inst.transform) kids.Add(c);
            foreach (var c in kids) c.SetParent(w.transform, false);
            Object.Destroy(inst);
            // Same visibility rule as the scene builder (SliceKit.Person): the first skinned mesh and its parents are shown.
            var skin = w.GetComponentInChildren<SkinnedMeshRenderer>(true);
            for (var t = skin != null ? skin.transform : null; t != null && t != w.transform; t = t.parent) t.gameObject.SetActive(true);
            RelaxArms(w.transform);
            w.Rebind();
            return true;
        }

        // Same bake the scene builder applies to Rocketbox workers, so a swapped body is never in a T-pose.
        private static void RelaxArms(Transform worker)
        {
            var fwd = worker.forward;
            foreach (var t in worker.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Bip01 L UpperArm") t.rotation = Quaternion.AngleAxis(72f, fwd) * t.rotation;
                else if (t.name == "Bip01 R UpperArm") t.rotation = Quaternion.AngleAxis(-72f, fwd) * t.rotation;
                else if (t.name == "Bip01 L Forearm" || t.name == "Bip01 R Forearm") t.rotation = Quaternion.AngleAxis(-15f, worker.right) * t.rotation;
            }
        }

        private static void Dress(CrewGestures w, Color hat, Color skin)
        {
            foreach (var r in w.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                var mats = r.materials;   // per-worker instances
                foreach (var m in mats)
                {
                    var n = m.name.ToLowerInvariant();
                    if (n.Contains("helmet")) Tint(m, hat);
                    else if (n.Contains("head") || n.Contains("body")) Tint(m, skin);
                }
                r.materials = mats;
            }
        }

        public static Color TintOf(Material m) => m.HasProperty("_BaseColor") ? m.GetColor("_BaseColor") : m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;

        private static void Tint(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        private static uint Hash(Vector3 p)
        {
            unchecked
            {
                var h = 2166136261u;
                foreach (var v in new[] { Mathf.RoundToInt(p.x * 10), Mathf.RoundToInt(p.z * 10) }) { h ^= (uint)v; h *= 16777619u; }
                return h;
            }
        }
    }
}
