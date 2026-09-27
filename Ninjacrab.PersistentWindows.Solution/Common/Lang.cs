using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace PersistentWindows.Common
{
    /// <summary>
    /// File-based translation table for the cn fork.
    ///
    /// All translations live in ONE external, user-editable file:
    ///     user_data\translations.json
    ///
    /// Every entry is a key mapped to a named-language row, one language per line:
    ///
    ///     "menu.captureDisk": {
    ///       "en": "Capture windows to disk",
    ///       "zh": "保存窗口布局(&C)"
    ///     },
    ///
    /// Call sites only reference the key:  Lang.T("menu.captureDisk")
    /// (entries may contain {0}-style placeholders:  Lang.T("balloon.snapshotCaptured", id))
    ///
    /// Adding a language = add a new name/value line to the entries (e.g. "jp"),
    /// and register it in Languages below. Editing a translation = edit the file and
    /// restart; no recompiling. Missing entries / broken file fall back to the
    /// built-in English defaults, and a missing file is regenerated on start.
    /// </summary>
    public static class Lang
    {
        /// <summary>languages the tray toggle cycles through, in order</summary>
        public static HashSet<string> Languages = new HashSet<string>();

        /// <summary>active language name (a key of the entries, e.g. "en" or "zh")</summary>
        public static string CurrentLang = "en";

        public static string LangSelection = "display_language.txt";      // current language
        public static string LangTranslation = "translations.json";   // translation of menu/message for all languages

        /// <summary>small helper: build a named-language entry</summary>
        private static Dictionary<string, string> Row(params string[] nameValuePairs)
        {
            var row = new Dictionary<string, string>();
            for (int i = 0; i + 1 < nameValuePairs.Length; i += 2)
                row[nameValuePairs[i]] = nameValuePairs[i + 1];
            return row;
        }

        /// <summary>active table = factory defaults overlaid with translations.json
        /// (the file can override any text and add new languages/keys)</summary>
        public static Dictionary<string, Dictionary<string, string>> Strings = new Dictionary<string, Dictionary<string, string>>();

        /// <summary>look up an entry by key; missing languages fall back to "en";
        /// args fill {0}-style placeholders when given</summary>
        public static string T(string key, params object[] args)
        {
            string text = key;
            Dictionary<string, string> row;
            if (Strings.TryGetValue(key, out row))
            {
                if (!row.TryGetValue(CurrentLang, out text))
                    row.TryGetValue("en", out text);
            }
            if (args != null && args.Length > 0)
                text = string.Format(text, args);
            return text;
        }

        /// <summary>load translations + persisted language choice (called once at startup)</summary>
        public static void Load(string dir)
        {
            LangSelection = Path.Combine(dir, LangSelection);
            string file = Path.Combine(dir, LangTranslation);
            try
            {
                if (File.Exists(file))
                    ReadTranslation(file);

                if (File.Exists(LangSelection))
                {
                    string v = File.ReadAllText(LangSelection).Trim();
                    if (v.Length > 0) CurrentLang = v;
                }
            }
            catch (Exception)
            {
                // any problem with the file just leaves the built-in defaults active
            }
        }

        private static void ReadTranslation(string file)
        {
            string json = File.ReadAllText(file);
            var parsed = new JavaScriptSerializer().Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            // overlay: parsed entries extend/replace the built-in defaults, so even a
            // partial file never leaves the UI without a string
            foreach (var kv in parsed)
            {
                //Strings[kv.Key] = kv.Value;
                foreach (var keyvalue in kv.Value)
                {
                    if (!Strings.ContainsKey(kv.Key))
                        Strings[kv.Key] = new Dictionary<string, string>();
                    Strings[kv.Key][keyvalue.Key] = keyvalue.Value;

                    if (!Languages.Contains(keyvalue.Key))
                        Languages.Add(keyvalue.Key);
                }
            }
        }

        private static void WriteStringsFile()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            int i = 0;
            foreach (var kv in Strings)
            {
                sb.Append("  ").Append(Quote(kv.Key)).Append(": {");
                int j = 0;
                foreach (var lang in kv.Value)
                {
                    sb.Append(j++ == 0 ? "\n" : ",\n")
                      .Append("    ").Append(Quote(lang.Key)).Append(": ").Append(Quote(lang.Value));
                }
                sb.Append("\n  }");
                if (++i < Strings.Count) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("}\n");
            File.WriteAllText(LangTranslation, sb.ToString(), new UTF8Encoding(false));
        }

        private static string Quote(string s)
        {
            return "\"" + s.Replace("\\", "\\\\").Replace("\"", "\\\"")
                           .Replace("\n", "\\n").Replace("\r", "\\r").Replace("\t", "\\t") + "\"";
        }

        /// <summary>display name of a registered language, shown in the tray menu;
        /// defined as a "lang.name.{code}" table row, falls back to the code itself</summary>
        public static string DisplayName(string code)
        {
            string k = "lang.name." + code;
            return Strings.ContainsKey(k) ? T(k) : code;
        }

        /// <summary>switch language and persist the choice</summary>
        public static void Set(string language)
        {
            CurrentLang = language;
            try
            {
                File.WriteAllText(LangSelection, CurrentLang);
            }
            catch (Exception)
            {
                // preference not persisted; session still switches
            }
        }
    }
}
