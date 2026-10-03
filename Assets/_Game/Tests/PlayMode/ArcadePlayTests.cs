using System.Collections;
using System.Linq;
using Jobsite.Core;
using Jobsite.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace Jobsite.PlayTests
{
    // Hazard Hunt in the real scene: straight into the round, compressed clock, live score, results + share card,
    // and no course progress written. Captures: Captures/t_arcade/ (HUD in the round, results page).
    public sealed class ArcadePlayTests
    {
        const int Daily = 9001;

        static IEnumerator Load(int episode)
        {
            ArcadeMode.Active = true; ArcadeMode.Daily = true; ArcadeMode.DailyNumber = Daily; ArcadeMode.Seed = 12345; ArcadeMode.Replay = false;
            EpisodeDirector.Selected = Episodes.Get(episode); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null; yield return null;
        }

        [TearDown]
        public void Reset()
        {
            ArcadeMode.Exit();
            foreach (var k in new[] { "arcade_daily_first_" + Daily, "arcade_daily_best_" + Daily }) PlayerPrefs.DeleteKey(k);
        }

        static void Shot(string name)
        {
            var cam = Object.FindFirstObjectByType<SitePlayer>().View;
            var rt = new RenderTexture(1600, 900, 24); var old = cam.targetTexture; cam.targetTexture = rt;
            Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_arcade"); System.IO.File.WriteAllBytes($"Captures/t_arcade/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = old; Object.Destroy(rt); Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator Round_StartsAtOnce_ScoresRealEvidence_AndEndsWithAShareCard()
        {
            var careerBefore = PlayerPrefs.GetString("career_v1", "");
            yield return Load(1);
            var d = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.That(d.Current, Is.EqualTo(ShiftDirector.Phase.Shift), "no check-in, no briefing");
            Assert.That(d.MenuOpen, Is.False);
            Assert.That(d.Seed, Is.EqualTo(12345), "the daily seed drives the round");
            Assert.That(d.Hints.Tokens, Is.EqualTo(ShiftDirector.ArcadeHints));
            Assert.That(d.GetComponent<ScaffoldCues>().Guidance, Is.EqualTo(ScaffoldCues.Off), "no mission zones in a scored round");

            // The shift clock runs ShiftLength / RealSeconds faster than real time.
            var c0 = d.Session.Clock; var t0 = Time.time;
            yield return new WaitForSeconds(0.6f);
            var rate = (d.Session.Clock - c0) / (Time.time - t0);
            Assert.That(rate, Is.EqualTo(ArcadeRules.TimeScale(ShiftDirector.ShiftLength)).Within(0.35f));

            // Report one real hazard the right way: the live score and the HUD pick it up.
            var hazard = d.Conditions.First(c => c.IsHazard);
            d.Photograph(hazard); yield return null;
            d.Report(hazard.Spec.Energy, hazard.Spec.Probability, hazard.Spec.Severity); yield return null;
            d.ToggleTablet(); yield return new WaitForSeconds(0.3f);
            var live = d.ScoreArcade();
            Assert.That(live.Found, Is.EqualTo(1));
            Assert.That(live.Score, Is.GreaterThan(ArcadeRules.Find), "find + speed + tag + risk");
            var hud = Object.FindFirstObjectByType<Hud>();
            StringAssert.Contains("PTS", hud.ArcadeBarText);
            StringAssert.Contains("FOUND 1/", hud.ArcadeBarText);
            Shot("01_round_hud");

            d.EndShift(); yield return null; yield return null;
            Assert.That(d.Finished, Is.True);
            var r = d.ArcadeResult;
            Assert.That(r, Is.Not.Null);
            Assert.That(r.Found, Is.EqualTo(1));
            Assert.That(r.Cells.Length, Is.EqualTo(d.Conditions.Count(c => c.IsHazard)));
            Assert.That(d.Quiz, Is.Null, "no closing quiz in a round");
            StringAssert.Contains("Daily Site #" + Daily, d.ArcadeShareText);
            StringAssert.Contains("/?daily", d.ArcadeShareText);
            Assert.That(ArcadeMode.FirstScore(Daily), Is.EqualTo(r.Score), "first attempt recorded");
            Assert.That(PlayerPrefs.GetString("career_v1", ""), Is.EqualTo(careerBefore), "arcade never writes course progress");
            var texts = Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text).ToList();
            Assert.That(texts.Any(t => t.Contains("ROUND OVER")), "results page is up");
            Assert.That(texts.Any(t => t.Contains("GRADE " + r.Grade)));
            Assert.That(ShareResult.Share(d.ArcadeShareText), Is.EqualTo(2));
            Assert.That(ShareResult.LastShared, Is.EqualTo(d.ArcadeShareText));
            Shot("02_results");
        }

        [UnityTest]
        public IEnumerator Menu_OffersPlayNow_AboveTheCourse()
        {
            ArcadeMode.Exit();
            EpisodeDirector.Selected = null; ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = false;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var eps = Object.FindFirstObjectByType<EpisodeDirector>();
            Assert.That(eps.Current, Is.EqualTo(EpisodeDirector.State.Menu), "no ?daily in the editor URL: the menu shows");
            var play = GameObject.Find("PlayDaily");
            Assert.That(play, Is.Not.Null);
            Assert.That(play.GetComponent<Button>(), Is.Not.Null);
            Assert.That(play.GetComponentsInChildren<Text>().Any(t => t.text.Contains("Daily Site #" + DailySite.Number(System.DateTime.UtcNow))));
            Assert.That(GameObject.Find("PlayPractice"), Is.Not.Null);
            yield return new WaitForSeconds(0.3f);
            var cam = eps.CinematicCamera;
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_arcade"); System.IO.File.WriteAllBytes("Captures/t_arcade/00_menu.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex);
        }

        [UnityTest]
        public IEnumerator FreeTextSpeakUp_IsScoredByTheRubric()
        {
            var consent = GameSettings.AiConsent; GameSettings.AiConsent = 0;   // offline: no model call in tests
            yield return Load(2);
            var d = Object.FindFirstObjectByType<ShiftDirector>();
            var hazard = d.Conditions.First(c => c.IsHazard);
            d.Photograph(hazard); yield return null;
            d.Report(hazard.Spec.Energy, hazard.Spec.Probability, hazard.Spec.Severity); yield return null;
            d.StopWork(); yield return null;
            Assert.That(d.PendingSpeakUp, Is.EqualTo(hazard.Id), "Ray pushes back after a justified stop");
            d.SpeakUpFreeText("No, it stays stopped until the fix is in. I'll help get it done fast."); yield return null;
            Assert.That(d.PendingSpeakUp, Is.Null);
            Assert.That(d.LastSpeakUpFreeText.Style, Is.EqualTo(SpeakUpStyle.Assertive));
            Assert.That(d.Session.GetEvidence(hazard.Id).SpeakUp, Is.EqualTo(SpeakUpStyle.Assertive));
            StringAssert.StartsWith("Ray:", d.Notice);
            GameSettings.AiConsent = consent;
        }

        [UnityTest]
        public IEnumerator CourseMode_IsUnchanged_WhenArcadeIsOff()
        {
            ArcadeMode.Exit();
            EpisodeDirector.Selected = Episodes.Get(1); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
            var d = Object.FindFirstObjectByType<ShiftDirector>();
            Assert.That(d.Current, Is.EqualTo(ShiftDirector.Phase.CheckIn), "course mode still starts at the gate");
            var c0 = d.Session.Clock;
            yield return new WaitForSeconds(0.3f);
            Assert.That(d.Session.Clock, Is.EqualTo(c0), "the shift clock waits for check-in");
        }
    
        [UnityTest]
        public IEnumerator Menu_Credits_ShowsTheLabAndUniversityLogos()
        {
            ArcadeMode.Exit();
            EpisodeDirector.Selected = null; ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = false;
            EpisodeDirector.OpenTab = EpisodeDirector.Tab.Credits;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null; yield return null;
            Assert.That(EpisodeDirector.OpenTab, Is.EqualTo(EpisodeDirector.Tab.Episodes), "the next menu opens on episodes again");
            var logos = Object.FindObjectsByType<RawImage>(FindObjectsSortMode.None).Where(r => r.name.StartsWith("Logo_")).Select(r => r.name).ToList();
            Assert.That(logos, Does.Contain("Logo_addie_lab"));
            Assert.That(logos, Does.Contain("Logo_ua_coe"));
            var texts = Object.FindObjectsByType<Text>(FindObjectsSortMode.None).Select(t => t.text).ToList();
            Assert.That(texts.Any(t => t.Contains("DEVELOPMENT CREDITS")), Is.True);
            Assert.That(texts.Any(t => t.Contains("Jewoong Moon")), Is.True);
            yield return new WaitForSeconds(0.3f);
            var cam = Object.FindFirstObjectByType<EpisodeDirector>().CinematicCamera;
            var rt = new RenderTexture(1600, 900, 24); cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1600, 900, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1600, 900), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_arcade"); System.IO.File.WriteAllBytes("Captures/t_arcade/03_credits.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt); Object.Destroy(tex);
        }
    }
}
