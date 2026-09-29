using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;

namespace Jobsite.Runtime
{
    // First-person walker. Drag with the mouse (left or right button) to turn the view, WASD to walk, Shift to hurry.
    // No pointer lock: the cursor stays free for the minimap and tablet. Tab = tablet, E = interact, Esc = pause.
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
        public bool HasLooked { get; private set; }       // HUD tutorial: the learner has dragged the view at least once
        private bool dragging;
        public void Configure(Camera camera, ShiftDirector shift) { view = camera; director = shift; }
        private void Awake() { controller = GetComponent<CharacterController>(); }

        private void Update()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (director == null || DemoAutoplay.Active) return;   // the trailer run drives the camera itself
            if (PauseMenu.Paused) return;
            var touch = MobileControls.Active;
            if (touch)
            {
                if (MobileControls.TakePause()) { PauseMenu.Open(); return; }
                if (MobileControls.TakeTablet()) director.ToggleTablet();
                if (MobileControls.TakeMap()) FindFirstObjectByType<Minimap>()?.Toggle();
            }
            if (keys == null || mouse == null) { if (touch) TouchUpdate(); return; }
            if (keys.escapeKey.wasPressedThisFrame)
            {
                if (director.MenuOpen && !director.Finished && director.Current == ShiftDirector.Phase.Shift) director.ToggleTablet();
                else if (director.TalkingTo != null) director.EndTalk();
                else PauseMenu.Open();
                Release();
                return;
            }
            if (keys.tabKey.wasPressedThisFrame) { director.ToggleTablet(); Release(); }
            if (director.MenuOpen && keys.fKey.wasPressedThisFrame) FirstPersonTablet.ToggleFullView();   // full view / handheld
            if (director.MenuOpen || director.Finished) { Release(); return; }

            // Mouse drag looks (a drag that starts on UI, e.g. the minimap, doesn't).
            if (mouse.leftButton.wasPressedThisFrame || mouse.rightButton.wasPressedThisFrame)
                dragging = EventSystem.current == null || !EventSystem.current.IsPointerOverGameObject();
            if (!mouse.leftButton.isPressed && !mouse.rightButton.isPressed) dragging = false;
            var lookDelta = MobileControls.TakeLook();
            if (dragging)
            {
                var d = mouse.delta.ReadValue();
                lookDelta += d * GameSettings.MouseSensitivity;
                if (d.sqrMagnitude > 4f) HasLooked = true;
            }
            Look(lookDelta);
            // Walk 1.4 m/s, Shift to hurry 2.5 m/s (SiteLayout §2 starting values).
            var axis = new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0)) + MobileControls.Move;
            Walk(axis, keys.leftShiftKey.isPressed);
            if (keys.eKey.wasPressedThisFrame || MobileControls.TakeAct()) director.Interact();
        }

        // Phones/tablets: stick moves, right-side drag looks, ACT interacts (MobileControls).
        private void TouchUpdate()
        {
            if (director.MenuOpen || director.Finished) return;
            Look(MobileControls.TakeLook());
            Walk(MobileControls.Move, false);
            if (MobileControls.TakeAct()) director.Interact();
        }

        private void Look(Vector2 delta)
        {
            transform.Rotate(0, delta.x, 0);
            pitch = Mathf.Clamp(pitch + (GameSettings.InvertY ? delta.y : -delta.y), -75, 75);
            view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
        }

        private void Walk(Vector2 axis, bool hurry)
        {
            axis = Vector2.ClampMagnitude(axis, 1);
            fallSpeed = controller.isGrounded ? -2 : fallSpeed - 18 * Time.deltaTime;
            var speed = hurry ? 2.5f : 1.4f;
            controller.Move((transform.forward * axis.y * speed + transform.right * axis.x * speed + Vector3.up * fallSpeed) * Time.deltaTime);
            if (axis.sqrMagnitude > 0.01f && controller.isGrounded && (stepClock += Time.deltaTime * speed) > 1.05f)
            { stepClock = 0; AudioDirector.Play("step_" + (stepIndex++ % 3), 0.5f); }
            KeepInZone();
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
