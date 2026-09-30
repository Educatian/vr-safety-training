using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;

namespace Jobsite.Tests
{
    // Event schema v1: every event kind the runtime logs must be on the server allowlist (Web/functions/api/_kinds.js),
    // otherwise the ingest would reject real play data. Source scan; skipped when run outside the project.
    public sealed class TelemetrySchemaTests
    {
        [Test]
        public void EveryLoggedKind_IsOnTheServerAllowlist()
        {
            var runtime = Path.Combine("Assets", "_Game", "Scripts", "Runtime");
            var kindsJs = Path.Combine("Web", "functions", "api", "_kinds.js");
            if (!Directory.Exists(runtime) || !File.Exists(kindsJs)) return;
            var allow = Regex.Matches(File.ReadAllText(kindsJs), "\"([A-Za-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value).ToList();
            var logged = Directory.GetFiles(runtime, "*.cs")
                .SelectMany(f => Regex.Matches(File.ReadAllText(f), "\\bLog\\(\"([A-Za-z_]+)\"").Cast<Match>().Select(m => m.Groups[1].Value))
                .Distinct().ToList();
            Assert.That(logged.Count, Is.GreaterThan(20));
            var missing = logged.Where(k => !allow.Contains(k)).ToList();
            Assert.That(missing.Count == 0, "kinds missing from _kinds.js: " + string.Join(", ", missing));
            foreach (var k in System.Enum.GetNames(typeof(Jobsite.Core.DayEventKind)))
                Assert.That(allow.Contains(k), "DayEventKind " + k + " missing from _kinds.js");
        }
    }
}
