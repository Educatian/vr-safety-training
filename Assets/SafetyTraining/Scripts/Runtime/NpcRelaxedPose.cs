using UnityEngine;

namespace SafetyTraining.Runtime
{
    public sealed class NpcRelaxedPose : MonoBehaviour
    {
        enum Gesture { None, Encourage, Celebrate }

        Animator animator;
        Transform head;
        Quaternion headBase;
        Transform leftUpperArm;
        Transform leftLowerArm;
        Transform rightUpperArm;
        Transform rightLowerArm;
        Gesture gesture;
        float gestureStarted;
        float gestureDuration;
        bool genericRig;
        Quaternion baseRotation;
        bool moving;
        bool hasLocomotionController;
        [SerializeField] bool useAuthoredWalk = true;
        float previewMovementPhase = -1f;
        float locomotionPace = 1f;
        Transform leftUpperLeg;
        Transform rightUpperLeg;
        Quaternion leftLegBase;
        Quaternion rightLegBase;

        void Awake()
        {
            animator = GetComponentInChildren<Animator>(true);
            if (animator == null)
                return;

            if (animator.isHuman)
            {
                head = animator.GetBoneTransform(HumanBodyBones.Head);
                leftUpperArm = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
                leftLowerArm = animator.GetBoneTransform(HumanBodyBones.LeftLowerArm);
                rightUpperArm = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
                rightLowerArm = animator.GetBoneTransform(HumanBodyBones.RightLowerArm);
                leftUpperLeg = animator.GetBoneTransform(HumanBodyBones.LeftUpperLeg);
                rightUpperLeg = animator.GetBoneTransform(HumanBodyBones.RightUpperLeg);
                if (leftUpperLeg != null)
                    leftLegBase = leftUpperLeg.localRotation;
                if (rightUpperLeg != null)
                    rightLegBase = rightUpperLeg.localRotation;
            }
            else
            {
                genericRig = true;
                head = FindBone("head");
                leftUpperArm = FindBone("leftupperarm", "lupperarm");
                leftLowerArm = FindBone("leftforearm", "lforearm", "leftlowerarm");
                rightUpperArm = FindBone("rightupperarm", "rupperarm");
                rightLowerArm = FindBone("rightforearm", "rforearm", "rightlowerarm");
                leftUpperLeg = FindBone("leftthigh", "lthigh", "leftupperleg");
                rightUpperLeg = FindBone("rightthigh", "rthigh", "rightupperleg");
                if (leftUpperLeg != null)
                    leftLegBase = leftUpperLeg.localRotation;
                if (rightUpperLeg != null)
                    rightLegBase = rightUpperLeg.localRotation;
            }
            baseRotation = transform.localRotation;
            if (head != null)
                headBase = head.localRotation;
            hasLocomotionController = animator.runtimeAnimatorController != null &&
                                      HasParameter("Moving", AnimatorControllerParameterType.Bool);
        }

        void LateUpdate()
        {
            if (animator == null)
                return;

            if (genericRig)
                ApplyGenericIdle(Time.time);
            // Rocketbox rigs intentionally stay on a deformation-safe procedural gait
            // until each imported walk clip is validated. Even with an Animator
            // Controller present, this LateUpdate pass guarantees visible stepping.
            if (moving || previewMovementPhase >= 0f || !hasLocomotionController || !useAuthoredWalk)
                ApplyWalkCycle(Time.time, previewMovementPhase >= 0f);

            var activeConversation = GetComponent<NpcTalkInteractable>()?.ConversationActive == true;
            var normalized = gesture == Gesture.None
                ? 0f
                : Mathf.Clamp01((Time.time - gestureStarted) / gestureDuration);
            if (gesture != Gesture.None && normalized >= 1f)
            {
                gesture = Gesture.None;
            }

            if (moving)
            {
                gesture = Gesture.None;
            }
            else if (gesture == Gesture.Encourage)
                ApplyEncourage(normalized);
            else if (gesture == Gesture.Celebrate)
                ApplyCelebrate(normalized);
            else if (activeConversation)
                ApplyExplain(Mathf.PingPong(Time.time * 0.7f, 1f));
            else if (!hasLocomotionController)
                ApplyRelaxed(Time.time);

            ApplyHeadMotion(Time.time, activeConversation);
        }

        public bool UsesAuthoredWalk => useAuthoredWalk;

        public void ConfigureLocomotion(bool authoredWalk)
        {
            useAuthoredWalk = authoredWalk;
        }

        public void SetMoving(bool value, float pace = 1f)
        {
            moving = value;
            locomotionPace = Mathf.Clamp(pace, 0.75f, 1.45f);
            if (!value)
            {
                previewMovementPhase = -1f;
                if (animator != null && !animator.enabled)
                    animator.enabled = true;
            }
            if (hasLocomotionController)
            {
                animator.SetBool("Moving", useAuthoredWalk && value);
                animator.speed = useAuthoredWalk && value ? locomotionPace : 1f;
                if (!useAuthoredWalk && !value)
                    animator.Play("Natural Idle", 0, 0f);
            }
        }

        public void PreviewMovementPose(float normalizedTime)
        {
            if (animator == null)
                return;

            moving = true;
            previewMovementPhase = Mathf.Repeat(normalizedTime, 1f);
            if (useAuthoredWalk && hasLocomotionController)
            {
                animator.SetBool("Moving", true);
                animator.Play("Natural Walk", 0, previewMovementPhase);
                animator.Update(0f);
                animator.speed = 0f;
                return;
            }

            if (hasLocomotionController)
            {
                animator.SetBool("Moving", false);
                animator.Play("Natural Idle", 0, 0f);
                animator.Update(0f);
                animator.speed = 0f;
                animator.enabled = false;
            }
            ApplyWalkCycle(Time.time, true);
        }
        public bool IsEncouraging => gesture is Gesture.Encourage or Gesture.Celebrate;

        public void PlayEncouragement(bool siteComplete)
        {
            gesture = siteComplete ? Gesture.Celebrate : Gesture.Encourage;
            gestureStarted = Time.time;
            gestureDuration = siteComplete ? 3.4f : 2.6f;
        }

        public void ReturnToIdle()
        {
            gesture = Gesture.None;
        }


        void ApplyWalkCycle(float time, bool snap = false)
        {
            if (leftUpperLeg == null || rightUpperLeg == null)
                return;
            var cycle = previewMovementPhase >= 0f
                ? previewMovementPhase * Mathf.PI * 2f
                : time * 7f * locomotionPace;
            var stride = moving ? Mathf.Sin(cycle) * 16f : 0f;
            var leftTarget = leftLegBase * Quaternion.Euler(stride, 0f, 0f);
            var rightTarget = rightLegBase * Quaternion.Euler(-stride, 0f, 0f);
            leftUpperLeg.localRotation = snap
                ? leftTarget
                : Quaternion.Slerp(leftUpperLeg.localRotation, leftTarget, 0.22f);
            rightUpperLeg.localRotation = snap
                ? rightTarget
                : Quaternion.Slerp(rightUpperLeg.localRotation, rightTarget, 0.22f);
            if (moving)
            {
                var swing = Mathf.Sin(cycle);
                SetArm(leftUpperArm, leftLowerArm,
                    new Vector3(-0.24f, -0.72f, 0.12f + swing * 0.48f), snap ? 1f : 0.32f);
                SetArm(rightUpperArm, rightLowerArm,
                    new Vector3(0.24f, -0.72f, 0.12f - swing * 0.48f), snap ? 1f : 0.32f);
            }
        }

        void ApplyRelaxed(float time)
        {
            var sway = Mathf.Sin(time * 0.8f) * 0.04f;
            SetArm(leftUpperArm, leftLowerArm, new Vector3(-0.18f - sway, -0.96f, 0.08f));
            SetArm(rightUpperArm, rightLowerArm, new Vector3(0.18f + sway, -0.96f, 0.08f));
        }

        void ApplyExplain(float amount)
        {
            var wave = Mathf.Sin(Time.time * 4f) * 0.08f * Mathf.Clamp01(amount + 0.2f);
            SetArm(leftUpperArm, leftLowerArm, new Vector3(-0.35f, -0.2f + wave, 0.45f));
            SetArm(rightUpperArm, rightLowerArm, new Vector3(0.35f, -0.2f - wave, 0.45f));
        }

        void ApplyEncourage(float amount)
        {
            var lift = Mathf.SmoothStep(0f, 1f, Mathf.Sin(Mathf.Clamp01(amount) * Mathf.PI));
            SetArm(leftUpperArm, leftLowerArm, new Vector3(-0.22f, -0.82f, 0.18f));
            SetArm(rightUpperArm, rightLowerArm,
                Vector3.Lerp(new Vector3(0.18f, -0.96f, 0.08f),
                    new Vector3(0.42f, 0.8f, 0.28f), lift));
        }

        void ApplyCelebrate(float amount)
        {
            var lift = Mathf.SmoothStep(0f, 1f, Mathf.Sin(Mathf.Clamp01(amount) * Mathf.PI));
            SetArm(leftUpperArm, leftLowerArm,
                Vector3.Lerp(new Vector3(-0.18f, -0.96f, 0.08f), new Vector3(-0.55f, 0.72f, 0.32f), lift));
            SetArm(rightUpperArm, rightLowerArm,
                Vector3.Lerp(new Vector3(0.18f, -0.96f, 0.08f), new Vector3(0.55f, 0.72f, 0.32f), lift));
        }

        void SetArm(Transform upperArm, Transform lowerArm, Vector3 direction, float blend = 0.18f)
        {
            if (upperArm == null || lowerArm == null)
                return;

            var upperDirection = lowerArm.position - upperArm.position;
            if (upperDirection.sqrMagnitude < 0.0001f)
                return;
            var targetDirection = transform.TransformDirection(direction).normalized;
            var delta = Quaternion.FromToRotation(upperDirection.normalized, targetDirection);
            upperArm.rotation = Quaternion.Slerp(upperArm.rotation, delta * upperArm.rotation, blend);
        }

        void ApplyHeadMotion(float time, bool talking)
        {
            if (head == null)
                return;
            var yaw = Mathf.Sin(time * (talking ? 1.7f : 0.45f)) * (talking ? 8f : 3f);
            var pitch = Mathf.Sin(time * 0.65f) * 2f;
            var animatedRotation = animator != null && animator.enabled ? head.localRotation : headBase;
            head.localRotation = Quaternion.Slerp(animatedRotation,
                animatedRotation * Quaternion.Euler(pitch, yaw, 0f), 0.32f);
        }

        void ApplyGenericIdle(float time)
        {
            var gestureWave = Mathf.Sin(time * 0.8f);
            var turn = gestureWave * 4f;
            var lean = Mathf.Sin(time * 1.2f) * 1.5f;
            transform.localRotation = Quaternion.Slerp(transform.localRotation,
                baseRotation * Quaternion.Euler(lean, turn, 0f), 0.08f);
        }

        Transform FindBone(params string[] fragments)
        {
            foreach (var candidate in GetComponentsInChildren<Transform>(true))
            {
                var normalized = candidate.name.ToLowerInvariant()
                    .Replace(" ", string.Empty)
                    .Replace("_", string.Empty)
                    .Replace(":", string.Empty);
                foreach (var fragment in fragments)
                    if (normalized.Contains(fragment))
                        return candidate;
            }
            return null;
        }

        bool HasParameter(string parameterName, AnimatorControllerParameterType type)
        {
            foreach (var parameter in animator.parameters)
                if (parameter.name == parameterName && parameter.type == type)
                    return true;
            return false;
        }
    }
}
