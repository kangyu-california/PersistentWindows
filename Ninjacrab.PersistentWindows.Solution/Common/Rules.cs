using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

using PersistentWindows.Common.Diagnostics;

namespace PersistentWindows.Common
{
    /// <summary>
    /// User-editable lists, stored in rules.json of the app data folder:
    ///   pause_process   - capture and auto restore are paused while any of these processes is running
    ///   exclude_process - windows of these processes are never captured or controlled
    ///   exclude_title   - windows whose title contains one of these texts are never captured or controlled
    /// Process names are compared without ".exe", everything is case-insensitive.
    /// The command line options -pause_process, -exclude_process and -exclude_title add
    /// entries for the running instance only; they are not shown in the editor nor saved.
    /// The lists are replaced as a whole on every change, so readers on other threads
    /// always see a complete list without locking.
    /// </summary>
    public static class Rules
    {
        public const string FileName = "rules.json";

        private static volatile string[] pauseProcess = new string[0];
        private static volatile string[] excludeProcess = new string[0];
        private static volatile string[] excludeTitle = new string[0];
        private static volatile HashSet<string> pauseProcessSet = NewSet(null);
        private static volatile HashSet<string> excludeProcessSet = NewSet(null);

        // entries given on the command line, and the saved + command line titles
        private static string[] cmdPauseProcess = new string[0];
        private static string[] cmdExcludeProcess = new string[0];
        private static string[] cmdExcludeTitle = new string[0];
        private static volatile string[] excludeTitleAll = new string[0];

        public static string[] PauseProcess { get { return pauseProcess; } }
        public static string[] ExcludeProcess { get { return excludeProcess; } }
        public static string[] ExcludeTitle { get { return excludeTitle; } }

        public static bool HasPauseRule { get { return pauseProcessSet.Count > 0; } }
        public static bool HasExcludeRule { get { return excludeProcessSet.Count > 0 || excludeTitleAll.Length > 0; } }
        public static bool HasExcludeProcessRule { get { return excludeProcessSet.Count > 0; } }
        public static bool HasExcludeTitleRule { get { return excludeTitleAll.Length > 0; } }

        private static HashSet<string> NewSet(IEnumerable<string> items)
        {
            var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            if (items != null)
                foreach (var item in items)
                    set.Add(item);
            return set;
        }

        /// <summary>trim, drop a trailing ".exe", drop blanks and duplicates</summary>
        public static string[] NormalizeProcessNames(IEnumerable<string> names)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            if (names != null)
                foreach (var name in names)
                {
                    if (name == null)
                        continue;
                    string s = name.Trim().Trim('"').Trim();
                    if (s.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
                        s = s.Substring(0, s.Length - 4);
                    if (s.Length > 0 && seen.Add(s))
                        result.Add(s);
                }
            return result.ToArray();
        }

        public static string[] NormalizeTitles(IEnumerable<string> titles)
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var result = new List<string>();
            if (titles != null)
                foreach (var title in titles)
                {
                    if (title == null)
                        continue;
                    string s = title.Trim();
                    if (s.Length > 0 && seen.Add(s))
                        result.Add(s);
                }
            return result.ToArray();
        }

        public static void Set(IEnumerable<string> pause_process, IEnumerable<string> exclude_process, IEnumerable<string> exclude_title)
        {
            var pause = NormalizeProcessNames(pause_process);
            var exclude = NormalizeProcessNames(exclude_process);
            var titles = NormalizeTitles(exclude_title);

            pauseProcess = pause;
            excludeProcess = exclude;
            excludeTitle = titles;
            Rebuild();
        }

        // merge the saved lists with the command line entries
        private static void Rebuild()
        {
            var pause = NewSet(pauseProcess);
            pause.UnionWith(cmdPauseProcess);
            var exclude = NewSet(excludeProcess);
            exclude.UnionWith(cmdExcludeProcess);
            var titles = new List<string>(excludeTitle);
            titles.AddRange(cmdExcludeTitle);

            pauseProcessSet = pause;
            excludeProcessSet = exclude;
            excludeTitleAll = NormalizeTitles(titles);
        }

        /// <summary>
        /// entries of the command line options, each a list separated by ';' like -ignore_process.
        /// null leaves that list unchanged
        /// </summary>
        public static void SetCommandLine(string pause_process, string exclude_process, string exclude_title)
        {
            if (pause_process != null)
                cmdPauseProcess = NormalizeProcessNames(pause_process.Split(';'));
            if (exclude_process != null)
                cmdExcludeProcess = NormalizeProcessNames(exclude_process.Split(';'));
            if (exclude_title != null)
                cmdExcludeTitle = NormalizeTitles(exclude_title.Split(';'));
            Rebuild();
        }

        public static void Load(string dir)
        {
            try
            {
                string file = Path.Combine(dir, FileName);
                if (!File.Exists(file))
                    return;

                var parsed = new JavaScriptSerializer().Deserialize<Dictionary<string, string[]>>(File.ReadAllText(file));
                if (parsed == null)
                    return;

                string[] pause, exclude, titles;
                parsed.TryGetValue("pause_process", out pause);
                parsed.TryGetValue("exclude_process", out exclude);
                parsed.TryGetValue("exclude_title", out titles);
                Set(pause, exclude, titles);
            }
            catch (Exception ex)
            {
                // a broken file leaves the lists empty
                Log.Error(ex.ToString());
            }
        }

        public static void Save(string dir)
        {
            var sb = new StringBuilder();
            sb.Append("{\r\n");
            AppendList(sb, "pause_process", pauseProcess, true);
            AppendList(sb, "exclude_process", excludeProcess, true);
            AppendList(sb, "exclude_title", excludeTitle, false);
            sb.Append("}\r\n");
            File.WriteAllText(Path.Combine(dir, FileName), sb.ToString(), new UTF8Encoding(false));
        }

        private static void AppendList(StringBuilder sb, string name, string[] items, bool more)
        {
            var js = new JavaScriptSerializer();
            sb.Append("  ").Append(js.Serialize(name)).Append(": [");
            for (int i = 0; i < items.Length; ++i)
                sb.Append(i == 0 ? "\r\n    " : ",\r\n    ").Append(js.Serialize(items[i]));
            sb.Append(items.Length > 0 ? "\r\n  ]" : "]").Append(more ? ",\r\n" : "\r\n");
        }

        public static bool IsExcludedProcess(string process_name)
        {
            return !string.IsNullOrEmpty(process_name) && excludeProcessSet.Contains(process_name);
        }

        public static bool IsExcludedTitle(string title)
        {
            if (string.IsNullOrEmpty(title))
                return false;

            var titles = excludeTitleAll;
            foreach (var t in titles)
            {
                if (title.IndexOf(t, StringComparison.OrdinalIgnoreCase) >= 0)
                    return true;
            }
            return false;
        }

        /// <summary>name of the first running process on the pause list, null if none is running</summary>
        public static string FindRunningPauseProcess()
        {
            var set = pauseProcessSet;
            if (set.Count == 0)
                return null;

            string found = null;
            Process[] processes;
            try
            {
                processes = Process.GetProcesses();
            }
            catch (Exception ex)
            {
                Log.Error(ex.ToString());
                return null;
            }

            foreach (var p in processes)
            {
                try
                {
                    if (found == null && set.Contains(p.ProcessName))
                        found = p.ProcessName;
                }
                catch (Exception)
                {
                    // process already exited
                }
                finally
                {
                    p.Dispose();
                }
            }

            return found;
        }
    }
}
