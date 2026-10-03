using UnityEngine;

namespace Jobsite.Runtime
{
    // Rigged mobile/boom-truck crane (GDD §17). Hierarchy: Carrier > Outriggers, Slew > Boom > Tele[] ;
    // hoist line from the boom tip to a hook block that swings as a damped pendulum.
    // Driven by an NPC operator or by signal-person commands (1926.1419 hand signals).
    public sealed class CraneRig : MonoBehaviour
    {
        public enum Signal { Stop, Hoist, Lower, SwingLeft, SwingRight, BoomUp, BoomDown, Extend, Retract }

        [SerializeField] private Transform[] outriggers;     // local +X/-X beams, extend along their own local X
        [SerializeField] private Transform slew;             // rotates about local Y
        [SerializeField] private Transform boom;             // luffs about local X
        [SerializeField] private Transform[] teleSections;   // slide along local Z
        [SerializeField] private Transform boomTip;
        [SerializeField] private Transform hookBlock;
        [SerializeField] private LineRenderer hoistLine;

        [SerializeField] private float outriggerExtent = 2.2f;
        [SerializeField] private float slewDegPerSec = 8f;
        [SerializeField] private float luffDegPerSec = 5f;
        [SerializeField] private float teleMetersPerSec = 0.5f;
        [SerializeField] private float hoistMetersPerSec = 0.6f;
        [SerializeField, Range(0, 80)] private float boomAngle = 10f;
        [SerializeField] private float teleExtension;        // per section, 0..teleMax
        [SerializeField] private float teleMax = 6f;
        [SerializeField] private float lineLength = 4f;

        private float outrigger01;
        private Vector3 hookVelocity;
        private Quaternion boomRest;
        private Vector3[] teleRest;

        public Signal Current { get; set; } = Signal.Stop;
        public bool OutriggersSet => outriggers == null || outriggers.Length == 0 || outrigger01 >= 1f; // model ships with outriggers deployed
        public float BoomAngle => boomAngle;
        public float LineLength => lineLength;
        public Vector3 HookPosition => hookBlock != null ? hookBlock.position : transform.position;
        public Transform Slew => slew;     // the rotating superstructure (counterweight swings with it)
        public Transform Boom => boom;
        public float LoadSwing => hookBlock == null || boomTip == null ? 0 :
            Vector3.Angle(Vector3.down, hookBlock.position - boomTip.position);

        public void Configure(Transform[] beams, Transform slewPivot, Transform boomPivot, Transform[] sections,
            Transform tip, Transform hook, LineRenderer line)
        {
            outriggers = beams; slew = slewPivot; boom = boomPivot; teleSections = sections; boomTip = tip; hookBlock = hook; hoistLine = line;
        }

        private void Awake()
        {
            if (boom != null) boomRest = boom.localRotation;
            teleRest = new Vector3[teleSections?.Length ?? 0];
            for (var i = 0; i < teleRest.Length; i++) teleRest[i] = teleSections[i].localPosition;
        }

        // Outriggers must be set before any boom motion (the rig refuses otherwise, like a load chart interlock).
        public void SetOutriggers(float dt) => outrigger01 = Mathf.MoveTowards(outrigger01, 1f, dt / 4f);

        private void Update()
        {
            var dt = Time.deltaTime;
            for (var i = 0; outriggers != null && i < outriggers.Length; i++)
                outriggers[i].localPosition = new Vector3(Mathf.Sign(outriggers[i].localPosition.x == 0 ? 1 : outriggers[i].localPosition.x) *
                    (0.5f + outrigger01 * outriggerExtent), outriggers[i].localPosition.y, outriggers[i].localPosition.z);

            if (OutriggersSet)
            {
                switch (Current)
                {
                    case Signal.SwingLeft: slew.Rotate(0, -slewDegPerSec * dt, 0, Space.Self); break;
                    case Signal.SwingRight: slew.Rotate(0, slewDegPerSec * dt, 0, Space.Self); break;
                    case Signal.BoomUp: boomAngle = Mathf.Min(80, boomAngle + luffDegPerSec * dt); break;
                    case Signal.BoomDown: boomAngle = Mathf.Max(0, boomAngle - luffDegPerSec * dt); break;
                    case Signal.Extend: teleExtension = Mathf.Min(teleMax, teleExtension + teleMetersPerSec * dt); break;
                    case Signal.Retract: teleExtension = Mathf.Max(0, teleExtension - teleMetersPerSec * dt); break;
                    case Signal.Hoist: lineLength = Mathf.Max(1f, lineLength - hoistMetersPerSec * dt); break;
                    case Signal.Lower: lineLength = Mathf.Min(40f, lineLength + hoistMetersPerSec * dt); break;
                }
            }

            if (boom != null) boom.localRotation = boomRest * Quaternion.Euler(-boomAngle, 0, 0);
            for (var i = 0; i < teleRest.Length; i++)
                teleSections[i].localPosition = teleRest[i] + Vector3.forward * teleExtension * (i + 1) / teleRest.Length;

            SwingHook(dt);
        }

        // Damped pendulum: the hook lags boom motion and swings (the struck-by hazard of a moving load).
        private void SwingHook(float dt)
        {
            if (hookBlock == null || boomTip == null) return;
            var tip = boomTip.position;
            hookVelocity += Physics.gravity * dt;
            hookVelocity *= Mathf.Exp(-0.35f * dt);
            var p = hookBlock.position + hookVelocity * dt;
            var dir = (p - tip).normalized;
            var constrained = tip + dir * lineLength;
            hookVelocity = (constrained - hookBlock.position) / Mathf.Max(dt, 1e-4f);
            hookBlock.position = constrained;
            if (hoistLine != null) { hoistLine.positionCount = 2; hoistLine.SetPosition(0, tip); hoistLine.SetPosition(1, constrained); }
        }
    }
}
