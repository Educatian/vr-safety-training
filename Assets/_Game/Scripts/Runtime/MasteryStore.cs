using System;
using System.Collections.Generic;
using System.Linq;
using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Best CP mastery per area across episodes (GDD §5.2), persisted per browser like the career.
    // Feeds the end-of-shift bars and the Friday capstone gate (MasteryGate).
    public static class MasteryStore
    {
        const string Key = "mastery_v1";
        [Serializable] private sealed class Row { public string[] areas = new string[0]; public float[] values = new float[0]; }

        public static Dictionary<CpArea, float> Load()
        {
            var result = new Dictionary<CpArea, float>();
            try
            {
                var row = JsonUtility.FromJson<Row>(PlayerPrefs.GetString(Key, "{}")) ?? new Row();
                for (var i = 0; i < Math.Min(row.areas?.Length ?? 0, row.values?.Length ?? 0); i++)
                    if (Enum.TryParse<CpArea>(row.areas[i], out var a)) result[a] = Mathf.Clamp01(row.values[i]);
            }
            catch (ArgumentException) { }
            return result;
        }

        public static Dictionary<CpArea, float> Record(IReadOnlyDictionary<CpArea, float> today)
        {
            var merged = MasteryGate.Merge(Load(), today);
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(new Row
            {
                areas = merged.Keys.Select(k => k.ToString()).ToArray(),
                values = merged.Values.ToArray(),
            }));
            PlayerPrefs.Save();
            return merged;
        }

        public static IReadOnlyList<CpArea> CapstoneMissing() => MasteryGate.Missing(Load());
        public static bool CapstoneOpen => GameSettings.UnlockAll || CapstoneMissing().Count == 0;
        public static void Reset() { PlayerPrefs.DeleteKey(Key); PlayerPrefs.Save(); }
    }
}
