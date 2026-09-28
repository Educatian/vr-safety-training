using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Jobsite.Runtime
{
    // First-person walker. Click the view to capture the mouse (browser pointer lock); Esc releases it and pauses.
    // Holding the right button also looks around, for trackpads and anyone who prefers it. Tab = tablet, E = interact.
    [RequireComponent(typeof(CharacterController))]
    public sealed class SitePlayer : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [SerializeField] private ShiftDirector director;
        private CharacterController controller;
        private float pitch;
        private float fallSpeed;
        private float stepClock;
        private int stepIndex;
        public Camera View => view;
        public bool Captured => Cursor.lockState == CursorLockMode.Locked;
        public void Configure(Camera camera, ShiftDirector shift) { view = camera; director = shift; }
        private void Awake() { controller = GetComponent<CharacterController>(); }

        private void Update()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (keys == null || mouse == null || director == null) return;
            if (PauseMenu.Paused) return;
            if (keys.escapeKey.wasPressedThisFrame)
            {
                if (director.MenuOpen && !director.Finished && director.Current == ShiftDirector.Phase.Shift) director.ToggleTablet();
                else if (director.TalkingTo != null) director.EndTalk();
                else PauseMenu.Open();
                Release();
                return;
            }
            if (keys.tabKey.wasPressedThisFrame) { director.ToggleTablet(); Release(); }
            if (director.MenuOpen || director.Finished) { Release(); return; }

            // Click to capture (ignored when the click lands on UI such as the minimap).
            if (!Captured && mouse.leftButton.wasPressedThisFrame && (EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject()))
            { Cursor.lockState = CursorLockMode.Locked; Cursor.visible = false; }
            if (!Captured) Cursor.visible = !mouse.rightButton.isPressed;
            if (Captured || mouse.rightButton.isPressed)
            {
                var look = mouse.delta.ReadValue() * GameSettings.MouseSensitivity;
                transform.Rotate(0, look.x, 0);
                pitch = Mathf.Clamp(pitch + (GameSettings.InvertY ? look.y : -look.y), -75, 75);
                view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            var axis = new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            axis = Vector2.ClampMagnitude(axis, 1);
            fallSpeed = controller.isGrounded ? -2 : fallSpeed - 18 * Time.deltaTime;
            // Walk 1.4 m/s, Shift to hurry 2.5 m/s (SiteLayout §2 starting values).
            var speed = keys.leftShiftKey.isPressed ? 2.5f : 1.4f;
            controller.Move((transform.forward * axis.y * speed + transform.right * axis.x * speed + Vector3.up * fallSpeed) * Time.deltaTime);
            if (axis.sqrMagnitude > 0.01f && controller.isGrounded && (stepClock += Time.deltaTime * speed) > 1.05f)
            { stepClock = 0; AudioDirector.Play("step_" + (stepIndex++ % 3), 0.5f); }
            KeepInZone();
            if (keys.eKey.wasPressedThisFrame) director.Interact();
        }

        // Today's controlled work zone (per episode): walking out is stopped with a reason, not an invisible wall.
        private float zoneWarnAt;
        private void KeepInZone()
        {
            var z = director.Episode.Zone;
            if (z == null) return;
            var p = transform.position;
            var inside = new Vector3(Mathf.Clamp(p.x, z[0], z[2]), p.y, Mathf.Clamp(p.z, z[1], z[3]));
            if ((inside - p).sqrMagnitude < 1e-4f) return;
            controller.enabled = false; transform.position = inside; controller.enabled = true;
            if (Time.time > zoneWarnAt) { director.Say("Dolores: That's outside today's work zone. Stay with your crews."); zoneWarnAt = Time.time + 5; }
        }

        private static void Release() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
        private void OnDisable() => Release();
    }
}
