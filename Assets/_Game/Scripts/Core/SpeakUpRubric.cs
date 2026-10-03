using System;
using System.Linq;
using System.Text.RegularExpressions;

namespace Jobsite.Core
{
    // "Say it in your own words" for the foreman's pushback (docs/AwesomeAiGames_Plan.md phase 4). The STANCE that is
    // scored comes from this deterministic, inspectable rubric, never from a language model: the model (with AI consent)
    // only voices Ray's reaction to the stance decided here, so the evidence stays reproducible.
    //   Aggressive: hostility (insults, threats, "not your call", shouting) - the stop holds but trust is spent.
    //   Assertive : the stop holds ("stays stopped", "not until", "can't") without hostility.
    //   Passive   : no hold, or the work is handed back ("ok", "after lunch", "keep going").
    // Quality notes (reason given, respect / collaboration, an offer to get the fix in) drive the feedback tip.
    public static class SpeakUpRubric
    {
        public sealed class Result
        {
            public SpeakUpStyle Style;
            public bool Hold, Reason, Respect, Offer, Hostile;
            public string Tip = "";
            public string Flags => $"hold={(Hold ? 1 : 0)} reason={(Reason ? 1 : 0)} respect={(Respect ? 1 : 0)} offer={(Offer ? 1 : 0)} hostile={(Hostile ? 1 : 0)}";
        }

        static readonly string[] Yield = { @"\bok(ay)?\b", @"\bfine\b", @"\bgo ahead\b", @"\blater\b", @"\blunch\b", @"\bkeep (them|em|it|going|working)\b",
            @"\byou'?re right\b", @"\bnever ?mind\b", @"\byour call\b(?! )", @"\bdon'?t (need to )?stop\b", @"\bno need to stop\b", @"\bcarry on\b", @"\bresume\b" };
        static readonly string[] HoldWords = { @"\bstop(ped|s)?\b", @"\bnot until\b", @"\buntil\b", @"\bcan'?t\b", @"\bwon'?t\b", @"\bcannot\b", @"\bno work\b",
            @"\bhold\b", @"\bnobody\b", @"\bno one\b", @"\bnot (safe|happening|going)\b", @"\bunsafe\b", @"\bfirst\b", @"\bbefore\b", @"\bnot yet\b", @"\bno\b", @"\bstays?\b" };
        static readonly string[] HostileWords = { @"\bshut up\b", @"\bback off\b", @"\bnot your call\b", @"\bidiot\b", @"\bstupid\b", @"\bmoron\b", @"\bi don'?t care\b",
            @"\bdo your (own )?job\b", @"\bor else\b", @"\bget you fired\b", @"\breport you\b", @"\bdumb\b", @"\bf+u+c+k", @"\bsh[i1]t\b", @"\bdamn\b", @"\bhell\b" };
        static readonly string[] ReasonWords = { @"fall", @"collapse", @"cave", @"struck", @"crush", @"shock", @"electr", @"hurt", @"injur", @"kill", @"die\b", @"osha", @"1926",
            @"\bstandard", @"\brule", @"\blaw\b", @"hazard", @"risk", @"\bsafe", @"expos", @"\blife\b", @"\blives\b", @"emergency", @"hospital" };
        static readonly string[] RespectWords = { @"\bplease\b", @"\bthanks?\b", @"\bthank you\b", @"\bi (understand|get it|hear you|know)\b", @"\bappreciate\b", @"\bsorry\b",
            @"\btogether\b", @"\blet'?s\b", @"\bwe\b", @"\bus\b", @"\bray\b" };
        static readonly string[] OfferWords = { @"\bi'?ll help\b", @"\bhelp (you|get|us)\b", @"\bi'?ll (get|grab|fix|call|bring|set)\b", @"\bquick(ly)?\b", @"\bfast\b",
            @"\bminutes?\b", @"\bright away\b", @"\bas soon as\b", @"\bthen (we|you|they)\b", @"\bback to work\b" };

        static bool Any(string text, string[] patterns) => patterns.Any(p => Regex.IsMatch(text, p, RegexOptions.IgnoreCase));

        public static Result Classify(string text, string hazardName = "")
        {
            var r = new Result();
            var t = (text ?? "").Trim();
            if (t.Length == 0) { r.Style = SpeakUpStyle.Passive; r.Tip = "Say something: silence hands the crew back to the hazard."; return r; }
            var letters = t.Where(char.IsLetter).ToList();
            var shouting = letters.Count >= 8 && letters.Count(char.IsUpper) > letters.Count * 0.7 || t.Count(c => c == '!') >= 3;
            r.Hostile = shouting || Any(t, HostileWords);
            var yields = Any(t, Yield);
            // "don't stop" / "no need to stop" are yields, not holds.
            var holdText = Regex.Replace(t, @"\b(don'?t|no need to) stop\b", "", RegexOptions.IgnoreCase);
            r.Hold = Any(holdText, HoldWords) && !(yields && !Regex.IsMatch(holdText, @"\b(not until|until|can'?t|won'?t|cannot|stays?)\b", RegexOptions.IgnoreCase));
            var hazardTokens = (hazardName ?? "").ToLowerInvariant().Split(new[] { ' ', '-', '/' }, StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 4);
            r.Reason = Any(t, ReasonWords) || hazardTokens.Any(w => t.ToLowerInvariant().Contains(w));
            r.Respect = Any(t, RespectWords) && !r.Hostile;
            r.Offer = Any(t, OfferWords);
            r.Style = r.Hostile ? SpeakUpStyle.Aggressive : r.Hold ? SpeakUpStyle.Assertive : SpeakUpStyle.Passive;
            r.Tip = r.Style == SpeakUpStyle.Aggressive ? "The stop holds, but hostility costs the foreman's trust. Keep it firm and respectful."
                : r.Style == SpeakUpStyle.Passive ? "Nothing in that keeps the stop in place. Say it stays stopped until the fix is in."
                : !r.Reason ? "Firm. Next time name the hazard and what could happen: a reason makes the stop stick."
                : !r.Offer ? "Firm, with a reason. Offering to help get the fix in fast keeps the foreman on side."
                : "Firm, respectful, with a reason and a way back to work. That's the competent-person answer.";
            return r;
        }
    }
}
