using Jobsite.Core;
using Jobsite.Runtime;
using UnityEngine;

namespace Jobsite.Editor
{
    // Bakes the instrument readings (GDD §19) into the scene. The single source is Core InstrumentTable: raw field
    // values only (depths, distances, trip tests), never the rule or the verdict. Look-alikes read their compliant
    // values, so the instrument informs the call instead of making it. The runtime reads the table directly too.
    public static class InstrumentReadings
    {
        public static void Apply()
        {
            foreach (var c in Object.FindObjectsByType<SiteCondition>(FindObjectsInactive.Include, FindObjectsSortMode.None))
                foreach (var entry in InstrumentTable.All)
                    if (entry.Key.Item1 == c.Id) c.SetReading(entry.Key.Item2, entry.Value);
        }
    }
}
