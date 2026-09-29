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
        [Serializable] sealed class Batch { public string session; public string classCode; public string learner; public int episode; public int seed; public List<Row> rows; }
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
            var batch = new Batch { session = SessionId, classCode = GameSettings.ClassCode, learner = GameSettings.LearnerId, episode = Episode, seed = Seed, rows = new List<Row>(pending) };
            pending.Clear();
            StartCoroutine(Post("/api/events", JsonUtility.ToJson(batch), ok => { if (ok != null) Sent += batch.rows.Count; else pending.InsertRange(0, batch.rows); }));
        }

        // Server signs the summary (HMAC) and returns a short completion code the instructor can verify.
        public void Complete(Completion c, Action<string> done)
        {
            Flush();
            c.session = SessionId; c.classCode = GameSettings.ClassCode; c.learner = GameSettings.LearnerId;
            if (!Online) { done(null); return; }
            StartCoroutine(Post("/api/complete", JsonUtility.ToJson(c), body => done(body == null ? null : JsonUtility.FromJson<CodeReply>(body)?.code)));
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
