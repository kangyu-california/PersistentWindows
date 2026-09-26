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
        public static readonly string[] Languages = { "en", "zh" };

        /// <summary>active language name (a key of the entries, e.g. "en" or "zh")</summary>
        public static string Current = "en";

        public static string LangFile = null;      // lang.txt: current language
        public static string StringsFile = null;   // translations.json: all translations

        /// <summary>small helper: build a named-language entry</summary>
        private static Dictionary<string, string> Row(params string[] nameValuePairs)
        {
            var row = new Dictionary<string, string>();
            for (int i = 0; i + 1 < nameValuePairs.Length; i += 2)
                row[nameValuePairs[i]] = nameValuePairs[i + 1];
            return row;
        }

        /// <summary>factory defaults: used to (re)create translations.json and as fallback</summary>
        private static readonly Dictionary<string, Dictionary<string, string>> Defaults = new Dictionary<string, Dictionary<string, string>>
        {
            // tray menu
            { "menu.captureDisk", Row(
                "en", "Capture windows to disk",
                "zh", "保存窗口布局(&C)") },
            { "menu.restoreDisk", Row(
                "en", "Restore windows from disk",
                "zh", "恢复窗口布局(&R)") },
            { "menu.restoreMinimized", Row(
                "en", "Restore all minimized windows",
                "zh", "展开所有最小化的窗口") },
            { "menu.captureSnapshot", Row(
                "en", "Capture snapshot",
                "zh", "捕捉布局快照(&S)") },
            { "menu.restoreSnapshot", Row(
                "en", "Restore snapshot",
                "zh", "恢复布局快照(&N)") },
            { "menu.pauseAutoRestore", Row(
                "en", "Pause auto restore",
                "zh", "暂停自动恢复(&P)") },
            { "menu.resumeAutoRestore", Row(
                "en", "Resume auto restore",
                "zh", "继续自动恢复") },
            { "menu.tryCustomIcon", Row(
                "en", "Try customized icon",
                "zh", "尝试自定义图标") },
            { "menu.disableCustomIcon", Row(
                "en", "Disable customized icon",
                "zh", "停用自定义图标") },
            { "menu.enableWebCommander", Row(
                "en", "Enable webpage commander",
                "zh", "启用网页控制窗口") },
            { "menu.disableWebCommander", Row(
                "en", "Disable webpage commander",
                "zh", "停用网页控制窗口") },
            { "menu.enableUpgradeNotice", Row(
                "en", "Enable upgrade notice",
                "zh", "启用升级提醒") },
            { "menu.disableUpgradeNotice", Row(
                "en", "Disable upgrade notice",
                "zh", "关闭升级提醒") },
            { "menu.upgradeTo", Row(
                "en", "Upgrade to {0}",
                "zh", "升级到 {0}") },
            { "menu.language", Row(
                "en", "Language",
                "zh", "语言 / Language") },
            { "lang.name.en", Row(
                "en", "English",
                "zh", "English") },
            { "lang.name.zh", Row(
                "en", "Chinese (Simplified)",
                "zh", "简体中文") },
            { "menu.help", Row(
                "en", "&Help",
                "zh", "帮助(&H)") },
            { "menu.exit", Row(
                "en", "&Exit",
                "zh", "退出(&X)") },

            // balloon notifications
            { "balloon.restoring", Row(
                "en", "Please wait while restoring windows",
                "zh", "正在恢复窗口布局，请稍候") },
            { "balloon.languageSwitched", Row(
                "en", "Language switched",
                "zh", "语言已切换") },
            { "balloon.languageApplied", Row(
                "en", "The interface language has been applied.",
                "zh", "界面语言已即时生效。") },
            { "balloon.upgradeAvailable", Row(
                "en", "{0} {1} upgrade is available",
                "zh", "{0} {1} 有新版本可用") },
            { "balloon.upgradeNoticeHint", Row(
                "en", "The upgrade notice can be disabled in menu",
                "zh", "可在菜单中关闭升级提醒") },
            { "balloon.snapshotCaptured", Row(
                "en", "snapshot '{0}' is captured",
                "zh", "快照 '{0}' 已保存") },
            { "balloon.snapshotRestoreHint", Row(
                "en", "click icon then immediately press key '{0}' to restore the snapshot",
                "zh", "点击图标后立即按数字键 '{0}' 即可恢复该快照") },
            { "balloon.webCommanderInvoked", Row(
                "en", "webpage commander is invoked via hotkey",
                "zh", "已通过热键呼出网页控制窗口") },
            { "balloon.webCommanderRevoke", Row(
                "en", "Press the hotkey (Alt + W) again to revoke",
                "zh", "再按一次热键 (Alt + W) 即可收回") },

            // message boxes
            { "msg.alreadyRunning", Row(
                "en", "Another instance is already running.",
                "zh", "程序已经在运行中。") },
            { "msg.proceedRestore", Row(
                "en", "Proceed to restore windows",
                "zh", "即将恢复窗口布局") },
            { "msg.switchVirtualDesktop", Row(
                "en", "Switch to another virtual desktop to restore windows",
                "zh", "请切换到其他虚拟桌面后再恢复窗口") },
            { "msg.webCommanderZKey", Row(
                "en", "You may also press Z key to toggle the size of webpage commander window",
                "zh", "也可以按 Z 键来调整网页控制窗口的大小") },

            // splash screen
            { "splash.infoLabel", Row(
                "en", "info",
                "zh", "关于") },
            { "splash.info", Row(
                "en", "\n    Persistent Windows\n    Version {0}\n                \n    Author:        Min Yong Kim\n    Contributors:  Kang Yu, Sean Aitken\n    ",
                "zh", "\n    Persistent Windows\n    版本 {0}\n                \n    作者:        Min Yong Kim\n    贡献者:  Kang Yu, Sean Aitken\n    ") },
            { "splash.contributors", Row(
                "en", "Recognize All Contributors",
                "zh", "致谢所有贡献者") },

            // webpage commander window
            { "webcmd.prevTab", Row(
                "en", "Prev Tab",
                "zh", "上一个标签") },
            { "webcmd.nextTab", Row(
                "en", "Next Tab",
                "zh", "下一个标签") },
            { "webcmd.closeTab", Row(
                "en", "Close Tab",
                "zh", "关闭标签") },
            { "webcmd.newTab", Row(
                "en", "New  Tab",
                "zh", "新建标签") },
            { "webcmd.home", Row(
                "en", "Home",
                "zh", "主页") },
            { "webcmd.end", Row(
                "en", "End",
                "zh", "末页") },
            { "webcmd.prevUrl", Row(
                "en", "Prev Url",
                "zh", "上一个网址") },
            { "webcmd.nextUrl", Row(
                "en", "Next Url",
                "zh", "下一个网址") },

            // dialogs / message boxes with buttons
            { "dlg.ok", Row(
                "en", "OK",
                "zh", "确定") },
            { "dlg.cancel", Row(
                "en", "Cancel",
                "zh", "取消") },
            { "dlg.selectLayout", Row(
                "en", "Select a desktop layout to restore",
                "zh", "请选择要恢复的桌面布局") },
            { "dlg.snapshotDigitName", Row(
                "en", "Enter one digit or a letter to name the snapshot",
                "zh", "输入一个数字或字母作为快照名称") },
            { "dlg.snapshotName", Row(
                "en", "Enter the name of snapshot",
                "zh", "请输入快照名称") },
            { "dlg.captureDiskName", Row(
                "en", "Enter the name of capture on disk",
                "zh", "请输入磁盘布局存档名称") },
            { "dlg.captureName", Row(
                "en", "Enter the name of capture",
                "zh", "请输入布局存档名称") },
        };

        /// <summary>active table = factory defaults overlaid with translations.json
        /// (the file can override any text and add new languages/keys)</summary>
        public static Dictionary<string, Dictionary<string, string>> Strings = new Dictionary<string, Dictionary<string, string>>(Defaults);

        /// <summary>look up an entry by key; missing languages fall back to "en";
        /// args fill {0}-style placeholders when given</summary>
        public static string T(string key, params object[] args)
        {
            string text = key;
            Dictionary<string, string> row;
            if (Strings.TryGetValue(key, out row))
            {
                if (!row.TryGetValue(Current, out text) && !row.TryGetValue("en", out text))
                    text = key;
            }
            if (args != null && args.Length > 0)
                text = string.Format(text, args);
            return text;
        }

        /// <summary>load translations + persisted language choice (called once at startup)</summary>
        public static void Load(string appDataFolder)
        {
            LangFile = Path.Combine(appDataFolder, "lang.txt");
            StringsFile = Path.Combine(appDataFolder, "translations.json");
            try
            {
                if (File.Exists(StringsFile))
                    ReadStringsFile();
                else
                    WriteStringsFile();   // first run: ship the default file

                if (File.Exists(LangFile))
                {
                    string v = File.ReadAllText(LangFile).Trim();
                    if (v.Length > 0) Current = v;
                }
            }
            catch (Exception)
            {
                // any problem with the file just leaves the built-in defaults active
            }
        }

        private static void ReadStringsFile()
        {
            string json = File.ReadAllText(StringsFile);
            var parsed = new JavaScriptSerializer().Deserialize<Dictionary<string, Dictionary<string, string>>>(json);
            if (parsed == null || parsed.Count == 0)
            {
                WriteStringsFile();
                return;
            }
            // overlay: parsed entries extend/replace the built-in defaults, so even a
            // partial file never leaves the UI without a string
            Strings = new Dictionary<string, Dictionary<string, string>>(Defaults);
            foreach (var kv in parsed)
                Strings[kv.Key] = kv.Value;
        }

        private static void WriteStringsFile()
        {
            var sb = new StringBuilder();
            sb.Append("{\n");
            int i = 0;
            foreach (var kv in Defaults)
            {
                sb.Append("  ").Append(Quote(kv.Key)).Append(": {");
                int j = 0;
                foreach (var lang in kv.Value)
                {
                    sb.Append(j++ == 0 ? "\n" : ",\n")
                      .Append("    ").Append(Quote(lang.Key)).Append(": ").Append(Quote(lang.Value));
                }
                sb.Append("\n  }");
                if (++i < Defaults.Count) sb.Append(",");
                sb.Append("\n");
            }
            sb.Append("}\n");
            File.WriteAllText(StringsFile, sb.ToString(), new UTF8Encoding(false));
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
            Current = language;
            try
            {
                if (LangFile != null)
                    File.WriteAllText(LangFile, Current);
            }
            catch (Exception)
            {
                // preference not persisted; session still switches
            }
        }
    }
}
