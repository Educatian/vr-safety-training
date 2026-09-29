using System;
using System.Collections.Generic;

namespace Jobsite.Core
{
    public enum Sky { Clear, Hazy, Overcast, Storm }

    // Site weather at a moment in the shift (what the CP's weather app shows).
    public sealed class WeatherState
    {
        public float TempF { get; }
        public float Humidity { get; }      // %
        public float WindMph { get; }
        public float GustMph { get; }
        public Sky Sky { get; }
        public bool Raining { get; }
        public bool GroundWet { get; }
        public WeatherState(float tempF, float humidity, float wind, float gust, Sky sky, bool raining = false, bool groundWet = false)
        { TempF = tempF; Humidity = humidity; WindMph = wind; GustMph = gust; Sky = sky; Raining = raining; GroundWet = groundWet; }

        public float HeatIndexF => HeatIndex.Compute(TempF, Humidity);
        public string Summary => $"{TempF:0}°F · RH {Humidity:0}% · heat index {HeatIndexF:0}°F · wind {WindMph:0} G{GustMph:0} mph" + (Raining ? " · rain" : GroundWet ? " · ground wet" : "");
    }

    public static class HeatIndex
    {
        // NWS heat index (Rothfusz regression with the NWS low-temperature form and humidity adjustments).
        public static float Compute(float t, float rh)
        {
            var simple = 0.5f * (t + 61f + (t - 68f) * 1.2f + rh * 0.094f);
            if ((simple + t) / 2f < 80f) return simple;
            var hi = -42.379f + 2.04901523f * t + 10.14333127f * rh - 0.22475541f * t * rh - 0.00683783f * t * t
                     - 0.05481717f * rh * rh + 0.00122874f * t * t * rh + 0.00085282f * t * rh * rh - 0.00000199f * t * t * rh * rh;
            if (rh < 13f && t >= 80f && t <= 112f) hi -= (13f - rh) / 4f * (float)Math.Sqrt((17f - Math.Abs(t - 95f)) / 17f);
            else if (rh > 85f && t >= 80f && t <= 87f) hi += (rh - 85f) / 10f * ((87f - t) / 5f);
            return hi;
        }

        // OSHA-NIOSH Heat Safety Tool risk bands.
        public static string Risk(float hi) => hi < 91 ? "Lower" : hi < 104 ? "Moderate" : hi <= 115 ? "High" : "Very high";
    }

    public sealed class WeatherOption
    {
        public string Text { get; }
        public int Quality { get; }         // 2 best, 1 partial, 0 poor
        public string Feedback { get; }
        public WeatherOption(string text, int quality, string feedback) { Text = text; Quality = quality; Feedback = feedback; }
    }

    // A change in the weather at a point in the shift, optionally asking the competent person for a call.
    public sealed class WeatherEvent
    {
        public string Id { get; }
        public float AtSeconds { get; }
        public WeatherState State { get; }
        public string Radio { get; }
        public string Prompt { get; }
        public string Cfr { get; }
        public IReadOnlyList<WeatherOption> Options { get; }
        public WeatherEvent(string id, float at, WeatherState state, string radio, string prompt = null, string cfr = "", params WeatherOption[] options)
        { Id = id; AtSeconds = at; State = state; Radio = radio; Prompt = prompt; Cfr = cfr; Options = options; }
        public bool IsDecision => Prompt != null && Options.Count > 0;
    }

    public sealed class WeatherPlan
    {
        public WeatherState Start { get; }
        public IReadOnlyList<WeatherEvent> Events { get; }
        public WeatherPlan(WeatherState start, params WeatherEvent[] events) { Start = start; Events = events; }

        public const int BestXp = 40, PartialXp = 10;
        public static int Xp(int quality) => quality >= 2 ? BestXp : quality == 1 ? PartialXp : 0;

        // Alabama week, late July. Starting values tuned for teaching moments, not a forecast.
        public static WeatherPlan For(int episode) => episode switch
        {
            1 => new WeatherPlan(new WeatherState(88, 62, 5, 9, Sky.Hazy),
                new WeatherEvent("ep1-heat", 300, new WeatherState(94, 58, 4, 8, Sky.Clear),
                    "Dolores: It's getting hot fast. Check the heat index on your tablet.",
                    "Heat index just passed 100°F. Two of the laborers started this week. What's your call?",
                    "OSHA Heat NEP (CPL 03-00-024); General Duty Clause",
                    new WeatherOption("Water, rest and shade on a schedule; watch the new hires (acclimatization) and use the buddy system", 2,
                        "Right. New workers are most at risk in their first week. Water, rest, shade, and eyes on each other."),
                    new WeatherOption("Remind everyone to drink water", 1,
                        "A reminder alone is weak. Put water, rest breaks and shade on a schedule, and watch new workers."),
                    new WeatherOption("Keep the schedule; they're used to Alabama heat", 0,
                        "Heat illness hits new and returning workers hardest. Acclimatization and planned breaks prevent it."))),
            2 => new WeatherPlan(new WeatherState(79, 88, 6, 11, Sky.Overcast, groundWet: true),
                new WeatherEvent("ep2-shower", 330, new WeatherState(76, 95, 12, 20, Sky.Storm, raining: true, groundWet: true),
                    "Ray: Just a shower. We'll keep laying pipe."),
                new WeatherEvent("ep2-after-rain", 420, new WeatherState(78, 92, 7, 12, Sky.Overcast, groundWet: true),
                    "Dolores: Rain's past. Before anyone climbs back in that cut...",
                    "The shower's over. Marcus wants back in the trench right now.",
                    "29 CFR 1926.651(k)(1)",
                    new WeatherOption("Re-inspect the excavation, box and spoil before anyone re-enters", 2,
                        "Correct. Inspections are required after every rainstorm or other hazard-increasing event, before work resumes."),
                    new WeatherOption("Let them in, but keep an eye on the walls", 1,
                        "Watching is not inspecting. Rain changes the soil; the competent person inspects before re-entry."),
                    new WeatherOption("Go ahead; the box is already in", 0,
                        "The box protects only where it is. Rain can undermine walls and spoil. Inspect first."))),
            3 => new WeatherPlan(new WeatherState(84, 55, 10, 16, Sky.Clear),
                new WeatherEvent("ep3-wind", 300, new WeatherState(83, 50, 22, 32, Sky.Hazy),
                    "Dolores: Feel that? Front's coming through.",
                    "Gusts are hitting 32 mph. Plywood sheets are loose on the deck, and the boom lift crew is going up.",
                    "Lift: manufacturer rated wind speed (ANSI/SAIA A92.22); falling objects: 29 CFR 1926.501(c)",
                    new WeatherOption("Secure or lower loose sheets and materials, and ground the lift until gusts drop below its rated wind speed", 2,
                        "Right. Wind turns loose sheets into sails and falling objects; lifts are rated for a maximum wind speed (commonly 28 mph)."),
                    new WeatherOption("Tell the crew to be careful up there", 1,
                        "Awareness helps, but loose materials and a lift over its wind rating need physical controls."),
                    new WeatherOption("Keep working; it'll pass", 0,
                        "Gusts above the lift's rating and loose sheets at the edge are how people and objects fall."))),
            _ => new WeatherPlan(new WeatherState(86, 60, 6, 10, Sky.Clear)),
        };
    }
}
