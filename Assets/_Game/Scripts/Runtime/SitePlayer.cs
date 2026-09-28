using UnityEngine;
using UnityEngine.InputSystem;

namespace Jobsite.Runtime
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class SitePlayer : MonoBehaviour
    {
        [SerializeField] private Camera view;
        [SerializeField] private ShiftDirector director;
        private CharacterController controller;
        private float pitch;
        private float fallSpeed;
        public Camera View => view;
        public void Configure(Camera camera, ShiftDirector shift) { view = camera; director = shift; }
        private void Awake() { controller = GetComponent<CharacterController>(); }
        private void Update()
        {
            var keys = Keyboard.current;
            var mouse = Mouse.current;
            if (keys == null || mouse == null || director == null) return;
            if (keys.escapeKey.wasPressedThisFrame || keys.tabKey.wasPressedThisFrame) director.ToggleTablet();
            if (director.MenuOpen || director.Finished) { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; return; }
            Cursor.lockState = mouse.rightButton.isPressed ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !mouse.rightButton.isPressed;
            if (mouse.rightButton.isPressed)
            {
                var look = mouse.delta.ReadValue() * .13f;
                transform.Rotate(0, look.x, 0);
                pitch = Mathf.Clamp(pitch - look.y, -75, 75);
                view.transform.localRotation = Quaternion.Euler(pitch, 0, 0);
            }
            var axis = new Vector2((keys.dKey.isPressed ? 1 : 0) - (keys.aKey.isPressed ? 1 : 0),
                (keys.wKey.isPressed ? 1 : 0) - (keys.sKey.isPressed ? 1 : 0));
            axis = Vector2.ClampMagnitude(axis, 1);
            fallSpeed = controller.isGrounded ? -2 : fallSpeed - 18 * Time.deltaTime;
            controller.Move((transform.forward * axis.y * 3.2f + transform.right * axis.x * 3.2f + Vector3.up * fallSpeed) * Time.deltaTime);
            if (keys.eKey.wasPressedThisFrame) director.Interact();
        }
        private void OnDisable() { Cursor.lockState = CursorLockMode.None; Cursor.visible = true; }
    }
}
