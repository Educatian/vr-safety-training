using System;
using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // Hands-on interactions (2026-10-02): field measurements, control installs, close inspection and equipment setup
    // done with the player's hands in the world instead of a tablet button. This file holds the judgements (engine-free,
    // unit-tested); Runtime/HandsOn drives the interaction. Results feed the existing ECD observables (measure,
    // install_success / install_misplaced) plus inspect_close. Thresholds are the cited OSHA values; tolerances are
    // starting values for playtest tuning.
    public readonly struct HandsOnResult
    {
        public readonly bool Ok; public readonly float Score; public readonly string Feedback; public readonly string Cfr;
        public HandsOnResult(bool ok, float score, string feedback, string cfr) { Ok = ok; Score = score; Feedback = feedback; Cfr = cfr; }
    }

    public enum InstallTask { None, Midrail, Ladder, Cover, Barricade, Gfci }
    public enum InspectItem { None, Cord, Sling }

    public static class HandsOnCatalog
    {
        static readonly Dictionary<string, InstallTask> Installs = new Dictionary<string, InstallTask>
        {
            ["wed-missing-midrail"] = InstallTask.Midrail,
            ["wed-short-ladder"] = InstallTask.Ladder,
            ["mon-trailer-ladder"] = InstallTask.Ladder,
            ["wed-open-hole"] = InstallTask.Cover,
            ["thu-open-skylight"] = InstallTask.Cover,
            ["tue-swing-radius"] = InstallTask.Barricade,
            ["thu-swing-radius"] = InstallTask.Barricade,
            ["mon-no-gfci"] = InstallTask.Gfci,
        };
        static readonly Dictionary<string, InspectItem> Inspections = new Dictionary<string, InspectItem>
        {
            ["mon-damaged-cord"] = InspectItem.Cord,
            ["thu-frayed-sling"] = InspectItem.Sling,
        };
        public static InstallTask InstallFor(string id) => id != null && Installs.TryGetValue(id, out var t) ? t : InstallTask.None;
        public static InspectItem InspectFor(string id) => id != null && Inspections.TryGetValue(id, out var t) ? t : InspectItem.None;
        public static IEnumerable<string> InstallIds => Installs.Keys;
        public static IEnumerable<string> InspectIds => Inspections.Keys;
    }

    public static class HandsOnRules
    {
        public const float InchesPerMeter = 39.3701f;
        public const float TopRailIn = 42f, MidrailToleranceIn = 3f, LadderAboveLandingIn = 36f;
        public const float LadderRatioMin = 3.5f, LadderRatioMax = 4.5f, BarricadeMaxGapDeg = 120f, CoverMinOverlapM = 0.05f;

        public static string FeetInches(float meters)
        {
            var inches = (int)Math.Round(Math.Max(0f, meters) * InchesPerMeter);
            return inches >= 12 ? $"{inches / 12} ft {inches % 12} in" : $"{inches} in";
        }

        // A measurement taken on the condition (both ends within its bounds plus a margin), not across the site.
        public static bool OnCondition(float[] a, float[] b, float[] min, float[] max, float margin = 1.5f)
        {
            bool In(float[] p) { for (var i = 0; i < 3; i++) if (p[i] < min[i] - margin || p[i] > max[i] + margin) return false; return true; }
            return In(a) && In(b);
        }

        // 1926.502(b)(2)(i): midrails midway between the top edge of the guardrail system and the walking level.
        public static HandsOnResult Midrail(float midIn, float topRailIn = TopRailIn)
        {
            var target = topRailIn / 2f;
            var off = Math.Abs(midIn - target);
            var ok = off <= MidrailToleranceIn;
            var where = $"{midIn:0} in up";
            return new HandsOnResult(ok, ok ? 1f : Math.Max(0f, 1f - (off - MidrailToleranceIn) / 12f),
                ok ? $"Midrail at {where}: midway between the deck and the {topRailIn:0} in top rail."
                   : $"Midrail at {where}. It goes midway between the deck and the top rail ({target:0} in).", "1926.502(b)(2)(i)");
        }

        // 1926.1053(b)(1): side rails at least 3 ft above the landing; (b)(5)(i): base out 1/4 of the working length.
        public static HandsOnResult Ladder(float aboveLandingIn, float riseM, float runM)
        {
            var extOk = aboveLandingIn >= LadderAboveLandingIn - 1f;
            var ratio = runM <= 0.01f ? float.PositiveInfinity : riseM / runM;
            var angleOk = ratio >= LadderRatioMin && ratio <= LadderRatioMax;
            var parts = new List<string>();
            parts.Add(extOk ? $"rails {aboveLandingIn / 12f:0.#} ft above the landing" : $"rails only {aboveLandingIn:0} in above the landing (need 3 ft)");
            parts.Add(angleOk ? $"base set at about 1:{ratio:0.#}" : ratio > LadderRatioMax ? $"base too close (1:{ratio:0.#}, too steep)" : $"base too far out (1:{ratio:0.#}, it can kick out)");
            var ok = extOk && angleOk;
            return new HandsOnResult(ok, (extOk ? 0.5f : 0f) + (angleOk ? 0.5f : 0f),
                (ok ? "Ladder set: " : "Not yet: ") + string.Join("; ", parts) + (ok ? "." : ". 3 ft above the landing, 1 ft out for every 4 ft up."), "1926.1053(b)(1)");
        }

        // Cones / barricade outside the swing path, all the way round: >= 4 cones, none inside the swing radius, none
        // absurdly far, and no gap wider than BarricadeMaxGapDeg (1926.1424(a)(2)).
        public static HandsOnResult Barricade(IList<(float x, float z)> cones, float cx, float cz, float radius)
        {
            if (cones == null || cones.Count < 4) return new HandsOnResult(false, 0f, $"{cones?.Count ?? 0} cones placed. Ring the whole swing path (at least 4).", "1926.1424(a)(2)");
            var inside = cones.Count(c => Dist(c, cx, cz) < radius * 0.97f);
            var far = cones.Count(c => Dist(c, cx, cz) > radius + 5f);
            var angles = cones.Select(c => Math.Atan2(c.z - cz, c.x - cx) * 180.0 / Math.PI).OrderBy(a => a).ToList();
            var gap = 0.0;
            for (var i = 0; i < angles.Count; i++) gap = Math.Max(gap, (i + 1 < angles.Count ? angles[i + 1] : angles[0] + 360.0) - angles[i]);
            var ok = inside == 0 && far == 0 && gap <= BarricadeMaxGapDeg;
            var why = inside > 0 ? $"{inside} cone{(inside == 1 ? " is" : "s are")} inside the swing path: people would stand where the counterweight swings."
                : far > 0 ? "Some cones are far from the swing path: keep the barricade just outside it."
                : gap > BarricadeMaxGapDeg ? $"There's a {gap:0}° opening in the ring: someone can walk straight in."
                : "Barricade rings the swing path with no way in.";
            var score = ok ? 1f : Math.Max(0f, 1f - inside * 0.25f - (gap > BarricadeMaxGapDeg ? 0.4f : 0f) - far * 0.1f);
            return new HandsOnResult(ok, score, why, "1926.1424(a)(2)");
        }

        // Cover over a floor/roof opening: overlaps every edge and is marked HOLE or COVER (1926.502(i)(3)-(4)).
        public static HandsOnResult Cover(float openMinX, float openMinZ, float openMaxX, float openMaxZ, float coverCx, float coverCz, float coverHalfX, float coverHalfZ, string label)
        {
            var overlap = Math.Min(Math.Min(openMinX - (coverCx - coverHalfX), (coverCx + coverHalfX) - openMaxX),
                                   Math.Min(openMinZ - (coverCz - coverHalfZ), (coverCz + coverHalfZ) - openMaxZ));
            var covers = overlap >= CoverMinOverlapM;
            var marked = label == "HOLE" || label == "COVER";
            var ok = covers && marked;
            var why = !covers ? "The cover doesn't overlap every edge of the opening: slide it until it laps all four sides."
                : !marked ? "Covered, but unmarked: a cover must say HOLE or COVER so nobody lifts it."
                : $"Covered on all sides and marked {label}.";
            return new HandsOnResult(ok, (covers ? 0.6f : 0f) + (marked ? 0.4f : 0f), why, "1926.502(i)(4)");
        }

        // Close inspection: find the defect (no wild guesses) or, on a sound item, call it sound.
        public static HandsOnResult Inspection(bool hasDefect, bool found, int falseMarks, bool declaredSound, string what)
        {
            if (hasDefect)
            {
                var score = found ? Math.Max(0.25f, 1f - 0.25f * falseMarks) : 0f;
                return new HandsOnResult(found, score, found ? $"Found it: the {what} is damaged. Remove it from service and tag it." + (falseMarks > 0 ? $" ({falseMarks} wrong mark{(falseMarks == 1 ? "" : "s")} first.)" : "")
                    : $"You called the {what} sound, but it's damaged. Look along its whole length.", what == "sling" ? "1926.251(c)(4)(iv)" : "1926.416(e)(1)");
            }
            var ok = declaredSound && falseMarks == 0;
            return new HandsOnResult(ok, declaredSound ? Math.Max(0f, 1f - 0.34f * falseMarks) : 0f,
                ok ? $"Sound {what}: nothing to tag." : $"This {what} is sound: {falseMarks} spot{(falseMarks == 1 ? "" : "s")} marked that weren't defects.", what == "sling" ? "1926.251(a)" : "1926.416(e)");
        }

        static float Dist((float x, float z) c, float cx, float cz) => (float)Math.Sqrt((c.x - cx) * (c.x - cx) + (c.z - cz) * (c.z - cz));
    }
}
