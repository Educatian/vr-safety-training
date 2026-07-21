using UnityEngine;

namespace SafetyTraining.Runtime
{
    /// <summary>
    /// Deterministic ambient motion for the fixed tower crane: slow jib slewing and
    /// trolley travel with the suspended load. Purely decorative and score-neutral;
    /// sine-based on Time.time so every session sees the same motion profile.
    /// </summary>
    public sealed class TowerCraneAnimator : MonoBehaviour
    {
        [SerializeField] Transform slewingUnit;
        [SerializeField] Transform trolleyAssembly;
        [SerializeField, Range(0f, 90f)] float slewDegrees = 24f;
        [SerializeField, Min(4f)] float slewPeriodSeconds = 26f;
        [SerializeField, Min(0f)] float trolleyTravelMeters = 2.6f;
        [SerializeField, Min(4f)] float trolleyPeriodSeconds = 15f;

        [SerializeField] bool liftAuthorized = true;

        Quaternion slewBaseRotation;
        Vector3 trolleyBasePosition;
        bool initialized;

        public bool LiftAuthorized => liftAuthorized;

        /// <summary>Gates trolley/load travel on rigging completion; ambient slewing continues.</summary>
        public void SetLiftAuthorized(bool authorized) => liftAuthorized = authorized;

        public void Configure(Transform slew, Transform trolley)
        {
            slewingUnit = slew;
            trolleyAssembly = trolley;
            CaptureBasePose();
        }

        void Awake()
        {
            CaptureBasePose();
        }

        void CaptureBasePose()
        {
            if (slewingUnit != null)
                slewBaseRotation = slewingUnit.localRotation;
            if (trolleyAssembly != null)
                trolleyBasePosition = trolleyAssembly.localPosition;
            initialized = slewingUnit != null;
        }

        void Update()
        {
            if (!initialized)
                return;
            var time = Time.time;
            var slew = Mathf.Sin(time * (2f * Mathf.PI) / slewPeriodSeconds) * slewDegrees;
            slewingUnit.localRotation = slewBaseRotation * Quaternion.Euler(0f, slew, 0f);
            if (trolleyAssembly != null && liftAuthorized)
            {
                var travel = (Mathf.Sin(time * (2f * Mathf.PI) / trolleyPeriodSeconds) * 0.5f + 0.5f) *
                             trolleyTravelMeters;
                trolleyAssembly.localPosition = trolleyBasePosition + new Vector3(travel, 0f, 0f);
            }
        }
    }
}
