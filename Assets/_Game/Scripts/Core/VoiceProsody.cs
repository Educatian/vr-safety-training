using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // Crew voices follow the bounded affect state (quality review area 7: "voice does not follow state").
    // Pure mapping, no audio: each cast member has a base pitch/rate and voice family; stress raises pitch, rate and
    // loudness, low trust flattens and lowers the voice, warmth slows it slightly. The web build speaks the line with
    // the browser's speech synthesis using these numbers (Runtime/CrewVoice); captions are always shown regardless.
    // Coefficients are starting values pending playtest listening sessions.
    public readonly struct Prosody
    {
        public readonly float Pitch;    // Web Speech API range 0..2, 1 = voice default
        public readonly float Rate;     // 0.1..10, 1 = normal
        public readonly float Volume;   // 0..1
        public readonly bool Female;
        public Prosody(float pitch, float rate, float volume, bool female) { Pitch = pitch; Rate = rate; Volume = volume; Female = female; }
        public override string ToString() => $"pitch {Pitch:0.00} rate {Rate:0.00} vol {Volume:0.00} {(Female ? "F" : "M")}";
    }

    public static class VoiceProsody
    {
        public const float MinPitch = 0.5f, MaxPitch = 1.6f, MinRate = 0.75f, MaxRate = 1.45f;

        // Base voice per cast id: (pitch, rate, female). "crew" = an unnamed crew member on the radio.
        static readonly Dictionary<string, (float pitch, float rate, bool female)> Base = new Dictionary<string, (float, float, bool)>
        {
            ["dolores"] = (0.95f, 0.95f, true),
            ["ray"] = (0.72f, 1.02f, false),
            ["marcus"] = (1.00f, 1.05f, false),
            ["luis"] = (0.90f, 1.00f, false),
            ["earl"] = (0.68f, 0.90f, false),
            ["tasha"] = (1.05f, 1.05f, true),
            ["kiara"] = (1.15f, 1.08f, true),
            ["dale"] = (0.84f, 0.96f, false),
            ["crew"] = (0.92f, 1.00f, false),
        };

        public static bool Knows(string id) => id != null && Base.ContainsKey(id);

        // Which affect person drives this speaker: the foreman has his own state, everyone else shares the crew's.
        public static string AffectPerson(string id) => id == "ray" ? CrewAffect.Foreman : CrewAffect.Crew;

        public static Prosody For(string id, float trust, float stress)
        {
            var b = Base.TryGetValue(id ?? "", out var v) ? v : Base["crew"];
            trust = Clamp(trust, -1f, 1f); stress = Clamp(stress, 0f, 1f);
            var distrust = Math.Max(0f, -trust); var warmth = Math.Max(0f, trust);
            var pitch = b.pitch + 0.22f * stress - 0.10f * distrust;
            var rate = b.rate + 0.28f * stress - 0.06f * warmth + 0.04f * distrust;
            var volume = 0.80f + 0.18f * stress - 0.08f * distrust;
            return new Prosody(Clamp(pitch, MinPitch, MaxPitch), Clamp(rate, MinRate, MaxRate), Clamp(volume, 0f, 1f), b.female);
        }

        public static Prosody For(string id, CrewAffect affect)
        {
            var who = AffectPerson(id);
            return affect == null ? For(id, 0f, 0f) : For(id, affect.Trust(who), affect.Stress(who));
        }

        // "Ray: text", "Dolores (radio): text", "Crew (radio): text" -> speaker id + the words to speak.
        // Anything else (instructions, system notices) is not a crew line and is not voiced.
        public static bool TrySplit(string line, out string id, out string text)
        {
            id = null; text = null;
            if (string.IsNullOrWhiteSpace(line)) return false;
            var colon = line.IndexOf(':');
            if (colon <= 0 || colon > 24) return false;
            var head = line.Substring(0, colon).Trim();
            var paren = head.IndexOf('(');
            if (paren > 0) head = head.Substring(0, paren).Trim();
            if (head.Contains(' ')) return false;
            if (head.Equals("Crew", StringComparison.OrdinalIgnoreCase)) id = "crew";
            else
            {
                var c = Cast.All.FirstOrDefault(ch => ch.Name.Split(' ')[0].Equals(head, StringComparison.OrdinalIgnoreCase));
                if (c == null) return false;
                id = c.Id;
            }
            text = line.Substring(colon + 1).Trim();
            return text.Length > 0;
        }

        static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }
}
