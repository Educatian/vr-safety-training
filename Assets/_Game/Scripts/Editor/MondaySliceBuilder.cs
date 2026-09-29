using Jobsite.Core;
using Jobsite.Runtime;
using UnityEditor;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Jobsite.Editor
{
    // Monday desktop slice (GDD §3 Mon: mobilization; SiteLayout §6.5 Mon loop gate → trailer → temp power).
    // Called at the end of JobsiteGreyboxBuilder.Build, so the whole scene stays reproducible from code.
    public static class MondaySliceBuilder
    {
        const string PolyHaven = "Assets/ThirdParty/PolyHaven/Models/";

        public static void Add(Transform site)
        {
            var gameplay = new GameObject("Gameplay").transform;
            var root = new GameObject("Conditions_Mon").transform;
            root.SetParent(gameplay);
            root.gameObject.AddComponent<PhaseMember>().Configure(WorkDay.Mon);

            var conditions = new[]
            {
                // Damaged cord across the walkway from temp power (electrical; replace = engineered fix).
                Cord(root, "mon-damaged-cord", "Damaged extension cord", true, new Vector3(20f, 0, 21f),
                    "Cord jacket is cut; conductors exposed. Remove from service."),
                // Look-alike: cord protected by a ramp where it crosses the walkway.
                Cord(root, "mon-cord-ramp", "Cord on a ramp", false, new Vector3(22f, 0, 24.2f),
                    "Protected crossing: intact cord under a rated cord ramp."),
                Receptacle(root, "mon-no-gfci", "Tool on a non-GFCI outlet", true, new Vector3(23f, 0, 27f)),
                Receptacle(root, "mon-gfci-ok", "Tool on a GFCI spider box", false, new Vector3(17.5f, 0, 22.8f)),
                TrailerLadder(root, new Vector3(6f, 0, 19.3f)),
                WaterStation(root, new Vector3(6.5f, 0, 36.6f)),
            };

            var rack = Box(root, "SupplyRack", new Vector3(17.2f, 0.9f, 15.4f), new Vector3(2f, 1.8f, 0.6f), new Color(0.2f, 0.32f, 0.45f));
            rack.AddComponent<BoxCollider>(); // Box() strips colliders; the rack must be hit by the E ray
            rack.AddComponent<ControlSupply>();

            // Player at the gate, eyes at 1.7 m.
            var player = new GameObject("Player");
            player.transform.position = new Vector3(12f, 0.1f, -1.5f);
            var cc = player.AddComponent<CharacterController>();
            cc.height = 1.8f; cc.center = new Vector3(0, 0.9f, 0); cc.radius = 0.3f;
            var cam = new GameObject("EyeCamera").AddComponent<Camera>();
            cam.transform.SetParent(player.transform, false);
            cam.transform.localPosition = new Vector3(0, 1.7f, 0);
            cam.fieldOfView = 70f;
            cam.tag = "MainCamera";
            UnityEngine.Rendering.Universal.CameraExtensions.GetUniversalAdditionalCameraData(cam).renderPostProcessing = true;   // grading + heat symptoms
            cam.gameObject.AddComponent<AudioListener>();
            var sitePlayer = player.AddComponent<SitePlayer>();

            var director = new GameObject("ShiftDirector").AddComponent<ShiftDirector>();
            director.gameObject.AddComponent<EpisodeDirector>();
            director.gameObject.AddComponent<Telemetry>();
            director.gameObject.AddComponent<AudioDirector>();
            sitePlayer.gameObject.AddComponent<HeatStrain>();
            director.gameObject.AddComponent<WeatherDirector>().Configure(WeatherFx("Rain", true), WeatherFx("WindDust", false));
            var radio = director.gameObject.AddComponent<AudioSource>();
            var tablet = BuildTablet(cam, out var panel, out var radioText, out var frame, out var flash);
            cam.gameObject.AddComponent<FirstPersonTablet>().Configure(
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/B-PROC/SM_FP_TabletHands.fbx"), panel, frame,
                AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/TR-3D/Rigs/SM_FP_Hands_Rig.fbx"), HandsMaterial());
            tablet.Configure(director, panel, radioText, radioText.font, frame, flash);
            sitePlayer.Configure(cam, director);
            director.Configure(conditions, sitePlayer, tablet, radio);

            var events = new GameObject("EventSystem", typeof(EventSystem), typeof(InputSystemUIInputModule));
            foreach (var go in new[] { player, director.gameObject, tablet.gameObject, events, rack })
                go.transform.SetParent(gameplay, true);
            TuesdaySliceBuilder.Add(gameplay);
            WednesdaySliceBuilder.Add(gameplay);
            ThursdaySliceBuilder.Add(gameplay);
            FridaySliceBuilder.Add(gameplay);
            GateAndCrewBuilder.Add(gameplay);
            InstrumentReadings.Apply();
        }

        // ---------- conditions ----------
        static SiteCondition Cord(Transform root, string id, string title, bool hazard, Vector3 at, string why)
        {
            var go = new GameObject(id);
            go.transform.SetParent(root);
            go.transform.position = at;
            var orange = new Color(0.95f, 0.45f, 0.05f);
            var before = Group(go.transform, "Unresolved");
            Box(before, "Cord", at + new Vector3(0, 0.012f, 0), new Vector3(6f, 0.022f, 0.022f), orange);
            if (hazard)
                Box(before, "CutJacket_ExposedConductors", at + new Vector3(0.4f, 0.016f, 0), new Vector3(0.12f, 0.026f, 0.03f), new Color(0.7f, 0.45f, 0.2f));
            else
                Box(before, "CordRamp", at + new Vector3(0, 0.03f, 0), new Vector3(0.9f, 0.06f, 0.5f), new Color(0.95f, 0.8f, 0.1f));
            var after = Group(go.transform, "Resolved");
            Box(after, "NewCord", at + new Vector3(0, 0.012f, 0), new Vector3(6f, 0.022f, 0.022f), new Color(0.95f, 0.8f, 0.1f));
            AddCollider(go, at + new Vector3(0, 0.3f, 0), new Vector3(6f, 0.6f, 0.8f));
            var c = go.AddComponent<SiteCondition>();
            c.Configure(id, title, why, hazard, EnergySource.Electrical, CpArea.Electrical, hazard ? 3 : 1, hazard ? 4 : 1, before.gameObject, after.gameObject);
            c.SetControlKey(ControlLevel.Engineering, 480f);
            if (hazard) c.SetStandard("29 CFR 1926.416(e)(1)", "Worn or frayed cords must not be used.", "Remove from service", false);
            return c;
        }

        static SiteCondition Receptacle(Transform root, string id, string title, bool hazard, Vector3 at)
        {
            var go = new GameObject(id);
            go.transform.SetParent(root);
            go.transform.position = at;
            var before = Group(go.transform, "Unresolved");
            Box(before, "Stake", at + new Vector3(0, 0.45f, 0), new Vector3(0.08f, 0.9f, 0.08f), new Color(0.6f, 0.5f, 0.35f));
            Box(before, hazard ? "PlainBox_NoGFCI" : "SpiderBox_GFCI", at + new Vector3(0, 0.7f, 0.08f),
                hazard ? new Vector3(0.12f, 0.18f, 0.06f) : new Vector3(0.35f, 0.3f, 0.25f),
                hazard ? new Color(0.55f, 0.56f, 0.56f) : new Color(0.95f, 0.75f, 0.05f));
            Model(before, PolyHaven + "Drill_01_1k.fbx", at + new Vector3(0.4f, 0.02f, 0.2f), 30f);
            var after = Group(go.transform, "Resolved");
            Box(after, "InlineGFCI", at + new Vector3(0.15f, 0.62f, 0.12f), new Vector3(0.1f, 0.14f, 0.06f), new Color(0.95f, 0.75f, 0.05f));
            AddCollider(go, at + new Vector3(0.1f, 0.5f, 0.1f), new Vector3(1f, 1f, 0.8f));
            var c = go.AddComponent<SiteCondition>();
            c.Configure(id, title, hazard ? "Handheld tool on an unprotected circuit. Use GFCI protection." : "GFCI-protected temporary power.",
                hazard, EnergySource.Electrical, CpArea.Electrical, hazard ? 3 : 1, hazard ? 5 : 1, before.gameObject, after.gameObject);
            c.SetControlKey(ControlLevel.Engineering, 540f);
            c.SetStandard("29 CFR 1926.404(b)(1)", "Use GFCIs or an assured grounding program on temporary power.", "120 V, 15/20 A receptacles", true);
            return c;
        }

        // Extension ladder to the trailer roof (AC service) that stops short of the 3 ft extension.
        static SiteCondition TrailerLadder(Transform root, Vector3 at)
        {
            var go = new GameObject("mon-trailer-ladder");
            go.transform.SetParent(root);
            go.transform.position = at;
            var before = Group(go.transform, "Unresolved");
            var ladder = Model(before, PolyHaven + "ladder_sectioned_01_1k.fbx", at, 0f);
            if (ladder != null) ladder.transform.rotation = Quaternion.Euler(-14f, 0, 0); // ~4:1 lean against the trailer
            var after = Group(go.transform, "Resolved");
            Box(after, "Ladder_Extended_TiedOff", at + new Vector3(0, 2.3f, 0.45f), new Vector3(0.5f, 4.6f, 0.08f), new Color(0.95f, 0.75f, 0.05f));
            AddCollider(go, at + new Vector3(0, 1.6f, 0.3f), new Vector3(0.8f, 3.2f, 1f));
            var c = go.AddComponent<SiteCondition>();
            c.Configure("mon-trailer-ladder", "Ladder to the trailer roof",
                "Rails must extend 3 ft above the landing and the ladder must be secured.", true,
                EnergySource.Gravity, CpArea.FallProtection, 3, 4, before.gameObject, after.gameObject);
            c.SetControlKey(ControlLevel.Engineering, 450f);
            c.SetStandard("29 CFR 1926.1053(b)(1)", "Side rails extend above the landing, or the ladder is secured with a grab device.", "3 ft above landing", true);
            return c;
        }

        // Heat (non-Focus-Four): empty water cooler at the shade tent. Best control is administrative.
        static SiteCondition WaterStation(Transform root, Vector3 at)
        {
            var go = new GameObject("mon-empty-water");
            go.transform.SetParent(root);
            go.transform.position = at;
            var before = Group(go.transform, "Unresolved");
            Box(before, "Table", at + new Vector3(0, 0.37f, 0), new Vector3(1.8f, 0.05f, 0.75f), new Color(0.9f, 0.9f, 0.88f));
            Box(before, "Cooler_Empty", at + new Vector3(0.4f, 0.62f, 0), new Vector3(0.35f, 0.45f, 0.35f), new Color(0.95f, 0.45f, 0.05f));
            var after = Group(go.transform, "Resolved");
            Box(after, "Cups_And_RestSchedule", at + new Vector3(-0.3f, 0.47f, 0), new Vector3(0.3f, 0.15f, 0.3f), Color.white);
            AddCollider(go, at + new Vector3(0, 0.6f, 0), new Vector3(1.9f, 1.2f, 0.9f));
            var c = go.AddComponent<SiteCondition>();
            c.Configure("mon-empty-water", "Empty water station",
                "Heat: water, rest and shade are the control here.", true,
                EnergySource.Temperature, CpArea.General, 4, 3, before.gameObject, after.gameObject);
            c.SetControlKey(ControlLevel.Administrative, 540f);
            c.SetStandard("OSH Act 5(a)(1) General Duty; OSHA heat guidance", "Provide water, rest and shade; acclimatize new workers.", "About 1 cup every 15-20 min", false);
            return c;
        }

        // ---------- tablet UI (screen-space camera so captures include it) ----------
        static ParticleSystem WeatherFx(string name, bool rain)
        {
            var go = new GameObject(name);
            var ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var main = ps.main;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = rain ? 3000 : 400;
            main.playOnAwake = false;
            main.startLifetime = rain ? 1.1f : 4f;
            main.startSpeed = rain ? 0f : 0.5f;
            main.startSize = rain ? new ParticleSystem.MinMaxCurve(0.02f, 0.035f) : new ParticleSystem.MinMaxCurve(0.6f, 1.8f);
            main.startColor = rain ? new Color(0.78f, 0.82f, 0.88f, 0.55f) : new Color(0.72f, 0.46f, 0.32f, 0.22f);   // red-clay dust
            main.gravityModifier = rain ? 1.4f : 0f;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = rain ? new Vector3(28, 0.5f, 28) : new Vector3(24, 3, 10);
            var vel = ps.velocityOverLifetime; vel.enabled = true; vel.space = ParticleSystemSimulationSpace.World;
            vel.x = rain ? new ParticleSystem.MinMaxCurve(-1.5f, -0.5f) : new ParticleSystem.MinMaxCurve(3f, 6f);
            vel.y = rain ? new ParticleSystem.MinMaxCurve(-9f, -7f) : new ParticleSystem.MinMaxCurve(-0.2f, 0.4f);
            vel.z = new ParticleSystem.MinMaxCurve(0f, 0f);
            var emission = ps.emission; emission.rateOverTime = 0;
            var r = go.GetComponent<ParticleSystemRenderer>();
            r.sharedMaterial = AssetDatabase.GetBuiltinExtraResource<Material>("Default-ParticleSystem.mat");
            if (rain) { r.renderMode = ParticleSystemRenderMode.Stretch; r.velocityScale = 0.05f; r.lengthScale = 2f; }
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return ps;
        }

        // Tripo glove texture (1024 px, Tools/blender/rig_fp_hands.py) on URP Lit.
        static Material HandsMaterial()
        {
            const string path = "Assets/_Game/Art/Materials/M_FP_Hands.mat";
            var m = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (m == null) { m = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(m, path); }
            m.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>("Assets/_Game/Art/Textures/FP/T_FP_Hands.png"));
            m.SetFloat("_Smoothness", 0.25f);
            EditorUtility.SetDirty(m);
            return m;
        }

        static FieldTablet BuildTablet(Camera cam, out RectTransform screen, out Text radioText, out RectTransform frame, out Image flash)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { "Assets/_Game/Resources/UI" }))
            {
                var imp = (TextureImporter)AssetImporter.GetAtPath(AssetDatabase.GUIDToAssetPath(guid));
                if (imp.textureType == TextureImporterType.Sprite) continue;
                imp.textureType = TextureImporterType.Sprite; imp.alphaIsTransparency = true; imp.mipmapEnabled = false;
                imp.SaveAndReimport();
            }
            var font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

            var canvasGo = new GameObject("TabletCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGo.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = cam;
            canvas.planeDistance = 0.4f;
            var scaler = canvasGo.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);

            // Rugged tablet frame (880x1168 art); the screen rect was measured from the art.
            var frameGo = new GameObject("TabletFrame", typeof(RectTransform), typeof(Image));
            frameGo.transform.SetParent(canvasGo.transform, false);
            frame = frameGo.GetComponent<RectTransform>();
            frame.anchorMin = frame.anchorMax = new Vector2(1, 0.5f);
            frame.pivot = new Vector2(1, 0.5f);
            frame.sizeDelta = new Vector2(780, 1035);
            frame.anchoredPosition = new Vector2(-30, 0);
            var frameImg = frameGo.GetComponent<Image>();
            frameImg.sprite = Resources.Load<Sprite>("UI/tablet_frame");
            frameImg.color = frameImg.sprite != null ? Color.white : new Color(0.07f, 0.09f, 0.1f, 0.94f);

            var screenGo = new GameObject("Screen", typeof(RectTransform), typeof(Image), typeof(VerticalLayoutGroup));
            screenGo.transform.SetParent(frameGo.transform, false);
            screen = screenGo.GetComponent<RectTransform>();
            screen.anchorMin = new Vector2(0.131f, 0.158f); screen.anchorMax = new Vector2(0.867f, 0.869f);
            screen.offsetMin = screen.offsetMax = Vector2.zero;
            screenGo.GetComponent<Image>().color = new Color(0.05f, 0.07f, 0.08f, 0.97f);
            var layout = screenGo.GetComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(22, 22, 22, 22);
            layout.spacing = 8;
            layout.childForceExpandHeight = false;
            layout.childControlHeight = true;
            layout.childControlWidth = true;       // cards span the tablet; default 100 px truncated text
            layout.childForceExpandWidth = true;

            var radioGo = new GameObject("RadioLine", typeof(RectTransform), typeof(Text), typeof(Shadow));
            radioGo.transform.SetParent(canvasGo.transform, false);
            var rr = radioGo.GetComponent<RectTransform>();
            rr.anchorMin = new Vector2(0.04f, 0.03f); rr.anchorMax = new Vector2(0.58f, 0.11f);
            rr.offsetMin = rr.offsetMax = Vector2.zero;
            radioText = radioGo.GetComponent<Text>();
            radioText.font = font;
            radioText.fontSize = 34;
            radioText.color = new Color(1f, 0.85f, 0.45f);

            var flashGo = new GameObject("PhotoFlash", typeof(RectTransform), typeof(Image));
            flashGo.transform.SetParent(canvasGo.transform, false);
            var fr = flashGo.GetComponent<RectTransform>(); fr.anchorMin = Vector2.zero; fr.anchorMax = Vector2.one; fr.offsetMin = fr.offsetMax = Vector2.zero;
            flash = flashGo.GetComponent<Image>(); flash.color = Color.clear; flash.raycastTarget = false;

            canvasGo.AddComponent<Minimap>().Build(canvasGo.transform);
            canvasGo.AddComponent<Hud>();
            canvasGo.AddComponent<MobileControls>();
            canvasGo.AddComponent<PauseMenu>();
            var tablet = canvasGo.AddComponent<FieldTablet>();
            tablet.name = "TabletCanvas";
            radioText.name = "RadioLine";
            return tablet;
        }

        // ---------- helpers ----------
        static Transform Group(Transform parent, string name)
        {
            var t = new GameObject(name).transform;
            t.SetParent(parent, false);
            t.position = parent.position;
            return t;
        }

        static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>()); // one photo collider per condition
            go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.localScale = size;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        static GameObject Model(Transform parent, string path, Vector3 at, float yaw)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (prefab == null) return null;
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = at;
            go.transform.rotation = Quaternion.Euler(0, yaw, 0);
            return go;
        }

        static void AddCollider(GameObject go, Vector3 worldCenter, Vector3 size)
        {
            var box = go.AddComponent<BoxCollider>();
            box.center = go.transform.InverseTransformPoint(worldCenter);
            box.size = size;
        }
    }

    // Shared primitives for other day builders (Tuesday etc.).
    public static class MondaySliceBuilderAccess
    {
        public static GameObject Box(Transform parent, string name, Vector3 center, Vector3 size, Color color)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            Object.DestroyImmediate(go.GetComponent<Collider>());
            go.transform.SetParent(parent, true);
            go.transform.position = center;
            go.transform.localScale = size;
            var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            m.SetColor("_BaseColor", color);
            go.GetComponent<Renderer>().sharedMaterial = m;
            return go;
        }

        // Bake a relaxed standing pose into Rocketbox workers so scene captures never show a T-pose.
        public static void RelaxArms(GameObject worker)
        {
            var fwd = worker.transform.forward;
            foreach (var t in worker.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Bip01 L UpperArm") t.rotation = Quaternion.AngleAxis(72f, fwd) * t.rotation;
                else if (t.name == "Bip01 R UpperArm") t.rotation = Quaternion.AngleAxis(-72f, fwd) * t.rotation;
                else if (t.name == "Bip01 L Forearm" || t.name == "Bip01 R Forearm") t.rotation = Quaternion.AngleAxis(-15f, worker.transform.right) * t.rotation;
            }
        }

        public static void Cone(Transform parent, Vector3 at)
        {
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Game/Art/Models/TR-3D/SM_TrafficCone28in.fbx");
            if (prefab == null) { Box(parent, "Cone", at + Vector3.up * 0.35f, new Vector3(0.3f, 0.7f, 0.3f), new Color(1f, 0.4f, 0f)); return; }
            var go = (GameObject)PrefabUtility.InstantiatePrefab(prefab, parent);
            go.transform.position = at;
            go.transform.rotation = prefab.transform.rotation;
            TripoImport.Apply(go, "SM_TrafficCone28in");
        }
    }
}
