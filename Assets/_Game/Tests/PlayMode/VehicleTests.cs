using Jobsite.Core;
using System.Collections;
using System.IO;
using Jobsite.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jobsite.PlayTests
{
    // GDD §17: walk to the pickup, E opens the door and seats the player, the truck drives, E exits.
    public sealed class VehicleTests
    {
        [UnityTest]
        public IEnumerator Pickup_EnterDriveExit()
        {
            EpisodeDirector.Selected = Episodes.Get(1); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var player = Object.FindFirstObjectByType<SitePlayer>();
            var truck = Object.FindFirstObjectByType<VehicleController>();
            Assert.That(truck, Is.Not.Null, "pickup vehicle missing from scene");
            director.Begin();
            yield return new WaitForSeconds(0.5f); // let suspension settle

            // Stand 2 m outside the driver door and look at it.
            var door = truck.transform.Find("SM_CrewPickup_Rig").GetComponentInChildren<BoxCollider>(true);
            var doorPos = GameObject.Find("DoorInteract").transform.position;
            var outward = doorPos - truck.transform.position; outward.y = 0; outward.Normalize();
            var cc = player.GetComponent<CharacterController>(); cc.enabled = false;
            player.transform.position = new Vector3(doorPos.x, 0.05f, doorPos.z) + outward * 1.6f;
            player.transform.rotation = Quaternion.LookRotation(-outward);
            player.View.transform.LookAt(doorPos);
            cc.enabled = true; Physics.SyncTransforms();
            Capture(player, "01_at_door");

            director.Interact();
            yield return new WaitForSeconds(0.65f);
            Capture(player, "02_door_open");
            yield return new WaitForSeconds(0.8f);
            Assert.That(truck.Current, Is.EqualTo(VehicleController.State.Seated));
            var eye = player.View.transform;
            var bottom = truck.GetComponentInChildren<BoxCollider>().bounds.min.y;
            Assert.That(eye.position.y, Is.GreaterThan(bottom + 1.0f), "driver eye must be in the cab, not under the chassis");
            Assert.That(Mathf.Abs(Vector3.Dot(eye.forward, Vector3.up)), Is.LessThan(0.5f), "driver should look out the windshield");
            Assert.That(Vector3.Dot(eye.forward, truck.transform.forward), Is.GreaterThan(0.7f), "driver faces the truck's forward");
            Capture(player, "03_seated");

            var start = truck.transform.position;
            truck.SendMessage("DebugThrottle", 1f, SendMessageOptions.DontRequireReceiver);
            for (var t = 0f; t < 3f; t += Time.deltaTime) yield return null;
            truck.SendMessage("DebugThrottle", 0f, SendMessageOptions.DontRequireReceiver);
            var moved = Vector3.Distance(start, truck.transform.position);
            Assert.That(moved, Is.GreaterThan(1f), "truck should drive forward under throttle");
            Capture(player, "04_driving");

            truck.Interact(player);
            yield return new WaitForSeconds(1.4f);
            Assert.That(truck.Current, Is.EqualTo(VehicleController.State.Parked));
            Assert.That(player.enabled, Is.True);
        }

        static void Capture(SitePlayer player, string name)
        {
            var cam = player.View; var rt = new RenderTexture(1600, 900, 24);
            cam.targetTexture = rt; cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            Directory.CreateDirectory("Captures/t_vehicle"); File.WriteAllBytes($"Captures/t_vehicle/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt);
        }
    }
}
