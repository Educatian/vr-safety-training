using System.Collections;
using System.Linq;
using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Scripted ~30 s trailer run for recording (web build with ?demo=1 only; normal play never sees it).
    // Menu -> EP4 cold open -> gate check-in + briefing -> crane site: mission cue, photo + instrument readings,
    // report with KSA feedback, engineered fix carried and set down -> suspended load: stop work, crew reacts ->
    // closing KSA profile. Drives the same public ShiftDirector actions a player uses.
    public sealed class DemoAutoplay : MonoBehaviour
    {
        public static bool Active { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (Active || !Application.absoluteURL.Contains("demo=1")) return;
            Active = true;
            GameSettings.Tutorial = false; GameSettings.Guidance = ScaffoldCues.Full; GameSettings.AiConsent = 0;
            ShiftDirector.SampleHazards = false;    // fixed answer key: the scripted hazards must be the real ones
            var go = new GameObject("DemoAutoplay"); DontDestroyOnLoad(go);
            go.AddComponent<DemoAutoplay>().StartCoroutine(go.GetComponent<DemoAutoplay>().Run());
        }

        // Segment markers for the video captions (Web/qa/record_demo.mjs reads them from the console).
        static void Mark(string label) => Debug.Log("[Demo] mark " + label);

        static IEnumerator Wait(float s) { for (var t = 0f; t < s; t += Time.unscaledDeltaTime) yield return null; }

        IEnumerator Run()
        {
            Mark("Competent Person · construction safety serious game");
            yield return Wait(2.5f);                                    // title menu
            EpisodeDirector.SkipIntro = false;
            EpisodeDirector.Play(Episodes.Get(4));
            yield return null; yield return null;
            Mark("Episode 4 · The Pick");
            yield return Wait(2.8f);                                    // cold open
            FindFirstObjectByType<EpisodeDirector>()?.Skip();
            ShiftDirector d = null; SitePlayer p = null;
            for (var i = 0; i < 300 && (d == null || p == null); i++) { d = FindFirstObjectByType<ShiftDirector>(); p = FindFirstObjectByType<SitePlayer>(); yield return null; }
            yield return Wait(0.8f);

            // Gate: walk up to Dolores (she waves), check in, briefing on the tablet.
            Mark("Gate check-in: sign in, PPE");
            var dolores = CrewGestures.Named("Dolores");
            var dp = dolores != null ? dolores.transform.position : p.transform.position + p.transform.forward * 6f;
            yield return Glide(p, p.transform.position, dp + (p.transform.position - dp).normalized * 3.2f, dp + Vector3.up * 1.6f, dp + Vector3.up * 1.5f, 2.4f);
            foreach (CheckInStation.Kind k in System.Enum.GetValues(typeof(CheckInStation.Kind))) { d.CheckIn(k); yield return Wait(0.3f); }
            yield return Wait(0.5f);
            Mark("Briefing: hierarchy of controls + toolbox quiz");
            yield return Wait(0.4f);
            d.SubmitHierarchy(HierarchyOrdering.Correct); yield return Wait(0.9f);
            while (d.Quiz != null && !d.Quiz.Done) { d.AnswerQuiz(d.Quiz.Current.Correct); yield return Wait(0.8f); }
            d.Begin(); yield return Wait(0.3f);

            // Crane site: the pick, Kiara signalling, mission diamond over the outrigger.
            Mark("Mission: pre-lift crane check");
            yield return Glide(p, new Vector3(38f, 0.05f, 36f), new Vector3(40f, 0.05f, 38.5f), new Vector3(48f, 4f, 50f), new Vector3(53.2f, 0.6f, 49.6f), 3f);

            var outrigger = d.Conditions.FirstOrDefault(c => c.Id == "thu-outrigger-no-mat");
            if (outrigger != null)
            {
                yield return Glide(p, new Vector3(57.2f, 0.05f, 52.8f), new Vector3(56.5f, 0.05f, 52f), new Vector3(53.2f, 0.4f, 49.6f), new Vector3(53.2f, 0.2f, 49.6f), 1.2f);
                Mark("Inspect + measure: penetrometer, laser");
                d.Photograph(outrigger); yield return Wait(0.6f);
                foreach (var g in d.Instruments.ToList()) if (g != GearId.FieldNotebook && outrigger.Reading(g) != null) { d.Measure(g); yield return Wait(0.5f); }   // penetrometer + laser readings
                var s = outrigger.Spec;
                Mark("Report: energy, risk, OSHA + KSA feedback");
                d.Report(s.Energy, s.Probability, s.Severity); yield return Wait(2.6f);            // feedback + KSA line
                d.Control(s.BestFeasibleControl); yield return Wait(0.6f);
                if (d.PendingInstall == outrigger.Id)
                {
                    Mark("Engineered control: carry the mats, set them down");
                    if (d.KitOptions != null) { yield return Wait(1.0f); d.ChooseKit(d.KitCorrect); }   // which control: mats, not a 2x4
                    d.PickUpKit(); yield return Wait(0.9f);                                        // mats in hand, drop ring
                    d.SetKitDown(outrigger, outrigger.PhotoBounds.center); yield return Wait(1.8f); // installed, crew thumbs-up
                }
                if (d.MenuOpen) d.ToggleTablet();
            }

            // Suspended load: stop the work, the crew reacts.
            var under = d.Conditions.FirstOrDefault(c => c.Id == "thu-under-load");
            if (under != null)
            {
                yield return Glide(p, new Vector3(42.2f, 0.05f, 62f), new Vector3(43f, 0.05f, 61f), new Vector3(47f, 2.6f, 57f), new Vector3(47f, 2.4f, 57f), 1f);
                Mark("Worker under the load: stop work");
                d.Photograph(under); yield return Wait(0.5f);
                d.Report(under.Spec.Energy, under.Spec.Probability, under.Spec.Severity); yield return Wait(1.1f);
                d.StopWork(); yield return Wait(1.6f);                                            // Ray pushes back
                if (d.PendingSpeakUp != null) { d.ChooseSpeakUp(d.SpeakUpOptions.Select((o, k) => (o, k)).First(t => t.o.Style == SpeakUpStyle.Assertive).k); yield return Wait(1.3f); }
                d.ToggleTablet(); yield return Wait(2.2f);                                        // shrug -> hands on hips
            }

            // Close: debrief + KSA profile.
            Mark("Debrief: competent-person KSA profile");
            d.EndShift();
            while (d.Quiz != null && !d.Quiz.Done) { d.AnswerQuiz(d.Quiz.Current.Correct); yield return Wait(0.15f); }
            d.ExplainBack(0);
            yield return Wait(3.2f);
            Debug.Log("[Demo] done");
        }

        // Smooth move + look (smoothstep) that bypasses input; the CharacterController is off while gliding.
        static IEnumerator Glide(SitePlayer p, Vector3 from, Vector3 to, Vector3 lookFrom, Vector3 lookTo, float seconds)
        {
            var cc = p.GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            for (var t = 0f; t <= seconds; t += Time.unscaledDeltaTime)
            {
                var e = Mathf.SmoothStep(0, 1, t / seconds);
                Place(p, Vector3.Lerp(from, to, e), Vector3.Lerp(lookFrom, lookTo, e));
                yield return null;
            }
            Place(p, to, lookTo);
            if (cc != null) cc.enabled = true;
        }

        static void Place(SitePlayer p, Vector3 at, Vector3 look)
        {
            p.transform.position = at;
            var flat = new Vector3(look.x - at.x, 0, look.z - at.z);
            if (flat.sqrMagnitude > 1e-4f) p.transform.rotation = Quaternion.LookRotation(flat);
            var eye = at + Vector3.up * 1.6f;
            p.View.transform.localRotation = Quaternion.Euler(-Mathf.Atan2(look.y - eye.y, flat.magnitude) * Mathf.Rad2Deg, 0, 0);
        }
    }
}
