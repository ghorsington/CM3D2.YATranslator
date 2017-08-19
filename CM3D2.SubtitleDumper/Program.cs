using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using CM3D2.Toolkit.Arc;
using CM3D2.Toolkit.Arc.Entry;
using CM3D2.Toolkit.Arc.FilePointer;
using CM3D2.Toolkit.Logging;

namespace CM3D2.SubtitleDumper
{
    public class DumpWorker
    {
        private readonly List<string> files;

        public DumpWorker(ManualResetEvent doneEvent, List<string> files)
        {
            DoneEvent = doneEvent;
            this.files = files;
            CapturedSubtitles = new Dictionary<string, string>();
        }

        public Dictionary<string, string> CapturedSubtitles { get; }

        public ManualResetEvent DoneEvent { get; set; }

        public void StartWork(object context)
        {
            DumpStrings();
            DoneEvent?.Set();
        }

        private void WriteSubtitlesToFile(string outputPath)
        {
            string directoryName = Path.GetDirectoryName(outputPath);
            if (!Directory.Exists(directoryName))
                Directory.CreateDirectory(directoryName);
            using (StreamWriter sw = new StreamWriter(File.Open(outputPath, FileMode.Append, FileAccess.Write),
                                                      Encoding.UTF8))
            {
                sw.WriteLine($"; Changes from {DateTime.Now:yyyy-MM-dd-HHmmss}");

                foreach (KeyValuePair<string, string> pair in CapturedSubtitles)
                    sw.WriteLine($"{pair.Key}\t{pair.Value}".Trim());
            }

            CapturedSubtitles.Clear();
        }

        private void DumpStrings()
        {
            foreach (string arcFile in files)
            {
                ArcFileSystem fileSystem = new ArcFileSystem();

                if (!fileSystem.LoadArc(arcFile))
                {
                    Program.Logger.Error($"Failed to load {arcFile}");
                    continue;
                }

                Program.Logger.Info($"Opened {fileSystem}");

                bool specifiedFileName = false;
                int fileCount = 0;
                int maxFiles = fileSystem.Files.Count();
                foreach (ArcFileEntry file in fileSystem.Files)
                {
                    fileCount++;
                    Program.Logger.Info($"Processing file {fileCount}/{maxFiles} : {file.Name}");
                    if (Path.GetExtension(file.Name) != Program.SCRIPT_EXTENSION)
                    {
                        Program.Logger.Info("File is not a script. Skipping...");
                        continue;
                    }

                    bool specifiedScriptName = false;
                    FilePointerBase pointer = file.Pointer;
                    byte[] data = pointer.Compressed ? pointer.Decompress().Data : pointer.Data;
                    using (StreamReader reader = new StreamReader(new MemoryStream(data), Encoding.GetEncoding(932)))
                    {
                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (!line.Contains(Program.VOICE_TAG))
                                continue;
                            Match voiceMatch = Program.VoiceRegex.Match(line);
                            if (!voiceMatch.Success)
                                continue;

                            string subFileName = voiceMatch.Groups["voice"].Value.Trim();

                            if (string.IsNullOrEmpty(subFileName))
                                continue;

                            string transcript = reader.ReadLine();
                            if (transcript == null)
                                continue;

                            transcript = transcript.Replace(";", "").Trim();

                            if (string.IsNullOrEmpty(transcript)
                                || transcript.StartsWith("@")
                                || transcript.StartsWith("*L"))
                                continue;

                            lock (Program.ExistingSubtitles)
                            {
                                if (Program.ExistingSubtitles.TryGetValue(subFileName, out string existingSubtitle)
                                    && (transcript.StartsWith("//")
                                        || existingSubtitle.Equals(transcript, StringComparison.InvariantCulture)))
                                    continue;
                            }

                            lock (Program.FoundSubtitles)
                            {
                                if (Program.FoundSubtitles.TryGetValue(subFileName, out string found)
                                    && (transcript.StartsWith("//")
                                        || found.Equals(transcript, StringComparison.InvariantCulture)))
                                    continue;
                                Program.FoundSubtitles[subFileName] = transcript;
                            }

                            if (!specifiedFileName)
                            {
                                specifiedFileName = true;
                                CapturedSubtitles.Add($"; {fileSystem.Name}", string.Empty);
                            }
                            if (!specifiedScriptName)
                            {
                                specifiedScriptName = true;
                                CapturedSubtitles.Add($"; From {fileSystem.Name}\\{file.Name}", string.Empty);
                            }
                            CapturedSubtitles[subFileName] = transcript;
                        }
                    }

                    if (Program.MergeType == MergeType.None && CapturedSubtitles.Count != 0)
                    {
                        string filePath = file.FullName.Substring(fileSystem.Root.Name.Length + 1);
                        filePath = Path.Combine(Path.GetFileNameWithoutExtension(fileSystem.Name),
                                                filePath.Remove(filePath.Length - 3, 3) + ".txt");
                        string outputPath = Path.Combine(Program.OUTPUT_DIR, filePath);

                        WriteSubtitlesToFile(outputPath);
                    }
                }

                if (Program.MergeType == MergeType.PerArc && CapturedSubtitles.Count != 0)
                {
                    string outputPath = Path.Combine(Program.OUTPUT_DIR,
                                                     Path.GetFileNameWithoutExtension(fileSystem.Name) + ".txt");
                    WriteSubtitlesToFile(outputPath);
                }
            }
        }
    }

    public enum MergeType
    {
        None,
        PerArc,
        SingleFile
    }

    public class Program
    {
        public static Dictionary<string, string> ExistingSubtitles = new Dictionary<string, string>();
        public static Dictionary<string, string> FoundSubtitles = new Dictionary<string, string>();

        public static readonly Logger Logger = new Logger();
        public const string MERGE_PARAM_START = "--merge=";
        public const string OUTPUT_DIR = "output";
        public static readonly Regex ParamRegex = new Regex(@"\((?<params>[\s\S]+)\)");
        public const string SCRIPT_EXTENSION = ".ks";
        public const string THREAD_PARAM_START = "--max-threads=";
        public const string VOICE_TAG = "voice=";
        public static readonly Regex VoiceRegex = new Regex(@"voice=(?<voice>\S*)", RegexOptions.Compiled);

        private static int MaxThreads = 4;
        public static MergeType MergeType { get; private set; } = MergeType.None;
        public static string ProcessName => Process.GetCurrentProcess().ProcessName;

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            string processName = ProcessName;

            Match paramMatch = ParamRegex.Match(processName);
            if (paramMatch.Success)
            {
                string[] prms = paramMatch.Groups["params"]
                                          .Value.Split(new[] {' '}, StringSplitOptions.RemoveEmptyEntries);

                ProcessParams(prms);
            }

            List<string> arcList = ProcessParams(args);

            if (!Directory.Exists(OUTPUT_DIR))
                Directory.CreateDirectory(OUTPUT_DIR);

            Logger.Info("Beginning to write new data");

            LoadPreviousSubtitles();

            DumpSubtitles(arcList);
        }

        private static List<string> ProcessParams(IEnumerable<string> args)
        {
            List<string> result = new List<string>();

            foreach (string arg in args)
                if (arg.StartsWith(MERGE_PARAM_START))
                {
                    string type = arg.Substring(MERGE_PARAM_START.Length).Trim();
                    try
                    {
                        MergeType = (MergeType) Enum.Parse(typeof(MergeType), type, true);
                    }
                    catch (Exception)
                    {
                        Logger.Error("Failed to parse merge type. Using default.");
                    }
                }
                else if (arg.StartsWith(THREAD_PARAM_START))
                {
                    string threadNum = arg.Substring(THREAD_PARAM_START.Length).Trim();
                    if (!int.TryParse(threadNum, out MaxThreads) || MaxThreads <= 0)
                        MaxThreads = 4;
                }
                else
                    result.Add(arg);

            return result;
        }

        private static void LoadPreviousSubtitles()
        {
            if (!Directory.Exists(OUTPUT_DIR))
                return;

            foreach (string file in Directory.EnumerateFiles(OUTPUT_DIR, "*", SearchOption.AllDirectories)
                                             .Where(f => f.EndsWith(".txt")))
            {
                Logger.Info($"Found possible previous dump: {Path.GetFileNameWithoutExtension(file)}");
                using (StreamReader sr = File.OpenText(file))
                {
                    string line;
                    while ((line = sr.ReadLine()) != null)
                    {
                        if (line.StartsWith(";"))
                            continue;

                        string[] parts = line.Split(new[] {'\t'}, StringSplitOptions.RemoveEmptyEntries);
                        if (parts.Length < 2)
                            continue;
                        ExistingSubtitles[parts[0]] = parts[1];
                    }
                }
            }
        }

        private static void DumpSubtitles(IReadOnlyCollection<string> arcFiles)
        {
            int filesPerThread = arcFiles.Count / MaxThreads;
            if (MaxThreads == 1 || filesPerThread <= 1)
            {
                RunSingleThread(arcFiles);
                return;
            }

            DumpWorker[] workers = new DumpWorker[MaxThreads];
            WaitHandle[] doneEvents = new WaitHandle[MaxThreads];

            int workerid = 0;
            List<string> workList = new List<string>();
            foreach (string arcFile in arcFiles)
            {
                if (!File.Exists(arcFile))
                {
                    Logger.Warn($"{arcFile} is not a file");
                    continue;
                }
                if (!ArcFileSystem.DetectMagic(arcFile))
                {
                    Logger.Warn($"{arcFile} is not a valid ARC file.");
                    continue;
                }
                workList.Add(arcFile);

                if (workList.Count >= filesPerThread)
                {
                    ManualResetEvent doneEvent = new ManualResetEvent(false);
                    doneEvents[workerid] = doneEvent;
                    workers[workerid++] = new DumpWorker(doneEvent, workList);
                    workList = new List<string>();
                }
            }
            if (workList.Count != 0)
            {
                ManualResetEvent doneEvent = new ManualResetEvent(false);
                doneEvents[workerid] = doneEvent;
                workers[workerid] = new DumpWorker(doneEvent, workList);
            }

            foreach (DumpWorker dumpWorker in workers)
                ThreadPool.QueueUserWorkItem(dumpWorker.StartWork, null);

            WaitHandle.WaitAll(doneEvents);

            if (MergeType == MergeType.SingleFile)
                WriteAllSubtitles(workers);
        }

        private static void WriteAllSubtitles(params DumpWorker[] workers)
        {
            if (workers.Length == 0)
                return;

            using (StreamWriter sw =
                    new StreamWriter(File.Open(Path.Combine(OUTPUT_DIR, "output.txt"),
                                               FileMode.Append,
                                               FileAccess.Write),
                                     Encoding.UTF8))
            {
                sw.WriteLine($"; Changes from {DateTime.Now:yyyy-MM-dd-HHmmss}");

                foreach (DumpWorker worker in workers)
                {
                    foreach (KeyValuePair<string, string> pair in worker.CapturedSubtitles)
                        sw.WriteLine($"{pair.Key}\t{pair.Value}".Trim());
                }
            }
        }

        private static void RunSingleThread(IEnumerable<string> args)
        {
            List<string> validEntries = new List<string>();
            foreach (string arcFile in args)
            {
                if (!File.Exists(arcFile))
                {
                    Logger.Warn($"{arcFile} is not a file");
                    continue;
                }
                if (!ArcFileSystem.DetectMagic(arcFile))
                {
                    Logger.Warn($"{arcFile} is not a valid ARC file.");
                    continue;
                }
                validEntries.Add(arcFile);
            }

            DumpWorker worker = new DumpWorker(null, validEntries);
            worker.StartWork(null);

            if (MergeType == MergeType.SingleFile)
                WriteAllSubtitles(worker);
        }

        private static void PrintHelp()
        {
            Console.WriteLine("CM3D2.SubtitleDumper");
            Console.WriteLine("Searches CM3D2 Arc files for subtitle transcripts and dumps them.");
            Console.WriteLine();
            Console.WriteLine("Usage:");
            Console.WriteLine($"{ProcessName}.exe <arc files>");
            Console.WriteLine($"All output is put into `{OUTPUT_DIR}` folder");
        }
    }

    public class Logger : ILogger
    {
        public string Name => "Basic logger";

        public void Debug(string message, params object[] args)
        {
            Log("[DEBUG]" + message, args);
        }

        public void Error(string message, params object[] args)
        {
            Log("[ERROR]" + message, args);
        }

        public void Info(string message, params object[] args)
        {
            Log("[INFO]" + message, args);
        }

        public void Trace(string message, params object[] args)
        {
            Log("[TRACE]" + message, args);
        }

        public void Warn(string message, params object[] args)
        {
            Log("[WARNING]" + message, args);
        }

        public void Fatal(string message, params object[] args)
        {
            Log("[FATAL]" + message, args);
        }

        public void Log(string message, params object[] args)
        {
            Console.WriteLine(message, args);
        }
    }
}