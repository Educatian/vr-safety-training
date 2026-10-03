using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace Jobsite.Runtime
{
    // Controller play (public build): left stick walk (click or RT to hurry), right stick look, A interact / press,
    // Y or View = tablet, B = back, LB = site map, Start = pause. In menus the D-pad / stick moves a highlighted
    // selection (UI navigation) and A presses it. The mouse takes over again as soon as it moves.
    public sealed class GamepadSupport : MonoBehaviour
    {
        public static bool Active { get; private set; }      // the last input came from a gamepad
        static GamepadSupport instance;
        GameObject outlined;
        static readonly Color Ring = new Color(1f, .78f, .1f, 1f);

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("GamepadSupport");
            DontDestroyOnLoad(go);
            instance = go.AddComponent<GamepadSupport>();
        }

        void Update()
        {
            var pad = Gamepad.current;
            if (pad != null && (pad.wasUpdatedThisFrame && (pad.leftStick.ReadValue().sqrMagnitude > 0.1f || pad.rightStick.ReadValue().sqrMagnitude > 0.1f
                || pad.buttonSouth.isPressed || pad.buttonNorth.isPressed || pad.buttonEast.isPressed || pad.startButton.isPressed || pad.dpad.ReadValue().sqrMagnitude > 0.1f)))
            {
                if (!Active) { Active = true; EnsureUiActions(); SelectFirst(); }
            }
            var mouse = Mouse.current;
            if (Active && mouse != null && (mouse.delta.ReadValue().sqrMagnitude > 4f || mouse.leftButton.wasPressedThisFrame)) Active = false;
            Highlight();
        }

        // UI navigation needs the module's actions (a module added by an editor script may have none assigned).
        static void EnsureUiActions()
        {
            var m = EventSystem.current != null ? EventSystem.current.GetComponent<InputSystemUIInputModule>() : null;
            if (m != null && (m.actionsAsset == null || m.move == null || m.submit == null)) m.AssignDefaultActions();
        }

        // After a page rebuild (tablet, pause menu, results), put the selection on its first button when on a controller.
        public static void SelectFirst(Transform root = null)
        {
            if (!Active || EventSystem.current == null) return;
            EnsureUiActions();
            Selectable first = null;
            if (root != null) first = root.GetComponentInChildren<Selectable>();
            if (first == null)
            {
                var cur = EventSystem.current.currentSelectedGameObject;
                if (cur != null && cur.activeInHierarchy) return;
                foreach (var s in Selectable.allSelectablesArray) if (s.IsInteractable() && s.gameObject.activeInHierarchy) { first = s; break; }
            }
            if (first != null && !(first is InputField)) EventSystem.current.SetSelectedGameObject(first.gameObject);
        }

        // A visible ring on the selected control (the dark UI's own selected tint is too subtle on a TV).
        void Highlight()
        {
            var sel = Active && EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            if (sel == outlined) return;
            if (outlined != null) { var o = outlined.GetComponent<Outline>(); if (o != null && o.effectColor == Ring) Destroy(o); }
            outlined = null;
            if (sel == null || sel.GetComponent<Graphic>() == null || sel.GetComponent<Outline>() != null) return;
            var ring = sel.AddComponent<Outline>(); ring.effectColor = Ring; ring.effectDistance = new Vector2(4, -4);
            outlined = sel;
        }

        // ---- in-world reads for SitePlayer ----
        public static Vector2 Move => Gamepad.current?.leftStick.ReadValue() ?? Vector2.zero;
        public static bool Hurry => Gamepad.current != null && (Gamepad.current.leftStickButton.isPressed || Gamepad.current.rightTrigger.ReadValue() > 0.5f);
        public static Vector2 Look(float dt)
        {
            var v = Gamepad.current?.rightStick.ReadValue() ?? Vector2.zero;
            v = v.sqrMagnitude < 0.02f ? Vector2.zero : v * v.magnitude;     // dead zone + gentle curve for aiming
            return v * 150f * dt;
        }
        public static bool Pressed(System.Func<Gamepad, ButtonControl> b) => Gamepad.current != null && b(Gamepad.current).wasPressedThisFrame;
    }
}
