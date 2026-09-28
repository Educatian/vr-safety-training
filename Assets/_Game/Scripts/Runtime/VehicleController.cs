using UnityEngine;
using UnityEngine.InputSystem;

namespace Jobsite.Runtime
{
    // Rideable site vehicle (GDD §17): E at the door opens it, enters the cab, closes it; drive with
    // WheelColliders; E again parks, opens and exits. Speed over the haul-road limit is reported.
    [RequireComponent(typeof(Rigidbody))]
    public sealed class VehicleController : MonoBehaviour
    {
        public enum State { Parked, Opening, Seated, Exiting }

        [SerializeField] private Transform door;            // pivot on the hinge axis (local Y)
        [SerializeField] private float doorOpenAngle = 65f; // Blender -60 (right-handed) == Unity +60 (left-handed)
        [SerializeField] private Transform seat;            // driver eye point
        [SerializeField] private Transform exitPoint;
        [SerializeField] private WheelCollider[] steerWheels;
        [SerializeField] private WheelCollider[] driveWheels;
        [SerializeField] private Transform[] wheelVisuals;  // same order as steer + drive
        [SerializeField] private float motorTorque = 2200f;
        [SerializeField] private float brakeTorque = 6000f;
        [SerializeField] private float maxSteer = 32f;
        [SerializeField] private float siteSpeedLimitMph = 8f;

        private const float DoorSeconds = 0.6f;
        private State state = State.Parked;
        private float doorT;
        private SitePlayer driver;
        private Transform driverParent;
        private Quaternion doorClosed;

        public State Current => state;
        public float SpeedMph => GetComponent<Rigidbody>().linearVelocity.magnitude * 2.23694f;
        public bool Speeding => SpeedMph > siteSpeedLimitMph;
        public int SpeedingEvents { get; private set; }
        private bool wasSpeeding;
        private float debugThrottle;

        // Test hook (PlayMode): drive without a keyboard device.
        private void DebugThrottle(float value) => debugThrottle = value;

        public void Configure(Transform doorPivot, Transform seatPoint, Transform exit, WheelCollider[] steer, WheelCollider[] drive, Transform[] visuals)
        {
            door = doorPivot; seat = seatPoint; exitPoint = exit; steerWheels = steer; driveWheels = drive; wheelVisuals = visuals;
        }

        private void Awake()
        {
            if (door != null) doorClosed = door.localRotation;
        }

        // Called by the player's interact (E) when looking at this vehicle's door within reach.
        public void Interact(SitePlayer player)
        {
            if (state == State.Parked) { driver = player; state = State.Opening; doorT = 0; }
            else if (state == State.Seated) { state = State.Exiting; doorT = 0; }
        }

        private void Update()
        {
            switch (state)
            {
                case State.Opening:
                    // Door swings open, player sits, door swings closed.
                    doorT += Time.deltaTime / DoorSeconds;
                    SetDoor(doorT < 1 ? doorT : Mathf.Max(0, 2 - doorT));
                    if (doorT >= 1 && driver != null && driver.transform.parent != seat) Seat(true);
                    if (doorT >= 2) state = State.Seated;
                    break;
                case State.Exiting:
                    doorT += Time.deltaTime / DoorSeconds;
                    SetDoor(doorT < 1 ? doorT : Mathf.Max(0, 2 - doorT));
                    if (doorT >= 1 && driver != null && driver.transform.parent == seat) Seat(false);
                    if (doorT >= 2) { state = State.Parked; driver = null; }
                    break;
                case State.Seated:
                    if (Keyboard.current != null && Keyboard.current.eKey.wasPressedThisFrame) Interact(driver);
                    break;
            }

            if (Speeding && !wasSpeeding) SpeedingEvents++;
            wasSpeeding = Speeding;
        }

        private void FixedUpdate()
        {
            var keys = Keyboard.current;
            var driving = state == State.Seated && keys != null;
            var throttle = driving ? (keys.wKey.isPressed ? 1f : 0f) - (keys.sKey.isPressed ? 1f : 0f) : 0f;
            if (state == State.Seated && debugThrottle != 0f) { throttle = debugThrottle; driving = true; }
            var steer = driving && keys != null ? (keys.dKey.isPressed ? 1f : 0f) - (keys.aKey.isPressed ? 1f : 0f) : 0f;
            var braking = !driving || (keys != null && keys.spaceKey.isPressed && debugThrottle == 0f);
            foreach (var w in steerWheels) w.steerAngle = steer * maxSteer;
            foreach (var w in driveWheels)
            {
                w.motorTorque = throttle * motorTorque;
                w.brakeTorque = braking ? brakeTorque : 0f;
            }
            SyncVisuals();
        }

        private void SetDoor(float t)
        {
            if (door != null) door.localRotation = doorClosed * Quaternion.Euler(0, doorOpenAngle * Mathf.SmoothStep(0, 1, t), 0);
        }

        private void Seat(bool enter)
        {
            var cc = driver.GetComponent<CharacterController>();
            if (enter)
            {
                driverParent = driver.transform.parent;
                if (cc) cc.enabled = false;
                driver.enabled = false;
                // World-space seat: the SeatEye empty carries the FBX axis-conversion rotation, so a local
                // placement put the eye under the chassis looking at the ground.
                driver.transform.SetParent(seat, true);
                driver.transform.SetPositionAndRotation(seat.position - Vector3.up * 1.7f, Quaternion.LookRotation(transform.forward, Vector3.up));
                driver.View.transform.localRotation = Quaternion.identity;
            }
            else
            {
                driver.transform.SetParent(driverParent, true);
                driver.transform.SetPositionAndRotation(exitPoint.position, exitPoint.rotation);
                if (cc) cc.enabled = true;
                driver.enabled = true;
            }
        }

        private void SyncVisuals()
        {
            if (wheelVisuals == null) return;
            var i = 0;
            foreach (var w in steerWheels) Pose(w, i++);
            foreach (var w in driveWheels) Pose(w, i++);
        }

        private void Pose(WheelCollider w, int i)
        {
            if (i >= wheelVisuals.Length || wheelVisuals[i] == null) return;
            w.GetWorldPose(out var p, out var q);
            wheelVisuals[i].SetPositionAndRotation(p, q);
        }
    }
}
