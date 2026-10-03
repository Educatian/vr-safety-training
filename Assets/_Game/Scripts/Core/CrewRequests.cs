using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // Side missions from the crew (2026-09-30: "more NPC activity missions"). During the shift a named co-worker radios
    // a request: check the water, look at a sign, come talk. Each completes on the same game events as the checklist
    // (MissionStep.Matches), so it is practised by doing the work, never by clicking. Answering is evidence of care for
    // the crew (ACare); a request still open at the whistle is a missed chance. Starting values; tune in playtest.
    public sealed class CrewRequest
    {
        public string Id { get; }
        public string Npc { get; }            // first name as shown on the radio
        public string Line { get; }           // the radio call
        public string DoneLine { get; }       // the thanks when it's done
        public float AtSeconds { get; }       // shift clock when the call comes in
        public int Xp { get; }
        public MissionStep Step { get; }      // what completes it (kind / targets / detail), with its standard and KSA
        public CrewRequest(string id, string npc, float at, string line, string doneLine, MissionStep step, int xp = 40)
        { Id = id; Npc = npc; AtSeconds = at; Line = line; DoneLine = doneLine; Step = step; Xp = xp; }
    }

    public static class CrewRequests
    {
        static MissionStep S(string text, string kind, string[] ids, string cfr, Ksa k, string why) => new MissionStep(text, kind, ids, null, cfr, k, why);

        public static IReadOnlyList<CrewRequest> For(int episode) => episode switch
        {
            1 => new[]
            {
                new CrewRequest("req-mon-water", "Dolores", 170f,
                    "Dolores: It's pushing 94. Can you check the water station by the trailer? Crew's been hitting it hard.",
                    "Dolores: Thanks. Water and shade are the first line in this heat.",
                    S("Check the crew's water station", "photo", new[] { "mon-empty-water" }, "OSH Act 5(a)(1)", Ksa.ACare,
                      "Heat illness starts with an empty cooler; a CP checks water, rest and shade.")),
            },
            2 => new[]
            {
                new CrewRequest("req-tue-board", "Marcus", 150f,
                    "Marcus: Hey CP, nobody's signed the inspection board today. Can you take a look before we go back down?",
                    "Marcus: Appreciate it. Now we know somebody actually looked.",
                    S("Look at the trench inspection board", "photo", new[] { "tue-cp-inspection" }, "1926.651(k)(1)", Ksa.AThorough,
                      "Daily inspection by a competent person before work starts, and after anything that changes the trench.")),
                new CrewRequest("req-tue-swing", "Luis", 330f,
                    "Luis: That excavator swings real close to the walkway. Is that okay?",
                    "Luis: Good to know. I'll keep the guys off that side.",
                    S("Check the walkway by the excavator", "photo", new[] { "tue-swing-radius" }, "", Ksa.ACare,
                      "Workers who ask are telling you where the exposure is; answer them.")),
            },
            3 => new[]
            {
                new CrewRequest("req-wed-talk", "Tasha", 200f,
                    "Tasha: Dolores wants to go over the tie-off plan with you. Swing by her when you can.",
                    "Tasha: Good. Same page on anchors and edges now.",
                    S("Talk to Dolores about the tie-off plan", "radio_query_open", new[] { "Dolores" }, "1926.502(d)", Ksa.SCommunicate,
                      "A plan the crew leader agrees with is a plan the crew follows.")),
            },
            4 => new[]
            {
                new CrewRequest("req-thu-skylight", "Kiara", 240f,
                    "Kiara: One of the roofers says a skylight up top isn't covered. Worth a look when you're on the roof.",
                    "Kiara: Thanks for checking. Skylights catch people every year.",
                    S("Check the roof skylights", "photo", new[] { "thu-open-skylight", "thu-skylight-screened" }, "1926.501(b)(4)", Ksa.ACare,
                      "A tip from the crew is a free inspection; follow it up.")),
            },
            _ => new[]
            {
                new CrewRequest("req-fri-sign", "Dale", 160f,
                    "Dale: Supposed to be a line warning sign at the pump. Can you make sure it's actually posted?",
                    "Dale: Great. My operator reads that sign every setup.",
                    S("Check the overhead-line warning sign", "photo", new[] { "fri-line-sign" }, "1926.1408(a)(2)", Ksa.AThorough,
                      "Posted clearance warnings keep the operator's attention on the line.")),
            },
        };

        public static IEnumerable<CrewRequest> All => Episodes.All.SelectMany(e => For(e.Number));
    }
}
