using System;
using System.IO;

namespace PersistentWindows.Common
{
    /// <summary>
    /// Minimal en/zh string switch for the cn fork.
    /// Lang.T(english, chinese) returns the variant for the active language.
    /// The choice is persisted in "lang.txt" under the app data folder and
    /// toggled from the tray menu ("Language"); it takes effect after restart.
    /// </summary>
    public static class Lang
    {
        public static bool Chinese = false;
        public static string LangFile = null;

        public static string T(string en, string zh)
        {
            return Chinese ? zh : en;
        }

        public static void Load(string appDataFolder)
        {
            LangFile = Path.Combine(appDataFolder, "lang.txt");
            try
            {
                if (File.Exists(LangFile))
                    Chinese = File.ReadAllText(LangFile).Trim() == "zh";
            }
            catch (Exception)
            {
                // fall back to English on any read problem
            }
        }

        public static void Set(bool chinese)
        {
            Chinese = chinese;
            try
            {
                if (LangFile != null)
                    File.WriteAllText(LangFile, chinese ? "zh" : "en");
            }
            catch (Exception)
            {
                // preference not persisted; session still switches
            }
        }
    }
}
