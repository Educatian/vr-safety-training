using System.Collections;
using System.Linq;
using Jobsite.Core;
using Jobsite.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace Jobsite.PlayTests
{
    // Rain shower -> re-inspection call (EP2) and wind call (EP3): visuals + tablet alert + scoring. Captures/t_weather/.
    public sealed class WeatherPlayTests
    {
        static void Shot(Camera cam, string name)
        {
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_weather"); System.IO.File.WriteAllBytes($"Captures/t_weather/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt);
        }

        static IEnumerator Shift(int episode)
        {
            EpisodeDirector.Selected = Episodes.Get(episode); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            Object.FindFirstObjectByType<ShiftDirector>().Begin();
            yield return null;
        }

        static void Face(SitePlayer p, Vector3 at, Vector3 look)
        {
            var cc = p.GetComponent<CharacterController>(); cc.enabled = false;
            p.transform.position = at; p.transform.LookAt(new Vector3(look.x, at.y, look.z)); cc.enabled = true;
            p.View.transform.localRotation = Quaternion.Euler(4, 0, 0);
        }

        [UnityTest]
        public IEnumerator Ep2_RainThenReinspectionCall_Ep3_WindCall()
        {
            yield return Shift(2);
            var director = Object.FindFirstObjectByType<ShiftDirector>();
            var weather = Object.FindFirstObjectByType<WeatherDirector>();
            var player = Object.FindFirstObjectByType<SitePlayer>();
            Face(player, new Vector3(62f, 0.05f, 20f), new Vector3(66f, 0f, 30f));
            Shot(player.View, "01_ep2_overcast");
            weather.Trigger(0);                             // shower
            yield return new WaitForSeconds(7f);
            Assert.That(weather.Current.Raining, Is.True);
            Shot(player.View, "02_ep2_rain");
            weather.Trigger(1);                             // after the rain: the re-inspection call
            yield return null;
            Assert.That(director.PendingWeather, Is.Not.Null);
            Assert.That(director.MenuOpen, Is.True, "the alert opens the tablet");
            Shot(player.View, "03_ep2_alert");
            var xp = director.Xp;
            var best = director.PendingWeather.Options.ToList().FindIndex(o => o.Quality == 2);
            director.ChooseWeather(best);
            Assert.That(director.Xp - xp, Is.EqualTo(WeatherPlan.BestXp));
            Assert.That(director.PendingWeather, Is.Null);

            // EP1: heat index > 100 °F in full sun -> heat strain symptoms blur the view; shade recovers.
            yield return Shift(1);
            director = Object.FindFirstObjectByType<ShiftDirector>();
            weather = Object.FindFirstObjectByType<WeatherDirector>();
            player = Object.FindFirstObjectByType<SitePlayer>();
            weather.Trigger(0);
            director.ChooseWeather(1);                      // a reminder only: strain builds at full rate
            Face(player, new Vector3(22f, 0.05f, 6f), new Vector3(34f, 1f, 30f));
            Shot(player.View, "06_ep1_heat_clear");
            var heat = player.GetComponent<HeatStrain>();
            heat.SetStrainForTest(0.9f);
            yield return new WaitForSeconds(4f);            // sweat beads build up
            Shot(player.View, "07_ep1_heat_symptoms");
            Assert.That(heat.Strain, Is.GreaterThan(0.85f), "strain keeps building in the sun");
            Face(player, new Vector3(6f, 0.05f, 12f), new Vector3(20f, 1f, 24f));   // trailer shade
            yield return new WaitForSeconds(2f);
            Assert.That(heat.Strain, Is.LessThan(0.9f), "shade recovers");

            yield return Shift(3);
            director = Object.FindFirstObjectByType<ShiftDirector>();
            weather = Object.FindFirstObjectByType<WeatherDirector>();
            player = Object.FindFirstObjectByType<SitePlayer>();
            Face(player, new Vector3(38f, 0.05f, 22f), new Vector3(46f, 4f, 32f));
            weather.Trigger(0);
            yield return new WaitForSeconds(4f);
            Assert.That(director.PendingWeather?.Id, Is.EqualTo("ep3-wind"));
            Shot(player.View, "04_ep3_wind_alert");
            director.ChooseWeather(0);
            yield return new WaitForSeconds(1f);
            Shot(player.View, "05_ep3_wind_dust");
        }
    }
}
