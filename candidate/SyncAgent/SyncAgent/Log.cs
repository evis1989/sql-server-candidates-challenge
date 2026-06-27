using System;
using System.IO;

namespace SyncAgent
{
    /// <summary>
    /// Minimal logger: timestamped lines to console and a log file beside the exe.
    /// ponytail: file+console sink, swap for Serilog/EventLog if structured logging is needed.
    /// </summary>
    public static class Log
    {
        private static readonly object Gate = new object();
        private static readonly string LogFile =
            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "sync-agent.log");

        public static void Info(string message) => Write("INFO", message);
        public static void Error(string message) => Write("ERROR", message);

        private static void Write(string level, string message)
        {
            var line = DateTime.UtcNow.ToString("o") + " [" + level + "] " + message;
            lock (Gate)
            {
                Console.WriteLine(line);
                try { File.AppendAllText(LogFile, line + Environment.NewLine); }
                catch { /* never let logging crash the loop */ }
            }
        }
    }
}
