using UnityEngine;

namespace Jobsite.Runtime
{
    // NPC excavator dig cycle (Tue): reach out, drag the bucket through the cut, curl, lift while swinging to the spoil
    // side, open the bucket to dump, swing back and lower into the trench. The counterweight sweeps the swing radius,
    // the struck-by / caught-between hazard a barricade must close off.
    // Rigging pass 2026-10-03: the joint axes come from the geometry (slew = the machine's up, luff = horizontal axis
    // across the boom), not from assumed import axes, and the pose is keyframed per joint with eased blends, so the
    // linkage moves like a machine: boom, stick and bucket overlap in time instead of all swinging on one sine.
    public sealed class ExcavatorRig : MonoBehaviour
    {
        [SerializeField] private Transform house;   // slews about the machine's up axis
        [SerializeField] private Transform boom;    // luffs about the horizontal axis across the boom
        [SerializeField] private Transform stick;
        [SerializeField] private Transform bucket;
        [SerializeField] private Vector3 pitchAxis = Vector3.forward; // legacy (unused): axes are measured at Awake
        [SerializeField] private float swingDegrees = 90f;
        [SerializeField] private float cycleSeconds = 18f;           // starting value: a typical trenching cycle
        [SerializeField] private bool running = true;

        private Quaternion h0, b0, s0, k0;
        private Vector3 slewAxis, boomAxis, stickAxis, bucketAxis;    // joint axes in each part's local frame
        private float t;
        private float swingVel;

        public bool Running { get => running; set => running = value; }  // stop-work halts the machine
        public float SwingAngle { get; private set; }
        public float CycleT => t;
        public Transform House => house;   // the rotating superstructure (counterweight swings with it)
        public Transform Boom => boom;

        public void Configure(Transform housePivot, Transform boomPivot, Transform stickPivot, Transform bucketPivot)
        { house = housePivot; boom = boomPivot; stick = stickPivot; bucket = bucketPivot; }

        // Pose keys: (t, swing 0..1, boom, stick, bucket). Degrees relative to the model pose; + boom = lower,
        // + stick = curl in toward the cab, + bucket = curl (close). Starting values, tuned by eye in DemoVideo/Vehicle captures.
        private static readonly float[,] Keys =
        {
            { 0.00f, 0f,  16f, -24f, -32f },   // reached out over the cut, bucket open
            { 0.20f, 0f,  21f,  14f,   8f },   // drag toward the machine
            { 0.30f, 0f,  10f,  24f,  46f },   // curl the load
            { 0.48f, 1f, -18f,  10f,  46f },   // raised clear and swung to the spoil side
            { 0.56f, 1f, -16f, -10f, -48f },   // stick out, bucket opens: dump above the pile
            { 0.62f, 1f, -16f, -12f, -40f },
            { 0.82f, 0f,   2f, -20f, -34f },   // swung back over the trench
            { 1.00f, 0f,  16f, -24f, -32f },
        };

        private void Awake()
        {
            h0 = house.localRotation; b0 = boom.localRotation; s0 = stick.localRotation; k0 = bucket.localRotation;
            var up = transform.up;
            // Boom direction from its pivot to the stick pivot, flattened: the arm points "forward" for the machine.
            var fwd = Vector3.ProjectOnPlane(stick.position - boom.position, up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = Vector3.ProjectOnPlane(bucket.position - house.position, up);
            if (fwd.sqrMagnitude < 1e-4f) fwd = transform.forward;
            var across = Vector3.Cross(up, fwd.normalized);         // + rotation about this pitches forward/down
            slewAxis = house.InverseTransformDirection(up).normalized;
            boomAxis = boom.InverseTransformDirection(across).normalized;
            stickAxis = stick.InverseTransformDirection(across).normalized;
            bucketAxis = bucket.InverseTransformDirection(across).normalized;
            t = Mathf.Repeat(transform.position.x * 0.013f + transform.position.z * 0.007f, 1f);   // machines on one site are out of step
        }

        private void Update()
        {
            if (!running) return;   // halted where it is (stop-work), the way an operator sets it down
            t = (t + Time.deltaTime / cycleSeconds) % 1f;
            Pose(t, out var swing, out var boomDeg, out var stickDeg, out var bucketDeg);
            // The swing eases in and out (heavy superstructure): smooth the keyed value a little more.
            var target = swing * swingDegrees;
            SwingAngle = Mathf.SmoothDamp(SwingAngle, target, ref swingVel, 0.35f, Mathf.Infinity, Time.deltaTime);
            // A dig has a little shake in it while the teeth bite (t 0.05..0.28).
            var bite = t > 0.05f && t < 0.28f ? (Mathf.PerlinNoise(Time.time * 6f, 0.3f) - 0.5f) * 1.6f : 0f;
            Apply(SwingAngle, boomDeg + bite, stickDeg, bucketDeg + bite * 2f);
        }

        // Test / capture hook: jump to a point of the cycle (no smoothing).
        public void SetCycle(float u)
        {
            t = Mathf.Repeat(u, 1f);
            Pose(t, out var swing, out var boomDeg, out var stickDeg, out var bucketDeg);
            SwingAngle = swing * swingDegrees; swingVel = 0f;
            Apply(SwingAngle, boomDeg, stickDeg, bucketDeg);
        }

        private void Apply(float swingDeg, float boomDeg, float stickDeg, float bucketDeg)
        {
            house.localRotation = h0 * Quaternion.AngleAxis(swingDeg, slewAxis);
            boom.localRotation = b0 * Quaternion.AngleAxis(boomDeg, boomAxis);
            stick.localRotation = s0 * Quaternion.AngleAxis(stickDeg, stickAxis);
            bucket.localRotation = k0 * Quaternion.AngleAxis(bucketDeg, bucketAxis);
        }

        // Eased interpolation between the two keys around `u`.
        public static void Pose(float u, out float swing, out float boomDeg, out float stickDeg, out float bucketDeg)
        {
            var n = Keys.GetLength(0);
            var i = 0;
            while (i < n - 2 && u > Keys[i + 1, 0]) i++;
            var a = Keys[i, 0]; var b = Keys[i + 1, 0];
            var k = Smooth(Mathf.Clamp01((u - a) / Mathf.Max(1e-4f, b - a)));
            swing = Mathf.Lerp(Keys[i, 1], Keys[i + 1, 1], k);
            boomDeg = Mathf.Lerp(Keys[i, 2], Keys[i + 1, 2], k);
            stickDeg = Mathf.Lerp(Keys[i, 3], Keys[i + 1, 3], k);
            bucketDeg = Mathf.Lerp(Keys[i, 4], Keys[i + 1, 4], k);
        }

        static float Smooth(float x) => x * x * (3 - 2 * x);
    }
}
