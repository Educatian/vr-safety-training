using UnityEngine;

namespace Jobsite.Runtime
{
    // NPC excavator dig cycle (Tue): dig at the trench, swing to the spoil side, dump, swing back.
    // The counterweight sweeps the swing radius, the struck-by / caught-between hazard a barricade must close off.
    public sealed class ExcavatorRig : MonoBehaviour
    {
        [SerializeField] private Transform house;   // slews about local Y
        [SerializeField] private Transform boom;    // pitch about the rig's lateral axis
        [SerializeField] private Transform stick;
        [SerializeField] private Transform bucket;
        [SerializeField] private Vector3 pitchAxis = Vector3.forward; // rig lateral axis after import
        [SerializeField] private float swingDegrees = 90f;
        [SerializeField] private float cycleSeconds = 18f;           // starting value: a typical trenching cycle
        [SerializeField] private bool running = true;

        private Quaternion h0, b0, s0, k0;

        public bool Running { get => running; set => running = value; }  // stop-work halts the machine
        public float SwingAngle { get; private set; }
        public Transform House => house;   // the rotating superstructure (counterweight swings with it)
        public Transform Boom => boom;

        public void Configure(Transform housePivot, Transform boomPivot, Transform stickPivot, Transform bucketPivot)
        { house = housePivot; boom = boomPivot; stick = stickPivot; bucket = bucketPivot; }

        private void Awake()
        {
            h0 = house.localRotation; b0 = boom.localRotation; s0 = stick.localRotation; k0 = bucket.localRotation;
        }

        private float t;

        private void Update()
        {
            if (!running) return;
            t = (t + Time.deltaTime / cycleSeconds) % 1f;
            // 0-.3 dig, .3-.5 swing out, .5-.6 dump, .6-.8 swing back, .8-1 lower into trench
            float swing = t < .3f ? 0 : t < .5f ? Smooth((t - .3f) / .2f) : t < .6f ? 1 : t < .8f ? 1 - Smooth((t - .6f) / .2f) : 0;
            float dig = t < .3f ? Mathf.Sin(t / .3f * Mathf.PI) : 0;
            float dump = t >= .5f && t < .6f ? Mathf.Sin((t - .5f) / .1f * Mathf.PI) : 0;
            float raise = t >= .3f && t < .8f ? 1 : t >= .8f ? 1 - Smooth((t - .8f) / .2f) : 0;
            SwingAngle = swing * swingDegrees;
            house.localRotation = h0 * Quaternion.AngleAxis(SwingAngle, Vector3.up);
            boom.localRotation = b0 * Quaternion.AngleAxis(-20f * raise + 10f * dig, pitchAxis);
            stick.localRotation = s0 * Quaternion.AngleAxis(30f * dig - 15f * raise, pitchAxis);
            bucket.localRotation = k0 * Quaternion.AngleAxis(60f * dig - 70f * dump, pitchAxis);
        }

        static float Smooth(float x) => x * x * (3 - 2 * x);
    }
}
