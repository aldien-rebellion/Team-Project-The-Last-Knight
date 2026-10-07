using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using UnityEngine;

namespace TheLastKnight.Core
{
    internal static class LocalizedTextCatalog
    {
        [Serializable] private sealed class Catalog { public Entry[] entries; }
        [Serializable] private sealed class Entry { public string en; public string th; public string source; }
        private sealed class Template
        {
            public Regex pattern;
            public string output;
        }
        private static readonly Dictionary<string, Entry> Texts = new Dictionary<string, Entry>(StringComparer.Ordinal);
        private static readonly List<Template> English = new List<Template>();
        private static readonly List<Template> Thai = new List<Template>();
        private static bool _loaded;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCache()
        {
            Texts.Clear();
            English.Clear();
            Thai.Clear();
            _loaded = false;
        }

        private static void Load()
        {
            if (_loaded) return;
            _loaded = true;
            var asset = Resources.Load<TextAsset>("Localization/GameTexts");
            if (asset == null) { Debug.LogError("Missing Localization/GameTexts catalog."); return; }
            foreach (var entry in JsonUtility.FromJson<Catalog>(asset.text).entries)
            {
                Texts[entry.en] = entry;
                Texts[entry.th] = entry;
                if (!string.IsNullOrEmpty(entry.source)) Texts[entry.source] = entry;
                // Dialogue presentation collapses paragraph breaks and normalizes punctuation.
                Texts[NormalizeDialogue(entry.en)] = entry;
                Texts[NormalizeDialogue(entry.th)] = entry;
                if (!Regex.IsMatch(entry.en, @"\{\d+\}")) continue;
                AddTemplate(English, entry.th, entry.en);
                AddTemplate(English, entry.en, entry.en);
                AddTemplate(Thai, entry.en, entry.th);
                AddTemplate(Thai, entry.th, entry.th);
            }
        }

        private static string NormalizeDialogue(string text)
        {
            return Regex.Replace(text, @"\n(?:[ \t]*\n)+", "\n").Trim()
                .Replace("—", "-").Replace("…", "...").Replace("|", ",").Replace("◆", "");
        }

        private static void AddTemplate(List<Template> templates, string source, string output)
        {
            var matches = Regex.Matches(source, @"\{(\d+)\}");
            int position = 0;
            string pattern = "^";
            foreach (Match match in matches)
            {
                pattern += Regex.Escape(source.Substring(position, match.Index - position));
                pattern += "(?<arg" + match.Groups[1].Value + ">.+?)";
                position = match.Index + match.Length;
            }
            pattern += Regex.Escape(source.Substring(position)) + "$";
            templates.Add(new Template { pattern = new Regex(pattern, RegexOptions.Singleline | RegexOptions.CultureInvariant), output = output });
        }

        public static string Translate(string source, GameLanguage language, int depth = 0)
        {
            if (string.IsNullOrEmpty(source) || depth > 8) return source;
            // Attribute identifiers stay the same in every locale, including template arguments.
            if (source == "STR" || source == "AGI" || source == "VIT" || source == "DEX") return source;
            Load();
            if (source.StartsWith("> ", StringComparison.Ordinal))
                return "> " + Translate(source.Substring(2), language, depth + 1);
            if (Texts.TryGetValue(source, out var entry))
                return language == GameLanguage.English ? entry.en : entry.th;
            // Templates preserve values, hotkeys and markup while translating dynamic UI text.
            foreach (var template in language == GameLanguage.English ? English : Thai)
            {
                var match = template.pattern.Match(source);
                if (!match.Success) continue;
                return Regex.Replace(template.output, @"\{(\d+)\}", placeholder =>
                    Translate(match.Groups["arg" + placeholder.Groups[1].Value].Value, language, depth + 1));
            }
            // Translate individual lines and rich-text spans without changing formatting tags.
            var pieces = Regex.Split(source, @"(<[^>]+>|\r?\n)");
            if (pieces.Length > 1)
            {
                for (int i = 0; i < pieces.Length; i++)
                    if (!pieces[i].StartsWith("<", StringComparison.Ordinal) && pieces[i] != "\n" && pieces[i] != "\r\n")
                        pieces[i] = Translate(pieces[i], language, depth + 1);
                return string.Concat(pieces);
            }
            string trimmed = source.Trim();
            if (trimmed != source && trimmed.Length > 0)
            {
                int start = source.IndexOf(trimmed, StringComparison.Ordinal);
                return source.Substring(0, start) + Translate(trimmed, language, depth + 1) + source.Substring(start + trimmed.Length);
            }
            return source;
        }
    }
}
