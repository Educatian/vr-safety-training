using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Jobsite.Runtime
{
    // Environment art (quality review area 8: 66 % of renderers were engine primitives with razor edges).
    // Every flat-colour (untextured) Unity cube in the scene is given a chamfered box mesh with a real-world bevel
    // (~2.5 cm, less on thin parts), so edges catch the light like cut timber, steel and plastic do. Runs while the
    // scene is processed for the build and for play mode (Editor/PrimitivePolishHook, before static batching), so
    // batched scenery is covered too. Textured cubes keep Unity's face UVs and are left alone.
    public static class PrimitivePolish
    {
        public const float Bevel = 0.025f;
        public static readonly string[] BuiltIn = { "Cube", "Cylinder", "Sphere", "Capsule", "Plane", "Quad" };

        static readonly Dictionary<Vector3Int, Mesh> cache = new Dictionary<Vector3Int, Mesh>();
        public static int Polished { get; private set; }

        // Play-mode fallback for scenes loaded after start (idempotent: treated cubes are no longer named "Cube",
        // statically batched ones already point at the combined mesh).
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void Hook() { SceneManager.sceneLoaded -= OnLoaded; SceneManager.sceneLoaded += OnLoaded; }
        private static void OnLoaded(Scene s, LoadSceneMode m) => Apply(s);

        public static void Apply(Scene scene)
        {
            Polished = 0;
            if (!scene.IsValid() || !scene.isLoaded) return;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                    if (Polish(mf)) Polished++;
        }

        // One flat-colour cube -> chamfered box (also used for props built at runtime, e.g. PropDetail).
        public static bool Polish(MeshFilter mf)
        {
            var mesh = mf != null ? mf.sharedMesh : null;
            if (mesh == null || mesh.name != "Cube") return false;
            var r = mf.GetComponent<MeshRenderer>();
            if (r == null || HasTexture(r)) return false;
            var s = mf.transform.lossyScale;
            var size = new Vector3(Mathf.Abs(s.x), Mathf.Abs(s.y), Mathf.Abs(s.z));
            if (size.x < 1e-4f || size.y < 1e-4f || size.z < 1e-4f) return false;
            var b = Mathf.Min(Bevel, 0.18f * Mathf.Min(size.x, Mathf.Min(size.y, size.z)));
            if (b < 0.003f) return false;   // wires and thin lines: nothing to gain
            var obj = new Vector3(b / size.x, b / size.y, b / size.z);
            var key = new Vector3Int(Mathf.RoundToInt(obj.x * 2000), Mathf.RoundToInt(obj.y * 2000), Mathf.RoundToInt(obj.z * 2000));
            if (!cache.TryGetValue(key, out var m) || m == null) cache[key] = m = Chamfered(new Vector3(key.x, key.y, key.z) / 2000f, size);
            mf.sharedMesh = m;
            return true;
        }

        static bool HasTexture(Renderer r)
        {
            foreach (var mat in r.sharedMaterials)
            {
                if (mat == null) continue;
                if (mat.HasProperty("_BaseMap") && mat.GetTexture("_BaseMap") != null) return true;
                if (mat.HasProperty("_MainTex") && mat.GetTexture("_MainTex") != null) return true;
            }
            return false;
        }

        // Unit box (-0.5..0.5) with chamfered edges; `bevel` is per axis in object space, `size` the world scale used
        // to give the chamfers true 45-degree world normals under non-uniform scale.
        public static Mesh Chamfered(Vector3 bevel, Vector3 size)
        {
            var v = new List<Vector3>(); var n = new List<Vector3>(); var uv = new List<Vector2>(); var tri = new List<int>();
            var h = 0.5f; var i = new Vector3(h - bevel.x, h - bevel.y, h - bevel.z);

            void Poly(Vector3 worldNormal, params Vector3[] pts)
            {
                var on = Vector3.Scale(worldNormal, size).normalized;   // object-space normal that renders as worldNormal
                var start = v.Count;
                foreach (var p in pts) { v.Add(p); n.Add(on); uv.Add(new Vector2(p.x + p.z + h, p.y + h)); }
                for (var k = 1; k + 1 < pts.Length; k++)
                {
                    int a = start, b = start + k, c = start + k + 1;
                    // Unity front faces: cross(b-a, c-a) points out of the face (checked against the built-in Quad).
                    var cr = Vector3.Cross(Vector3.Scale(pts[k] - pts[0], size), Vector3.Scale(pts[k + 1] - pts[0], size));
                    if (Vector3.Dot(cr, worldNormal) > 0) tri.AddRange(new[] { a, b, c }); else tri.AddRange(new[] { a, c, b });
                }
            }

            // Six inset faces.
            for (var axis = 0; axis < 3; axis++)
                for (var sgn = -1; sgn <= 1; sgn += 2)
                {
                    var nrm = Vector3.zero; nrm[axis] = sgn;
                    int u = (axis + 1) % 3, w = (axis + 2) % 3;
                    var q = new Vector3[4];
                    var corners = new[] { (-1, -1), (1, -1), (1, 1), (-1, 1) };
                    for (var k = 0; k < 4; k++) { var p = Vector3.zero; p[axis] = sgn * h; p[u] = corners[k].Item1 * i[u]; p[w] = corners[k].Item2 * i[w]; q[k] = p; }
                    Poly(nrm, q);
                }
            // Twelve edge chamfers.
            for (var axis = 0; axis < 3; axis++)   // the edge runs along `axis`
            {
                int a1 = (axis + 1) % 3, a2 = (axis + 2) % 3;
                for (var s1 = -1; s1 <= 1; s1 += 2)
                    for (var s2 = -1; s2 <= 1; s2 += 2)
                    {
                        var nrm = Vector3.zero; nrm[a1] = s1; nrm[a2] = s2; nrm = nrm.normalized;
                        Vector3 P(float along, bool onA1)
                        {
                            var p = Vector3.zero; p[axis] = along * i[axis];
                            p[a1] = s1 * (onA1 ? h : i[a1]); p[a2] = s2 * (onA1 ? i[a2] : h);
                            return p;
                        }
                        Poly(nrm, P(-1, true), P(1, true), P(1, false), P(-1, false));
                    }
            }
            // Eight corner triangles.
            for (var sx = -1; sx <= 1; sx += 2)
                for (var sy = -1; sy <= 1; sy += 2)
                    for (var sz = -1; sz <= 1; sz += 2)
                        Poly(new Vector3(sx, sy, sz).normalized,
                            new Vector3(sx * h, sy * i.y, sz * i.z), new Vector3(sx * i.x, sy * h, sz * i.z), new Vector3(sx * i.x, sy * i.y, sz * h));

            var mesh = new Mesh { name = "ChamferBox" };
            mesh.SetVertices(v); mesh.SetNormals(n); mesh.SetUVs(0, uv); mesh.SetTriangles(tri, 0);
            mesh.RecalculateBounds(); mesh.RecalculateTangents();
            return mesh;
        }

        // The area-8 metric: renderers still drawing an untreated engine primitive, renderers on a chamfered box,
        // and all mesh renderers. Statically batched parts report their combined mesh and count as neither.
        public static (int primitives, int chamfered, int total) Census(Scene scene)
        {
            int p = 0, c = 0, t = 0;
            foreach (var root in scene.GetRootGameObjects())
                foreach (var mf in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (mf.GetComponent<MeshRenderer>() == null || mf.sharedMesh == null) continue;
                    t++;
                    if (System.Array.IndexOf(BuiltIn, mf.sharedMesh.name) >= 0) p++;
                    else if (mf.sharedMesh.name == "ChamferBox") c++;
                }
            return (p, c, t);
        }
    }
}
