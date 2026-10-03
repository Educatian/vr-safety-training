using System;
using System.Linq;
using Jobsite.Core;
using UnityEngine;

namespace Jobsite.Runtime
{
    // Which kind of round this scene load is. Course mode (the 5-day story with check-in, briefing, toolbox talk,
    // completion codes and research consent) is the default; Hazard Hunt drops straight into a 3-minute round.
    // Arcade rounds never write course progress (career, mastery, carry-forward, completion code), and their telemetry
    // rows carry mode=arcade in session_start so public play can't be mistaken for course data.
    public static class ArcadeMode
    {
        public static bool Active;
        public static bool Daily;           // the ranked daily site (vs a practice round)
        public static int DailyNumber;
        public static int Seed;
        public static bool Replay;          // this daily was already played once on this device
        static bool urlConsumed;

        public static int[] PlayableEpisodes => Episodes.All.Where(e => e.Playable).Select(e => e.Number).ToArray();
        public static Episode TodaysEpisode(DateTime utc) => Episodes.Get(DailySite.Episode(utc, PlayableEpisodes));

        public static void StartDaily() => StartDaily(DateTime.UtcNow);

        public static void StartDaily(DateTime utc)
        {
            Active = true; Daily = true;
            DailyNumber = DailySite.Number(utc);
            Seed = DailySite.Seed(utc);
            Replay = FirstScore(DailyNumber) >= 0;
            EpisodeDirector.SkipIntro = true;
            EpisodeDirector.Play(TodaysEpisode(utc));
        }

        // Practice: a random playable episode (or the given one) with a fresh seed. Not a record.
        public static void StartPractice(Episode ep = null)
        {
            var pool = PlayableEpisodes;
            Active = true; Daily = false; DailyNumber = 0; Replay = false;
            Seed = Environment.TickCount & 0x7fffffff;
            EpisodeDirector.SkipIntro = true;
            EpisodeDirector.Play(ep ?? Episodes.Get(pool[new System.Random(Seed).Next(pool.Length)]));
        }

        public static void Exit() { Active = false; Daily = false; Replay = false; }

        // A shared link (https://.../?daily) opens straight into today's round, once per page load.
        public static bool TryStartFromUrl()
        {
            if (urlConsumed) return false;
            urlConsumed = true;
            var url = Application.absoluteURL ?? "";
            var q = url.IndexOf('?');
            if (q < 0) return false;
            var query = url.Substring(q + 1).ToLowerInvariant();
            if (!(query == "daily" || query.StartsWith("daily&") || query.Contains("play=daily") || query.Contains("&daily"))) return false;
            StartDaily();
            return true;
        }

        // ---- local records (this device) ----
        static string FirstKey(int n) => "arcade_daily_first_" + n;
        static string BestKey(int n) => "arcade_daily_best_" + n;
        public static int FirstScore(int n) => PlayerPrefs.GetInt(FirstKey(n), -1);
        public static int BestScore(int n) => PlayerPrefs.GetInt(BestKey(n), -1);
        public static int PracticeBest => PlayerPrefs.GetInt("arcade_practice_best", -1);

        public static void Record(int score)
        {
            if (Daily)
            {
                if (FirstScore(DailyNumber) < 0) PlayerPrefs.SetInt(FirstKey(DailyNumber), score);
                if (score > BestScore(DailyNumber)) PlayerPrefs.SetInt(BestKey(DailyNumber), score);
                // Streak: consecutive daily sites played.
                var last = PlayerPrefs.GetInt("arcade_last_daily", 0);
                if (last != DailyNumber)
                {
                    PlayerPrefs.SetInt("arcade_streak", last == DailyNumber - 1 ? PlayerPrefs.GetInt("arcade_streak", 0) + 1 : 1);
                    PlayerPrefs.SetInt("arcade_last_daily", DailyNumber);
                }
            }
            else if (score > PracticeBest) PlayerPrefs.SetInt("arcade_practice_best", score);
            PlayerPrefs.Save();
        }

        public static int Streak => PlayerPrefs.GetInt("arcade_streak", 0);

        public static string ShareUrl
        {
            get
            {
                var url = Application.absoluteURL;
                if (string.IsNullOrEmpty(url) || !url.StartsWith("http")) return "https://competent-person.pages.dev/?daily";
                var q = url.IndexOf('?'); if (q >= 0) url = url.Substring(0, q);
                var h = url.IndexOf('#'); if (h >= 0) url = url.Substring(0, h);
                return url.TrimEnd('/') + "/?daily";
            }
        }
    }
}
