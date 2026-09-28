using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Site soundscape (Resources/Audio, synthesized by Tools/audio/synth.py): ambience bed, 3D equipment loops
    // that tell you where the energy is (excavator diesel, saw whine, generator hum, backup alarm), and UI one-shots.
    public sealed class AudioDirector : MonoBehaviour
    {
        private static AudioDirector instance;
        private readonly Dictionary<string, AudioClip> clips = new Dictionary<string, AudioClip>();
        private AudioSource ui;

        public static void Play(string name, float volume = 1f)
        {
            if (instance == null) instance = new GameObject("AudioDirector").AddComponent<AudioDirector>();
            var clip = instance.Clip(name);
            if (clip != null) instance.ui.PlayOneShot(clip, volume);
        }

        private AudioClip Clip(string name)
        {
            if (!clips.TryGetValue(name, out var c)) clips[name] = c = Resources.Load<AudioClip>("Audio/" + name);
            return c;
        }

        private void Awake()
        {
            instance = this;
            ui = gameObject.AddComponent<AudioSource>(); ui.spatialBlend = 0; ui.playOnAwake = false;
        }

        private void Start()
        {
            var bed = gameObject.AddComponent<AudioSource>();
            bed.clip = Clip("ambience"); bed.loop = true; bed.volume = 0.35f; bed.spatialBlend = 0; bed.Play();
            // Equipment loops sit on the equipment the learner can see this day.
            foreach (var rig in FindObjectsByType<ExcavatorRig>(FindObjectsSortMode.None)) Loop(rig.transform, "engine_loop", 0.9f, 6, 60);
            foreach (var v in FindObjectsByType<VehicleController>(FindObjectsSortMode.None).Where(v => v.name.Contains("Dump"))) Loop(v.transform, "backup_loop", 0.5f, 5, 45);
            foreach (var c in FindObjectsByType<SiteCondition>(FindObjectsSortMode.None))
            {
                if (c.Id == "tue-dry-cutting" && c.IsHazard) Loop(c.transform, "saw_loop", 0.7f, 3, 35);
                if (c.Id.StartsWith("mon-") && c.Energy == Jobsite.Core.EnergySource.Electrical) Loop(c.transform, "generator_loop", 0.35f, 2, 18);
            }
        }

        private static void Loop(Transform at, string clip, float volume, float min, float max)
        {
            var s = at.gameObject.AddComponent<AudioSource>();
            s.clip = Resources.Load<AudioClip>("Audio/" + clip); s.loop = true; s.volume = volume;
            s.spatialBlend = 1; s.rolloffMode = AudioRolloffMode.Linear; s.minDistance = min; s.maxDistance = max;
            s.dopplerLevel = 0; s.time = Random.Range(0f, 1f) * (s.clip != null ? s.clip.length : 0); s.Play();
        }
    }
}
