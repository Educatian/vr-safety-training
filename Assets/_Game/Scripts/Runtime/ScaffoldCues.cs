using System.Collections.Generic;
using System.Linq;
using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    // In-world visual scaffolding for learners (GDD §14 hints, §22 missions). Guidance fades as the learner's career
    // level rises (full -> light -> off; hint cues always show because the learner asked for them):
    //   FULL  mission diamond + ground ring + step label, status pins, kit drop ring
    //   LIGHT mission diamond + status pins
    //   OFF   hint cues only
    // Hint tiers: 1 = zone ring (where to look), 2 = + energy label (what to look for), 3 = + diamond on the hazard.
    public sealed class ScaffoldCues : MonoBehaviour
    {
        public const int Off = 0, Light = 1, Full = 2;

        // GameSettings.Guidance: -1 auto (by career level), else fixed.
        public static int Level(int careerLevel) => GameSettings.Guidance >= 0 ? GameSettings.Guidance
            : careerLevel <= 1 ? Full : careerLevel == 2 ? Light : Off;

        // Auto guidance, then one step by the learner model: best mastery in today's hazard areas (Core.Fading).
        public static int Level(int careerLevel, IEnumerable<CpArea> todaysAreas, out string reason)
        {
            reason = GameSettings.Guidance >= 0 ? "fixed" : "career";
            var start = Level(careerLevel);
            if (GameSettings.Guidance >= 0 || todaysAreas == null) return start;
            var best = MasteryStore.Load();
            return Fading.Adjust(start, todaysAreas.Distinct().Select(a => best.TryGetValue(a, out var v) ? v : (float?)null), out reason);
        }

        static readonly Color MissionColor = new Color(0.25f, 0.85f, 1f, 0.9f);
        static readonly Color HintColor = new Color(1f, 0.8f, 0.1f, 0.85f);
        static readonly Color KitColor = new Color(1f, 0.75f, 0.1f, 0.9f);

        private ShiftDirector director;
        private Material mat;
        private Transform root, missionCue, missionRing, missionLabel, kitRing;
        private readonly Dictionary<string, Renderer> pins = new Dictionary<string, Renderer>();
        private readonly List<(Transform t, float until)> hints = new List<(Transform, float)>();
        private Vector3? missionAt;
        private float nextPins;
        public int Guidance { get; private set; }
        public Vector3? MissionTarget => missionAt;

        // Mission cues mark a ZONE, never the condition itself (GDD pillar 1): centred on the step's candidates
        // (hazard and look-alike alike) and shifted by up to ZoneJitter so the centre is not the answer.
        public const float ZoneRadius = 6f, ZoneJitter = 3f;

        // True while the current mission cue covers this condition: a find made now is scaffolded (evidence flag).
        public bool Cueing(string conditionId)
        {
            if (director == null || director.Mission == null || !missionAt.HasValue || Guidance < Light) return false;
            var step = director.Mission.Next;
            return step != null && step.Targets.Contains(conditionId);
        }

        private void Start()
        {
            director = GetComponent<ShiftDirector>() ?? FindFirstObjectByType<ShiftDirector>();
            mat = new Material(Shader.Find("Sprites/Default"));       // always included in builds; unlit + alpha
            root = new GameObject("ScaffoldCues").transform;
            Guidance = director != null ? Level(director.Career.Level, director.TodaysAreas, out _) : Level(1);
            missionCue = Diamond("MissionCue", MissionColor, 0.45f);
            missionRing = Ring("MissionRing", MissionColor, ZoneRadius, 0.15f);
            kitRing = Ring("KitDropRing", KitColor, 1.2f, 0.15f);
            Refresh();
        }

        public void Refresh()
        {
            if (director == null || director.Mission == null) return;
            missionAt = null;
            var step = director.Mission.Next;
            if (step != null && Guidance >= Light && director.Started) missionAt = StepTarget(step);
            if (missionLabel != null) Destroy(missionLabel.gameObject);
            if (missionAt.HasValue && Guidance >= Full)
            {
                missionLabel = new GameObject("MissionLabel").transform;
                missionLabel.SetParent(root, false);
                missionLabel.gameObject.AddComponent<NameTag>().Configure("CHECK: " + step.Text, step.Cfr, 0f);
            }
        }

        // Where the learner should go for this checklist step: nearest active target condition, the named crew
        // member, or the access point. Null when the step isn't tied to a place (weather call, check-in).
        private Vector3? StepTarget(MissionStep step)
        {
            var me = director.transform.position;
            var player = FindFirstObjectByType<SitePlayer>();
            if (player != null) me = player.transform.position;
            if (step.Kind == "radio_query_open" && step.Targets.Length > 0)
            {
                var c = FindObjectsByType<CrewMember>(FindObjectsSortMode.None).FirstOrDefault(m => step.Targets.Contains(m.DisplayName));
                return c != null ? c.transform.position + Vector3.up * 2.6f : (Vector3?)null;
            }
            if (step.Kind == "access")
            {
                var a = FindObjectsByType<AccessPoint>(FindObjectsSortMode.None).OrderBy(x => Vector3.Distance(x.transform.position, me)).FirstOrDefault();
                return a != null ? a.transform.position + Vector3.up * 2.2f : (Vector3?)null;
            }
            var targets = director.Conditions.Where(c => c != null && c.isActiveAndEnabled && step.Targets.Contains(c.Id)).ToList();
            if (targets.Count == 0) return null;
            var centre = targets.Aggregate(Vector3.zero, (s, c) => s + c.PhotoBounds.center) / targets.Count;
            var top = targets.Max(c => c.PhotoBounds.max.y);
            var rng = new System.Random(director.Seed ^ step.Text.Length * 7919 ^ director.Mission.Completed * 104729);
            var angle = rng.NextDouble() * Mathf.PI * 2; var dist = (float)rng.NextDouble() * ZoneJitter;
            return new Vector3(centre.x + Mathf.Cos((float)angle) * dist, top + 1.2f, centre.z + Mathf.Sin((float)angle) * dist);
        }

        // Called by ShiftDirector.UseHint after the hint is spent.
        public void Hint(SiteCondition target, int tier)
        {
            var b = target.PhotoBounds;
            var ground = new Vector3(b.center.x, b.min.y + 0.05f, b.center.z);
            var zone = Ring("HintZone", HintColor, tier >= 3 ? 2.5f : 7f, 0.2f);
            zone.position = ground + new Vector3(Random.Range(-1.5f, 1.5f), 0, Random.Range(-1.5f, 1.5f)) * (tier >= 3 ? 0 : 1);  // zone, not the exact spot
            hints.Add((zone, Time.time + 25f));
            if (tier >= 2)
            {
                var label = new GameObject("HintLabel").transform;
                label.SetParent(root, false); label.position = zone.position + Vector3.up * 2.4f;
                label.gameObject.AddComponent<NameTag>().Configure("Look for: " + target.Spec.Energy + " energy", target.Spec.FocusFour == FocusFour.None ? "" : "Focus Four: " + target.Spec.FocusFour, 0f);
                hints.Add((label, Time.time + 25f));
            }
            if (tier >= 3)
            {
                var d = Diamond("HintDiamond", HintColor, 0.4f);
                d.position = new Vector3(b.center.x, b.max.y + 1f, b.center.z);
                hints.Add((d, Time.time + 25f));
            }
        }

        private void Update()
        {
            if (director == null) return;
            var bob = Mathf.Sin(Time.time * 2.4f) * 0.15f;
            var showMission = missionAt.HasValue && director.Started && !director.Finished;
            missionCue.gameObject.SetActive(showMission);
            missionRing.gameObject.SetActive(showMission && Guidance >= Full);
            if (missionLabel != null) missionLabel.gameObject.SetActive(showMission);
            if (showMission)
            {
                missionCue.position = missionAt.Value + Vector3.up * bob;
                missionCue.rotation = Quaternion.Euler(0, Time.time * 90f, 0);
                if (missionLabel != null) missionLabel.position = missionAt.Value + Vector3.up * 0.9f;
                var ground = missionAt.Value; ground.y = GroundY(ground);
                missionRing.position = ground; missionRing.localScale = Vector3.one * (1f + 0.08f * Mathf.Sin(Time.time * 3f));
            }

            // Kit drop: where the carried control goes (full guidance only).
            var goal = director.Carrying && Guidance >= Full ? director.Conditions.FirstOrDefault(c => c.Id == director.PendingInstall) : null;
            kitRing.gameObject.SetActive(goal != null);
            if (goal != null)
            {
                var b = goal.PhotoBounds;
                kitRing.position = new Vector3(b.center.x, GroundY(b.center), b.center.z);
                kitRing.localScale = Vector3.one * (1f + 0.15f * Mathf.Abs(Mathf.Sin(Time.time * 4f)));
            }

            foreach (var h in hints.ToList())
                if (h.t == null || Time.time > h.until) { if (h.t != null) Destroy(h.t.gameObject); hints.Remove(h); }
                else if (h.t.name == "HintDiamond") h.t.rotation = Quaternion.Euler(0, Time.time * 90f, 0);

            if (Time.time >= nextPins) { nextPins = Time.time + 0.5f; Pins(); }
        }

        // Status pins over reported hazards: red = open, amber = temporary / lapsed / installing, green = controlled.
        private void Pins()
        {
            if (director.Session == null || Guidance < Light) return;
            foreach (var c in director.Conditions)
            {
                if (c == null || !c.IsHazard) continue;
                var st = director.Session.GetState(c.Id);
                if (st == HazardState.Latent) continue;
                if (!pins.TryGetValue(c.Id, out var r))
                {
                    var p = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                    Destroy(p.GetComponent<Collider>());
                    p.name = "Pin_" + c.Id; p.transform.SetParent(root, false); p.transform.localScale = Vector3.one * 0.35f;
                    var b = c.PhotoBounds; p.transform.position = new Vector3(b.center.x, b.max.y + 0.5f, b.center.z);
                    r = p.GetComponent<Renderer>(); r.sharedMaterial = new Material(mat);
                    pins[c.Id] = r;
                }
                r.sharedMaterial.color = st == HazardState.Controlled ? new Color(0.3f, 0.95f, 0.4f)
                    : st == HazardState.Reported || st == HazardState.Stopped ? new Color(1f, 0.3f, 0.25f)
                    : st == HazardState.Incident ? new Color(0.5f, 0.05f, 0.05f) : new Color(1f, 0.7f, 0.15f);
            }
        }

        public Color PinColor(string id) => pins.TryGetValue(id, out var r) ? r.sharedMaterial.color : Color.clear;

        static float GroundY(Vector3 p) =>
            Physics.Raycast(p + Vector3.up * 0.5f, Vector3.down, out var hit, 40f, ~0, QueryTriggerInteraction.Ignore) ? hit.point.y + 0.04f : p.y - 2f;

        // ---------- procedural cue meshes ----------
        private Transform Ring(string name, Color color, float radius, float width)
        {
            const int n = 48;
            var v = new Vector3[n * 2]; var tri = new int[n * 6];
            for (var i = 0; i < n; i++)
            {
                var a = i * Mathf.PI * 2 / n; var d = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                v[i * 2] = d * (radius - width); v[i * 2 + 1] = d * radius;
                var j = (i + 1) % n;
                tri[i * 6] = i * 2; tri[i * 6 + 1] = j * 2; tri[i * 6 + 2] = i * 2 + 1;
                tri[i * 6 + 3] = i * 2 + 1; tri[i * 6 + 4] = j * 2; tri[i * 6 + 5] = j * 2 + 1;
            }
            return MeshObject(name, v, tri.Concat(tri.Reverse()).ToArray(), color);   // double-sided
        }

        // Upside-down pyramid ("go here" marker).
        private Transform Diamond(string name, Color color, float size)
        {
            var h = size * 1.4f;
            var v = new[] { new Vector3(0, -h, 0), new Vector3(-size, 0, -size), new Vector3(size, 0, -size), new Vector3(size, 0, size), new Vector3(-size, 0, size), new Vector3(0, h * 0.35f, 0) };
            var tri = new[] { 0, 1, 2, 0, 2, 3, 0, 3, 4, 0, 4, 1, 5, 2, 1, 5, 3, 2, 5, 4, 3, 5, 1, 4 };
            return MeshObject(name, v, tri, color);
        }

        private Transform MeshObject(string name, Vector3[] v, int[] tri, Color color)
        {
            var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(root, false);
            var mesh = new Mesh { vertices = v, triangles = tri };
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.GetComponent<MeshRenderer>();
            r.sharedMaterial = new Material(mat) { color = color };
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off; r.receiveShadows = false;
            return go.transform;
        }
    }
}
