using System.Collections.Generic;

namespace Jobsite.Core
{
    // Game-level cinematic opening (first launch, and "Watch intro" on the menu): the place, the people, the role,
    // the legal meaning of "competent person", the pressure, and the loop. One narration line per shot; each line's
    // clip is Resources/Audio/VO/vo_prologue_{i}.wav (i from 1), and the shot lasts as long as its clip.
    public static class Prologue
    {
        public enum ShotKind { Aerial, Crew, Gate, Machine, Foreman, Walk, Title }

        public sealed class Beat
        {
            public readonly ShotKind Shot; public readonly Line Line;
            public Beat(ShotKind shot, string text, float seconds) { Shot = shot; Line = new Line("", text, seconds); }
        }

        public static readonly IReadOnlyList<Beat> Beats = new[]
        {
            new Beat(ShotKind.Aerial, "Autauga County, Alabama. Loblolly Creek is getting a new lift station: a deep trench, a crane pick, a roof and a concrete pour, in five working days.", 9.5f),
            new Beat(ShotKind.Crew, "Eight names on the crew list. Every one of them is counting on someone to see what they have stopped noticing.", 8f),
            new Beat(ShotKind.Gate, "That someone is you. Four years laying pipe, OSHA 30 finished last month, and since Hank Doss retired, the competent person on this job.", 9f),
            new Beat(ShotKind.Machine, "OSHA's definition is short: someone who can identify existing and predictable hazards, and who has the authority to take prompt corrective measures to eliminate them.", 10f),
            new Beat(ShotKind.Foreman, "Authority is the hard part. The foreman is Ray Tillman, the city wants the station running before hurricane season, and every late day costs the company twenty-five hundred dollars.", 10f),
            new Beat(ShotKind.Walk, "So walk the site. Photograph what looks wrong, judge the risk, fix it at the source, and stop the work when you have to.", 8f),
            new Beat(ShotKind.Title, "Nobody gets hurt on your watch. Monday starts at the gate.", 5f),
        };

        public const string SeenKey = "prologue_seen_v1";
    }
}
