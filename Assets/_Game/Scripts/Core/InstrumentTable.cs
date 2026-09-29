using System.Collections.Generic;

namespace Jobsite.Core
{
    // Instrument readings (GDD §19) as RAW field values: a depth, a distance, a trip test. The verdict ("needs 2 ft",
    // "Type C", "above the PEL") is the learner's job, so readings never state the rule or the threshold.
    // A condition showing its control (installed, or the compliant twin of a replay) reads its compliant value.
    public static class InstrumentTable
    {
        static readonly Dictionary<(string, GearId), string> Hazard = new Dictionary<(string, GearId), string>
        {
            { ("mon-no-gfci", GearId.GfciTester), "Test button pressed: no trip. The circuit is not GFCI-protected." },
            { ("mon-gfci-ok", GearId.GfciTester), "Test button pressed: trips, then resets." },
            { ("tue-no-protective-system", GearId.Penetrometer), "Wet clay reads under 0.5 tsf." },
            { ("tue-no-protective-system", GearId.LaserMeasure), "Trench depth 6.0 ft. Worker stands 3 ft past the end of the box." },
            { ("tue-box-ok", GearId.Penetrometer), "Wet clay reads under 0.5 tsf." },
            { ("tue-box-ok", GearId.LaserMeasure), "Box walls rise above the 6.0 ft cut." },
            { ("tue-spoil-at-edge", GearId.LaserMeasure), "Spoil toe to trench edge: 0 ft." },
            { ("tue-no-egress", GearId.LaserMeasure), "No ladder or ramp found along the trench from this worker." },
            { ("tue-dry-cutting", GearId.DustMonitor), "Respirable dust at the saw: far above the background reading, rising with each cut." },
            { ("wed-missing-midrail", GearId.LaserMeasure), "Top rail at 42 in. Open gap of 42 in below it." },
            { ("wed-open-hole", GearId.LaserMeasure), "Opening 4 ft x 4 ft; 14 ft drop to the slab." },
            { ("wed-covered-hole", GearId.LaserMeasure), "Cover overlaps the 4 ft opening on all sides." },
            { ("wed-short-ladder", GearId.LaserMeasure), "Side rails end flush with the deck: 0 ft above the landing." },
            { ("thu-outrigger-no-mat", GearId.Penetrometer), "Clay under the float reads under 0.5 tsf." },
            { ("thu-outrigger-no-mat", GearId.LaserMeasure), "Float has sunk about 2 in into the clay." },
            { ("thu-outrigger-matted", GearId.Penetrometer), "Clay beside the mats reads under 0.5 tsf." },
            { ("thu-outrigger-matted", GearId.LaserMeasure), "Float level on three mats; no settlement." },
            { ("thu-swing-radius", GearId.LaserMeasure), "Walkway passes 2 ft behind the counterweight's swing path." },
            { ("thu-under-load", GearId.LaserMeasure), "Worker directly under the load, 14 ft below it." },
            { ("thu-roof-edge", GearId.LaserMeasure), "Roofer 1 ft from the edge, 5 ft outside the warning line; 52 ft drop." },
            { ("thu-open-skylight", GearId.LaserMeasure), "Skylight 4 ft x 8 ft, nothing over it; 12 ft drop to level 4." },
            { ("fri-boom-near-line", GearId.LaserMeasure), "Boom tip about 8.5 ft from the nearest conductor." },
            { ("fri-backing-mixer", GearId.LaserMeasure), "Laborer 6 ft behind the mixer's rear bumper." },
            { ("fri-rebar-impalement", GearId.LaserMeasure), "Seven bars stick up 27 in at the step-down." },
        };

        static readonly Dictionary<(string, GearId), string> Controlled = new Dictionary<(string, GearId), string>
        {
            { ("mon-no-gfci", GearId.GfciTester), "Test button pressed: trips, then resets." },
            { ("tue-no-protective-system", GearId.Penetrometer), "Wet clay reads under 0.5 tsf." },
            { ("tue-no-protective-system", GearId.LaserMeasure), "Trench depth 6.0 ft. The crew works inside the box." },
            { ("tue-spoil-at-edge", GearId.LaserMeasure), "Spoil toe to trench edge: 3 ft." },
            { ("tue-no-egress", GearId.LaserMeasure), "Ladder about 10 ft of travel from this worker." },
            { ("tue-dry-cutting", GearId.DustMonitor), "Respirable dust at the saw: close to the background reading." },
            { ("wed-missing-midrail", GearId.LaserMeasure), "Top rail at 42 in; midrail at 21 in." },
            { ("wed-open-hole", GearId.LaserMeasure), "Cover overlaps the 4 ft opening on all sides." },
            { ("wed-short-ladder", GearId.LaserMeasure), "Side rails extend 3 ft above the landing." },
            { ("thu-outrigger-no-mat", GearId.Penetrometer), "Clay beside the mats reads under 0.5 tsf." },
            { ("thu-outrigger-no-mat", GearId.LaserMeasure), "Float level on mats; no settlement." },
            { ("thu-swing-radius", GearId.LaserMeasure), "Barricade 3 ft outside the counterweight's swing path." },
            { ("thu-under-load", GearId.LaserMeasure), "No one under the load; nearest worker 20 ft outside the fall zone." },
            { ("thu-roof-edge", GearId.LaserMeasure), "Roofer 8 ft from the edge, inside the warning line." },
            { ("thu-open-skylight", GearId.LaserMeasure), "Skylight 4 ft x 8 ft, screened." },
            { ("fri-boom-near-line", GearId.LaserMeasure), "Boom tip about 14 ft from the nearest conductor." },
            { ("fri-backing-mixer", GearId.LaserMeasure), "Spotter at the rear corner; lane behind the mixer clear." },
            { ("fri-rebar-impalement", GearId.LaserMeasure), "Seven bars at the step-down, all capped." },
        };

        public static bool Knows(string id)
        {
            foreach (var k in Hazard.Keys) if (k.Item1 == id) return true;
            return false;
        }

        // null = this instrument gives nothing specific here.
        public static string Get(string id, GearId gear, bool showingControl)
        {
            if (showingControl && Controlled.TryGetValue((id, gear), out var c)) return c;
            return Hazard.TryGetValue((id, gear), out var h) ? h : null;
        }

        // Key: (condition id, instrument).
        public static IEnumerable<KeyValuePair<(string, GearId), string>> All => Hazard;
    }
}
