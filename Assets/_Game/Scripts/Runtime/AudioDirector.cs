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

        // Sound captions (GameSettings.SoundCaptions): sound-only cues as short bracketed text, bottom left, ~2.5 s.
        static readonly Dictionary<string, string> Captions = new Dictionary<string, string>
        {
            ["radio"] = "[radio chirp]", ["shutter"] = "[camera shutter]", ["alarm"] = "[alarm sounding]",
            ["success"] = "[confirmation chime]", ["thunder"] = "[thunder]", ["whistle"] = "[stop-work whistle]",
        };
        private UnityEngine.UI.Text captionText;
        private float captionUntil;
        public static string LastCaption { get; private set; } = "";

        private void ShowCaption(string text)
        {
            LastCaption = text;
            if (captionText == null)
            {
                var go = new GameObject("SoundCaptions", typeof(Canvas), typeof(UnityEngine.UI.CanvasScaler));
                var c = go.GetComponent<Canvas>(); c.renderMode = RenderMode.ScreenSpaceOverlay; c.sortingOrder = 400;
                var sc = go.GetComponent<UnityEngine.UI.CanvasScaler>(); sc.uiScaleMode = UnityEngine.UI.CanvasScaler.ScaleMode.ScaleWithScreenSize;
                sc.referenceResolution = new Vector2(1920, 1080) / GameSettings.TextScale; sc.matchWidthOrHeight = 1;
                go.transform.SetParent(transform, false);
                var t = new GameObject("Caption", typeof(RectTransform), typeof(UnityEngine.UI.Text), typeof(UnityEngine.UI.Outline));
                t.transform.SetParent(go.transform, false);
                var r = (RectTransform)t.transform; r.anchorMin = new Vector2(0.04f, 0.11f); r.anchorMax = new Vector2(0.5f, 0.15f); r.offsetMin = r.offsetMax = Vector2.zero;
                captionText = t.GetComponent<UnityEngine.UI.Text>();
                captionText.font = Resources.Load<Font>("Fonts/BarlowCondensed-SemiBold") ?? Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                captionText.fontSize = 26; captionText.color = new Color(1f, 1f, 1f, 0.9f); captionText.raycastTarget = false; captionText.alignment = TextAnchor.MiddleLeft;
            }
            captionText.text = text; captionText.enabled = true; captionUntil = Time.unscaledTime + 2.5f;
        }

        private void LateUpdate()
        {
            if (captionText != null && captionText.enabled && Time.unscaledTime > captionUntil) captionText.enabled = false;
        }

        public static void Play(string name, float volume = 1f)
        {
            if (instance == null) instance = new GameObject("AudioDirector").AddComponent<AudioDirector>();
            var clip = instance.Clip(name);
            if (clip != null) instance.ui.PlayOneShot(clip, volume);
            // Sound captions: sound-only cues also appear as text (the clicks are feedback for a visible press, skip them).
            if (GameSettings.SoundCaptions && Captions.TryGetValue(name, out var caption)) instance.ShowCaption(caption);
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
