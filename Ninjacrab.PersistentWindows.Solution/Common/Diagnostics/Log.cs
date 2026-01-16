using System;
using System.IO;
using System.Diagnostics;

namespace PersistentWindows.Common.Diagnostics
{
    public class Log
    {
        static EventLog eventLog;
        public static bool silent = false;
        static bool registered = false;
        public static void Init()
        {
            eventLog = new EventLog();
            string app_name = System.Windows.Forms.Application.ProductName;
            try
            {
                if (!EventLog.SourceExists(app_name))
                {
                    // CreateEventSource requires administrative privileges
                    EventLog.CreateEventSource(app_name, "Application");
                    Console.WriteLine($"Created Event Source '{app_name}'. Please restart the application for changes to take full effect.");
                    // Note: If you create a new source for a custom log, you might need to restart the computer for changes to take full effect in the Event Viewer.
                }
                registered = true;
                eventLog.Source = app_name;
            }
            catch (Exception ex)
            {
                eventLog.Source = "Application";
            }
        }

        public static void Exit()
        {
            eventLog.Close();
        }

        /// <summary>
        /// Occurs when something is logged. STATIC EVENT!
        /// </summary>

        public static void Trace(string format, params object[] args)
        {
            if (silent)
                return;
#if DEBUG
            var message = Format(format, args);
            Console.Write(message);
#endif
        }

        public static void Info(string format, params object[] args)
        {
            if (silent)
                return;
#if DEBUG
            var message = Format(format, args);
            Console.Write(message);
#endif
        }

        public static void Error(string format, params object[] args)
        {
            if (silent)
                return;

            var message = Format(format, args);
            if (message.Contains("Cannot create a file when that file already exists"))
            {
                // ignore trivial error
                return;
            }

            if (message.Contains("Access is denied"))
            {
                // ignore window move failure due to lack of admin privilege
                return;
            }

#if DEBUG
            Console.Write(message);
#endif
            if (!registered)
            {
                message = message.Substring(message.IndexOf("::") + 3);
                eventLog.WriteEntry(System.Windows.Forms.Application.ProductName + ": " + message, EventLogEntryType.Information, 9999, 0);
            }
            else
                eventLog.WriteEntry(message, EventLogEntryType.Information, 9999, 0);
        }

        public static void Event(string format, params object[] args)
        {
            if (silent)
                return;

            var message = Format(format, args);
#if DEBUG
            Console.Write(message);
#endif
            if (!registered)
            {
                message = message.Substring(message.IndexOf("::") + 3);
                eventLog.WriteEntry(System.Windows.Forms.Application.ProductName + ": " + message, EventLogEntryType.Information, 9990, 0);
            }
            else
                eventLog.WriteEntry(message, EventLogEntryType.Information, 9990, 0);
        }

        /// <summary>
        /// Since string.Format doesn't like args being null or having no entries.
        /// </summary>
        /// <param name="format">The format.</param>
        /// <param name="args">The args.</param>
        /// <returns></returns>
        private static string Format(string format, params object[] args)
        {
            if (string.IsNullOrEmpty(format))
            {
                return "\n";
            }

            bool arg_null = args.Length == 0;
            if (!registered)
            return arg_null ? $"{DateTime.Now} :: " + format + "\n":
                $"{DateTime.Now} :: " + string.Format(format, args) + "\n";

            return arg_null ? format + "\n":
                string.Format(format, args) + "\n";
        }

    }
}
