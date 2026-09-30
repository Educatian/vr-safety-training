using System.Runtime.InteropServices;
using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Speaks crew lines with the browser's speech synthesis (WebGL only; see Plugins/CrewVoice.jslib), with pitch,
    // rate and loudness from VoiceProsody so a stressed foreman sounds clipped and a trusting crew sounds relaxed.
    // Editor, batch-mode tests and non-web builds never make sound: the call is recorded (Last*, Spoken) and dropped.
    // Captions stay on screen in every case, so nothing is carried by voice alone.
    public static class CrewVoice
    {
#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")] private static extern void CP_Speak(string text, float pitch, float rate, float volume, int female);
        [DllImport("__Internal")] private static extern void CP_StopSpeech();
#endif
        public static string LastId { get; private set; }
        public static string LastText { get; private set; }
        public static Prosody LastProsody { get; private set; }
        public static int Spoken { get; private set; }

        public static bool Enabled => GameSettings.CrewVoices && GameSettings.VoiceVolume > 0.01f;

        // A full "Name: words" line; returns false when it is not a crew line (nothing spoken).
        public static bool SpeakLine(string line, CrewAffect affect)
        {
            if (!VoiceProsody.TrySplit(line, out var id, out var text)) return false;
            Speak(id, text, affect);
            return true;
        }

        public static void Speak(string id, string text, CrewAffect affect)
        {
            if (string.IsNullOrWhiteSpace(text) || !Enabled) return;
            var p = VoiceProsody.For(id, affect);
            LastId = id; LastText = text; LastProsody = p; Spoken++;
#if UNITY_WEBGL && !UNITY_EDITOR
            CP_Speak(text, p.Pitch, p.Rate, Mathf.Clamp01(p.Volume * GameSettings.VoiceVolume), p.Female ? 1 : 0);
#endif
        }

        public static void Stop()
        {
#if UNITY_WEBGL && !UNITY_EDITOR
            CP_StopSpeech();
#endif
        }
    }
}
