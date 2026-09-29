using System.Collections.Generic;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Procedural body language for crews (Rocketbox Bip01 or Tripo/Mixamo rigs; no animator controller, web-cheap).
    // Poses are expressed in character space (x right, y up, z forward) and re-applied from the rest pose every frame,
    // so nothing drifts. Situations play short gesture COMBOS (e.g. stop-work: shrug -> hands on hips; foreman
    // pressure: tap the watch -> head shake -> hands on hips), with a small per-person delay so a crew never moves in sync.
    public sealed class CrewGestures : MonoBehaviour
    {
        public enum Activity { Crew, Idle, Dig, Saw, Walk, TiedOff, Signal }
        public enum Situation { Greet, Acknowledge, Puzzled, Hint, ControlInstalled, WorkStopped, ForemanPressure, BackToWork, NearMiss, WeatherTurn, Listening }

        [SerializeField] private Activity activity = Activity.Crew;
        [SerializeField] private Vector3 walkA, walkB;

        private static readonly List<CrewGestures> all = new List<CrewGestures>();
        public static IReadOnlyList<CrewGestures> All => all;

        private Transform pelvis, spine, neck, head, lUpper, lFore, lHand, rUpper, rFore, rHand, lThigh, rThigh, lCalf, rCalf;
        private Transform[] bones;
        private Quaternion[] rest;
        private Transform viewer;
        private CrewMember crew;
        private NameTag nameTag;
        private NpcFace face;
        private bool waved;
        private float seed;

        private enum Shot { None, Wave, Point, Explain, Nod, ThumbsUp, Beckon, Alarmed, Shrug, TapWatch, HandsOnHips, HeadShake, PointUp, ChinScratch }
        private readonly Queue<(Shot shot, float seconds)> combo = new Queue<(Shot, float)>();
        private Shot shot; private float shotUntil; private Vector3 pointAt;
        private enum Flavor { Relaxed, HandsOnHips, CrossedArms, WipeBrow, LookAround }
        private Flavor flavor; private float flavorUntil;

        public Activity CurrentActivity => activity;
        public bool Busy => shot != Shot.None || combo.Count > 0;
        public string Showing => shot.ToString();

        public void Configure(Activity a, Vector3 a0 = default, Vector3 a1 = default) { activity = a; walkA = a0; walkB = a1; }

        public void Wave(float seconds = 2.2f) => Play(Shot.Wave, seconds);
        public void Explain(float seconds = 4f) => Play(Shot.Explain, seconds);
        public void Nod(float seconds = 1.6f) => Play(Shot.Nod, seconds);
        public void Point(Vector3 world, float seconds = 3f) { pointAt = world; Play(Shot.Point, seconds); }

        private void Play(Shot s, float seconds) { combo.Clear(); shot = s; shotUntil = Time.time + seconds; if (s == Shot.Explain && face != null) face.Speak(seconds); }

        // Situation -> combo. Starting durations; tune by eye in the capture tests.
        public void React(Situation s, Vector3 target = default, float delay = 0f)
        {
            if (target != default) pointAt = target;
            combo.Clear(); shot = Shot.None; shotUntil = 0;
            if (delay > 0) combo.Enqueue((Shot.None, delay));
            if (face != null && face.HasFace) face.Express(MoodFor(s), s == Situation.WorkStopped ? 6f : 3f);
            switch (s)
            {
                case Situation.Greet: Q(Shot.Wave, 2f); Q(Shot.Nod, 1f); break;
                case Situation.Acknowledge: Q(Shot.Nod, 1.2f); Q(Shot.ThumbsUp, 1.6f); break;
                case Situation.Puzzled: Q(Shot.HeadShake, 1.2f); Q(Shot.Shrug, 1.6f); break;
                case Situation.Hint: Q(Shot.Point, 3f); Q(Shot.Beckon, 1.8f); break;
                case Situation.ControlInstalled: Q(Shot.ThumbsUp, 1.8f); Q(Shot.Nod, 1f); break;
                case Situation.WorkStopped: Q(Shot.Shrug, 1.5f); Q(Shot.HandsOnHips, Jobsite.Core.DaySession.StopHoldSeconds); break;
                case Situation.ForemanPressure: Q(Shot.TapWatch, 2.2f); Q(Shot.HeadShake, 1.2f); Q(Shot.HandsOnHips, 3f); break;
                case Situation.BackToWork: Q(Shot.Beckon, 2f); Q(Shot.Explain, 1.5f); break;
                case Situation.NearMiss: Q(Shot.Alarmed, 1.8f); Q(Shot.Point, 2.5f); break;
                case Situation.WeatherTurn: Q(Shot.PointUp, 2f); Q(Shot.Explain, 2f); break;
                case Situation.Listening: Q(Shot.Nod, 0.8f); Q(Shot.ChinScratch, 1.6f); break;
            }
        }

        private void Q(Shot s, float seconds) => combo.Enqueue((s, seconds));

        // Facial expression that goes with each situation (Tripo NPCs with a face rig).
        private static NpcFace.Mood MoodFor(Situation s) => s switch
        {
            Situation.Greet or Situation.Acknowledge or Situation.ControlInstalled => NpcFace.Mood.Smile,
            Situation.Puzzled or Situation.WorkStopped => NpcFace.Mood.Frown,
            Situation.ForemanPressure => NpcFace.Mood.Angry,
            Situation.NearMiss => NpcFace.Mood.Surprise,
            Situation.Hint or Situation.WeatherTurn or Situation.Listening => NpcFace.Mood.BrowUp,
            _ => NpcFace.Mood.Neutral,
        };

        public static CrewGestures Named(string name)
        {
            foreach (var g in all)
                if (g != null && g.isActiveAndEnabled &&
                    ((g.crew != null && g.crew.DisplayName == name) || (g.nameTag != null && g.nameTag.DisplayName != null && g.nameTag.DisplayName.StartsWith(name))))
                    return g;
            return null;
        }

        // Everyone active within `radius` of a spot reacts, each with a small random delay.
        public static int ReactNear(Vector3 at, float radius, Situation s, Vector3 target = default)
        {
            var n = 0;
            foreach (var g in all)
                if (g != null && g.isActiveAndEnabled && Vector3.Distance(g.transform.position, at) <= radius)
                { g.React(s, target, n == 0 ? 0f : Random.Range(0.15f, 0.6f)); n++; }
            return n;
        }

        private void OnEnable() => all.Add(this);
        private void OnDisable() => all.Remove(this);

        private void Awake()
        {
            var ts = GetComponentsInChildren<Transform>(true);
            // Rocketbox "Bip01 X" or Mixamo "mixamorig:X" / "mixamorig_X" / "X".
            Transform Find(params string[] names)
            {
                foreach (var n in names)
                    foreach (var t in ts)
                        if (t.name == n || t.name.EndsWith(":" + n) || t.name.EndsWith("_" + n)) return t;
                return null;
            }
            pelvis = Find("Bip01 Pelvis", "Hips"); spine = Find("Bip01 Spine1", "Spine1", "Bip01 Spine", "Spine");
            neck = Find("Bip01 Neck", "Neck"); head = Find("Bip01 Head", "Head");
            lUpper = Find("Bip01 L UpperArm", "LeftArm"); lFore = Find("Bip01 L Forearm", "LeftForeArm"); lHand = Find("Bip01 L Hand", "LeftHand");
            rUpper = Find("Bip01 R UpperArm", "RightArm"); rFore = Find("Bip01 R Forearm", "RightForeArm"); rHand = Find("Bip01 R Hand", "RightHand");
            lThigh = Find("Bip01 L Thigh", "LeftUpLeg"); rThigh = Find("Bip01 R Thigh", "RightUpLeg"); lCalf = Find("Bip01 L Calf", "LeftLeg"); rCalf = Find("Bip01 R Calf", "RightLeg");
            bones = new[] { pelvis, spine, neck, head, lUpper, lFore, rUpper, rFore, lThigh, rThigh, lCalf, rCalf };
            rest = new Quaternion[bones.Length];
            for (var i = 0; i < bones.Length; i++) if (bones[i] != null) rest[i] = bones[i].localRotation;
            crew = GetComponent<CrewMember>();
            nameTag = GetComponent<NameTag>();
            face = GetComponent<NpcFace>();
            seed = Random.value * 100f;
            flavorUntil = Time.time + Random.Range(4f, 9f);
        }

        private void Start() { var p = FindFirstObjectByType<SitePlayer>(); viewer = p != null ? p.transform : null; }

        private void LateUpdate()
        {
            if (bones == null || lUpper == null) return;
            for (var i = 0; i < bones.Length; i++) if (bones[i] != null) bones[i].localRotation = rest[i];
            var t = Time.time + seed;
            if (Time.time > shotUntil)
            {
                shot = Shot.None;
                if (combo.Count > 0)
                {
                    var next = combo.Dequeue(); shot = next.shot; shotUntil = Time.time + next.seconds;
                    if (shot == Shot.Explain && face != null) face.Speak(next.seconds);
                }
            }
            var reacting = shot != Shot.None || combo.Count > 0;

            // Working crews keep working unless they are reacting to something.
            if (!reacting)
                switch (activity)
                {
                    case Activity.Dig: Dig(t); return;
                    case Activity.Saw: Saw(t); return;
                    case Activity.Walk: Walk(t); return;
                    case Activity.Signal: Signal(t); return;
                }

            // --- crew / idle people ---
            var toViewer = viewer != null ? viewer.position - transform.position : Vector3.zero;
            var near = viewer != null && toViewer.magnitude < 8f;
            if (activity == Activity.Crew && near && !waved) { waved = true; if (!reacting) React(Situation.Greet); }   // never interrupts a reaction
            var talking = crew != null && crew.IsTalking;
            if (talking && crew.Thinking && !reacting) React(Situation.Listening);
            if (talking && crew.JustAnswered) { React(Situation.Acknowledge); combo.Clear(); Q(Shot.Explain, Mathf.Clamp(crew.LastReplyLength / 14f, 2.5f, 7f)); Q(Shot.Nod, 1f); }

            Breathe(t);
            if (Time.time > flavorUntil && !talking)
            {
                flavor = activity == Activity.TiedOff ? Flavor.CrossedArms : (Flavor)Random.Range(0, 5);
                flavorUntil = Time.time + (flavor == Flavor.Relaxed ? Random.Range(6f, 10f) : Random.Range(3f, 5f));
            }

            var down = new Vector3(0f, -1f, 0.1f);
            switch (shot)
            {
                case Shot.Wave:
                    Arm(false, new Vector3(-0.2f, -0.95f, 0.1f), down);
                    Arm(true, new Vector3(0.55f, 0.55f, 0.15f), new Vector3(0.15f + Mathf.Sin(t * 9f) * 0.45f, 1f, 0.1f));
                    break;
                case Shot.Point:
                    var dir = transform.InverseTransformDirection((pointAt - rUpper.position).normalized);
                    dir.y = Mathf.Clamp(dir.y, -0.3f, 0.6f);
                    Arm(true, dir, dir);
                    Arm(false, new Vector3(-0.2f, -0.95f, 0.1f), new Vector3(0f, -1f, 0.15f));
                    Look(transform.TransformPoint(dir * 10f), 0.8f);
                    return;
                case Shot.Explain:
                    var w = Mathf.Sin(t * 3.6f);
                    Arm(false, new Vector3(-0.35f, -0.75f, 0.4f), new Vector3(-0.15f, 0.05f + w * 0.2f, 1f));
                    Arm(true, new Vector3(0.35f, -0.75f, 0.4f), new Vector3(0.2f, 0.1f - w * 0.25f, 1f));
                    break;
                case Shot.ThumbsUp:
                    Arm(false, new Vector3(-0.2f, -0.95f, 0.1f), down);
                    Arm(true, new Vector3(0.25f, -0.6f, 0.75f), new Vector3(0.1f, 0.85f, 0.4f));
                    break;
                case Shot.Beckon:   // "over here": arm forward, forearm curls toward the body
                    Arm(false, new Vector3(-0.2f, -0.95f, 0.1f), down);
                    Arm(true, new Vector3(0.2f, -0.45f, 0.85f), new Vector3(0f, 0.45f + Mathf.Sin(t * 7f) * 0.4f, 0.65f));
                    break;
                case Shot.Alarmed:  // hands up, lean back
                    Bend(spine, -8f, 0);
                    Arm(false, new Vector3(-0.5f, 0.15f, 0.8f), new Vector3(-0.2f, 0.85f, 0.45f));
                    Arm(true, new Vector3(0.5f, 0.15f, 0.8f), new Vector3(0.2f, 0.85f, 0.45f));
                    break;
                case Shot.Shrug:
                    Bend(head, 0, 8f);
                    Arm(false, new Vector3(-0.35f, -0.8f, 0.3f), new Vector3(-0.7f, 0.1f, 0.7f));
                    Arm(true, new Vector3(0.35f, -0.8f, 0.3f), new Vector3(0.7f, 0.1f, 0.7f));
                    break;
                case Shot.TapWatch: // left wrist up, right finger taps it, eyes on the watch
                    Bend(head, 18f, 0);
                    Arm(false, new Vector3(-0.25f, -0.8f, 0.45f), new Vector3(0.9f, 0.12f, 0.4f));
                    Arm(true, new Vector3(0.2f, -0.8f, 0.4f), new Vector3(-0.8f, 0.2f + Mathf.Abs(Mathf.Sin(t * 8f)) * 0.12f, 0.5f));
                    return;
                case Shot.HandsOnHips:
                    Arm(false, new Vector3(-0.75f, -0.6f, -0.2f), new Vector3(0.55f, -0.35f, 0.5f));
                    Arm(true, new Vector3(0.75f, -0.6f, -0.2f), new Vector3(-0.55f, -0.35f, 0.5f));
                    break;
                case Shot.HeadShake:
                    Idle(t);
                    Turn(head, Mathf.Sin(t * 10f) * 14f);
                    return;
                case Shot.PointUp:  // weather: points at the sky, looks up
                    Arm(false, new Vector3(-0.2f, -0.95f, 0.1f), down);
                    Arm(true, new Vector3(0.2f, 0.85f, 0.35f), new Vector3(0.1f, 1f, 0.2f));
                    Bend(head, -22f, 0);
                    return;
                case Shot.ChinScratch: // thinking while the learner talks
                    Arm(false, new Vector3(-0.3f, -0.85f, 0.35f), new Vector3(0.95f, 0.15f, 0.3f));
                    Arm(true, new Vector3(0.2f, -0.7f, 0.6f), new Vector3(-0.4f, 0.8f, 0.3f));
                    break;
                default:
                    Idle(t);
                    break;
            }
            if (shot == Shot.Nod) Bend(head, Mathf.Sin(Time.time * 9f) * 7f, 0);
            if (near || talking) Look(viewer.position + Vector3.up * 1.6f, talking ? 1f : 0.7f);
            else if (flavor == Flavor.LookAround) Turn(head, Mathf.Sin(t * 0.9f) * 35f);
        }

        private void Idle(float t)
        {
            switch (flavor)
            {
                case Flavor.HandsOnHips:
                    Arm(false, new Vector3(-0.75f, -0.6f, -0.2f), new Vector3(0.55f, -0.35f, 0.5f));
                    Arm(true, new Vector3(0.75f, -0.6f, -0.2f), new Vector3(-0.55f, -0.35f, 0.5f));
                    break;
                case Flavor.CrossedArms:
                    Arm(false, new Vector3(-0.3f, -0.85f, 0.35f), new Vector3(0.95f, 0.15f, 0.3f));
                    Arm(true, new Vector3(0.3f, -0.85f, 0.4f), new Vector3(-0.95f, 0.2f, 0.25f));
                    break;
                case Flavor.WipeBrow:   // Alabama heat
                    Arm(false, new Vector3(-0.2f, -0.95f, 0.1f), new Vector3(0f, -1f, 0.1f));
                    Arm(true, new Vector3(0.35f, 0.15f, 0.7f), new Vector3(-0.55f + Mathf.Sin(t * 4f) * 0.25f, 0.75f, -0.35f));
                    break;
                default:
                    var sway = Mathf.Sin(t * 0.8f) * 0.05f;
                    Arm(false, new Vector3(-0.2f - sway, -0.96f, 0.08f), new Vector3(-0.05f, -1f, 0.18f));
                    Arm(true, new Vector3(0.2f + sway, -0.96f, 0.08f), new Vector3(0.05f, -1f, 0.18f));
                    break;
            }
        }

        // Weight shift + breathing: pelvis sways, spine rises and falls slightly.
        private void Breathe(float t)
        {
            Bend(pelvis, 0, Mathf.Sin(t * 0.5f) * 2.5f);
            Bend(spine, Mathf.Sin(t * 1.6f) * 1.2f, -Mathf.Sin(t * 0.5f) * 1.5f);
        }

        private void Dig(float t)
        {
            var c = (Mathf.Sin(t * 2.2f) + 1) * 0.5f;                   // 0 = blade down, 1 = lifting
            Bend(spine, Mathf.Lerp(38f, 18f, c), 0);
            Bend(head, Mathf.Lerp(10f, 0f, c), 0);
            Arm(false, new Vector3(-0.1f, Mathf.Lerp(-0.7f, -0.2f, c), 0.7f), new Vector3(0.25f, Mathf.Lerp(-0.8f, -0.2f, c), 0.6f));
            Arm(true, new Vector3(0.15f, Mathf.Lerp(-0.85f, -0.45f, c), 0.5f), new Vector3(-0.1f, Mathf.Lerp(-0.9f, -0.4f, c), 0.5f));
            Legs(0, 12f);
        }

        private void Saw(float t)
        {
            var buzz = Mathf.Sin(t * 40f) * 0.015f;
            Bend(spine, 34f, 0);   // lean from the waist; pelvis stays over the feet
            Bend(head, 18f, 0);
            Arm(false, new Vector3(-0.15f, -0.85f, 0.45f), new Vector3(0.25f, -0.55f + buzz, 0.8f));
            Arm(true, new Vector3(0.15f, -0.85f, 0.45f), new Vector3(-0.2f, -0.6f - buzz, 0.8f));
            Legs(0, 18f);
        }

        // Crane signal person (1926.1428, App. A standard hand signals): HOIST (forearm up, finger circling) ->
        // STOP (arm straight out, palm down) -> hold, eyes on the load (walkA = the load / boom tip).
        private void Signal(float t)
        {
            Breathe(t);
            var cycle = Mathf.Repeat(t, 9f);
            Arm(false, new Vector3(-0.2f, -0.95f, 0.1f), new Vector3(0f, -1f, 0.1f));
            if (cycle < 6f) Arm(true, new Vector3(1f, 0.05f, 0.15f), new Vector3(0.18f * Mathf.Cos(t * 6f), 1f, 0.18f * Mathf.Sin(t * 6f)));
            else Arm(true, new Vector3(1f, 0f, 0.1f), new Vector3(1f, 0f, 0.1f));
            if (walkA != Vector3.zero) Look(walkA, 1f);
        }

        private void Walk(float t)
        {
            // Back and forth along the walkway at 1.2 m/s, turning at each end.
            var span = Vector3.Distance(walkA, walkB);
            if (span < 0.5f) { Idle(t); return; }
            var s = Mathf.PingPong(t * 1.2f, span) / span;
            var goingB = Mathf.Repeat(t * 1.2f, span * 2) < span;
            var pos = Vector3.Lerp(walkA, walkB, s);
            transform.position = new Vector3(pos.x, transform.position.y, pos.z);
            var fwd = (goingB ? walkB - walkA : walkA - walkB); fwd.y = 0;
            transform.rotation = Quaternion.Slerp(transform.rotation, Quaternion.LookRotation(fwd), 0.1f);
            var phase = Mathf.Sin(t * 1.2f * Mathf.PI * 1.6f);
            Legs(phase * 24f, 0);
            Arm(false, new Vector3(-0.15f, -0.96f, -phase * 0.3f), new Vector3(-0.05f, -1f, -phase * 0.2f + 0.2f));
            Arm(true, new Vector3(0.15f, -0.96f, phase * 0.3f), new Vector3(0.05f, -1f, phase * 0.2f + 0.2f));
            Bend(spine, 3f, 0);
        }

        // ---------- helpers (character space) ----------
        private void Arm(bool right, Vector3 upperDir, Vector3 foreDir)
        {
            var up = right ? rUpper : lUpper; var fore = right ? rFore : lFore; var hand = right ? rHand : lHand;
            Aim(up, fore, upperDir);
            if (hand != null) Aim(fore, hand, foreDir);
        }

        private void Aim(Transform bone, Transform child, Vector3 localDir)
        {
            if (bone == null || child == null) return;
            var cur = child.position - bone.position;
            if (cur.sqrMagnitude < 1e-6f) return;
            var target = transform.TransformDirection(localDir.normalized);
            bone.rotation = Quaternion.FromToRotation(cur, target) * bone.rotation;
        }

        private void Bend(Transform bone, float forwardDeg, float sideDeg)
        {
            if (bone == null) return;
            bone.rotation = Quaternion.AngleAxis(forwardDeg, transform.right) * Quaternion.AngleAxis(sideDeg, transform.forward) * bone.rotation;
        }

        private void Turn(Transform bone, float yawDeg)
        {
            if (bone != null) bone.rotation = Quaternion.AngleAxis(yawDeg, transform.up) * bone.rotation;
        }

        // Head (and a little neck) toward a world point, clamped to a natural range.
        private void Look(Vector3 world, float weight)
        {
            if (head == null) return;
            var d = transform.InverseTransformDirection(world - head.position);
            var yaw = Mathf.Clamp(Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg, -70f, 70f) * weight;
            var pitch = Mathf.Clamp(-Mathf.Atan2(d.y, new Vector2(d.x, d.z).magnitude) * Mathf.Rad2Deg, -25f, 30f) * weight;
            Turn(neck, yaw * 0.35f); Turn(head, yaw * 0.65f);
            Bend(head, pitch, 0);
        }

        private void Legs(float swingDeg, float kneeBend)
        {
            Bend(lThigh, -swingDeg - kneeBend * 0.5f, 0); Bend(rThigh, swingDeg - kneeBend * 0.5f, 0);
            Bend(lCalf, Mathf.Max(0, swingDeg) * 0.8f + kneeBend, 0); Bend(rCalf, Mathf.Max(0, -swingDeg) * 0.8f + kneeBend, 0);
        }
    }
}
