using System.Collections.Generic;
using System.Linq;

namespace Jobsite.Core
{
    // Gear the competent person buys with Safety Points (GDD §19). Every item is a real field instrument or tool:
    // it gives the learner more INFORMATION (measurements, tests), never protection or immunity. The call stays theirs.
    public enum GearId { LaserMeasure, GfciTester, Penetrometer, DustMonitor, FieldNotebook }

    public sealed class GearItem
    {
        public GearId Id { get; }
        public string Name { get; }
        public int Cost { get; }
        public int MinLevel { get; }
        public string Effect { get; }
        public string RealWorld { get; }
        public GearItem(GearId id, string name, int cost, int minLevel, string effect, string realWorld)
        { Id = id; Name = name; Cost = cost; MinLevel = minLevel; Effect = effect; RealWorld = realWorld; }
    }

    public static class GearCatalog
    {
        // Costs and levels are starting values: a full clean episode (~500-700 XP) should buy one item.
        public static readonly IReadOnlyList<GearItem> All = new[]
        {
            new GearItem(GearId.FieldNotebook, "Competent-person logbook", 100, 1,
                "+1 hint token every shift.", "Daily inspection log the CP signs (e.g. trench inspections, 1926.651(k))."),
            new GearItem(GearId.LaserMeasure, "Laser distance meter", 150, 1,
                "Photograph from 8 m instead of 5 m; shows measured heights, gaps and depths.", "Handheld laser measure."),
            new GearItem(GearId.GfciTester, "Receptacle / GFCI tester", 150, 1,
                "Test temporary outlets: see whether a circuit is GFCI-protected.", "Plug-in tester with GFCI trip button."),
            new GearItem(GearId.Penetrometer, "Pocket penetrometer", 250, 2,
                "Read soil strength in a cut to classify it (Type A/B/C).", "Estimates unconfined compressive strength (1926 Subpart P App. A)."),
            new GearItem(GearId.DustMonitor, "Real-time dust monitor", 300, 2,
                "Read respirable dust at a task to judge silica exposure.", "Direct-reading aerosol monitor; confirms with sampling."),
        };

        public static GearItem Get(GearId id) => All.First(g => g.Id == id);
    }

    public enum BuyResult { Bought, AlreadyOwned, NeedLevel, NotEnoughPoints }

    // Career state across episodes: lifetime XP (drives level), spendable points, owned gear.
    public sealed class Career
    {
        public const int BadgeBonus = 100, HintTokensPerShift = 1;

        public int LifetimeXp { get; private set; }
        public int Points { get; private set; }
        public HashSet<GearId> Owned { get; } = new HashSet<GearId>();

        public Career(int lifetimeXp = 0, int points = 0, IEnumerable<GearId> owned = null)
        { LifetimeXp = lifetimeXp; Points = points; if (owned != null) Owned.UnionWith(owned); }

        public int Level => XpRules.Level(LifetimeXp);
        public static string Rank(int level) => level switch { 1 => "Trainee", 2 => "Crew Lead", 3 => "Site Lead", _ => "Competent Person" };
        public bool Has(GearId id) => Owned.Contains(id);
        public int StartingHints => HintTokensPerShift + (Has(GearId.FieldNotebook) ? 1 : 0);
        public float PhotoRange => Has(GearId.LaserMeasure) ? 8f : 5f;

        // End of episode: XP counts toward level; points = XP + a bonus per badge earned.
        public int Award(int episodeXp, int badges)
        {
            var points = episodeXp + badges * BadgeBonus;
            LifetimeXp += episodeXp; Points += points;
            return points;
        }

        public BuyResult Buy(GearId id)
        {
            var item = GearCatalog.Get(id);
            if (Has(id)) return BuyResult.AlreadyOwned;
            if (Level < item.MinLevel) return BuyResult.NeedLevel;
            if (Points < item.Cost) return BuyResult.NotEnoughPoints;
            Points -= item.Cost; Owned.Add(id);
            return BuyResult.Bought;
        }

        // Hint text for tier 1-3 on a hazard the learner has not found yet (Dolores over the radio).
        public static string HintText(int tier, string displayName, EnergySource energy, FocusFour focus, string where) => tier switch
        {
            1 => $"Dolores: Head {where}. Something there doesn't sit right with me.",
            2 => $"Dolores: Think {energy.ToString().ToLowerInvariant()} energy" + (focus != FocusFour.None ? $", a {FocusName(focus)} hazard." : "."),
            _ => $"Dolores: Look at the {displayName.ToLowerInvariant()}.",
        };

        static string FocusName(FocusFour f) => f switch
        {
            FocusFour.CaughtIn => "caught-in/between", FocusFour.StruckBy => "struck-by", FocusFour.Electrocution => "electrocution", _ => "fall",
        };
    }
}
