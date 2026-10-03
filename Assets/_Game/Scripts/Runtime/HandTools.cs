using System.Collections.Generic;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Hand tools for working crew (quality review 2026-09-30, P0: Marcus dug and Luis sawed with empty hands).
    // Built as small dedicated meshes (octagonal prisms + a disc), not engine primitives. Local frame: +Z runs from the
    // grip toward the working end, +Y is "up" for the tool, origin = the upper/rear grip.
    public static class HandTools
    {
        public enum Kind { Shovel, CutoffSaw }

        static Material wood, steel, orange, rubber;

        public static Transform Build(Kind kind, Transform parent)
        {
            Init();
            var root = new GameObject(kind == Kind.Shovel ? "HandTool_Shovel" : "HandTool_CutoffSaw").transform;
            root.SetParent(parent, false);
            if (kind == Kind.Shovel)
            {
                Part(root, "Handle", Prism(0.017f, 0f, 1.10f, 8), wood);
                Part(root, "Grip", Prism(0.022f, -0.12f, 0.02f, 8), rubber);       // D-grip stub above the upper hand
                Part(root, "Blade", Blade(0.23f, 0.29f, 0.012f, 1.08f), steel);
            }
            else
            {
                Part(root, "RearHandle", Prism(0.02f, -0.06f, 0.08f, 8), rubber);
                Part(root, "Housing", Box(new Vector3(0.13f, 0.16f, 0.30f), new Vector3(0f, 0.02f, 0.18f)), orange);
                Part(root, "FrontBar", PrismX(0.014f, 0.22f, new Vector3(0f, 0.12f, 0.24f), 8), rubber);
                Part(root, "Arm", Box(new Vector3(0.04f, 0.05f, 0.30f), new Vector3(0.07f, 0f, 0.45f)), steel);
                Part(root, "Disc", Disc(0.15f, 0.004f, new Vector3(0.1f, 0f, 0.58f), 24), steel);
            }
            return root;
        }

        static void Init()
        {
            if (wood != null) return;
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            Material M(Color c, float smooth, float metal) { var m = new Material(sh) { color = c }; m.SetFloat("_Smoothness", smooth); m.SetFloat("_Metallic", metal); return m; }
            wood = M(new Color(0.55f, 0.38f, 0.2f), 0.25f, 0f); steel = M(new Color(0.46f, 0.48f, 0.5f), 0.55f, 0.8f);
            orange = M(new Color(0.93f, 0.43f, 0.06f), 0.4f, 0f); rubber = M(new Color(0.08f, 0.08f, 0.08f), 0.2f, 0f);
        }

        static void Part(Transform root, string name, Mesh mesh, Material mat)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>(); r.sharedMaterial = mat; r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
        }

        // n-gon prism along +Z from z0 to z1.
        static Mesh Prism(float radius, float z0, float z1, int n) => Extrude(radius, n, i => new Vector3(0, 0, i == 0 ? z0 : z1), Vector3.forward, Vector3.up);
        // n-gon prism along +X, centred at c.
        static Mesh PrismX(float radius, float length, Vector3 c, int n) =>
            Extrude(radius, n, i => c + new Vector3(i == 0 ? -length / 2 : length / 2, 0, 0), Vector3.right, Vector3.up);

        static Mesh Extrude(float radius, int n, System.Func<int, Vector3> end, Vector3 axis, Vector3 up)
        {
            var side = Vector3.Cross(axis, up).normalized;
            var v = new List<Vector3>(); var t = new List<int>();
            for (var e = 0; e < 2; e++)
                for (var i = 0; i < n; i++)
                {
                    var a = i * Mathf.PI * 2 / n;
                    v.Add(end(e) + (up * Mathf.Cos(a) + side * Mathf.Sin(a)) * radius);
                }
            for (var i = 0; i < n; i++)
            {
                int a = i, b = (i + 1) % n, c = n + i, d = n + (i + 1) % n;
                t.AddRange(new[] { a, c, b, b, c, d });
            }
            for (var i = 1; i < n - 1; i++) { t.AddRange(new[] { 0, i + 1, i }); t.AddRange(new[] { n, n + i, n + i + 1 }); }
            return Finish(v, t, "Prism");
        }

        static Mesh Box(Vector3 size, Vector3 c)
        {
            var h = size * 0.5f; var v = new List<Vector3>(); var t = new List<int>();
            Vector3[] n = { Vector3.right, Vector3.left, Vector3.up, Vector3.down, Vector3.forward, Vector3.back };
            foreach (var f in n)
            {
                var u = Mathf.Abs(f.y) > 0.5f ? Vector3.forward : Vector3.up; var r = Vector3.Cross(f, u);
                var o = v.Count;
                v.Add(c + Vector3.Scale(f + u + r, h)); v.Add(c + Vector3.Scale(f + u - r, h)); v.Add(c + Vector3.Scale(f - u - r, h)); v.Add(c + Vector3.Scale(f - u + r, h));
                t.AddRange(new[] { o, o + 1, o + 2, o, o + 2, o + 3 });
            }
            return Finish(v, t, "Box");
        }

        // Slightly dished spade blade, hanging from the handle end at z0, width x length, thin in Y.
        static Mesh Blade(float width, float length, float thick, float z0)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            const int cols = 6, rows = 5;
            for (var s = 0; s < 2; s++)
                for (var r = 0; r <= rows; r++)
                    for (var c = 0; c <= cols; c++)
                    {
                        float u = c / (float)cols - 0.5f, w = r / (float)rows;
                        var taper = Mathf.Lerp(0.8f, 1f, Mathf.Sin(w * Mathf.PI * 0.6f));
                        var dish = (0.25f - u * u) * 0.08f;
                        v.Add(new Vector3(u * width * taper, dish + (s == 0 ? thick / 2 : -thick / 2), z0 + w * length));
                    }
            var stride = cols + 1; var layer = (rows + 1) * stride;
            for (var r = 0; r < rows; r++)
                for (var c = 0; c < cols; c++)
                {
                    int a = r * stride + c, b = a + 1, d = a + stride, e = d + 1;
                    t.AddRange(new[] { a, d, b, b, d, e });
                    t.AddRange(new[] { layer + a, layer + b, layer + d, layer + b, layer + e, layer + d });
                }
            return Finish(v, t, "Blade");
        }

        static Mesh Disc(float radius, float thick, Vector3 c, int n)
        {
            var v = new List<Vector3>(); var t = new List<int>();
            for (var s = 0; s < 2; s++)
            {
                var x = c.x + (s == 0 ? thick / 2 : -thick / 2);
                v.Add(new Vector3(x, c.y, c.z));
                for (var i = 0; i < n; i++) { var a = i * Mathf.PI * 2 / n; v.Add(new Vector3(x, c.y + Mathf.Cos(a) * radius, c.z + Mathf.Sin(a) * radius)); }
            }
            var o = n + 1;
            for (var i = 0; i < n; i++)
            {
                int a = 1 + i, b = 1 + (i + 1) % n;
                t.AddRange(new[] { 0, a, b }); t.AddRange(new[] { o, o + b, o + a });
                t.AddRange(new[] { a, o + a, b, b, o + a, o + b });
            }
            return Finish(v, t, "Disc");
        }

        static Mesh Finish(List<Vector3> v, List<int> t, string name)
        {
            var m = new Mesh { name = "HandTool_" + name };
            m.SetVertices(v); m.SetTriangles(t, 0); m.RecalculateNormals(); m.RecalculateBounds();
            return m;
        }
    }
}
