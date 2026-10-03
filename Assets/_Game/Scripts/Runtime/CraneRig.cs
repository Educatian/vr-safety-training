using System.Linq;
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
        // Rigging pass 2026-10-03: joint axes measured from the model (not assumed import axes). The boom used to luff
        // about local X, which on this import is the boom's own length axis: it rolled instead of rising.
        private Vector3 luffAxis, slewAxis;           // in boom / slew local space
        private Vector3[] teleDir;                    // slide direction in each section's parent space
        // Hydraulic lift cylinder: its own part pivoting on the superstructure, aimed at (and stretched to) a pin on the boom.
        private Transform liftCyl, liftAim;
        private Vector3 liftAnchorOnBoom; private float liftRestLength;

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
            teleDir = new Vector3[teleRest.Length];
            var up = transform.up;
            FixBoomTip();
            var boomDir = boom != null && boomTip != null ? (boomTip.position - boom.position).normalized : transform.forward;
            var flat = Vector3.ProjectOnPlane(boomDir, up); if (flat.sqrMagnitude < 1e-4f) flat = transform.forward;
            var across = Vector3.Cross(up, flat.normalized);         // + about this pitches the boom down
            if (boom != null) luffAxis = boom.InverseTransformDirection(across).normalized;
            if (slew != null) slewAxis = slew.InverseTransformDirection(up).normalized;
            for (var i = 0; i < teleRest.Length; i++)
            {
                teleRest[i] = teleSections[i].localPosition;
                var par = teleSections[i].parent;
                teleDir[i] = par != null ? par.InverseTransformDirection(boomDir).normalized : boomDir;
            }
            RigLiftCylinder();
            PaintNewParts();
        }

        // The builder put BoomTip at the last section's max-X end, assuming the model's +X is the boom direction; the
        // import flips X, so the "tip" sat at the section's heel (mid-boom) and the hook hung from the middle of the boom.
        // Put it at the section end farthest from the boom pivot.
        private void FixBoomTip()
        {
            if (boom == null || boomTip == null || teleSections == null || teleSections.Length == 0) return;
            var r = teleSections[teleSections.Length - 1].GetComponent<Renderer>();
            if (r == null) return;
            var b = r.bounds;
            var dir = b.center - boom.position; dir.y = 0f;
            if (dir.sqrMagnitude < 1e-4f) return;
            dir.Normalize();
            var reach = Mathf.Abs(dir.x) * b.extents.x + Mathf.Abs(dir.z) * b.extents.z;
            boomTip.position = b.center + dir * reach;
        }

        private void RigLiftCylinder()
        {
            if (boom == null || slew == null) return;
            foreach (var tr in GetComponentsInChildren<Transform>(true)) if (tr.name == "LiftCyl") { liftCyl = tr; break; }
            if (liftCyl == null) return;
            var mf = liftCyl.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null) return;
            // Rod end = the corner of the mesh bounds farthest from the cylinder's pivot (its base pin); the web build's
            // meshes are not CPU-readable, so the bounds stand in for the vertices (off by about the rod radius).
            var mb = mf.sharedMesh.bounds;
            var far = liftCyl.position; var best = 0f;
            for (var i = 0; i < 8; i++)
            {
                var c = new Vector3((i & 1) == 0 ? mb.min.x : mb.max.x, (i & 2) == 0 ? mb.min.y : mb.max.y, (i & 4) == 0 ? mb.min.z : mb.max.z);
                var w = liftCyl.TransformPoint(c); var d = (w - liftCyl.position).sqrMagnitude;
                if (d > best) { best = d; far = w; }
            }
            // Pull the corner back onto the cylinder's centre line (the bounds' centre axis through the pivot).
            var axis = (liftCyl.TransformPoint(mb.center) - liftCyl.position);
            if (axis.sqrMagnitude > 1e-4f) { far = liftCyl.position + axis.normalized * Vector3.Dot(far - liftCyl.position, axis.normalized); best = (far - liftCyl.position).sqrMagnitude; }
            liftRestLength = Mathf.Sqrt(best);
            if (liftRestLength < 0.2f) return;
            liftAnchorOnBoom = boom.InverseTransformPoint(far);
            liftAim = new GameObject("LiftCylAim").transform;
            liftAim.SetParent(liftCyl.parent, false);
            liftAim.SetPositionAndRotation(liftCyl.position, Quaternion.LookRotation(far - liftCyl.position, slew.up));
            liftCyl.SetParent(liftAim, true);
        }

        // Parts split out after the scene was built (LiftCyl, CabFrame) carry the FBX's import material, not the
        // site's Tripo material: give them the boom's paint.
        private void PaintNewParts()
        {
            var br = boom != null ? boom.GetComponent<Renderer>() : null;
            if (br == null) return;
            foreach (var tr in GetComponentsInChildren<Transform>(true))
                if (tr.name == "LiftCyl" || tr.name == "CabFrame")
                {
                    var r = tr.GetComponent<Renderer>();
                    if (r != null) r.sharedMaterials = System.Linq.Enumerable.Repeat(br.sharedMaterial, r.sharedMaterials.Length).ToArray();
                }
        }

        // Thursday's pick: the roof beam bundle is already flying when the shift starts. Slew and luff so the boom tip
        // is plumb over the load (and telescope out if it is beyond reach), hang the hook at the sling apex, and let the
        // rig draw the hoist line (the scene's fixed placeholder line went up into the sky over nothing).
        private void Start()
        {
            var load = GameObject.Find("SuspendedBeams");
            if (load == null || !load.activeInHierarchy || boomTip == null || hookBlock == null) return;
            if (Vector3.Distance(load.transform.position, transform.position) > 30f) return;
            var apex = load.transform.position + transform.up * 1.8f;   // sling apex (ThursdaySliceBuilder: 4.4 -> 6.2)
            var up = transform.up;
            for (var pass = 0; pass < 3; pass++)
            {
                var have = Vector3.ProjectOnPlane(boomTip.position - boom.position, up);
                var want = Vector3.ProjectOnPlane(apex - boom.position, up);
                var yaw = Vector3.SignedAngle(have, want, up);
                if (Mathf.Abs(yaw) < 0.05f) break;
                slew.localRotation *= Quaternion.AngleAxis(yaw, slewAxis);
            }
            boom.localRotation = boomRest;
            var reach = (boomTip.position - boom.position);
            var rest = Mathf.Asin(Mathf.Clamp(Vector3.Dot(reach.normalized, up), -1f, 1f)) * Mathf.Rad2Deg;   // tip elevation at rest
            var horizontal = Vector3.ProjectOnPlane(apex - boom.position, up).magnitude;
            // Out of reach at a working luff: run the telescope out (an inner section fills the gap, see TeleTube).
            var needed = horizontal / Mathf.Cos(Mathf.Clamp(30f + rest, 5f, 80f) * Mathf.Deg2Rad);
            teleExtension = Mathf.Clamp(needed - reach.magnitude, 0f, teleMax);
            var len = reach.magnitude + teleExtension;
            boomAngle = Mathf.Clamp(Mathf.Acos(Mathf.Clamp01(horizontal / len)) * Mathf.Rad2Deg - rest, 0f, 80f);
            boom.localRotation = boomRest * Quaternion.AngleAxis(-boomAngle, luffAxis);
            for (var i = 0; i < teleRest.Length; i++)
                teleSections[i].localPosition = teleRest[i] + teleDir[i] * teleExtension * (i + 1) / teleRest.Length;
            TeleTube();
            lineLength = Mathf.Max(1f, Vector3.Dot(boomTip.position - apex, up));
            hookBlock.position = boomTip.position - up * lineLength; hookVelocity = Vector3.zero;
            TrackLiftCylinder();
            // The placeholder line from the scene builder (same root as the load).
            var placeholder = load.transform.parent != null ? load.transform.parent.Find("HoistLine") : null;
            if (placeholder != null) placeholder.gameObject.SetActive(false);
        }

        // The model's telescopic section has no inner tube behind it: when it runs out, a plain box section (the inner
        // stage, slightly smaller than the outer one) bridges the base boom and the section so the boom never shows a gap.
        private Transform teleTube;
        private void TeleTube()
        {
            if (teleSections == null || teleSections.Length == 0 || boom == null) return;
            var sec = teleSections[teleSections.Length - 1];
            var ext = teleExtension;
            if (ext < 0.05f) { if (teleTube != null) teleTube.gameObject.SetActive(false); return; }
            var mf = sec.GetComponent<MeshFilter>();
            if (mf == null || mf.sharedMesh == null || sec.parent != boom) return;
            if (teleTube == null)
            {
                teleTube = GameObject.CreatePrimitive(PrimitiveType.Cube).transform;
                teleTube.name = "TeleInner";
                var c = teleTube.GetComponent<Collider>(); if (c != null) Destroy(c);
                teleTube.SetParent(boom, false);
                var br = boom.GetComponent<Renderer>();
                // Same shader, no texture: the atlas would map onto the cube as camouflage blotches.
                if (br != null) { var m = new Material(br.sharedMaterial) { name = "M_TeleInner" }; m.mainTexture = null; m.color = new Color(0.86f, 0.67f, 0.13f); teleTube.GetComponent<Renderer>().sharedMaterial = m; }
            }
            teleTube.gameObject.SetActive(true);
            var axis = teleDir[teleSections.Length - 1];                     // boom-local slide direction
            var mb = mf.sharedMesh.bounds;                                      // section-local (axes = boom-local at rest)
            var centre = sec.localRotation * mb.center;
            var perp = centre - axis * Vector3.Dot(centre, axis);               // tube centre line, off the pivot line
            var size = sec.localRotation * mb.size; size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
            var rest = teleRest[teleSections.Length - 1];
            var len = ext + 0.8f;                                               // 0.4 m inside each section
            teleTube.localPosition = rest + perp + axis * (ext * 0.5f);
            teleTube.localRotation = Quaternion.LookRotation(axis, Mathf.Abs(Vector3.Dot(axis, Vector3.up)) > 0.9f ? Vector3.right : Vector3.up);
            // Cross-section: the section's size across the axis, a little smaller (it nests inside).
            var right = teleTube.localRotation * Vector3.right; var upL = teleTube.localRotation * Vector3.up;
            var w = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(right.x), Mathf.Abs(right.y), Mathf.Abs(right.z))));
            var h = Mathf.Abs(Vector3.Dot(size, new Vector3(Mathf.Abs(upL.x), Mathf.Abs(upL.y), Mathf.Abs(upL.z))));
            teleTube.localScale = new Vector3(w * 0.78f, h * 0.72f, len);
        }

        private void TrackLiftCylinder()
        {
            if (liftAim == null) return;
            var d = boom.TransformPoint(liftAnchorOnBoom) - liftAim.position;
            if (d.sqrMagnitude < 1e-4f) return;
            liftAim.rotation = Quaternion.LookRotation(d, slew.up);
            liftAim.localScale = new Vector3(1f, 1f, d.magnitude / liftRestLength);   // the rod slides out of the barrel
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
                    case Signal.SwingLeft: slew.localRotation *= Quaternion.AngleAxis(-slewDegPerSec * dt, slewAxis); break;
                    case Signal.SwingRight: slew.localRotation *= Quaternion.AngleAxis(slewDegPerSec * dt, slewAxis); break;
                    case Signal.BoomUp: boomAngle = Mathf.Min(80, boomAngle + luffDegPerSec * dt); break;
                    case Signal.BoomDown: boomAngle = Mathf.Max(0, boomAngle - luffDegPerSec * dt); break;
                    case Signal.Extend: teleExtension = Mathf.Min(teleMax, teleExtension + teleMetersPerSec * dt); break;
                    case Signal.Retract: teleExtension = Mathf.Max(0, teleExtension - teleMetersPerSec * dt); break;
                    case Signal.Hoist: lineLength = Mathf.Max(1f, lineLength - hoistMetersPerSec * dt); break;
                    case Signal.Lower: lineLength = Mathf.Min(40f, lineLength + hoistMetersPerSec * dt); break;
                }
            }

            if (boom != null) boom.localRotation = boomRest * Quaternion.AngleAxis(-boomAngle, luffAxis);
            for (var i = 0; i < teleRest.Length; i++)
                teleSections[i].localPosition = teleRest[i] + teleDir[i] * teleExtension * (i + 1) / teleRest.Length;
            TrackLiftCylinder();
            TeleTube();

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
