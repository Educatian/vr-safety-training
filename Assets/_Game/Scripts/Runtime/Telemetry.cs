using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Jobsite.Runtime
{
    // Sends play events to the course server (Cloudflare Pages Functions + D1) so instructors can see them.
    // Same-origin on the web build (/api/...); editor/desktop can point at the deployed site via ServerBase.
    // Rows are PII-free: pseudonymous class/learner codes, event kinds, hazard ids, seconds. Chat text is never sent.
    public sealed class Telemetry : MonoBehaviour
    {
        public static string ServerBase = "";   // "" = same origin (web); set in editor tests to the deployed URL
        const float FlushEvery = 10f;

        [Serializable] public sealed class Row { public string t; public string kind; public string condition; public string detail; public float clock; public string cfr; public string ksa; public float score; }
        // Event schema v1 (docs/DesignUpgrade_2026-09-30.md §11): versions travel with every batch; the server rejects unknown
        // kinds (Web/functions/api/_kinds.js, kept in sync by TelemetrySchemaTests) and non-roster learner codes.
        public const string Schema = "cp-events-v1";
        [Serializable] sealed class Batch { public string schema; public string build; public string consent; public string ecd; public string session; public string classCode; public string learner; public int episode; public int seed; public List<Row> rows; }
        [Serializable] sealed class Withdrawal { public List<string> sessions; }
        const string SessionsKey = "research_sessions";
        [Serializable] public sealed class Completion
        {
            public string session; public string classCode; public string learner; public int episode; public int xp; public float hii;
            public float precision; public int incidents; public int quizCorrect; public int quizTotal; public string code;
        }
        [Serializable] sealed class CodeReply { public string code; }

        private readonly List<Row> pending = new List<Row>();
        private float nextFlush;
        public string SessionId { get; } = Guid.NewGuid().ToString("N");
        public int Episode { get; set; }
        public int Seed { get; set; }
        public bool Online => Application.platform == RuntimePlatform.WebGLPlayer || !string.IsNullOrEmpty(ServerBase);
        public int Sent { get; private set; }

        public void Add(string kind, string condition, string detail, float clock, string cfr = "", string ksa = "", float score = -1f)
        {
            pending.Add(new Row { t = DateTime.UtcNow.ToString("O"), kind = kind, condition = condition, detail = detail, clock = clock, cfr = cfr, ksa = ksa, score = score });
        }

        private void Update()
        {
            if (Time.unscaledTime < nextFlush) return;
            nextFlush = Time.unscaledTime + FlushEvery;
            Flush();
        }

        public void Flush()
        {
            if (!Online || pending.Count == 0) return;
            // No research consent, no play events off the device (the completion code below still works).
            if (GameSettings.ResearchConsent != 1) { pending.Clear(); return; }
            var batch = new Batch { schema = Schema, build = Application.version, consent = GameSettings.ConsentVersion, ecd = Jobsite.Core.EvidenceModel.Current.version,
                session = SessionId, classCode = RosterCode(GameSettings.ClassCode), learner = RosterCode(GameSettings.LearnerId), episode = Episode, seed = Seed, rows = new List<Row>(pending) };
            Remember(SessionId);
            pending.Clear();
            StartCoroutine(Post("/api/events", JsonUtility.ToJson(batch), ok => { if (ok != null) Sent += batch.rows.Count; else pending.InsertRange(0, batch.rows); }));
        }

        // Server signs the summary (HMAC) and returns a short completion code the instructor can verify.
        public void Complete(Completion c, Action<string> done)
        {
            Flush();
            c.session = SessionId; c.classCode = RosterCode(GameSettings.ClassCode); c.learner = RosterCode(GameSettings.LearnerId);
            if (!Online) { done(null); return; }
            StartCoroutine(Post("/api/complete", JsonUtility.ToJson(c), body => done(body == null ? null : JsonUtility.FromJson<CodeReply>(body)?.code)));
        }

        // Roster codes only (letters, digits, - and _): a typed name loses its spaces and punctuation before it can leave.
        public static string RosterCode(string v)
        {
            if (string.IsNullOrEmpty(v)) return "";
            var sb = new StringBuilder();
            foreach (var ch in v) if (char.IsLetterOrDigit(ch) && ch < 128 || ch == '-' || ch == '_') sb.Append(ch);
            return sb.Length > 24 ? sb.ToString(0, 24) : sb.ToString();
        }

        // Sessions whose events left this device (kept locally so the participant can withdraw them later).
        static void Remember(string session)
        {
            var list = PlayerPrefs.GetString(SessionsKey, "");
            if (list.Contains(session)) return;
            PlayerPrefs.SetString(SessionsKey, list.Length == 0 ? session : list + "," + session); PlayerPrefs.Save();
        }

        public static int RememberedSessions => PlayerPrefs.GetString(SessionsKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries).Length;

        // Participant withdrawal: deletes every session this browser sent (server: /api/withdraw), then forgets them.
        public static IEnumerator Withdraw(Action<string> done)
        {
            var ids = new List<string>(PlayerPrefs.GetString(SessionsKey, "").Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries));
            if (ids.Count == 0) { done("Nothing to withdraw: no research data was sent from this browser."); yield break; }
            string reply = null;
            yield return Post("/api/withdraw", JsonUtility.ToJson(new Withdrawal { sessions = ids }), body => reply = body);
            if (reply == null) { done("Couldn't reach the server. Try again when online."); yield break; }
            PlayerPrefs.DeleteKey(SessionsKey); PlayerPrefs.Save();
            done($"Withdrawn: {ids.Count} session(s) deleted from the research data.");
        }

        static IEnumerator Post(string path, string json, Action<string> done)
        {
            using var req = new UnityWebRequest(ServerBase + path, UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 15;
            yield return req.SendWebRequest();
            done(req.result == UnityWebRequest.Result.Success ? req.downloadHandler.text : null);
        }
    }
}
