using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jobsite.Core
{
    // Deterministic check on every generated crew reply (quality review 2026-09-30, area 9). The LLM only proposes
    // wording; this guard decides whether it may be shown. A reply that fails falls back to the authored answer and the
    // reason is logged (chat_reply verdict). Rules:
    //  - in character: never mentions AI, models, chatbots or vendors;
    //  - grounded: every 29 CFR 1926 citation it makes must appear in the facts it was given (no invented standards);
    //  - safe: never asks for personal information; no links, code or markup;
    //  - short: at most MaxSentences sentences / MaxChars characters (longer replies are trimmed, not rejected).
    public static class ReplyGuard
    {
        public const int MaxSentences = 3, MaxChars = 420;

        static readonly Regex OutOfCharacter = new Regex(
            @"\b(as an ai|an ai\b|a\.?i\. (model|assistant)|language model|large language|llm|chat ?bot|chatgpt|gpt-?\d|openai|open ?router|anthropic|claude|gemini|i am an assistant|i'm an assistant|my training data|i was trained)\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex AsksForPii = new Regex(
            @"\b(your|ur) (full |real |last )?(name|phone|phone number|number|e-?mail|email address|address|birthday|date of birth|ssn|social security)\b|\bwhat('?s| is) your name\b",
            RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex Markup = new Regex(@"https?://|www\.|```|<[a-z/][^>]*>|\[[^\]]+\]\([^)]+\)", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        static readonly Regex Cfr = new Regex(@"\b1926\.\d+(\([a-z0-9]+\))*", RegexOptions.IgnoreCase | RegexOptions.Compiled);

        public readonly struct Verdict
        {
            public readonly bool Ok; public readonly string Reason; public readonly string Text;
            public Verdict(bool ok, string reason, string text) { Ok = ok; Reason = reason; Text = text; }
        }

        public static Verdict Check(string reply, string facts)
        {
            if (string.IsNullOrWhiteSpace(reply)) return new Verdict(false, "empty", "");
            var text = Regex.Replace(reply.Trim(), @"\s+", " ");
            if (OutOfCharacter.IsMatch(text)) return new Verdict(false, "out_of_character", "");
            if (AsksForPii.IsMatch(text)) return new Verdict(false, "asks_pii", "");
            if (Markup.IsMatch(text)) return new Verdict(false, "markup", "");
            var cited = Cfr.Matches(text).Cast<Match>().Select(m => Root(m.Value)).Distinct().ToList();
            var allowed = new HashSet<string>(Cfr.Matches(facts ?? "").Cast<Match>().Select(m => Root(m.Value)));
            var invented = cited.Where(c => !allowed.Contains(c)).ToList();
            if (invented.Count > 0) return new Verdict(false, "invented_cfr:" + string.Join("|", invented), "");
            return new Verdict(true, Trimmed(text) == text ? "ok" : "ok_trimmed", Trimmed(text));
        }

        // Section-level match: 1926.451(g)(1) is grounded if the facts cite 1926.451.
        static string Root(string cfr) { var i = cfr.IndexOf('('); return (i > 0 ? cfr.Substring(0, i) : cfr).ToLowerInvariant(); }

        static string Trimmed(string text)
        {
            var sentences = Regex.Split(text, @"(?<=[.!?])\s+").Where(s => s.Length > 0).ToList();
            var t = string.Join(" ", sentences.Take(MaxSentences));
            if (t.Length > MaxChars) { t = t.Substring(0, MaxChars); var cut = t.LastIndexOf(' '); t = (cut > 200 ? t.Substring(0, cut) : t).TrimEnd(',', ';', ' ') + "…"; }
            return t;
        }
    }
}
