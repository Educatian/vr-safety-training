using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Loads the ECD model (Resources/ecd.json) once per run. Invalid data is rejected with a warning and the built-in
    // defaults (EvidenceModel) stay in force, so a bad edit can never break scoring silently.
    public static class EcdLoader
    {
        static bool loaded;
        public static string Status { get; private set; } = "defaults";

        public static void LoadOnce()
        {
            if (loaded) return;
            loaded = true;
            var asset = Resources.Load<TextAsset>("ecd");
            if (asset == null) { Status = "defaults (no Resources/ecd.json)"; return; }
            EvidenceModel model;
            try { model = JsonUtility.FromJson<EvidenceModel>(asset.text); }
            catch (System.Exception e) { Status = "defaults (ecd.json unreadable: " + e.Message + ")"; Debug.LogWarning("[ECD] " + Status); return; }
            var problems = EvidenceModel.Use(model);
            Status = problems.Count == 0 ? "ecd.json " + model.version : "defaults (ecd.json rejected: " + string.Join("; ", problems) + ")";
            if (problems.Count > 0) Debug.LogWarning("[ECD] " + Status);
        }
    }
}
