using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityInjector.ConsoleUtil;

namespace CM3D2.YATranslator.Plugin.Utils
{
    [Flags]
    public enum VerbosityLevel
    {
        None = 0,
        Strings = 1 << 0,
        Textures = 1 << 1,
        Assets = 1 << 2,
        All = Strings | Textures | Assets
    }

    public class LogLevel
    {
        public static readonly LogLevel Error = new LogLevel(ConsoleColor.Red);
        public static readonly LogLevel Minor = new LogLevel(ConsoleColor.DarkGray);
        public static readonly LogLevel Normal = new LogLevel(ConsoleColor.Gray);
        public static readonly LogLevel Warning = new LogLevel(ConsoleColor.Yellow);

        public LogLevel(ConsoleColor color)
        {
            Color = color;
        }

        public ConsoleColor Color { get; }
    }

    public static class Logger
    {
        private static HashSet<string> cachedDumps;
        private const string DUMP_FILENAME = "TRANSLATION_DUMP";
        private static bool dumpFileLoaded;

        private static TextWriter dumpStream;
        public static string DumpPath { get; set; }
        public static bool EnableDump { get; set; }
        public static VerbosityLevel Verbosity { get; set; }

        public static bool InitDump()
        {
            if (!EnableDump || string.IsNullOrEmpty(DumpPath))
                return false;
            if (dumpFileLoaded)
                return true;
            if (!Directory.Exists(DumpPath))
                try
                {
                    Directory.CreateDirectory(DumpPath);
                }
                catch (Exception e)
                {
                    WriteLine(LogLevel.Error, $"Failed to create dump directory because {e.Message}");
                    return false;
                }
            string filePath = Path.Combine(DumpPath, $"{DUMP_FILENAME}.{DateTime.Now:yyyy-MM-dd-HHmmss}.txt");
            try
            {
                dumpStream = File.CreateText(filePath);
                dumpFileLoaded = true;
                cachedDumps = new HashSet<string>();
            }
            catch (Exception e)
            {
                WriteLine(LogLevel.Error, $"Failed to write dump to {filePath}. Reason: {e.Message}");
                return false;
            }
            return true;
        }

        public static void Dispose()
        {
            if (!dumpFileLoaded)
                return;

            dumpStream.Flush();
            dumpStream.Dispose();
        }

        public static void DumpLine(string line)
        {
            if (!InitDump())
                return;
            if (cachedDumps.Contains(line))
                return;
            cachedDumps.Add(line);
            lock (dumpStream)
            {
                dumpStream.WriteLine(line);
                dumpStream.Flush();
            }
        }

        public static void WriteLine(VerbosityLevel verbosity, LogLevel logLevel, string message)
        {
            if (IsLoggingTo(verbosity))
                WriteLine(logLevel, message);
        }

        public static void WriteLine(VerbosityLevel verbosity, string message)
        {
            WriteLine(verbosity, LogLevel.Normal, message);
        }

        public static bool IsLoggingTo(VerbosityLevel verbosity) => (verbosity & Verbosity) != 0;

        public static void WriteLine(LogLevel logLevel, string message)
        {
            ConsoleColor oldColor = SafeConsole.BackgroundColor;
            SafeConsole.ForegroundColor = logLevel.Color;
            WriteLine(message);
            SafeConsole.ForegroundColor = oldColor;
        }

        public static void WriteLine(string message)
        {
            Console.WriteLine(message);
        }
    }
}
