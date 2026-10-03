using System.Collections.Generic;
using System.Linq;
using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Episode weather (Core.WeatherPlan): drives sun/fog/sky, rain and wind-blown dust, tree sway and the weather
    // soundscape, and hands weather decisions (heat, rain re-inspection, wind) to the ShiftDirector at their time.
    public sealed class WeatherDirector : MonoBehaviour
    {
        [SerializeField] private ParticleSystem rain;
        [SerializeField] private ParticleSystem windDust;

        public WeatherState Current { get; private set; }
        private WeatherPlan plan;
        private int next;
        private ShiftDirector director;
        private Light sun;
        private float sunBase = 1.2f;
        private Color sunColorBase = Color.white;
        private float fogEndBase = 400f;
        private readonly List<(Transform t, Quaternion rest, float phase)> trees = new List<(Transform, Quaternion, float)>();
        private AudioSource rainAudio, windAudio;
        private float skyBlend;       // 0 clear .. 1 storm, eased
        private float skyExposureBase = 1f;

        public void Configure(ParticleSystem rainFx, ParticleSystem dustFx) { rain = rainFx; windDust = dustFx; }

        private void Start()
        {
            director = FindFirstObjectByType<ShiftDirector>();
            plan = WeatherPlan.For(director != null ? director.Episode.Number : 1);
            Current = plan.Start;
            sun = RenderSettings.sun;
            if (sun != null) { sunBase = sun.intensity; sunColorBase = sun.color; }
            fogEndBase = RenderSettings.fogEndDistance;
            if (RenderSettings.skybox != null && RenderSettings.skybox.HasProperty("_Exposure"))
            {   // work on a copy so the project asset is never changed at runtime
                RenderSettings.skybox = new Material(RenderSettings.skybox);
                skyExposureBase = RenderSettings.skybox.GetFloat("_Exposure");
            }
            foreach (var r in FindObjectsByType<MeshRenderer>(FindObjectsSortMode.None).Where(r => r.name == "PineImpostor"))
                trees.Add((r.transform, r.transform.rotation, Random.value * 10f));
            rainAudio = Loop("rain_loop"); windAudio = Loop("wind_loop");
            skyBlend = Target(Current);
        }

        private AudioSource Loop(string clip)
        {
            var s = gameObject.AddComponent<AudioSource>();
            s.clip = Resources.Load<AudioClip>("Audio/" + clip); s.loop = true; s.volume = 0; s.spatialBlend = 0;
            if (s.clip != null) s.Play();
            return s;
        }

        static float Target(WeatherState w) => w.Sky switch { Sky.Clear => 0f, Sky.Hazy => 0.25f, Sky.Overcast => 0.65f, _ => 1f };

        // Test hook: jump to an event now.
        public void Trigger(int index) { while (next <= index && next < plan.Events.Count) Fire(plan.Events[next++]); }

        private void Fire(WeatherEvent ev)
        {
            var wasRaining = Current.Raining;
            Current = ev.State;
            director?.WeatherChanged(ev);
            if (Current.Raining && !wasRaining) AudioDirector.Play("thunder", 0.8f);
        }

        private void Update()
        {
            if (plan == null) return;
            var clock = director != null && director.Session != null ? director.Session.Clock : Time.timeSinceLevelLoad;
            // QA/demo: ?wx=now brings each weather event forward to ~5 s into the shift.
            var soon = (Application.absoluteURL ?? "").Contains("wx=now");
            if (next < plan.Events.Count && director != null && director.Current == ShiftDirector.Phase.Shift &&
                clock >= (soon ? 5f + next * 15f : plan.Events[next].AtSeconds))
                Fire(plan.Events[next++]);

            // Ease the look over ~6 s so fronts roll in rather than switch.
            skyBlend = Mathf.MoveTowards(skyBlend, Target(Current), Time.deltaTime / 6f);
            if (sun != null)
            {
                sun.intensity = sunBase * Mathf.Lerp(1f, 0.28f, skyBlend);
                sun.color = Color.Lerp(sunColorBase, new Color(0.78f, 0.82f, 0.9f), skyBlend);
                sun.shadowStrength = Mathf.Lerp(1f, 0.35f, skyBlend);
            }
            RenderSettings.fogEndDistance = Mathf.Lerp(fogEndBase, fogEndBase * 0.35f, skyBlend);
            RenderSettings.ambientIntensity = Mathf.Lerp(1f, 0.75f, skyBlend);
            var sky = RenderSettings.skybox;
            if (sky != null && sky.HasProperty("_Exposure"))
            {
                sky.SetFloat("_Exposure", skyExposureBase * Mathf.Lerp(1f, 0.45f, skyBlend));
                if (sky.HasProperty("_Tint")) sky.SetColor("_Tint", Color.Lerp(new Color(0.5f, 0.5f, 0.5f), new Color(0.42f, 0.44f, 0.46f), skyBlend));
                if (sky.HasProperty("_Saturation")) sky.SetFloat("_Saturation", Mathf.Lerp(1f, 0.25f, skyBlend));
            }
            RenderSettings.fogColor = Color.Lerp(new Color(0.7f, 0.74f, 0.78f), new Color(0.52f, 0.55f, 0.58f), skyBlend);

            // Rain and wind-blown dust follow the player; wind sways the treeline.
            var cam = Camera.main;
            if (cam != null)
            {
                if (rain != null) rain.transform.position = cam.transform.position + Vector3.up * 10f;
                if (windDust != null) windDust.transform.position = cam.transform.position + cam.transform.forward * 8f;
            }
            SetEmission(rain, Current.Raining ? 1400f : 0f);
            SetEmission(windDust, Current.GustMph >= 25 ? (Current.GustMph - 20) * 6f : 0f);
            var gust = Current.WindMph + (Current.GustMph - Current.WindMph) * Mathf.PerlinNoise(Time.time * 0.35f, 0.3f);
            var sway = gust * 0.12f;
            foreach (var (t, rest, phase) in trees)
                t.rotation = rest * Quaternion.Euler(Mathf.Sin(Time.time * 1.3f + phase) * sway * 0.4f, 0, Mathf.Sin(Time.time * 0.9f + phase) * sway);
            if (rainAudio != null) rainAudio.volume = Mathf.MoveTowards(rainAudio.volume, Current.Raining ? 0.55f : 0f, Time.deltaTime * 0.3f);
            if (windAudio != null) windAudio.volume = Mathf.MoveTowards(windAudio.volume, Mathf.Clamp01((gust - 6f) / 30f) * 0.6f, Time.deltaTime * 0.3f);
        }

        static void SetEmission(ParticleSystem ps, float rate)
        {
            if (ps == null) return;
            var e = ps.emission; e.rateOverTime = rate;
            if (rate > 0 && !ps.isPlaying) ps.Play();
        }
    }
}
