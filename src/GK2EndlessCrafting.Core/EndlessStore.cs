using System.Collections.Generic;
using System.Text;

namespace GK2EndlessCrafting.Core
{
    // Простой построчный формат: "stationId|recipeId".
    public static class EndlessStore
    {
        public static string Serialize(EndlessRegistry registry)
        {
            var sb = new StringBuilder();
            if (registry == null) return string.Empty;
            var keys = new List<string>();
            var map = new Dictionary<string, string>();
            foreach (var e in registry.All()) { keys.Add(e.Key); map[e.Key] = e.Value; }
            keys.Sort(System.StringComparer.Ordinal);
            foreach (var k in keys)
            {
                if (string.IsNullOrEmpty(k) || string.IsNullOrEmpty(map[k])) continue;
                sb.Append(k).Append('|').Append(map[k]).Append('\n');
            }
            return sb.ToString();
        }

        public static void Load(EndlessRegistry registry, string text)
        {
            if (registry == null) return;
            Clear(registry);
            if (string.IsNullOrEmpty(text)) return;
            foreach (var raw in text.Split('\n'))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line[0] == '#') continue;
                var i = line.IndexOf('|');
                if (i <= 0 || i >= line.Length - 1) continue;
                registry.Set(line.Substring(0, i).Trim(), line.Substring(i + 1).Trim());
            }
        }

        private static void Clear(EndlessRegistry registry)
        {
            var keys = new List<string>();
            foreach (var e in registry.All()) keys.Add(e.Key);
            foreach (var k in keys) registry.Clear(k);
        }
    }
}
