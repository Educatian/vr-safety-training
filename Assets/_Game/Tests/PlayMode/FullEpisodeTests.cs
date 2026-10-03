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
    // Plays each episode end to end through every interaction the design intends: check-in -> hierarchy -> toolbox
    // quiz -> shift (photo + instrument readings, report, stop work, control, carry-and-place install, crew talk,
    // access, weather calls) -> closing quiz -> explain-back. Asserts the field mission signs off, every hazard is
    // controlled, KSA evidence covers K, S and A, crews react with gestures and the visual cues respond.
    public sealed class FullEpisodeTests
    {
        static IEnumerator Load(int ep)
        {
            EpisodeDirector.Selected = Episodes.Get(ep); ShiftDirector.SampleHazards = false; EpisodeDirector.SkipIntro = true;
            GameSettings.Guidance = ScaffoldCues.Full;
            yield return SceneManager.LoadSceneAsync("Assets/_Game/Scenes/Jobsite.unity", LoadSceneMode.Single);
            yield return null;
        }

        static void Shot(Camera cam, string name)
        {
            var rt = new RenderTexture(1280, 720, 24); cam.targetTexture = rt; Canvas.ForceUpdateCanvases(); cam.Render(); RenderTexture.active = rt;
            var tex = new Texture2D(1280, 720, TextureFormat.RGB24, false); tex.ReadPixels(new Rect(0, 0, 1280, 720), 0, 0); tex.Apply();
            System.IO.Directory.CreateDirectory("Captures/t_full"); System.IO.File.WriteAllBytes($"Captures/t_full/{name}.png", tex.EncodeToPNG());
            RenderTexture.active = null; cam.targetTexture = null; Object.Destroy(rt);
        }

        static void Stand(SitePlayer p, Vector3 at, Vector3 look)
        {
            var cc = p.GetComponent<CharacterController>(); cc.enabled = false;
            p.transform.position = at; p.transform.LookAt(new Vector3(look.x, at.y, look.z)); cc.enabled = true;
            var eye = at + Vector3.up * 1.6f;
            p.View.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(look.y - eye.y, new Vector2(look.x - eye.x, look.z - eye.z).magnitude) * Mathf.Rad2Deg, 0, 0);
        }

        [UnityTest]
        public IEnumerator Ep1_to_Ep5_PlayThrough()
        {
            for (var ep = 1; ep <= 5; ep++)
            {
                yield return Load(ep);
                var d = Object.FindFirstObjectByType<ShiftDirector>();
                var p = Object.FindFirstObjectByType<SitePlayer>();
                var cues = Object.FindFirstObjectByType<ScaffoldCues>();
                Assert.That(cues, Is.Not.Null, $"EP{ep}: visual cues present");

                // Gate: check in, rank the controls, toolbox quiz.
                foreach (CheckInStation.Kind k in System.Enum.GetValues(typeof(CheckInStation.Kind))) d.CheckIn(k);
                Assert.That(d.Current, Is.EqualTo(ShiftDirector.Phase.Briefing), $"EP{ep}: check-in complete");
                d.SubmitHierarchy(HierarchyOrdering.Correct);
                while (!d.Quiz.Done) d.AnswerQuiz(d.Quiz.Current.Correct);
                d.Begin();
                yield return null;

                // Crew talk (communication) with whoever is on site.
                var ray = Object.FindObjectsByType<CrewMember>(FindObjectsSortMode.None).FirstOrDefault(c => c.DisplayName == "Ray");
                if (ray != null) { d.StartTalk(ray); d.EndTalk(); }

                // Walk every condition: photograph (instruments read), report real hazards, control them.
                foreach (var c in d.Conditions.Where(c => c != null && c.isActiveAndEnabled).ToList())
                {
                    Assert.That(ConditionNames.Has(c.Id), $"EP{ep} {c.Id}: neutral pre-report name authored");
                    d.Photograph(c);
                    // Inspect before the call: measure with every instrument that reads something here.
                    foreach (var g in d.Instruments.ToList()) if (g != GearId.FieldNotebook && c.Reading(g) != null) d.Measure(g);
                    if (!c.IsHazard) { d.ConfirmCompliant(); d.ToggleTablet(); continue; }
                    var s = c.Spec;
                    d.Report(s.Energy, s.Probability, s.Severity);
                    Assert.That(d.Session.GetState(c.Id), Is.EqualTo(HazardState.Reported), $"EP{ep} {c.Id}: report accepted");
                    if (s.RequiresStopWork)
                    {
                        d.StopWork();
                        Assert.That(d.Session.GetState(c.Id), Is.EqualTo(HazardState.Stopped), $"EP{ep} {c.Id}: stop work");
                        var r = CrewGestures.Named("Ray");
                        if (r != null) Assert.That(r.Busy, Is.True, $"EP{ep}: Ray reacts to the stop");
                        // Ray pushes back: hold the stop, firmly and respectfully.
                        Assert.That(d.PendingSpeakUp, Is.EqualTo(c.Id), $"EP{ep} {c.Id}: foreman pushback");
                        d.ChooseSpeakUp(d.SpeakUpOptions.Select((o, k) => (o, k)).First(t => t.o.Style == SpeakUpStyle.Assertive).k);
                        Assert.That(d.Session.GetState(c.Id), Is.EqualTo(HazardState.Stopped), $"EP{ep} {c.Id}: stop held");
                    }
                    d.Control(s.BestFeasibleControl);
                    if (d.PendingInstall == c.Id)
                    {
                        if (d.KitOptions != null)
                        {
                            // A wrong kit fails at the hazard and must be chosen again.
                            var wrong = Enumerable.Range(0, d.KitOptions.Count).First(k => k != d.KitCorrect);
                            d.ChooseKit(wrong); d.PickUpKit(); d.SetKitDown(c, c.PhotoBounds.center);
                            Assert.That(d.Session.GetState(c.Id), Is.EqualTo(HazardState.Installing).Or.EqualTo(HazardState.Stopped), $"EP{ep} {c.Id}: wrong kit rejected");
                            d.ChooseKit(d.KitCorrect);
                        }
                        d.PickUpKit(); Assert.That(d.Carrying); d.SetKitDown(c, c.PhotoBounds.center);
                    }
                    Assert.That(d.Session.GetState(c.Id), Is.EqualTo(HazardState.Controlled).Or.EqualTo(HazardState.Installing).Or.EqualTo(HazardState.Reported),
                        $"EP{ep} {c.Id}: control applied ({d.Notice})");
                    if (d.MenuOpen) d.ToggleTablet();
                }
                yield return new WaitForSeconds(0.6f);   // pins refresh every 0.5 s
                var firstHazard = d.Conditions.First(c => c.IsHazard);
                Assert.That(cues.PinColor(firstHazard.Id), Is.Not.EqualTo(Color.clear), $"EP{ep}: status pin over reported hazard");

                // EP4: stair tower to the roof (access step).
                if (ep == 4) { Stand(p, new Vector3(85.6f, 0.05f, 24f), new Vector3(87.4f, 1.2f, 24f)); yield return null; d.Interact(); yield return null; }

                // Weather: fire the day's plan and make the best call on each decision.
                var wd = Object.FindFirstObjectByType<WeatherDirector>();
                for (var i = 0; i < 6; i++)
                {
                    wd?.Trigger(i); yield return null;
                    if (d.PendingWeather != null)
                    {
                        var best = d.PendingWeather.Options.Select((o, k) => (o, k)).OrderByDescending(t => t.o.Quality).First().k;
                        d.ChooseWeather(best);
                    }
                }

                var run = d.Mission;
                var open = string.Join(", ", run.Mission.Steps.Where((st, i) => !run.IsDone(i)).Select(st => st.Text));
                Assert.That(run.Complete, Is.True, $"EP{ep} mission '{run.Mission.Title}' open steps: {open}");

                Stand(p, p.transform.position, p.transform.position + p.transform.forward * 5f);
                yield return null; Shot(p.View, $"ep{ep}_after");

                // Close: debrief quiz, explain-back.
                d.EndShift();
                while (!d.Quiz.Done) d.AnswerQuiz(d.Quiz.Current.Correct);
                // Toolbox talk: brief the highest-risk findings first and answer the why.
                foreach (var f in d.Findings.OrderByDescending(f => ToolboxTalk.Risk(f.Spec)).Take(ToolboxTalk.Picks).ToList()) d.ToggleTalkPick(f.Id);
                d.SubmitToolboxTalk(d.TalkWhy.Current.Correct);
                Assert.That(d.EpisodeComplete, $"EP{ep} complete");
                Assert.That(d.TalkScore, Is.GreaterThanOrEqualTo(ToolboxTalk.GoodTalk), $"EP{ep}: a good toolbox talk scores as good");
                Assert.That(d.MasteryToday, Is.Not.Null.And.Not.Empty, $"EP{ep}: mastery recorded");
                Assert.That(d.Session.HazardIdentificationIndex, Is.EqualTo(1f).Within(1e-4), $"EP{ep}: all hazards found");
                Assert.That(d.Session.ConfirmedCompliant, Is.EqualTo(d.Conditions.Count(c => c != null && c.isActiveAndEnabled && !c.IsHazard)),
                    $"EP{ep}: every look-alike confirmed compliant");
                foreach (var dom in new[] { 'K', 'S', 'A' })
                    Assert.That(d.Competence.Rows.Any(r => KsaInfo.Domain(r.Ksa) == dom), $"EP{ep}: KSA evidence for {dom}");
                Assert.That(d.Competence.Rows.All(r => r.Cfr.Length > 0),
                    $"EP{ep}: scored actions carry an OSHA standard: " + string.Join(",", d.Competence.Rows.Where(r => r.Cfr.Length == 0).Select(r => r.Source).Distinct()));
                Debug.Log($"[FullEpisode] EP{ep} xp={d.Xp} ksaRows={d.Competence.Rows.Count} mission={run.Completed}/{run.Mission.Steps.Count} " +
                          string.Join(" ", System.Enum.GetValues(typeof(Ksa)).Cast<Ksa>().Where(k => d.Competence.Mean(k).HasValue).Select(k => $"{k}={d.Competence.Mean(k):F2}")));
            }
            GameSettings.Guidance = -1;
        }
    }
}
