using System.Collections;
using Jobsite.Core;
using Jobsite.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jobsite.PlayTests
{
    // Controller play with a virtual gamepad: left stick walks, A photographs, Y opens the tablet, B closes it.
    public sealed class GamepadTests
    {
        Gamepad pad;

        [TearDown]
        public void Remove() { if (pad != null) InputSystem.RemoveDevice(pad); pad = null; ArcadeMode.Exit(); }

        // Frame counts mean nothing in batch mode (uncapped frame rate): hold a state for real seconds, or one press for a frame.
        static IEnumerator Hold(Gamepad pad, GamepadState state, int frames, float seconds = 0f)
        {
            var until = Time.realtimeSinceStartup + seconds;
            for (var i = 0; i < frames || Time.realtimeSinceStartup < until; i++) { InputSystem.QueueStateEvent(pad, state); yield return null; }
        }

        [UnityTest]
        public IEnumerator Gamepad_WalksActsAndUsesTheTablet()
        {
            ArcadeMode.Active = true; ArcadeMode.Daily = false; ArcadeMode.Seed = 7;
            EpisodeDirector.Selected = Episodes.Get(1); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null; yield return null;
            pad = InputSystem.AddDevice<Gamepad>();
            var d = Object.FindFirstObjectByType<ShiftDirector>();
            var player = Object.FindFirstObjectByType<SitePlayer>();
            Assert.That(d.Current, Is.EqualTo(ShiftDirector.Phase.Shift));

            var start = player.transform.position;
            yield return Hold(pad, new GamepadState { leftStick = new Vector2(0f, 1f) }, 2, 0.8f);
            yield return Hold(pad, new GamepadState(), 2);
            Assert.That(Vector3.Distance(start, player.transform.position), Is.GreaterThan(0.1f), "left stick walks");
            Assert.That(GamepadSupport.Active, Is.True);

            var yaw = player.transform.eulerAngles.y;
            yield return Hold(pad, new GamepadState { rightStick = new Vector2(1f, 0f) }, 2, 0.5f);
            yield return Hold(pad, new GamepadState(), 2);
            Assert.That(Mathf.Abs(Mathf.DeltaAngle(yaw, player.transform.eulerAngles.y)), Is.GreaterThan(5f), "right stick turns");

            yield return Hold(pad, new GamepadState().WithButton(GamepadButton.North), 1);
            yield return Hold(pad, new GamepadState(), 2);
            Assert.That(d.MenuOpen, Is.True, "Y opens the tablet");
            yield return Hold(pad, new GamepadState().WithButton(GamepadButton.East), 1);
            yield return Hold(pad, new GamepadState(), 2);
            Assert.That(d.MenuOpen, Is.False, "B closes it");

            // A = interact: whatever is in front (a photo attempt or the prompt to move closer) leaves a notice.
            var before = d.Notice;
            yield return Hold(pad, new GamepadState().WithButton(GamepadButton.South), 1);
            yield return Hold(pad, new GamepadState(), 2);
            Assert.That(d.Notice, Is.Not.EqualTo(before), "A acts on what's in view");
        }
    }
}
