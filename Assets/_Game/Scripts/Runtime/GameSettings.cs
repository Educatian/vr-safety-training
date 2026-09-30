using UnityEngine;

namespace Jobsite.Runtime
{
    // Player settings + roster sign-in, persisted per browser (PlayerPrefs -> IndexedDB on WebGL).
    public static class GameSettings
    {
        public static float MasterVolume { get => PlayerPrefs.GetFloat("set_master", 0.8f); set { PlayerPrefs.SetFloat("set_master", value); Apply(); } }
        public static float VoiceVolume { get => PlayerPrefs.GetFloat("set_voice", 1f); set => PlayerPrefs.SetFloat("set_voice", value); }
        public static float MouseSensitivity { get => PlayerPrefs.GetFloat("set_mouse", 0.13f); set => PlayerPrefs.SetFloat("set_mouse", value); }
        public static bool InvertY { get => PlayerPrefs.GetInt("set_invert", 0) == 1; set => PlayerPrefs.SetInt("set_invert", value ? 1 : 0); }
        public static int Quality { get => PlayerPrefs.GetInt("set_quality", 1); set { PlayerPrefs.SetInt("set_quality", value); Apply(); } }     // 0 low, 1 medium, 2 high
        public static float TextScale { get => PlayerPrefs.GetFloat("set_text", 1f); set => PlayerPrefs.SetFloat("set_text", value); }             // 1, 1.25, 1.5
        public static int Guidance { get => PlayerPrefs.GetInt("set_guidance", -1); set => PlayerPrefs.SetInt("set_guidance", value); }   // -1 auto by level, 0 off, 1 light, 2 full
        public static bool Tutorial { get => PlayerPrefs.GetInt("set_tutorial", 1) == 1; set => PlayerPrefs.SetInt("set_tutorial", value ? 1 : 0); }
        // Accessibility (quality review 2026-09-30, areas 10/11): no camera motion the learner didn't cause (heat-strain
        // sway, cinematic dollies become cuts), and text captions for sound-only cues.
        public static bool ReduceMotion { get => PlayerPrefs.GetInt("set_reduce_motion", 0) == 1; set => PlayerPrefs.SetInt("set_reduce_motion", value ? 1 : 0); }
        public static bool SoundCaptions { get => PlayerPrefs.GetInt("set_sound_captions", 1) == 1; set => PlayerPrefs.SetInt("set_sound_captions", value ? 1 : 0); }
        // Crew lines spoken by the browser voice (web build), pitch/rate following each speaker's trust/stress.
        public static bool CrewVoices { get => PlayerPrefs.GetInt("set_crew_voices", 1) == 1; set => PlayerPrefs.SetInt("set_crew_voices", value ? 1 : 0); }
        // AI chat consent: -1 not asked, 0 declined (offline answers), 1 agreed.
        public static int AiConsent { get => PlayerPrefs.GetInt("set_ai_consent", -1); set => PlayerPrefs.SetInt("set_ai_consent", value); }
        // Research participation (IRB): play events reach the course server only after an explicit opt-in.
        // -1 not asked, 0 declined, 1 agreed. Completion codes (the course record) work either way.
        public static int ResearchConsent { get => PlayerPrefs.GetInt("set_research_consent", -1); set => PlayerPrefs.SetInt("set_research_consent", value); }
        public const string ConsentVersion = "cp-research-v1";
        // Instructor / facilitator switch: open every episode regardless of the mastery gate.
        public static bool UnlockAll { get => PlayerPrefs.GetInt("set_unlock_all", 0) == 1; set => PlayerPrefs.SetInt("set_unlock_all", value ? 1 : 0); }
        // Automated play tests run silent (never persisted).
        public static bool ForceMute;
        // Pseudonymous roster: class code from the instructor + a student ID they assign. Never a real name.
        public static string ClassCode { get => PlayerPrefs.GetString("roster_class", ""); set => PlayerPrefs.SetString("roster_class", Clean(value)); }
        public static string LearnerId { get => PlayerPrefs.GetString("roster_learner", ""); set => PlayerPrefs.SetString("roster_learner", Clean(value)); }

        static string Clean(string s)
        {
            var t = (s ?? "").Trim().ToUpperInvariant();
            return t.Length > 24 ? t.Substring(0, 24) : t;
        }

        public static void Apply()
        {
            AudioListener.volume = ForceMute ? 0f : MasterVolume;
            var levels = QualitySettings.names.Length;
            if (levels > 0) QualitySettings.SetQualityLevel(Mathf.Clamp(Mathf.RoundToInt(Quality / 2f * (levels - 1)), 0, levels - 1), true);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot() => Apply();
    }
}
