using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using Newtonsoft.Json.Linq;

namespace SafetyTraining.Editor
{
    internal static class AnalyticsCsv
    {
        public static void Write(string path, IReadOnlyList<string> header, IEnumerable<string[]> rows)
        {
            var builder = new StringBuilder();
            builder.AppendLine(string.Join(",", header.Select(Escape)));
            foreach (var row in rows)
                builder.AppendLine(string.Join(",", row.Select(Escape)));
            File.WriteAllText(path, builder.ToString());
        }

        public static string ReadString(JObject json, string key, string fallback = "")
        {
            var token = json[key];
            return token == null ? fallback : token.ToString();
        }

        public static float ReadFloat(JObject json, string key)
        {
            var token = json[key];
            return token == null ? 0f : token.Value<float>();
        }

        public static int ReadInteger(JObject json, string key)
        {
            var token = json[key];
            return token == null ? 0 : token.Value<int>();
        }

        public static bool ReadBoolean(JObject json, string key)
        {
            var token = json[key];
            return token != null && token.Value<bool>();
        }

        public static DateTime ReadDate(JObject json, string key)
        {
            var token = json[key];
            return token == null ? DateTime.MinValue : DateTime.Parse(token.ToString(), null, DateTimeStyles.RoundtripKind);
        }

        public static string FormatDate(DateTime value)
        {
            return value == DateTime.MinValue ? string.Empty : value.ToUniversalTime().ToString("O", CultureInfo.InvariantCulture);
        }

        public static string FormatFloat(float value)
        {
            return value.ToString("0.###", CultureInfo.InvariantCulture);
        }

        public static string FormatInteger(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }

        static string Escape(string value)
        {
            value ??= string.Empty;
            if (!value.Contains(",") && !value.Contains("\"") && !value.Contains("\n") && !value.Contains("\r"))
                return value;
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        }
    }
}
