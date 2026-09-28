using Jobsite.Core;
using Jobsite.Runtime;
using UnityEngine;

namespace Jobsite.Editor
{
    // Authored instrument readings (GDD §19), matched to the built geometry. Look-alikes read "OK" so the
    // instrument confirms rather than decides. Laser falls back to measured size where nothing is authored.
    public static class InstrumentReadings
    {
        public static void Apply()
        {
            foreach (var c in Object.FindObjectsByType<SiteCondition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                switch (c.Id)
                {
                    case "mon-no-gfci": c.SetReading(GearId.GfciTester, "Test button: no trip. This circuit is not GFCI-protected."); break;
                    case "mon-gfci-ok": c.SetReading(GearId.GfciTester, "Test button: trips, resets. GFCI is working."); break;
                    case "tue-no-protective-system":
                        c.SetReading(GearId.Penetrometer, "Wet clay reads under 0.5 tsf. Type C soil.");
                        c.SetReading(GearId.LaserMeasure, "Trench depth 6.0 ft; worker 3 ft past the end of the box."); break;
                    case "tue-box-ok":
                        c.SetReading(GearId.Penetrometer, "Wet clay reads under 0.5 tsf. Type C soil.");
                        c.SetReading(GearId.LaserMeasure, "Box walls rise above the 6.0 ft cut."); break;
                    case "tue-spoil-at-edge": c.SetReading(GearId.LaserMeasure, "Spoil toe is at the trench edge: 0 ft back (needs 2 ft)."); break;
                    case "tue-no-egress": c.SetReading(GearId.LaserMeasure, "No ladder or ramp within 25 ft of travel from this worker."); break;
                    case "tue-dry-cutting": c.SetReading(GearId.DustMonitor, "Respirable dust spiking far above background at the saw. Silica exposure likely above the 50 µg/m³ PEL."); break;
                    case "wed-missing-midrail": c.SetReading(GearId.LaserMeasure, "Top rail 42 in. Open gap of 42 in below it, no midrail."); break;
                    case "wed-open-hole": c.SetReading(GearId.LaserMeasure, "Opening 4 ft x 4 ft, 14 ft drop to the slab."); break;
                    case "wed-covered-hole": c.SetReading(GearId.LaserMeasure, "Cover overlaps the 4 ft opening on all sides."); break;
                    case "wed-short-ladder": c.SetReading(GearId.LaserMeasure, "Side rails end flush with the deck: 0 ft above the landing (needs 3 ft)."); break;
                }
        }
    }
}
