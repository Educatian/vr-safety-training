using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // Character bible (docs/Characters.md). Fiction; every beat is tied to an episode's learning goal.
    // Talkable NPCs feed Backstory + Voice + the current episode Beat into the LLM persona prompt.
    public sealed class Character
    {
        public string Id { get; }
        public string Name { get; }
        public int Age { get; }
        public string Role { get; }
        public string Backstory { get; }
        public string Want { get; }
        public string Voice { get; }
        public IReadOnlyDictionary<int, string> Beats { get; }   // episode number -> where they are in their arc

        public Character(string id, string name, int age, string role, string backstory, string want, string voice, Dictionary<int, string> beats)
        { Id = id; Name = name; Age = age; Role = role; Backstory = backstory; Want = want; Voice = voice; Beats = beats; }

        public string Beat(int episode) => Beats.TryGetValue(episode, out var b) ? b : "";

        // Persona for the LLM: who they are, how they talk, and where the story is today. Never overrides OSHA facts.
        public string Persona(int episode) =>
            $"{Role}, age {Age}. {Backstory} Wants: {Want} Voice: {Voice} Today: {Beat(episode)}".Trim();
    }

    public static class Cast
    {
        public const string Player = "You are the newly designated competent person: four years laying pipe for this contractor, " +
            "OSHA 30 finished last month, promoted mid-project when Hank Doss retired. Marcus and Luis were your crewmates last week; now they answer to you.";

        public static readonly IReadOnlyList<Character> All = new[]
        {
            new Character("dolores", "Dolores Villanueva", 56, "Corporate site safety manager (CHST), your mentor",
                "Grew up in Mobile, a shipyard welder's daughter. Started as a flagger in 1992, bilingual English/Spanish. " +
                "In 2009 she signed a trench inspection in the morning; it rained at noon and the wall came down on a laborer, Tommy Greer. " +
                "She retires at the end of the year and this is her last project.",
                "to leave behind one person who sees what she sees, then step back.",
                "calm, dry, short sentences; asks questions instead of giving answers; switches to Spanish with Luis; never lectures twice.",
                new Dictionary<int, string>
                {
                    [1] = "first day with the new competent person; she watches more than she talks.",
                    [2] = "rain last night, the kind of morning Tommy died on; quieter and sharper than usual.",
                    [3] = "tests whether you check the boring things (midrails, covers, ladder length) without being told.",
                    [4] = "stays at the trailer on purpose; you run the pick.",
                    [5] = "the capstone; she will not make the call for you.",
                }),
            new Character("ray", "Ray Tillman", 49, "General foreman",
                "Third-generation concrete man from Prattville. The city's lift station has to be online before hurricane season, " +
                "and every late day costs the company $2,500 in liquidated damages. Ray promised his crew the job bonus. " +
                "He is not careless; he believes experience beats paperwork, because it usually has.",
                "to pour Friday and send every one of his people home.",
                "blunt, funny, Southern; pushes back hard, then backs down when a hazard and a fix are named clearly; never apologizes, just fixes it.",
                new Dictionary<int, string>
                {
                    [1] = "sizes you up; thinks the promotion came too early.",
                    [2] = "Marcus is his crew; a stop-work in the trench feels personal and costs him the morning.",
                    [3] = "the missing midrail was his crew's miss; embarrassed, he fixes it himself.",
                    [4] = "backs the signal person over the operator's hurry, the first time he quotes you back.",
                    [5] = "under the power line, he is the one who calls all-stop.",
                }),
            new Character("marcus", "Marcus Bell", 31, "Pipe layer, your old crew partner",
                "Two kids, coaches Little League, the fastest pipe layer on the job. Treats rules as suggestions for other people.",
                "to stay the guy everyone counts on.",
                "jokes first; 'I'll be two minutes'.",
                new Dictionary<int, string> { [2] = "working in the unshored gap past the trench box to finish a joint." }),
            new Character("luis", "Luis Ortega", 38, "Laborer and saw operator",
                "Spanish first, English second; twelve years on concrete crews. The water pump on the saw cart quit this morning.",
                "to keep up without being the reason the crew is late.",
                "polite, careful, quiet in English, animated in Spanish.",
                new Dictionary<int, string> { [2] = "cutting concrete pipe dry because the water feed broke." }),
            new Character("earl", "Earl Whitfield", 61, "Excavator operator",
                "Forty years in the seat. Has never hit anyone and intends to retire that way.",
                "for people to stay out of the swing radius so he can do his job.",
                "slow drawl; 'I can't see behind me. That's why you keep them out.'",
                new Dictionary<int, string> { [2] = "digging with a walkway cut through his swing radius." }),
            new Character("tasha", "Tasha Greene", 34, "Ironworker foreman",
                "Birmingham ironworker, second generation. Always tied off, always checks her crew's gear before they climb.",
                "a deck where nobody has to think about the edge.",
                "precise, confident, zero patience for shortcuts.",
                new Dictionary<int, string>
                {
                    [3] = "tied off at the edge, the good example; she noticed the missing midrail too and wants to see if you do.",
                    [4] = "connecting roof steel as the crane sets it.",
                }),
            new Character("kiara", "Kiara Wells", 27, "Qualified signal person",
                "Earned her rigger and signal qualification last spring; youngest person on the crane crew.",
                "to be trusted with the radio on a big pick.",
                "clear, procedural, uses standard hand signals.",
                new Dictionary<int, string> { [4] = "signaling the roof steel pick while the operator wants to rush it." }),
            new Character("dale", "Dale Pruitt", 45, "Concrete pump operator",
                "Owner-operator of the boom pump; paid by the yard, not by the hour.",
                "to set up fast and pump before the trucks stack up.",
                "friendly, hurried, 'we've done it closer than this'.",
                new Dictionary<int, string> { [5] = "setting up the pump boom near the overhead line." }),
        };

        public static Character Get(string id) => All.First(c => c.Id == id);
    }
}
