using System.Linq;
using Jobsite.Core;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace Jobsite.Runtime
{
    // The learner's own heat strain (GDD §20): builds in the sun when the heat index is high, eases in shade and
    // at a working water station. Symptoms are shown, not told: blur, tunnel vision, washed-out colour, a slow sway.
    // Dolores names them (heat exhaustion) before they get serious; at the limit she sits you down in the trailer.
    public sealed class HeatStrain : MonoBehaviour
    {
        // Starting values: at heat index 108 °F in full sun, strain goes 0 -> 1 in about 4 minutes.
        const float SecondsToFullAt108 = 240f, ShadeRecover = 0.05f, WaterRecover = 0.25f;
        public static readonly Rect ShadeArea = new Rect(0f, 4f, 13f, 20f);   // along the job trailers (x, z)

        public float Strain { get; private set; }
        public void SetStrainForTest(float v) => Strain = Mathf.Clamp01(v);
        private ShiftDirector director;
        private WeatherDirector weather;
        private SitePlayer player;
        private Volume volume;
        private DepthOfField dof; private Vignette vignette; private ColorAdjustments color;
        private bool warned, collapsed;
        private Quaternion lastSway = Quaternion.identity;
        private SiteCondition water;

        private void Start()
        {
            director = FindFirstObjectByType<ShiftDirector>();
            weather = FindFirstObjectByType<WeatherDirector>();
            player = GetComponent<SitePlayer>();
            water = FindObjectsByType<SiteCondition>(FindObjectsSortMode.None).FirstOrDefault(c => c.Id == "mon-empty-water");
            // QA/demo: ?heat=0.9 starts the shift already heat-strained.
            var url = Application.absoluteURL ?? ""; var i = url.IndexOf("heat=");
            if (i >= 0 && float.TryParse(new string(url.Substring(i + 5).TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray()),
                    System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var h)) Strain = Mathf.Clamp01(h);

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            dof = profile.Add<DepthOfField>(true); dof.mode.Override(DepthOfFieldMode.Gaussian);
            dof.gaussianStart.Override(1.5f); dof.gaussianEnd.Override(12f); dof.gaussianMaxRadius.Override(1.5f);
            vignette = profile.Add<Vignette>(true); vignette.intensity.Override(0.55f); vignette.smoothness.Override(0.9f); vignette.color.Override(new Color(0.25f, 0.05f, 0.02f));
            color = profile.Add<ColorAdjustments>(true); color.saturation.Override(-45f); color.postExposure.Override(0.35f);
            var go = new GameObject("HeatStrainVolume");
            volume = go.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 50; volume.profile = profile; volume.weight = 0;
        }

        private bool WaterWorks =>
            water != null && water.gameObject.activeInHierarchy &&
            (!water.IsHazard || director.Session.GetState(water.Id) == HazardState.Controlled) &&
            Vector3.Distance(transform.position, water.transform.position) < 3f;

        private void Update()
        {
            if (director == null || director.Session == null || weather?.Current == null) return;
            var inShift = director.Current == ShiftDirector.Phase.Shift && !director.MenuOpen && !PauseMenu.Paused;
            if (inShift)
            {
                var hi = weather.Current.HeatIndexF;
                var p = new Vector2(transform.position.x, transform.position.z);
                var shade = ShadeArea.Contains(p) || weather.Current.Sky >= Sky.Overcast;
                var scheduled = director.WeatherCalls.Any(c => c.ev.Id == "ep1-heat" && c.quality == 2);   // breaks on a schedule
                if (WaterWorks) Strain -= WaterRecover * Time.deltaTime;
                else if (shade) Strain -= ShadeRecover * Time.deltaTime;
                else if (hi > 90f) Strain += (hi - 90f) / 18f / SecondsToFullAt108 * (scheduled ? 0.5f : 1f) * Time.deltaTime;
                Strain = Mathf.Clamp01(Strain);

                if (Strain > 0.55f && !warned)
                {
                    warned = true;
                    director.Say("Dolores: You're pale and sweating hard. Dizzy? Headache? That's heat exhaustion starting. Shade and water, now.");
                    director.LogHeat("heat_warning", Strain);
                }
                if (Strain < 0.3f) warned = false;
                if (Strain >= 1f && !collapsed) Collapse();
            }

            // Symptoms fade in from 35 % strain.
            var s = Mathf.InverseLerp(0.35f, 1f, Strain);
            volume.weight = s;
            dof.gaussianStart.value = Mathf.Lerp(6f, 1f, s);
            // Dizziness: a slow sway, swapped in each frame (previous sway removed first, so it never accumulates).
            if (player != null && player.View != null)
            {
                var sway = inShift && s > 0 ? Quaternion.Euler(Mathf.Sin(Time.time * 0.7f) * 1.2f * s, 0, Mathf.Sin(Time.time * 0.5f) * 2.5f * s) : Quaternion.identity;
                var v = player.View.transform;
                v.localRotation = v.localRotation * Quaternion.Inverse(lastSway) * sway;
                lastSway = sway;
            }
        }

        // At the limit: sat down in the air-conditioned trailer. Time passes (the shift clock keeps running).
        private void Collapse()
        {
            collapsed = true;
            var cc = GetComponent<CharacterController>();
            if (cc != null) cc.enabled = false;
            transform.position = new Vector3(6f, 0.05f, 9.5f);
            if (cc != null) cc.enabled = true;
            Strain = 0.3f;
            director.Say("Dolores: Sit. Drink this. You pushed through heat exhaustion; next stop is heat stroke. Water, rest, shade are controls, not favors.");
            director.LogHeat("heat_exhaustion", 1f);
            collapsed = false;
        }
    }
}
