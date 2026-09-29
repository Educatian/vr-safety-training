using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // ---------- Toolbox talk writer (GDD N7 / §4 "Reflect"): the generative explain-back, inside the fiction ----------
    // At the end of the shift the learner picks up to three of TODAY'S findings, orders them the way they will brief
    // the crew tomorrow, and answers one topic "why" item. Scored deterministically against the expert risk key.
    public static class ToolboxTalk
    {
        public const int Picks = 3;
        public const float GoodTalk = 0.75f;              // starting value: earns a banked hint token

        public static int Risk(HazardSpec s) => s.Probability * s.Severity;

        // Did the learner pick the highest-risk findings? |picks ∩ top-k(found)| / k (ties count as top).
        public static float SelectionScore(IReadOnlyList<HazardSpec> picks, IReadOnlyList<HazardSpec> found)
        {
            if (found.Count == 0) return 1f;
            var k = Math.Min(Picks, found.Count);
            var cut = found.Select(Risk).OrderByDescending(r => r).ElementAt(k - 1);
            return Math.Min(1f, (float)picks.Count(p => Risk(p) >= cut) / k);
        }

        // Pairwise agreement of the learner's order with the risk key (ties agree). 1 for zero or one pick.
        public static float OrderScore(IReadOnlyList<HazardSpec> ordered)
        {
            int pairs = 0, agree = 0;
            for (var i = 0; i < ordered.Count; i++)
                for (var j = i + 1; j < ordered.Count; j++)
                {
                    pairs++;
                    if (Risk(ordered[i]) >= Risk(ordered[j])) agree++;
                }
            return pairs == 0 ? 1f : (float)agree / pairs;
        }

        // GDD §14 XP rule: the talk opens with the day's highest-risk finding.
        public static bool LeadsWithTopRisk(IReadOnlyList<HazardSpec> ordered, IReadOnlyList<HazardSpec> found) =>
            ordered.Count > 0 && found.Count > 0 && Risk(ordered[0]) >= found.Max(Risk);

        public static float Score(IReadOnlyList<HazardSpec> ordered, IReadOnlyList<HazardSpec> found, bool whyCorrect) =>
            0.4f * SelectionScore(ordered, found) + 0.3f * OrderScore(ordered) + 0.3f * (whyCorrect ? 1f : 0f);

        // Topic "why" items (shuffled per session through QuizSession). Distractors carry the course's target
        // misconceptions: PPE first, schedule first, "careful is enough".
        public static QuizItem Why(int episode) => episode switch
        {
            1 => new QuizItem("tt-ep1", "Why take a damaged cord out of service instead of taping it?",
                new[] { "Tape doesn't restore the jacket or strain relief, so the shock path stays.",
                        "Taping is fine on a GFCI circuit; removal is only extra caution.",
                        "Only cords over 50 ft must come out; short ones can be taped." }, 0,
                "Worn or frayed cords shall not be used (1926.416(e)(1)). GFCI is a backup, not a repair."),
            2 => new QuizItem("tt-ep2", "Why get the crew out of an unprotected cut before anything else?",
                new[] { "The operator needs a clear view, so it's a visibility rule.",
                        "A wall gives no warning, and a cubic yard of soil can crush a worker.",
                        "Wet clay holds for an hour after rain, so exposure is short anyway." }, 1,
                "Protective system at 5 ft or deeper (1926.652(a)(1)). Collapse is sudden."),
            3 => new QuizItem("tt-ep3", "Why is a guardrail better than telling the crew to stay back from the edge?",
                new[] { "A warning works as well if everyone signs the talk sheet.",
                        "Guardrails are mainly for inspectors; the crew knows where the edge is.",
                        "The rail protects a distracted worker; a warning depends on memory." }, 2,
                "Engineering controls work without anyone remembering. That is the hierarchy."),
            4 => new QuizItem("tt-ep4", "Why barricade the counterweight swing instead of relying on a spotter?",
                new[] { "A barricade keeps people out on every swing; a spotter can look away.",
                        "Spotters are only needed at night, so a barricade saves a worker by day.",
                        "The operator sees the counterweight, so it's mostly for visitors." }, 0,
                "Barricade the swing radius (1926.1424(a)(2)). Physical separation beats attention."),
            _ => new QuizItem("tt-ep5", "Why stop the pump instead of asking the operator to be careful near the line?",
                new[] { "Careful is enough if the boom is insulated and the crew wears gloves.",
                        "Current can arc inside the clearance without contact; only distance protects.",
                        "13 kV is too low to arc, so a reminder covers it." }, 1,
                "Keep 10 ft from lines up to 50 kV (1926.1408). Distance is the control."),
        };
    }

    // ---------- Speak-up under production pressure (GDD N4, §5.4) ----------
    public enum SpeakUpStyle { Passive, Assertive, Aggressive }

    public sealed class SpeakUpOption
    {
        public SpeakUpStyle Style { get; }
        public string Text { get; }
        public SpeakUpOption(SpeakUpStyle style, string text) { Style = style; Text = text; }
    }

    public static class SpeakUp
    {
        public static string Pushback(string hazard) =>
            $"Ray: The {hazard.ToLowerInvariant()}? Stopping that crew costs us the morning. Can't it wait till lunch?";

        static readonly SpeakUpOption[] Base =
        {
            new SpeakUpOption(SpeakUpStyle.Passive, "Okay. Keep them going and we'll fix it at lunch."),
            new SpeakUpOption(SpeakUpStyle.Assertive, "It stays stopped until the fix is in. I'll help get it done fast."),
            new SpeakUpOption(SpeakUpStyle.Aggressive, "Not your call, Ray. Back off and let me do my job."),
        };

        public static IReadOnlyList<SpeakUpOption> Options(int seed)
        {
            var rng = new Random(seed);
            return Base.OrderBy(_ => rng.Next()).ToList();
        }

        // Assertive-respectful holds the stop and keeps the foreman on side; aggressive holds it but costs trust;
        // passive gives the crew back to the hazard.
        public static float Score(SpeakUpStyle s) => s == SpeakUpStyle.Assertive ? 1f : s == SpeakUpStyle.Aggressive ? 0.5f : 0f;

        public static string Reply(SpeakUpStyle s) => s switch
        {
            SpeakUpStyle.Assertive => "Ray: ...Fine. Tell me what you need and I'll get the guys on it.",
            SpeakUpStyle.Aggressive => "Ray: Wow. Okay. Stopped. But I'm remembering that.",
            _ => "Ray: Good. Crew's back at it. We'll look at it later.",
        };
    }

    // ---------- Debrief classification (GDD N10, §15 item 4: chosen vs. best feasible control) ----------
    public enum DebriefGroup { ControlledAtBest, ControlledBelowBest, FoundNotControlled, Missed }

    public static class Debrief
    {
        public static DebriefGroup Group(HazardSpec s, HazardEvidence ev, HazardState state)
        {
            if (!ev.Detected) return DebriefGroup.Missed;
            if (state == HazardState.Controlled && ev.AppliedControl.HasValue)
                return ev.AppliedControl.Value <= s.BestFeasibleControl && ev.Lapses == 0 ? DebriefGroup.ControlledAtBest : DebriefGroup.ControlledBelowBest;
            return DebriefGroup.FoundNotControlled;
        }

        public static string Level(ControlLevel? l) => l switch
        {
            ControlLevel.Elimination => "Eliminate",
            ControlLevel.Engineering => "Engineering",
            ControlLevel.Administrative => "Admin",
            ControlLevel.Ppe => "PPE",
            _ => "none",
        };

        public static string GroupTitle(DebriefGroup g) => g switch
        {
            DebriefGroup.ControlledAtBest => "CONTROLLED AT THE BEST LEVEL",
            DebriefGroup.ControlledBelowBest => "CONTROLLED, BUT A STRONGER CONTROL WAS FEASIBLE",
            DebriefGroup.FoundNotControlled => "FOUND, NOT CONTROLLED",
            _ => "MISSED",
        };

        // One no-gore line for the near-miss card: what almost happened (GDD §8).
        public static string WhatAlmostHappened(string id) => id switch
        {
            "mon-damaged-cord" => "Luis grabbed the cord at the split. A tingle, then he let go. It could have locked his hand on.",
            "mon-no-gfci" => "The drill shorted in the damp. Nothing tripped. The operator felt it through his gloves.",
            "mon-trailer-ladder" => "The ladder kicked out at the top. He caught the gutter on the way down.",
            "mon-empty-water" => "A new laborer went dizzy and sat down hard in the sun. Early heat illness.",
            "tue-no-protective-system" => "The wall past the box slumped. Marcus scrambled out with seconds to spare.",
            "tue-spoil-at-edge" => "A slab of spoil slid back in, right where the crew had been kneeling.",
            "tue-no-egress" => "Water came in fast. With no ladder close, the crew had to claw up the wall.",
            "tue-swing-radius" => "The counterweight swung through the walkway a step behind a laborer.",
            "tue-dry-cutting" => "The saw crew worked all morning in a white cloud. Silica does its damage quietly.",
            "wed-missing-midrail" => "A worker stumbled against the rail and his leg went through the open gap.",
            "wed-open-hole" => "A laborer carrying plywood stepped into the opening. The sheet caught on the edges.",
            "wed-short-ladder" => "Stepping off the ladder, he had nothing to hold and nearly went backward.",
            "thu-outrigger-no-mat" => "The float punched into the clay mid-lift. The load swung before the operator set it down.",
            "thu-swing-radius" => "The counterweight pinned a cart against the column where a worker had just been.",
            "thu-under-load" => "A beam slipped in the choker and dropped a foot, right over the worker below.",
            "thu-frayed-sling" => "The sling parted under load. The steel dropped onto the mats.",
            "thu-roof-edge" => "The roofer stepped back to look at his seam and his heel went over the edge.",
            "thu-open-skylight" => "A roofer backed onto the skylight dome. It cracked under his boot.",
            "fri-boom-near-line" => "The boom swung toward the line and arced. The hose man felt the jolt.",
            "fri-backing-mixer" => "The mixer backed into the laborer's space. He jumped clear at the last second.",
            "fri-rebar-impalement" => "A finisher tripped at the step-down and caught himself one bar away.",
            _ => "A worker was exposed and got lucky this time.",
        };
    }

    // ---------- Mastery gate (GDD §5.2, Cook 2013): Friday unlocks only when each prior area is competent ----------
    public static class MasteryGate
    {
        public static readonly CpArea[] CapstoneAreas = { CpArea.Electrical, CpArea.Excavation, CpArea.FallProtection, CpArea.StruckBy };

        public static string AreaName(CpArea a) => a switch
        {
            CpArea.Excavation => "Excavation", CpArea.FallProtection => "Fall protection", CpArea.Scaffold => "Scaffolds",
            CpArea.StruckBy => "Rigging & struck-by", CpArea.Electrical => "Electrical", _ => "General",
        };

        // Where to practise an area (episode numbers).
        public static string Practice(CpArea a) => a switch
        {
            CpArea.Electrical => "EP1", CpArea.Excavation => "EP2", CpArea.FallProtection => "EP3", CpArea.StruckBy => "EP2 or EP4", _ => "any episode",
        };

        public static Dictionary<CpArea, float> Merge(IReadOnlyDictionary<CpArea, float> best, IReadOnlyDictionary<CpArea, float> today)
        {
            var merged = best.ToDictionary(kv => kv.Key, kv => kv.Value);
            foreach (var kv in today)
                merged[kv.Key] = merged.TryGetValue(kv.Key, out var b) ? Math.Max(b, kv.Value) : kv.Value;
            return merged;
        }

        public static IReadOnlyList<CpArea> Missing(IReadOnlyDictionary<CpArea, float> best) =>
            CapstoneAreas.Where(a => !best.TryGetValue(a, out var v) || v < DaySession.CompetentThreshold).ToList();
    }

    // ---------- Story follows play (GDD §18): epilogue lines gated on how the shift went ----------
    public enum LineGate { Always, CleanShift, RoughShift }

    public static class ShiftVerdict
    {
        // Clean = no incidents, every stop-work hazard was found and stopped (and the stop held), most hazards found.
        public static bool Clean(DaySession day, IEnumerable<HazardSpec> specs)
        {
            var real = specs.Where(s => s.IsHazard).ToList();
            if (day.NearMisses + day.Recordables > 0 || day.HazardIdentificationIndex < 0.75f) return false;
            return real.Where(s => s.RequiresStopWork).All(s =>
            {
                var ev = day.GetEvidence(s.Id);
                return ev.Detected && ev.StopWorkCalled && ev.SpeakUp != SpeakUpStyle.Passive;
            });
        }

        public static bool Plays(LineGate gate, bool clean) =>
            gate == LineGate.Always || (gate == LineGate.CleanShift) == clean;
    }
}
