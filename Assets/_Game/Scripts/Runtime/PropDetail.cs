using System.Collections.Generic;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Environment art (quality review area 8, docs/AwesomeAiGames_Plan.md phase 3): the props that sit in the launch
    // framings stop reading as boxes. Built at scene start from the builders' named primitives, parented next to them
    // (so they follow condition states), never touching colliders: the shipping container gets corrugation, rails,
    // corner posts, castings and door bars; the fuel box becomes a tank on saddles inside a containment tray; trench box
    // walls get stiffeners, a cap rail and lifting lugs, and the spreaders become pipes with collars.
    public static class PropDetail
    {
        public static int Built { get; private set; }
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static Mesh cylinder;

        public static void Apply(IEnumerable<MeshRenderer> renderers)
        {
            Built = 0;
            var list = new List<MeshRenderer>(renderers);
            foreach (var r in list)
            {
                if (r == null) continue;
                switch (r.name)
                {
                    case "conex": Container(r); break;
                    case "fuel": FuelTank(r); break;
                    case "TrenchBox_WallW": TrenchWall(r, +1f); break;
                    case "TrenchBox_WallE": TrenchWall(r, -1f); break;
                    case "Spreader": Spreader(r); break;
                }
            }
        }

        static Material Mat(string key, Color c, float metallic = 0f, float smooth = 0.3f)
        {
            if (mats.TryGetValue(key, out var m) && m != null) return m;
            m = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = "M_Detail_" + key };
            m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", metallic);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", smooth);
            return mats[key] = m;
        }

        // A chamfered box part, world-aligned to `frame`'s rotation, sized in metres.
        static Transform Part(Transform parent, string name, Vector3 world, Quaternion rot, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(world, rot);
            go.transform.localScale = Divide(size, parent != null ? parent.lossyScale : Vector3.one);
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            PrimitivePolish.Polish(go.GetComponent<MeshFilter>());
            Built++;
            return go.transform;
        }

        static Transform Pipe(Transform parent, string name, Vector3 world, Quaternion rot, float diameter, float length, Material m)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation(world, rot);
            go.transform.localScale = Divide(new Vector3(diameter, length / 2f, diameter), parent != null ? parent.lossyScale : Vector3.one);
            go.GetComponent<MeshFilter>().sharedMesh = Cylinder();
            go.GetComponent<MeshRenderer>().sharedMaterial = m;
            Built++;
            return go.transform;
        }

        static Mesh Cylinder()
        {
            if (cylinder != null) return cylinder;
            var tmp = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            cylinder = tmp.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(tmp);
            return cylinder;
        }

        static Vector3 Divide(Vector3 a, Vector3 b) => new Vector3(a.x / Nz(b.x), a.y / Nz(b.y), a.z / Nz(b.z));
        static float Nz(float v) => Mathf.Abs(v) < 1e-5f ? 1f : v;

        // 20-ft shipping container: long axis = the box's X.
        static void Container(MeshRenderer box)
        {
            var t = box.transform; var p = t.parent; var q = t.rotation;
            var s = t.lossyScale; float L = s.x, H = s.y, W = s.z;
            var body = box.sharedMaterial != null ? box.sharedMaterial : Mat("conex", new Color(0.2f, 0.32f, 0.45f), 0.3f, 0.35f);
            var rail = Mat("conex_rail", new Color(0.14f, 0.23f, 0.33f), 0.35f, 0.3f);
            var cast = Mat("casting", new Color(0.13f, 0.13f, 0.14f), 0.4f, 0.25f);
            var steel = Mat("steel_bar", new Color(0.58f, 0.6f, 0.61f), 0.85f, 0.5f);
            Vector3 P(float x, float y, float z) => t.position + q * new Vector3(x, y, z);
            // Corrugation on both long sides.
            for (var x = -L / 2 + 0.35f; x <= L / 2 - 0.3f; x += 0.3f)
                foreach (var sz in new[] { -1f, 1f })
                    Part(p, "Conex_Rib", P(x, 0f, sz * (W / 2 + 0.02f)), q, new Vector3(0.08f, H - 0.32f, 0.05f), body);
            // Top and bottom rails (long + short sides), corner posts and castings.
            foreach (var sy in new[] { -1f, 1f })
            {
                foreach (var sz in new[] { -1f, 1f }) Part(p, "Conex_Rail", P(0, sy * (H / 2 - 0.07f), sz * (W / 2 + 0.035f)), q, new Vector3(L + 0.02f, 0.14f, 0.08f), rail);
                foreach (var sx in new[] { -1f, 1f }) Part(p, "Conex_EndRail", P(sx * (L / 2 + 0.035f), sy * (H / 2 - 0.07f), 0), q, new Vector3(0.08f, 0.14f, W + 0.02f), rail);
            }
            foreach (var sx in new[] { -1f, 1f })
                foreach (var sz in new[] { -1f, 1f })
                {
                    Part(p, "Conex_Post", P(sx * (L / 2 - 0.07f), 0, sz * (W / 2 - 0.07f)), q, new Vector3(0.2f, H, 0.2f), rail);
                    foreach (var sy in new[] { -1f, 1f }) Part(p, "Conex_Casting", P(sx * (L / 2 - 0.08f), sy * (H / 2 - 0.06f), sz * (W / 2 - 0.08f)), q, new Vector3(0.22f, 0.13f, 0.22f), cast);
                }
            // Door end (+X): centre seam, four locking bars with handles.
            Part(p, "Conex_DoorSeam", P(L / 2 + 0.03f, 0, 0), q, new Vector3(0.03f, H - 0.3f, 0.04f), cast);
            foreach (var fz in new[] { -0.36f, -0.14f, 0.14f, 0.36f })
            {
                Pipe(p, "Conex_LockBar", P(L / 2 + 0.06f, 0, fz * W), q, 0.045f, H - 0.36f, steel);
                Part(p, "Conex_Handle", P(L / 2 + 0.09f, -0.22f * H, fz * W + 0.08f * Mathf.Sign(fz)), q, new Vector3(0.04f, 0.05f, 0.18f), steel);
            }
        }

        // Diesel tank on two saddles in a bunded tray (the old box keeps its collider but is no longer drawn).
        static void FuelTank(MeshRenderer box)
        {
            if (box.GetComponentInParent<SiteCondition>() != null) return;   // a scored condition keeps its own look
            var t = box.transform; var p = t.parent; var q = t.rotation;
            var s = t.lossyScale; float L = s.x, H = s.y, W = s.z;
            var ground = t.position.y - H / 2f;
            var tray = Mat("tray", new Color(0.24f, 0.25f, 0.26f), 0.4f, 0.3f);
            var red = Mat("tank_red", new Color(0.62f, 0.09f, 0.06f), 0.2f, 0.55f);
            var dark = Mat("casting", new Color(0.13f, 0.13f, 0.14f), 0.4f, 0.25f);
            Vector3 P(float x, float y, float z) => new Vector3(t.position.x, ground, t.position.z) + q * new Vector3(x, y, z);
            box.enabled = false;
            Part(p, "Fuel_TrayBase", P(0, 0.04f, 0), q, new Vector3(L + 0.2f, 0.08f, W + 0.2f), tray);
            foreach (var sz in new[] { -1f, 1f }) Part(p, "Fuel_TrayWall", P(0, 0.19f, sz * (W / 2 + 0.07f)), q, new Vector3(L + 0.2f, 0.3f, 0.06f), tray);
            foreach (var sx in new[] { -1f, 1f }) Part(p, "Fuel_TrayWall", P(sx * (L / 2 + 0.07f), 0.19f, 0), q, new Vector3(0.06f, 0.3f, W + 0.08f), tray);
            var r = Mathf.Min(0.72f, W * 0.36f);
            var saddleH = 0.32f;
            foreach (var sx in new[] { -0.3f, 0.3f }) Part(p, "Fuel_Saddle", P(sx * L, 0.08f + saddleH / 2f, 0), q, new Vector3(0.16f, saddleH, r * 1.5f), dark);
            var cy = 0.08f + saddleH + r * 0.82f;
            var len = L * 0.86f;
            Pipe(p, "Fuel_Tank", P(0, cy, 0), q * Quaternion.Euler(0, 0, 90), 2f * r, len, red);
            foreach (var sx in new[] { -1f, 1f })
            {
                var head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                head.name = "Fuel_TankHead"; Object.Destroy(head.GetComponent<Collider>());
                head.transform.SetParent(p, false);
                head.transform.SetPositionAndRotation(P(sx * len / 2f, cy, 0), q);
                head.transform.localScale = Divide(new Vector3(0.55f * r, 2f * r, 2f * r), p != null ? p.lossyScale : Vector3.one);
                head.GetComponent<MeshRenderer>().sharedMaterial = red;
                Built++;
            }
            // Pump, manway and hose on top.
            Part(p, "Fuel_Pump", P(L * 0.22f, cy + r + 0.17f, 0), q, new Vector3(0.3f, 0.34f, 0.26f), dark);
            Pipe(p, "Fuel_Manway", P(-L * 0.2f, cy + r + 0.03f, 0), q, 0.36f, 0.08f, dark);
            Pipe(p, "Fuel_Hose", P(L * 0.22f + 0.18f, cy + r * 0.4f, r * 0.5f), q * Quaternion.Euler(18, 0, 0), 0.05f, r * 1.6f, dark);
        }

        // Aluminium trench box wall: inward-facing stiffeners, cap rail, lifting lugs. `inward` = +1 if the trench is at +X.
        static void TrenchWall(MeshRenderer wall, float inward)
        {
            var t = wall.transform; var p = t.parent; var q = t.rotation;
            var s = t.lossyScale; float T = s.x, H = s.y, D = s.z;
            var alu = Mat("alu", new Color(0.56f, 0.59f, 0.62f), 0.75f, 0.45f);
            var dark = Mat("alu_dark", new Color(0.4f, 0.42f, 0.45f), 0.75f, 0.4f);
            Vector3 P(float x, float y, float z) => t.position + q * new Vector3(x, y, z);
            foreach (var z in new[] { -0.36f, 0f, 0.36f })
                Part(p, "TrenchBox_Stiffener", P(inward * (T / 2 + 0.035f), -0.04f * H, z * D), q, new Vector3(0.07f, H * 0.88f, 0.09f), alu);
            Part(p, "TrenchBox_Cap", P(0, H / 2 + 0.03f, 0), q, new Vector3(T + 0.08f, 0.06f, D), dark);
            Part(p, "TrenchBox_Knife", P(0, -H / 2 + 0.05f, 0), q, new Vector3(T + 0.04f, 0.1f, D), dark);
            foreach (var z in new[] { -0.33f, 0.33f })
                Part(p, "TrenchBox_Lug", P(0, H / 2 + 0.12f, z * D), q, new Vector3(0.05f, 0.14f, 0.14f), dark);
        }

        // Spreader box -> steel pipe with collars at both walls (keeps its blue paint).
        static void Spreader(MeshRenderer r)
        {
            var t = r.transform; var s = t.lossyScale;
            var len = Mathf.Max(s.x, Mathf.Max(s.y, s.z));
            var axis = s.x >= s.y && s.x >= s.z ? Vector3.right : s.z >= s.y ? Vector3.forward : Vector3.up;
            var rot = Quaternion.FromToRotation(Vector3.up, t.rotation * axis);
            var mat = r.sharedMaterial;
            var dark = Mat("collar", new Color(0.15f, 0.2f, 0.36f), 0.6f, 0.4f);
            r.enabled = false;
            Pipe(t.parent, "Spreader_Pipe", t.position, rot, 0.11f, len, mat);
            foreach (var sd in new[] { -1f, 1f })
                Pipe(t.parent, "Spreader_Collar", t.position + rot * Vector3.up * sd * (len / 2f - 0.05f), rot, 0.17f, 0.1f, dark);
        }
    }
}
