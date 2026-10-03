using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jobsite.Core
{
    // Conversation as competency evidence (2026-10-02). The site's talkable people (Dolores, Ray) know things the site
    // doesn't show; the competent person has to ask. Plus three spoken tasks scored by inspectable rubrics: the
    // toolbox talk in your own words, coaching a worker out of an unsafe act, and the daily excavation inspection log.
    // As with the speak-up rubric, rules and authored facts score; a language model (with consent) only voices people.

    public sealed class Clue
    {
        public string Id, Npc, HazardId, Line, AllClear, Lead, Prompt;
        public int Episode;
        public string[][] Groups;          // every group must match (any alternative inside a group)
    }

    public static class CrewInterview
    {
        static Clue C(string id, int ep, string npc, string hazard, string prompt, string line, string allClear, string lead, params string[][] groups) =>
            new Clue { Id = id, Episode = ep, Npc = npc, HazardId = hazard, Prompt = prompt, Line = line, AllClear = allClear, Lead = lead, Groups = groups };
        static string[] G(params string[] alts) => alts;

        public static readonly IReadOnlyList<Clue> All = new[]
        {
            C("mon-clue-cord", 1, "Dolores", "mon-damaged-cord", "Any damaged cords out there?",
                "Dolores: Now that you ask, the skid steer ran over a cord by the spider box this morning. Check the jacket.",
                "Dolores: Cords looked fine on my walk this morning.", "Cord by the spider box was run over this morning (Dolores)",
                G(@"cord", @"extension", @"cable", @"\bplug")),
            C("mon-clue-gfci", 1, "Ray", "mon-no-gfci", "Has anyone tested the temp outlets?",
                "Ray: Electrician hasn't been by since Friday. Nobody's tested those temp outlets, if that's what you're asking.",
                "Ray: Electrician tested the temp outlets first thing.", "Temp outlets not tested since Friday (Ray)",
                G(@"gfci", @"outlet", @"spider ?box", @"temp(orary)? power", @"electric")),
            C("mon-clue-water", 1, "Ray", "mon-empty-water", "How's the crew doing in this heat?",
                "Ray: Cooler ran dry around ten. Haven't had a minute to refill it.",
                "Ray: Water's topped up, they're taking breaks.", "Water cooler ran dry around ten (Ray)",
                G(@"water", @"drink", @"cooler", @"hydrat", @"\bheat", @"\bbreaks?\b", @"shade")),
            C("tue-clue-rain", 2, "Dolores", "tue-no-protective-system", "Did the rain do anything to the trench?",
                "Dolores: It rained hard last night. Luis said the east wall sloughed a chunk at first light. Treat that soil as Type C.",
                "Dolores: Walls held up fine after the rain.", "East wall sloughed after last night's rain (Dolores)",
                G(@"rain", @"\bwet\b", @"water", @"soil", @"\bwalls?\b", @"slough", @"cav(e|ing)", @"crack")),
            C("tue-clue-inspect", 2, "Ray", "tue-cp-inspection", "Who inspected the trench this morning?",
                "Ray: Nobody's signed the inspection yet today. I looked at it yesterday, it was fine.",
                "Ray: Inspection's signed, it's on the board.", "No competent-person inspection signed today (Ray)",
                G(@"inspect", @"checked", @"\bsign(ed)?\b", @"board", @"competent")),
            C("tue-clue-depth", 2, "Ray", "tue-no-protective-system", "How deep are you going today?",
                "Ray: Six feet now, eight by lunch.",
                "Ray: We're staying shallow today.", "Trench is at 6 ft, going to 8 ft by lunch (Ray)",
                G(@"\bdeep", @"depth", @"how far down", @"\bfeet\b", @"\bft\b")),
            C("tue-clue-spoil", 2, "Dolores", "tue-spoil-at-edge", "Where's the spoil going?",
                "Dolores: They've been dumping spoil right at the lip to save a swing.",
                "Dolores: Spoil's going well back from the edge.", "Spoil dumped right at the trench lip (Dolores)",
                G(@"spoil", @"\bpile", @"\bdirt\b", @"\blip\b", @"\bedge")),
            C("wed-clue-hole", 3, "Dolores", "wed-open-hole", "Has anyone moved a hole cover today?",
                "Dolores: The electricians pulled a cover to drop conduit this morning. I'm not sure it went back on.",
                "Dolores: All the deck covers are on and marked.", "A deck hole cover was pulled for conduit (Dolores)",
                G(@"\bholes?\b", @"opening", @"cover", @"penetration", @"floor")),
            C("wed-clue-ladder", 3, "Ray", "wed-short-ladder", "How are guys getting up to the deck?",
                "Ray: That ladder to the deck is a rung short. Guys step off the top.",
                "Ray: Ladder's tied off and long enough.", "Deck ladder is short; crew steps off the top (Ray)",
                G(@"ladder", @"access", @"climb", @"getting up")),
            C("wed-clue-rail", 3, "Ray", "wed-missing-midrail", "Did anything come off the guardrail?",
                "Ray: We pulled a midrail to land the joists. Might not be back yet.",
                "Ray: Rails are all back on.", "A midrail was pulled to land joists (Ray)",
                G(@"\brails?\b", @"guard", @"midrail", @"perimeter", @"\bedge")),
            C("thu-clue-sling", 4, "Dolores", "thu-frayed-sling", "Any problems with the rigging?",
                "Dolores: Kiara flagged a cut sling on the second pick, but I saw it back on the hook.",
                "Dolores: Rigging was inspected this morning, all tagged.", "A cut sling went back on the hook (Dolores)",
                G(@"sling", @"rigg", @"\brig\b", @"strap", @"choker", @"\bload", @"\bpicks?\b")),
            C("thu-clue-mats", 4, "Ray", "thu-outrigger-no-mat", "How's the crane set up?",
                "Ray: Left front outrigger's on that soft fill. Mats are still on the truck.",
                "Ray: Crane's on mats, level and solid.", "Left front outrigger on soft fill, no mats (Ray)",
                G(@"outrigger", @"\bmats?\b", @"ground", @"\bsoil", @"set ?up", @"crane", @"float")),
            C("thu-clue-roof", 4, "Dolores", "thu-roof-edge", "How are the roofers doing at the edge?",
                "Dolores: Roofers keep drifting past the warning line on the east side.",
                "Dolores: Roofers are staying inside the warning line.", "Roofers drifting past the warning line (Dolores)",
                G(@"roof", @"\bedge", @"\bfall", @"harness", @"tie", @"warning line")),
            C("fri-clue-line", 5, "Ray", "fri-boom-near-line", "How close does the boom get to that line?",
                "Ray: Pump boom got pretty close to that line on the last pour. Ten feet, maybe less.",
                "Ray: Boom stays well clear of the line.", "Pump boom came within ~10 ft of the line (Ray)",
                G(@"power ?line", @"\bline\b", @"overhead", @"\bboom", @"clearance", @"distance", @"\bwires?\b")),
            C("fri-clue-spotter", 5, "Ray", "fri-backing-mixer", "Who's spotting the mixer trucks?",
                "Ray: Our spotter went home sick. Drivers are backing in blind.",
                "Ray: Spotter's on every truck.", "No spotter today; mixers backing in blind (Ray)",
                G(@"spotter", @"backing", @"revers", @"mixer", @"truck", @"back(ing)? up")),
            C("fri-clue-rebar", 5, "Dolores", "fri-rebar-impalement", "Are the rebar ends capped?",
                "Dolores: We ran out of rebar caps yesterday. Some of the step-down bars are bare.",
                "Dolores: Every bar's capped.", "Rebar caps ran out; bars bare at the step-down (Dolores)",
                G(@"rebar", @"\bcaps?\b", @"\bbars?\b", @"impal", @"\bstubs?\b", @"dowel")),
        };

        // Questions that sound useful but aren't (offline question chips mix these in so the list isn't the answer key).
        public static readonly string[] RedHerrings = { "How's the schedule looking?", "Where's the supply rack?", "Who's on lunch first?" };

        public static IEnumerable<Clue> For(int episode) => All.Where(c => c.Episode == episode);

        public static bool Matches(Clue c, string question)
        {
            if (string.IsNullOrWhiteSpace(question)) return false;
            return c.Groups.All(g => g.Any(p => Regex.IsMatch(question, p, RegexOptions.IgnoreCase)));
        }

        // The first not-yet-asked clue this person knows that the question is about.
        public static Clue Match(int episode, string npc, string question, ICollection<string> asked) =>
            For(episode).FirstOrDefault(c => npc != null && npc.StartsWith(c.Npc, StringComparison.OrdinalIgnoreCase) && !asked.Contains(c.Id) && Matches(c, question));
    }

    // ---------- spoken tasks: shared element checks ----------
    static class Words
    {
        public static bool Any(string text, params string[] patterns) => patterns.Any(p => Regex.IsMatch(text ?? "", p, RegexOptions.IgnoreCase));
        public static readonly string[] Consequence = { @"\bfall", @"cave", @"collaps", @"crush", @"struck", @"shock", @"electrocut", @"\bburn", @"hurt", @"injur", @"\bkill", @"\bdie\b", @"\bdead\b", @"hospital", @"heat ?stroke", @"pinch", @"impal", @"buried" };
        public static readonly string[] Control = { @"guard", @"\brails?\b", @"cover", @"\bbox\b", @"shor", @"slope", @"bench", @"\bmats?\b", @"gfci", @"barricad", @"cones?\b", @"spotter", @"\bcaps?\b", @"tie ?off", @"tied off", @"harness", @"ladder", @"water", @"shade", @"\bbreaks?\b", @"lock", @"\btag", @"inspect", @"clearance", @"\b10 ?f(ee)?t\b", @"warning line" };
        public static readonly string[] Standard = { @"\b19\d\d\.\d", @"\bosha\b", @"standard", @"\brule", @"\bcode\b", @"\blaw\b", @"regulat" };
        public static readonly string[] Action = { @"\bwill\b", @"\bmust\b", @"make sure", @"\bbefore\b", @"every(one|body)", @"going to", @"need to", @"don'?t", @"stop work", @"tell me", @"report", @"\bcheck\b", @"\balways\b", @"\bnever\b" };
        public static readonly string[] Ask = { @"\?", @"\bwhat\b", @"\bhow\b", @"\bwhy\b", @"can you", @"could you", @"do you", @"walk me", @"help me understand" };
        public static readonly string[] Agree = { @"\blet'?s\b", @"\bwe\b", @"together", @"can we", @"i'?ll", @"you'?ll", @"\bmove\b", @"step (back|inside|away|out)", @"clip", @"tie ?off", @"\bstay\b", @"\bwait\b", @"spotter", @"\bdeal\b", @"sound good", @"\bok(ay)?\?" };
        public static readonly string[] Hostile = { @"\bshut up\b", @"\bidiot\b", @"\bstupid\b", @"\bmoron\b", @"\bdumb\b", @"\bf+u+c+k", @"\bsh[i1]t\b", @"\bdamn\b", @"get you fired", @"\byou'?re fired\b", @"what'?s wrong with you", @"are you crazy" };
        public static bool Shouting(string t)
        {
            var letters = (t ?? "").Where(char.IsLetter).ToList();
            return letters.Count >= 8 && letters.Count(char.IsUpper) > letters.Count * 0.7 || (t ?? "").Count(c => c == '!') >= 3;
        }
    }

    public readonly struct SpokenResult
    {
        public readonly float Score; public readonly string Feedback; public readonly string Flags;
        public SpokenResult(float score, string feedback, string flags) { Score = score; Feedback = feedback; Flags = flags; }
    }

    // Tomorrow's toolbox talk in your own words: hazard, what could happen, the control, the standard, what to do.
    public static class BriefingRubric
    {
        public static SpokenResult Score(string text, string hazardName, EnergySource energy)
        {
            var t = text ?? "";
            var tokens = (hazardName ?? "").ToLowerInvariant().Split(new[] { ' ', '-', '/', '(', ')' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 4).ToList();
            var hazard = tokens.Any(w => t.ToLowerInvariant().Contains(w)) || Words.Any(t, energy.ToString().ToLowerInvariant());
            var parts = new (bool ok, string missing)[]
            {
                (hazard, "name the hazard"),
                (Words.Any(t, Words.Consequence), "say what could happen"),
                (Words.Any(t, Words.Control), "say what controls it"),
                (Words.Any(t, Words.Standard), "point to the rule"),
                (Words.Any(t, Words.Action), "tell them what to do"),
            };
            var n = parts.Count(p => p.ok);
            var missing = parts.Where(p => !p.ok).Select(p => p.missing).ToList();
            var fb = n == 5 ? "A complete talk: hazard, consequence, control, rule and what to do." : $"Good start ({n}/5). Next time also {string.Join(", ", missing)}.";
            return new SpokenResult(n / 5f, fb, string.Join(" ", parts.Select((p, i) => $"{new[] { "hazard", "consequence", "control", "rule", "action" }[i]}={(p.ok ? 1 : 0)}")));
        }
    }

    // Coaching a worker out of an unsafe act: ask, explain why, agree the fix, stay respectful.
    public static class CoachingRubric
    {
        static readonly HashSet<string> Behavioural = new HashSet<string> { "thu-roof-edge", "thu-under-load", "fri-backing-mixer", "tue-swing-radius", "thu-swing-radius" };
        public static bool Applies(string id) => id != null && Behavioural.Contains(id);

        public static readonly Dictionary<string, string> Worker = new Dictionary<string, string>
        {
            ["thu-roof-edge"] = "Roofer: I'm just finishing this row. Two minutes.",
            ["thu-under-load"] = "Laborer: It's a quick pick, I'm out of the way in a sec.",
            ["fri-backing-mixer"] = "Laborer: Driver can see me, it's fine.",
            ["tue-swing-radius"] = "Laborer: I always cut through here, it's faster.",
            ["thu-swing-radius"] = "Laborer: The crane's not even swinging right now.",
        };

        public static string[] Samples(string id) => new[]
        {
            "Hey, can I grab you a second? If that swings or you slip, you're hurt. Can we move you back behind the line and finish it from there?",
            "Get out of there right now. What's wrong with you?",
            "Be careful, okay.",
        };

        public static SpokenResult Score(string text)
        {
            var t = text ?? "";
            var hostile = Words.Shouting(t) || Words.Any(t, Words.Hostile);
            var ask = Words.Any(t, Words.Ask);
            var why = Words.Any(t, Words.Consequence) || Words.Any(t, @"\bif\b.*\b(you|it|that)\b");
            var agree = Words.Any(t, Words.Agree);
            var score = hostile ? 0.2f : (ask ? 0.25f : 0f) + (why ? 0.35f : 0f) + (agree ? 0.4f : 0f);
            var fb = hostile ? "They'll move while you're watching, and do it again when you're not. Ask, explain why, agree on the fix."
                : score >= 0.99f ? "Asked, explained why and agreed on the fix: that change sticks."
                : "Better: " + string.Join(", ", new[] { ask ? null : "ask what's going on", why ? null : "say what could happen", agree ? null : "agree on what they'll do instead" }.Where(s => s != null)) + ".";
            return new SpokenResult(score, fb, $"ask={(ask ? 1 : 0)} why={(why ? 1 : 0)} agree={(agree ? 1 : 0)} hostile={(hostile ? 1 : 0)}");
        }

        // How long the reminder holds: a coached change sticks, a lecture fades fast.
        public static float LapseFactor(float score) => score >= 0.7f ? float.PositiveInfinity : score >= 0.4f ? 1f : 0.5f;
    }

    // Daily excavation inspection log (1926.651(k)(1)): what a competent person writes down before work starts.
    public static class InspectionLog
    {
        public static readonly string[] Soils = { "Not tested", "Type A", "Type B", "Type C", "Stable rock" };
        public static readonly string[] Systems = { "None", "Trench box", "Sloping", "Shoring" };

        public static SpokenResult Score(string soil, bool water, string system, ICollection<string> listed, ICollection<string> realHazards, ICollection<string> lookAlikes, string actions,
            string soilKey = "Type C", bool waterKey = true, string systemKey = "Trench box")
        {
            var soilOk = soil == soilKey; var waterOk = water == waterKey; var systemOk = system == systemKey;
            var listedReal = realHazards.Count(listed.Contains);
            var recall = realHazards.Count == 0 ? 1f : (float)listedReal / realHazards.Count;
            var wrong = lookAlikes.Count(listed.Contains);
            var hazardScore = Math.Max(0f, recall - 0.25f * wrong);
            var actionOk = Words.Any(actions, Words.Control) && (actions ?? "").Trim().Length >= 8;
            var score = (soilOk ? 0.3f : 0f) + (waterOk ? 0.15f : 0f) + (systemOk ? 0.15f : 0f) + 0.25f * hazardScore + (actionOk ? 0.15f : 0f);
            var notes = new System.Collections.Generic.List<string>();
            if (!soilOk) notes.Add($"soil: wet clay under 0.5 tsf after rain is {soilKey}");
            if (!waterOk) notes.Add("water: last night's rain counts");
            if (!systemOk) notes.Add($"protective system: {systemKey}");
            if (recall < 1f) notes.Add($"{realHazards.Count - listedReal} hazard{(realHazards.Count - listedReal == 1 ? "" : "s")} you know about isn't on the log");
            if (wrong > 0) notes.Add($"{wrong} compliant condition{(wrong == 1 ? "" : "s")} logged as a hazard");
            if (!actionOk) notes.Add("actions: write what you're doing about it");
            var fb = notes.Count == 0 ? "Complete log: soil, water, protective system, hazards and actions. Sign it." : "Log filed. Fix next time: " + string.Join("; ", notes) + ".";
            return new SpokenResult(score, fb, $"soil={(soilOk ? 1 : 0)} water={(waterOk ? 1 : 0)} system={(systemOk ? 1 : 0)} recall={recall:0.00} wrong={wrong} action={(actionOk ? 1 : 0)}");
        }
    }
}
