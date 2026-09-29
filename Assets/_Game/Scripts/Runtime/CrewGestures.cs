using UnityEngine;

namespace Jobsite.Runtime
{
    // Procedural body language for Rocketbox crews (Bip01 rig, no animator controller needed; web-cheap).
    // Poses are expressed in character space (x right, y up, z forward) and re-applied from the rest pose every frame,
    // so nothing drifts. Crew talk: idle variety, wave on approach, look at the learner, nod while listening,
    // explain while answering, point at hazards (hints). Hazard workers: dig / saw / walk loops.
    public sealed class CrewGestures : MonoBehaviour
    {
        public enum Activity { Crew, Idle, Dig, Saw, Walk, TiedOff }

        [SerializeField] private Activity activity = Activity.Crew;
        [SerializeField] private Vector3 walkA, walkB;

        private Transform pelvis, spine, neck, head, lUpper, lFore, lHand, rUpper, rFore, rHand, lThigh, rThigh, lCalf, rCalf;
        private Transform[] bones;
        private Quaternion[] rest;
        private Transform viewer;
        private CrewMember crew;
        private bool waved;
        private float seed;

        // Current one-shot gesture and idle flavour.
        private enum Shot { None, Wave, Point, Explain, Nod }
        private Shot shot; private float shotUntil; private Vector3 pointAt;
        private enum Flavor { Relaxed, HandsOnHips, CrossedArms, WipeBrow, LookAround }
        private Flavor flavor; private float flavorUntil;
        private float blend; // eases pose changes

        public void Configure(Activity a, Vector3 a0 = default, Vector3 a1 = default) { activity = a; walkA = a0; walkB = a1; }

        public void Wave(float seconds = 2.2f) => Play(Shot.Wave, seconds);
        public void Explain(float seconds = 4f) => Play(Shot.Explain, seconds);
        public void Nod(float seconds = 1.6f) => Play(Shot.Nod, seconds);
        public void Point(Vector3 world, float seconds = 3f) { pointAt = world; Play(Shot.Point, seconds); }

        private void Play(Shot s, float seconds) { shot = s; shotUntil = Time.time + seconds; }

        private void Awake()
        {
            Transform Find(string n)
            {
                foreach (var t in GetComponentsInChildren<Transform>(true)) if (t.name == n) return t;
                return null;
            }
            pelvis = Find("Bip01 Pelvis"); spine = Find("Bip01 Spine1") ?? Find("Bip01 Spine"); neck = Find("Bip01 Neck"); head = Find("Bip01 Head");
            lUpper = Find("Bip01 L UpperArm"); lFore = Find("Bip01 L Forearm"); lHand = Find("Bip01 L Hand");
            rUpper = Find("Bip01 R UpperArm"); rFore = Find("Bip01 R Forearm"); rHand = Find("Bip01 R Hand");
            lThigh = Find("Bip01 L Thigh"); rThigh = Find("Bip01 R Thigh"); lCalf = Find("Bip01 L Calf"); rCalf = Find("Bip01 R Calf");
            bones = new[] { pelvis, spine, neck, head, lUpper, lFore, rUpper, rFore, lThigh, rThigh, lCalf, rCalf };
            rest = new Quaternion[bones.Length];
            for (var i = 0; i < bones.Length; i++) if (bones[i] != null) rest[i] = bones[i].localRotation;
            crew = GetComponent<CrewMember>();
            seed = Random.value * 100f;
            flavorUntil = Time.time + Random.Range(4f, 9f);
        }

        private void Start() { var p = FindFirstObjectByType<SitePlayer>(); viewer = p != null ? p.transform : null; }

        private void LateUpdate()
        {
            if (bones == null || lUpper == null) return;
            for (var i = 0; i < bones.Length; i++) if (bones[i] != null) bones[i].localRotation = rest[i];
            var t = Time.time + seed;
            if (Time.time > shotUntil) shot = Shot.None;

            switch (activity)
            {
                case Activity.Dig: Dig(t); return;
                case Activity.Saw: Saw(t); return;
                case Activity.Walk: Walk(t); return;
            }

            // --- crew / idle people ---
            var toViewer = viewer != null ? viewer.position - transform.position : Vector3.zero;
            var near = viewer != null && toViewer.magnitude < 8f;
            if (activity == Activity.Crew && near && !waved) { waved = true; Wave(); }
            var talking = crew != null && crew.IsTalking;
            if (talking && crew.Thinking && shot == Shot.None) Nod(0.8f);
            if (talking && crew.JustAnswered) Explain(Mathf.Clamp(crew.LastReplyLength / 14f, 2.5f, 7f));

            Breathe(t);
            if (Time.time > flavorUntil && !talking)
            {
                flavor = activity == Activity.TiedOff ? Flavor.CrossedArms : (Flavor)Random.Range(0, 5);
                flavorUntil = Time.time + (flavor == Flavor.Relaxed ? Random.Range(6f, 10f) : Random.Range(3f, 5f));
            }

            switch (shot)
            {
                case Shot.Wave:
                    Arm(false, new Vector3(-0.2f, -0.95f, 0.1f), new Vector3(0f, -1f, 0.1f));
                    Arm(true, new Vector3(0.55f, 0.55f, 0.15f), new Vector3(0.15f + Mathf.Sin(t * 9f) * 0.45f, 1f, 0.1f));
                    break;
                case Shot.Point:
                    var dir = transform.InverseTransformDirection((pointAt - (rUpper.position)).normalized);
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
                default:
                    Idle(t);
                    break;
            }
            if (shot == Shot.Nod) Bend(head, Mathf.Sin((Time.time) * 9f) * 7f, 0);
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
            // Saw held low in front of the hips: upper arms mostly down, forearms angled forward-down.
            Arm(false, new Vector3(-0.15f, -0.85f, 0.45f), new Vector3(0.25f, -0.55f + buzz, 0.8f));
            Arm(true, new Vector3(0.15f, -0.85f, 0.45f), new Vector3(-0.2f, -0.6f - buzz, 0.8f));
            Legs(0, 18f);
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
