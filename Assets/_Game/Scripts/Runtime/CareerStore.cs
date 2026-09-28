using System;
using System.Linq;
using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Career persists per browser/device in PlayerPrefs (WebGL -> IndexedDB). Load once per scene; save on change.
    public static class CareerStore
    {
        const string Key = "career_v1";
        [Serializable] private sealed class Row { public int lifetimeXp; public int points; public string[] owned = new string[0]; }

        public static Career Load()
        {
            try
            {
                var row = JsonUtility.FromJson<Row>(PlayerPrefs.GetString(Key, "{}")) ?? new Row();
                var owned = (row.owned ?? new string[0]).Select(s => Enum.TryParse<GearId>(s, out var g) ? (GearId?)g : null).Where(g => g.HasValue).Select(g => g.Value);
                return new Career(row.lifetimeXp, row.points, owned);
            }
            catch (ArgumentException) { return new Career(); }
        }

        public static void Save(Career c)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Row { lifetimeXp = c.LifetimeXp, points = c.Points, owned = c.Owned.Select(g => g.ToString()).ToArray() }));
            PlayerPrefs.Save();
        }

        public static void Reset() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    }
}
