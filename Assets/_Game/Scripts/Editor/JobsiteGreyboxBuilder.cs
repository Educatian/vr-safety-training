using System.Collections.Generic;
using System.IO;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Jobsite.Runtime;

namespace Jobsite.Editor
{
    // T1.3: builds Assets/_Game/Scenes/Jobsite.unity from Tools/layout/site_layout.json
    // (the single source of site coordinates; JSON y -> Unity z). Re-runnable: overwrites the scene.
    // Batch: Unity.exe -batchmode -quit -executeMethod Jobsite.Editor.JobsiteGreyboxBuilder.Build
    public static class JobsiteGreyboxBuilder
    {
        const string ScenePath = "Assets/_Game/Scenes/Jobsite.unity";
        const string MatDir = "Assets/_Game/Art/Materials";
        const string TexDir = "Assets/_Game/Art/Textures/PolyHaven";
        const float TrenchDepth = 1.83f; // 6 ft (GDD day Tue)
        const float DeckHeight = 4.2f;

        static readonly Dictionary<string, Material> Mats = new Dictionary<string, Material>();
        static readonly Dictionary<Material, float> TileMetres = new Dictionary<Material, float>();

        [MenuItem("Jobsite/Build Greybox Scene")]
        public static void Build()
        {
            var layout = JObject.Parse(File.ReadAllText("Tools/layout/site_layout.json"));
            Directory.CreateDirectory(MatDir);
            Directory.CreateDirectory(Path.GetDirectoryName(ScenePath));
            Mats.Clear();
            TileMetres.Clear();

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            var root = new GameObject("Site").transform;
            var ground = Group(root, "Ground");
            var surfaces = Group(root, "Surfaces");
            var logistics = Group(root, "Logistics");
            var civil = Group(root, "Civil_Trench");
            var building = Group(root, "Phase_Wed_Building");
            var utilities = Group(root, "Utilities");

            BuildGround(ground, layout);
            foreach (var r in layout["rects"])
                BuildRect(r, surfaces, logistics, civil, building);
            BuildHaulRoad(surfaces, layout["haul_road"]);
            BuildFence(root, layout);
            BuildPowerLine(utilities, layout["power_line"]);
            BuildDeck(building);
            BuildLighting();
            BuildSurroundings(root);
            BuildDayVariants(root, civil, layout);
            new GameObject("SitePhaseController").AddComponent<SitePhaseController>();
            MondaySliceBuilder.Add(root);

            var spawn = new GameObject("PlayerSpawn").transform;
            spawn.position = new Vector3((float)layout["spawn"]["x"], 0f, (float)layout["spawn"]["y"]);

            EditorSceneManager.SaveScene(scene, ScenePath);
            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            Debug.Log("[Greybox] Built " + ScenePath);
        }

        // ---------- ground and trench cut ----------
        static void BuildGround(Transform parent, JObject layout)
        {
            var t = layout["rects"].First(r => (string)r["id"] == "trench");
            float tx = (float)t["x"], tw = (float)t["w"], ty = (float)t["y"], th = (float)t["h"];
            float E = (float)layout["parcel"][0] + 10f, N = (float)layout["parcel"][1] + 4f; // site block extents
            var dirt = Mat("M_RedClayGraded", "red_dirt_mud_01", 5f); // Alabama red clay
            // Top surface at y=0, 2 m thick; the trench is a real cut 1.83 m deep.
            Slab(parent, "Ground_West", -10, -12, tx + 10, N + 12, dirt);
            Slab(parent, "Ground_East", tx + tw, -12, E - (tx + tw), N + 12, dirt);
            Slab(parent, "Ground_TrenchSouth", tx, -12, tw, ty + 12, dirt);
            Slab(parent, "Ground_TrenchNorth", tx, ty + th, tw, N - (ty + th), dirt);
            // Surroundings to the horizon: dry late-summer field around the parcel block.
            var field = Mat("M_SouthernLawn", "sparse_grass", 3f); // humid Deep South roadside grass
            Slab(parent, "Surround_South", -400, -400, 900, 388, field);
            Slab(parent, "Surround_North", -400, N, 900, 500 - N, field);
            Slab(parent, "Surround_West", -400, -12, 390, N + 12, field);
            Slab(parent, "Surround_East", E, -12, 500 - E, N + 12, field);
            var floor = Box(parent, "Trench_Floor_Bedding", new Vector3(tx + tw / 2, -TrenchDepth - 0.5f, ty + th / 2),
                new Vector3(tw, 1f, th), Mat("M_BeddingStone", "gravel_road", 2f));
            floor.name = "Trench_Floor_Bedding";
            var soil = Mat("M_RedClayCut", "red_laterite_soil_stones", 2f);
            Box(parent, "Trench_Wall_W", new Vector3(tx + 0.01f, -TrenchDepth / 2, ty + th / 2), new Vector3(0.02f, TrenchDepth, th), soil);
            Box(parent, "Trench_Wall_E", new Vector3(tx + tw - 0.01f, -TrenchDepth / 2, ty + th / 2), new Vector3(0.02f, TrenchDepth, th), soil);
        }

        static void Slab(Transform parent, string name, float x, float z, float w, float d, Material m)
        {
            if (w <= 0 || d <= 0) return;
            Box(parent, name, new Vector3(x + w / 2, -1f, z + d / 2), new Vector3(w, 2f, d), m);
        }

        // ---------- layout rectangles ----------
        static void BuildRect(JToken r, Transform surfaces, Transform logistics, Transform civil, Transform building)
        {
            string id = (string)r["id"], kind = (string)r["kind"];
            float x = (float)r["x"], z = (float)r["y"], w = (float)r["w"], d = (float)r["h"];
            var c = new Vector3(x + w / 2, 0, z + d / 2);
            switch (kind)
            {
                case "asphalt": Flat(surfaces, id, c, w, d, Mat("M_Asphalt", "asphalt_03", 8f, "Assets/ThirdParty/PolyHaven/Environment/Textures")); break;
                case "concrete": Flat(surfaces, id, c, w, d, Mat("M_ConcreteSidewalk", "concrete_panels", 3f, "Assets/ThirdParty/PolyHaven/Environment/Textures")); break;
                case "stone": case "gravel": Flat(surfaces, id, c, w, d, Mat("M_CrushedStone", "gravel_road", 4f)); break;
                case "laydown": Flat(surfaces, id, c, w, d, Mat("M_CrushedStone", "gravel_road", 4f)); break;
                case "mats": Flat(surfaces, id, c, w, d, Color("M_CraneMatTimber", new Color(0.36f, 0.26f, 0.17f)), 0.15f); break;
                case "locates": Flat(surfaces, id, c, w, d, Mat("M_RedClayGraded", "red_dirt_mud_01", 5f), 0.01f); break;
                case "trench": break; // cut in BuildGround
                case "trailer": Box(logistics, id, c + Vector3.up * 1.95f, new Vector3(w, 2.7f, d), Color("M_TrailerWhite", new Color(0.86f, 0.85f, 0.8f))); break;
                case "conex": Box(logistics, id, c + Vector3.up * 1.3f, new Vector3(w, 2.6f, d), Color("M_ConexBlue", new Color(0.2f, 0.32f, 0.45f))); break;
                case "dumpster": Box(logistics, id, c + Vector3.up * 0.9f, new Vector3(w, 1.8f, d), Color("M_DumpsterGreen", new Color(0.12f, 0.3f, 0.2f))); break;
                case "fuel": Box(logistics, id, c + Vector3.up * 0.8f, new Vector3(w, 1.6f, d), Color("M_FuelTankRed", new Color(0.55f, 0.08f, 0.06f))); break;
                case "electrical": Box(logistics, id, c + Vector3.up * 0.75f, new Vector3(0.6f, 1.5f, 0.3f), Color("M_PanelGrey", new Color(0.5f, 0.52f, 0.52f))); break;
                case "welfare":
                    for (var i = 0; i < 3; i++)
                        Box(logistics, "PortableToilet_" + i, new Vector3(x + 1 + i * 1.3f, 1.15f, z + 1), new Vector3(1.1f, 2.3f, 1.1f), Color("M_ToiletBlue", new Color(0.12f, 0.3f, 0.6f)));
                    break;
                case "stockpile": Mound(id == "spoil" ? civil : logistics, id, c, w, d, id == "gravel_pile" ? 2.2f : 2.5f,
                        id == "gravel_pile" ? Mat("M_CrushedStone", "gravel_road", 4f) : Mat("M_RedClayCut", "red_laterite_soil_stones", 2f)); break;
                case "building_b": BuildBuildingB(building.parent, x, z, w, d); break;
                case "building": Box(building, "Slab_On_Grade", c + Vector3.up * 0.1f, new Vector3(w, 0.2f, d), Color("M_Concrete", new Color(0.62f, 0.61f, 0.58f))); break;
            }

            if (id == "pipe_laydown") Tag(PipeStack(civil, c), WorkDay.Mon | WorkDay.Tue | WorkDay.Wed);
            if (id == "spoil") Tag(civil.Find("spoil").gameObject, WorkDay.Tue | WorkDay.Wed);
            if (id == "crane_pad") Tag(surfaces.Find("crane_pad").gameObject, WorkDay.Thu);
            if (id == "building") Tag(building.Find("Slab_On_Grade").gameObject, WorkDay.Wed | WorkDay.Thu | WorkDay.Fri);
        }

        static void Flat(Transform parent, string name, Vector3 c, float w, float d, Material m, float h = 0.03f)
            => Box(parent, name, c + Vector3.up * (h / 2), new Vector3(w, h, d), m);

        static void Mound(Transform parent, string name, Vector3 c, float w, float d, float h, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = c;
            go.transform.localScale = new Vector3(w, h * 2, d);
            go.GetComponent<Renderer>().sharedMaterial = m;
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
        }

        static GameObject PipeStack(Transform parent, Vector3 c)
        {
            parent = Group(parent, "PipeStack_RCP");
            // 24 in RCP, 8 ft sections on dunnage (Tue pipe laydown).
            var concrete = Color("M_Concrete", new Color(0.62f, 0.61f, 0.58f));
            for (var row = 0; row < 2; row++)
                for (var i = 0; i < 4 - row; i++)
                {
                    var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                    go.name = "RCP_24in";
                    go.transform.SetParent(parent);
                    go.transform.position = c + new Vector3(-1.2f + i * 0.78f + row * 0.39f, 0.45f + row * 0.62f, 0);
                    go.transform.rotation = Quaternion.Euler(90, 0, 0);
                    go.transform.localScale = new Vector3(0.76f, 1.22f, 0.76f);
                    go.GetComponent<Renderer>().sharedMaterial = concrete;
                }
            return parent.gameObject;
        }

        // ---------- haul road ----------
        static void BuildHaulRoad(Transform parent, JToken poly)
        {
            var pts = poly.Select(p => new Vector3((float)p[0], 0, (float)p[1])).ToList();
            pts.Add(pts[0]);
            var mud = Mat("M_HaulRoadRuts", "muddy_tracks", 5f, TexDir, new Color(0.95f, 0.85f, 0.8f)); // red-clay ruts, Alabama
            for (var i = 0; i < pts.Count - 1; i++)
            {
                var a = pts[i]; var b = pts[i + 1];
                var len = Vector3.Distance(a, b);
                var go = Box(parent, "HaulRoad_" + i, (a + b) / 2 + Vector3.up * 0.02f, new Vector3(6f, 0.04f, len + 6f), mud);
                go.transform.rotation = Quaternion.LookRotation(b - a);
            }
        }

        // ---------- perimeter ----------
        static void BuildFence(Transform root, JObject layout)
        {
            var fence = Group(root, "Perimeter_Fence");
            var screen = Color("M_FenceWindscreen", new Color(0.08f, 0.2f, 0.14f));
            var gate = layout["rects"].First(r => (string)r["id"] == "gate_pad");
            float gx0 = (float)gate["x"], gx1 = gx0 + (float)gate["w"];
            float W = (float)layout["parcel"][0], D = (float)layout["parcel"][1];
            void Run(Vector3 a, Vector3 b)
            {
                var len = Vector3.Distance(a, b);
                var go = Box(fence, "FencePanel_Windscreen", (a + b) / 2 + Vector3.up * 0.915f, new Vector3(0.05f, 1.83f, len), screen);
                go.transform.rotation = Quaternion.LookRotation(b - a);
            }
            Run(new Vector3(0, 0, 0), new Vector3(gx0, 0, 0));
            Run(new Vector3(gx1, 0, 0), new Vector3(64.5f, 0, 0));   // trench tie-in gap at the street
            Run(new Vector3(67.5f, 0, 0), new Vector3(W, 0, 0));
            Run(new Vector3(W, 0, 0), new Vector3(W, 0, D));
            Run(new Vector3(W, 0, D), new Vector3(0, 0, D));
            Run(new Vector3(0, 0, D), new Vector3(0, 0, 0));
        }

        static void BuildPowerLine(Transform parent, JToken line)
        {
            float z = (float)line["y"], h = (float)line["height_m"];
            var wood = Color("M_UtilityPoleWood", new Color(0.3f, 0.22f, 0.15f));
            var wire = Color("M_Conductor", new Color(0.1f, 0.1f, 0.1f));
            var xs = line["poles_x"].Select(v => (float)v).ToList();
            foreach (var x in xs)
            {
                Box(parent, "UtilityPole", new Vector3(x, (h + 1.5f) / 2, z), new Vector3(0.3f, h + 1.5f, 0.3f), wood);
                Box(parent, "Crossarm", new Vector3(x, h + 0.3f, z), new Vector3(0.1f, 0.1f, 2.4f), wood);
            }
            foreach (var off in new[] { -1f, 0f, 1f })
            {
                var len = xs.Last() - xs.First() + 20f;
                Box(parent, "Conductor_13kV", new Vector3((xs.First() + xs.Last()) / 2, h + 0.4f, z + off), new Vector3(len, 0.03f, 0.03f), wire);
            }
        }

        // ---------- Wed building state: columns, deck, guardrail FBX ----------
        static void BuildDeck(Transform building)
        {
            var parent = Group(building, "PumpStation_Frame");
            Tag(parent.gameObject, WorkDay.Wed | WorkDay.Thu | WorkDay.Fri);
            float x0 = 34, z0 = 26, w = 18, d = 12;
            var steel = Color("M_PrimedSteel", new Color(0.35f, 0.33f, 0.3f));
            for (var i = 0; i <= 3; i++)
                for (var j = 0; j <= 2; j++)
                    Box(parent, "Column_W8", new Vector3(x0 + i * 6, DeckHeight / 2, z0 + j * 6), new Vector3(0.25f, DeckHeight, 0.25f), steel);
            var deck = Color("M_MetalDeck", new Color(0.36f, 0.37f, 0.37f));
            // Deck with one floor opening (hazard pool slot) left open at x 40-41.2, z 30-31.2.
            Box(parent, "Deck_A", new Vector3(x0 + 3, DeckHeight, z0 + d / 2), new Vector3(6, 0.15f, d), deck);
            Box(parent, "Deck_B", new Vector3(x0 + 12, DeckHeight, z0 + d / 2), new Vector3(12, 0.15f, d), deck);
            Box(parent, "StairTower", new Vector3(x0 - 2, DeckHeight / 2, z0 + 3), new Vector3(3, DeckHeight, 5), Color("M_ScaffoldGalv", new Color(0.6f, 0.62f, 0.63f)));

            var rail = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/B-PROC/SM_Guardrail_service_worn.fbx");
            var gap = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/B-PROC/SM_Guardrail_missing_midrail_worn.fbx");
            if (rail == null) return;
            var top = DeckHeight + 0.075f;
            for (var k = 0; k < 9; k++)
            {
                // North edge: one module is the missing-midrail variant (hazard slot); the rest compliant.
                var prefab = k == 6 && gap != null ? gap : rail;
                Place(parent, prefab, new Vector3(x0 + 1 + k * 2, top, z0 + d - 0.1f), 0);
                Place(parent, rail, new Vector3(x0 + 1 + k * 2, top, z0 + 0.1f), 180);
            }
            for (var k = 0; k < 6; k++)
                Place(parent, rail, new Vector3(x0 + w - 0.1f, top, z0 + 1 + k * 2), 90);
        }

        // Bldg B: 4-storey steel frame (Wed state). Wire-rope perimeter guardrail on L2-L3, shaft and
        // stair openings, low-slope roof at 16 m with a 6 ft warning line and skylights (SiteLayout §7).
        static void BuildBuildingB(Transform root, float x0, float z0, float w, float d)
        {
            var b = Group(root, "Bldg_B_4Storey");
            const float floorH = 4f;
            var steel = Color("M_PrimedSteel", new Color(0.35f, 0.33f, 0.3f));
            var slab = Color("M_ConcreteDeck", new Color(0.58f, 0.57f, 0.54f));
            var rope = Color("M_WireRope", new Color(0.55f, 0.56f, 0.57f));
            var flag = Color("M_FlaggingOrange", new Color(0.95f, 0.35f, 0.05f));
            Box(b, "Slab_On_Grade", new Vector3(x0 + w / 2, 0.1f, z0 + d / 2), new Vector3(w, 0.2f, d), slab);
            for (var i = 0; i <= 4; i++)
                for (var j = 0; j <= 3; j++)
                    Box(b, "Column_W10", new Vector3(x0 + i * 6, 8f, z0 + j * 6), new Vector3(0.3f, 16f, 0.3f), steel);

            for (var level = 1; level <= 4; level++)
            {
                var y = level * floorH;
                var name = level == 4 ? "Roof" : "L" + (level + 1);
                // Floor with an elevator shaft (x 6-8.4, z 6-8.4) and stair opening (x 12-15, z 6-11) cut out.
                Box(b, name + "_Deck_S", new Vector3(x0 + w / 2, y, z0 + 3), new Vector3(w, 0.2f, 6), slab);
                Box(b, name + "_Deck_N", new Vector3(x0 + w / 2, y, z0 + 11.5f + (d - 11.5f) / 2 - 0.0f), new Vector3(w, 0.2f, d - 11), slab);
                Box(b, name + "_Deck_Mid_W", new Vector3(x0 + 3, y, z0 + 8.5f), new Vector3(6, 0.2f, 5), slab);
                Box(b, name + "_Deck_Mid_C", new Vector3(x0 + 10.2f, y, z0 + 8.5f), new Vector3(3.6f, 0.2f, 5), slab);
                Box(b, name + "_Deck_Mid_E", new Vector3(x0 + 19.5f, y, z0 + 8.5f), new Vector3(9, 0.2f, 5), slab);

                if (level <= 2)
                {
                    // Wire-rope perimeter guardrail: top 42 in, mid 21 in, flagged every 6 ft (1926.502(b)(9)).
                    foreach (var h in new[] { 1.0668f, 0.5334f })
                    {
                        Box(b, name + "_Rope_S", new Vector3(x0 + w / 2, y + h, z0 - 0.05f), new Vector3(w, 0.012f, 0.012f), rope);
                        Box(b, name + "_Rope_N", new Vector3(x0 + w / 2, y + h, z0 + d + 0.05f), new Vector3(w, 0.012f, 0.012f), rope);
                        Box(b, name + "_Rope_E", new Vector3(x0 + w + 0.05f, y + h, z0 + d / 2), new Vector3(0.012f, 0.012f, d), rope);
                    }
                    for (var f = 0f; f < w; f += 1.83f)
                        Box(b, "Flag", new Vector3(x0 + f, y + 1.0f, z0 - 0.05f), new Vector3(0.12f, 0.1f, 0.01f), flag);
                }
            }

            // Roof (16 m): warning line 6 ft (1.83 m) back from the edge, stanchions 34-39 in, flags every 6 ft.
            var roofY = 4 * floorH;
            var lineH = 0.92f; // 36 in, inside the 34-39 in band (1926.502(f)(2)(iii))
            var inset = 1.83f;
            var roofWork = Group(b, "BldgB_RoofWork");
            Tag(roofWork.gameObject, WorkDay.Thu | WorkDay.Fri);
            b = roofWork;
            var stanchion = Color("M_WarningLineStanchion", new Color(0.9f, 0.1f, 0.08f));
            var rect = new[] { new Vector2(x0 + inset, z0 + inset), new Vector2(x0 + w - inset, z0 + inset), new Vector2(x0 + w - inset, z0 + d - inset), new Vector2(x0 + inset, z0 + d - inset) };
            for (var k = 0; k < 4; k++)
            {
                var a = rect[k]; var c = rect[(k + 1) % 4];
                var len = Vector2.Distance(a, c);
                var mid = (a + c) / 2;
                var line = Box(b, "WarningLine", new Vector3(mid.x, roofY + lineH, mid.y), new Vector3(0.01f, 0.01f, len), rope);
                line.transform.rotation = Quaternion.LookRotation(new Vector3(c.x - a.x, 0, c.y - a.y));
                for (var t = 0f; t <= len; t += 1.83f)
                {
                    var p = Vector2.Lerp(a, c, t / len);
                    Box(b, "Stanchion", new Vector3(p.x, roofY + lineH / 2, p.y), new Vector3(0.04f, lineH, 0.04f), stanchion);
                }
            }
            var skylight = Color("M_SkylightAcrylic", new Color(0.75f, 0.85f, 0.9f));
            Box(b, "Skylight_1", new Vector3(x0 + 4, roofY + 0.25f, z0 + 14), new Vector3(1.2f, 0.3f, 2.4f), skylight);
            Box(b, "Skylight_2", new Vector3(x0 + 20, roofY + 0.25f, z0 + 14), new Vector3(1.2f, 0.3f, 2.4f), skylight);
            Box(b, "RoofHatch_Curb", new Vector3(x0 + 13.5f, roofY + 0.3f, z0 + 8.5f), new Vector3(1f, 0.4f, 1.2f), steel);
            b = roofWork.parent;
            Box(b, "StairTower_Ext", new Vector3(x0 - 2.5f, 8f, z0 + 4), new Vector3(3.5f, 16.5f, 5), Color("M_ScaffoldGalv", new Color(0.6f, 0.62f, 0.63f)));
            Box(b, "BoomLift_60ft_PH", new Vector3(x0 + 5, 1.3f, z0 - 4), new Vector3(2.4f, 2.6f, 8f), Color("M_LiftOrange", new Color(0.85f, 0.4f, 0.05f)));
            Box(b, "DebrisChute_PH", new Vector3(x0 + w + 0.6f, 6f, z0 + 3), new Vector3(0.8f, 12f, 0.8f), Color("M_ChuteYellow", new Color(0.85f, 0.65f, 0.1f)));
        }

        static void Tag(GameObject go, WorkDay days) => go.AddComponent<PhaseMember>().Configure(days);

        // Day-specific site states (SiteLayout §3): stakeout, trench backfill, crane pick, pump truck.
        static void BuildDayVariants(Transform root, Transform civil, JObject layout)
        {
            var days = Group(root, "DayVariants");
            // Mon-Tue: pump-station stakeout (lath stakes + flagging at the corners, string line).
            var stake = Group(days, "PumpStation_Stakeout");
            Tag(stake.gameObject, WorkDay.Mon | WorkDay.Tue);
            var lath = Color("M_LathStake", new Color(0.78f, 0.66f, 0.46f));
            var pink = Color("M_SurveyPink", new Color(1f, 0.3f, 0.6f));
            foreach (var p in new[] { new Vector2(34, 26), new Vector2(52, 26), new Vector2(52, 38), new Vector2(34, 38) })
            {
                Box(stake, "LathStake", new Vector3(p.x, 0.6f, p.y), new Vector3(0.04f, 1.2f, 0.02f), lath);
                Box(stake, "Flagging", new Vector3(p.x, 1.15f, p.y + 0.06f), new Vector3(0.03f, 0.25f, 0.1f), pink);
            }

            // Trench closed on Mon (not dug) and Thu-Fri (backfilled): red-clay fill slightly crowned.
            var t = layout["rects"].First(r => (string)r["id"] == "trench");
            float tx = (float)t["x"], tw = (float)t["w"], ty = (float)t["y"], th = (float)t["h"];
            var fill = Box(days, "Trench_Backfill", new Vector3(tx + tw / 2, -TrenchDepth / 2 + 0.03f, ty + th / 2),
                new Vector3(tw + 0.4f, TrenchDepth + 0.06f, th), Mat("M_RedClayGraded", "red_dirt_mud_01", 5f));
            Tag(fill, WorkDay.Mon | WorkDay.Thu | WorkDay.Fri);

            // Thu: boom truck on the crane pad lifting a rooftop unit toward Bldg B (placeholder).
            var crane = Group(days, "Crane_BoomTruck_PH");
            Tag(crane.gameObject, WorkDay.Thu);
            var white = Color("M_CraneWhite", new Color(0.85f, 0.85f, 0.82f));
            Box(crane, "Carrier", new Vector3(48, 1.5f, 46), new Vector3(2.5f, 3f, 10f), white);
            var boom = Box(crane, "Boom", new Vector3(56, 9f, 46), new Vector3(0.6f, 0.6f, 20f), white);
            boom.transform.rotation = Quaternion.Euler(-40, 90, 0);
            Box(crane, "Load_RTU", new Vector3(63, 12f, 46), new Vector3(2.4f, 1.3f, 1.6f), Color("M_RtuGrey", new Color(0.55f, 0.57f, 0.58f)));

            // Fri: concrete pump truck set up under the overhead line (capstone), boom raised toward it.
            var pump = Group(days, "ConcretePumpTruck_PH");
            Tag(pump.gameObject, WorkDay.Fri);
            var red = Color("M_PumpTruckRed", new Color(0.7f, 0.1f, 0.08f));
            var lineZ = (float)layout["power_line"]["y"];
            Box(pump, "Truck", new Vector3(40, 1.6f, lineZ - 10), new Vector3(2.5f, 3.2f, 11f), white);
            var pboom = Box(pump, "Boom", new Vector3(40, 6.5f, lineZ - 5.5f), new Vector3(0.5f, 0.5f, 12f), red);
            pboom.transform.rotation = Quaternion.Euler(-35, 0, 0);
        }

        static void Place(Transform parent, GameObject prefab, Vector3 pos, float yaw)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
        }

        // ---------- Alabama surroundings: loblolly-type pine treeline, shrubs, hydrant ----------
        static void BuildSurroundings(Transform root)
        {
            var group = Group(root, "Surroundings_Alabama");
            var pine = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/B-LIB/pine_tree_01/pine_tree_01_1k.fbx");
            var shrub = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/B-LIB/shrub_01/shrub_01_1k.fbx");
            var hydrant = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/B-LIB/fire_hydrant/fire_hydrant_1k.fbx");
            var rng = new System.Random(20260928); // deterministic dressing
            float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            void Scatter(GameObject prefab, int count, float x0, float x1, float z0, float z1, float s0, float s1)
            {
                if (prefab == null) return;
                for (var i = 0; i < count; i++)
                {
                    var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, group);
                    go.transform.position = new Vector3(R(x0, x1), 0, R(z0, z1));
                    go.transform.rotation = Quaternion.Euler(0, R(0, 360), 0);
                    go.transform.localScale *= R(s0, s1);
                    FixFoliage(go);
                }
            }
            // Pine stand behind the north line and the east/west edges; the street side stays open.
            Scatter(pine, 80, -40, 170, 90, 140, 0.9f, 1.4f);
            Scatter(pine, 30, 132, 175, -5, 90, 0.9f, 1.4f);
            Scatter(pine, 30, -60, -16, -5, 90, 0.9f, 1.4f);
            Scatter(shrub, 50, -10, 130, 84, 90, 0.8f, 1.5f);
            if (hydrant != null)
            {
                var h = (GameObject)PrefabUtility.InstantiatePrefab(hydrant, group);
                h.transform.position = new Vector3(30, 0, -2.5f);
            }
        }

        // URP's FBX material preprocessor leaves foliage opaque (needle cards render as white slabs)
        // and drops the shrub texture. Swap in cut-out, two-sided materials we own.
        static void FixFoliage(GameObject go)
        {
            foreach (var r in go.GetComponentsInChildren<Renderer>())
            {
                var mats = r.sharedMaterials;
                for (var i = 0; i < mats.Length; i++)
                {
                    var n = mats[i] == null ? "" : mats[i].name;
                    if (n.Contains("twig")) mats[i] = Cutout("M_PineTwig_Cutout", "Assets/_Game/Art/Models/B-LIB/pine_tree_01/textures/pine_tree_01_twig_diff_1k.png",
                        new Color(0.42f, 0.55f, 0.3f)); // deep green of loblolly pine, not the source's blue-grey
                    else if (n.StartsWith("pine_tree_01_trunk") && mats[i].GetTexture("_BaseMap") == null)
                        mats[i] = Textured("M_" + n, $"Assets/_Game/Art/Models/B-LIB/pine_tree_01/textures/{n}_diff_1k.png");
                    else if (n.StartsWith("shrub")) mats[i] = Cutout("M_Shrub_Cutout", "Assets/_Game/Art/Models/B-LIB/shrub_01/textures/shrub_01_diffalpha_1k.png");
                }
                r.sharedMaterials = mats;
            }
        }

        static Material Textured(string name, string texPath)
        {
            if (Mats.TryGetValue(name, out var cached)) return cached;
            var m = Color(name, UnityEngine.Color.white);
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            return m;
        }

        static Material Cutout(string name, string texPath, Color? tint = null)
        {
            if (Mats.TryGetValue(name, out var cached)) return cached;
            var m = Color(name, tint ?? UnityEngine.Color.white);
            // Transparent texels are white in these atlases; without colour dilation the mips bleed
            // white into the needles and the whole canopy reads as frosted at distance.
            var importer = (TextureImporter)AssetImporter.GetAtPath(texPath);
            if (!importer.alphaIsTransparency || !importer.mipMapsPreserveCoverage)
            {
                importer.alphaIsTransparency = true;
                importer.mipMapsPreserveCoverage = true;
                importer.alphaTestReferenceValue = 0.45f;
                importer.SaveAndReimport();
            }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(texPath));
            m.SetFloat("_AlphaClip", 1f);
            m.SetFloat("_Cutoff", 0.45f);
            m.EnableKeyword("_ALPHATEST_ON");
            m.SetFloat("_Cull", 0f);
            m.renderQueue = (int)RenderQueue.AlphaTest;
            return m;
        }

        // ---------- lighting, sky, post ----------
        static void BuildLighting()
        {
            var sun = new GameObject("Sun").AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.color = new Color(1f, 0.95f, 0.86f);
            sun.intensity = 1.35f;
            sun.shadows = LightShadows.Soft;
            sun.transform.rotation = Quaternion.Euler(42, -35, 0);

            // Sky-only HDRI: a backdrop with foreign buildings would break site authenticity (PropBible).
            var hdr = AssetDatabase.LoadAssetAtPath<Texture>("Assets/_Game/Art/HDRI/kloofendal_48d_partly_cloudy_puresky_2k.hdr");
            var cube = hdr is Cubemap;
            var sky = new Material(Shader.Find(cube ? "Skybox/Cubemap" : "Skybox/Panoramic"));
            sky.SetTexture(cube ? "_Tex" : "_MainTex", hdr);
            sky.SetFloat("_Exposure", 1.1f);
            AssetDatabase.DeleteAsset(MatDir + "/M_Sky_ConstructionYard.mat");
            AssetDatabase.CreateAsset(sky, MatDir + "/M_Sky_ConstructionYard.mat");
            RenderSettings.skybox = sky;
            RenderSettings.ambientMode = AmbientMode.Skybox;
            RenderSettings.sun = sun;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = new Color(0.7f, 0.74f, 0.78f); // humid haze
            RenderSettings.fogStartDistance = 40f;
            RenderSettings.fogEndDistance = 420f;
            DynamicGI.UpdateEnvironment();

            var profilePath = "Assets/_Game/Settings/PP_Jobsite.asset";
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(profilePath);
            if (profile == null)
            {
                profile = ScriptableObject.CreateInstance<VolumeProfile>();
                AssetDatabase.CreateAsset(profile, profilePath);
                var tone = profile.Add<Tonemapping>(true);
                tone.mode.value = TonemappingMode.ACES;
                var bloom = profile.Add<Bloom>(true);
                bloom.intensity.value = 0.25f;
                var color = profile.Add<ColorAdjustments>(true);
                color.postExposure.value = 0.2f;
                color.saturation.value = -8f;
                foreach (var c in profile.components) AssetDatabase.AddObjectToAsset(c, profile);
            }
            if (profile.TryGet<ColorAdjustments>(out var adjust))
                adjust.postExposure.value = -0.1f; // bright concrete decks were clipping to white
            EditorUtility.SetDirty(profile);
            var volume = new GameObject("PostProcess_Global").AddComponent<Volume>();
            volume.isGlobal = true;
            volume.sharedProfile = profile;
        }

        // ---------- helpers ----------
        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent);
            return t;
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Material m)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent);
            go.transform.position = center;
            go.transform.localScale = size;
            go.GetComponent<Renderer>().sharedMaterial = m;
            if (TileMetres.TryGetValue(m, out var tile))
                WorldUv(go, size, tile);
            GameObjectUtility.SetStaticEditorFlags(go, StaticEditorFlags.BatchingStatic);
            return go;
        }

        // Rewrites cube UVs so textures repeat every `tile` metres regardless of box size.
        static void WorldUv(GameObject go, Vector3 size, float tile)
        {
            var filter = go.GetComponent<MeshFilter>();
            var mesh = Object.Instantiate(filter.sharedMesh);
            var uv = mesh.uv; var n = mesh.normals;
            for (var i = 0; i < uv.Length; i++)
            {
                var a = new Vector3(Mathf.Abs(n[i].x), Mathf.Abs(n[i].y), Mathf.Abs(n[i].z));
                var face = a.y > 0.5f ? new Vector2(size.x, size.z) : a.x > 0.5f ? new Vector2(size.z, size.y) : new Vector2(size.x, size.y);
                uv[i] = Vector2.Scale(uv[i], face) / tile;
            }
            mesh.uv = uv;
            filter.sharedMesh = mesh;
        }

        static Material Color(string name, Color c)
        {
            if (Mats.TryGetValue(name, out var cached)) return cached;
            var path = $"{MatDir}/{name}.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null)
            {
                m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                AssetDatabase.CreateAsset(m, path);
            }
            m.SetColor("_BaseColor", c);
            m.SetFloat("_Smoothness", 0.2f);
            EditorUtility.SetDirty(m);
            return Mats[name] = m;
        }

        // Tiled PBR material; world-scale tiling via uniform tile size (metres per repeat).
        static Material Mat(string name, string tex, float metresPerTile, string dir = TexDir, Color? tint = null)
        {
            if (Mats.TryGetValue(name, out var cached)) return cached;
            var m = Color(name, tint ?? UnityEngine.Color.white);
            var baseMap = FindTex(dir, tex, "diff");
            var normal = FindTex(dir, tex, "nor");
            if (normal != null)
            {
                var importer = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GetAssetPath(normal));
                if (importer.textureType != TextureImporterType.NormalMap)
                {
                    importer.textureType = TextureImporterType.NormalMap;
                    importer.SaveAndReimport();
                }
                m.SetTexture("_BumpMap", normal);
                m.EnableKeyword("_NORMALMAP");
            }
            m.SetTexture("_BaseMap", baseMap);
            TileMetres[m] = metresPerTile;
            m.SetFloat("_Smoothness", 0.12f);
            return m;
        }

        static Texture2D FindTex(string dir, string tex, string channel)
        {
            var guid = AssetDatabase.FindAssets(tex + "_" + channel, new[] { dir }).FirstOrDefault();
            return guid == null ? null : AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath(guid));
        }
    }
}
