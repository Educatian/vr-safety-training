using System;
using System.Collections;
using System.Text;
using System.Text.RegularExpressions;
using UnityEngine;
using UnityEngine.Networking;

namespace Jobsite.Runtime
{
    // Hazard Hunt daily board (Web/functions/api/scores.js). Opt-in and public: the player picks a display handle;
    // nothing here touches the research tables, the roster code or consent. First attempts at today's site only.
    public static class Leaderboard
    {
        [Serializable] public sealed class Entry { public string handle; public int score; public string grade; public int found; public int total; public int falseAlarms; public int seconds; }
        [Serializable] public sealed class Board { public int day; public Entry[] entries = new Entry[0]; public int players; }
        [Serializable] sealed class Post { public int day; public string handle; public int score, found, total, falseAlarms, incidents; public string grade; public int seconds; public string session; public bool replay; public string build; }
        [Serializable] sealed class Reply { public bool ok; public int rank; public int players; public string error; }

        public static bool Online => Application.platform == RuntimePlatform.WebGLPlayer || !string.IsNullOrEmpty(Telemetry.ServerBase);
        public static bool HandleOk(string h) => !string.IsNullOrEmpty(h) && Regex.IsMatch(h, "^[A-Za-z0-9_]{3,12}$");
        public static string Handle { get => PlayerPrefs.GetString("arcade_handle", ""); set { PlayerPrefs.SetString("arcade_handle", value ?? ""); PlayerPrefs.Save(); } }

        public static IEnumerator Fetch(int day, Action<Board, string> done)
        {
            using var req = UnityWebRequest.Get(Telemetry.ServerBase + "/api/scores?day=" + day);
            req.timeout = 10;
            yield return req.SendWebRequest();
            if (req.result != UnityWebRequest.Result.Success) { done(null, "Board offline right now."); yield break; }
            Board b = null;
            try { b = JsonUtility.FromJson<Board>(req.downloadHandler.text); } catch (ArgumentException) { }
            done(b, b == null ? "Board offline right now." : null);
        }

        public static IEnumerator Submit(int day, string handle, Jobsite.Core.ArcadeRules.Result r, float seconds, string session, bool replay, Action<int, int, string> done)
        {
            var json = JsonUtility.ToJson(new Post
            {
                day = day, handle = handle, score = r.Score, found = r.Found, total = r.Total, falseAlarms = r.FalseAlarms, incidents = r.Incidents,
                grade = r.Grade, seconds = Mathf.RoundToInt(seconds), session = session, replay = replay, build = Application.version,
            });
            using var req = new UnityWebRequest(Telemetry.ServerBase + "/api/scores", UnityWebRequest.kHttpVerbPOST);
            req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("Content-Type", "application/json");
            req.timeout = 10;
            yield return req.SendWebRequest();
            Reply reply = null;
            try { reply = JsonUtility.FromJson<Reply>(req.downloadHandler?.text ?? ""); } catch (ArgumentException) { }
            if (req.result == UnityWebRequest.Result.Success && reply != null && reply.ok) done(reply.rank, reply.players, null);
            else done(0, 0, !string.IsNullOrEmpty(reply?.error) ? reply.error : "Couldn't reach the board. Try again in a moment.");
        }
    }
}
