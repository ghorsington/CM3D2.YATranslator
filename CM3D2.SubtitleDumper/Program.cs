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
        }

        public ManualResetEvent DoneEvent { get; set; }

        public void StartWork(object context)
        {
            DumpStrings();
            DoneEvent?.Set();
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

                bool isChangeDateWritten = false;

                Program.Logger.Info($"Opened {fileSystem}");

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

                    string filePath = file.FullName.Substring(fileSystem.Root.Name.Length + 1);
                    filePath = Path.Combine(Path.GetFileNameWithoutExtension(fileSystem.Name),
                                            filePath.Remove(filePath.Length - 3, 3) + ".txt");
                    string outputPath = Path.Combine(Program.OUTPUT_DIR, filePath);
                    string directoryName = Path.GetDirectoryName(outputPath);

                    Dictionary<string, string> prevTranslations = new Dictionary<string, string>();

                    if (File.Exists(outputPath))
                    {
                        Program.Logger.Info("Found existing dump. Reading previous text");
                        using (StreamReader sr = File.OpenText(outputPath))
                        {
                            string line;
                            while ((line = sr.ReadLine()) != null)
                            {
                                if (line.StartsWith(";"))
                                    continue;

                                string[] parts = line.Split(new[] {'\t'}, StringSplitOptions.RemoveEmptyEntries);
                                if (parts.Length < 2)
                                    continue;
                                prevTranslations[parts[0]] = parts[1];
                            }
                        }
                    }

                    StreamWriter sw = null;
                    FilePointerBase pointer = file.Pointer;
                    byte[] data = pointer.Compressed ? pointer.Decompress().Data : pointer.Data;

                    using (StreamReader reader = new StreamReader(new MemoryStream(data), Encoding.GetEncoding(932)))
                    {
                        bool isDescriptionWritten = false;

                        string line;
                        while ((line = reader.ReadLine()) != null)
                        {
                            line = line.Trim();
                            if (line.StartsWith(";") || !line.Contains(Program.VOICE_TAG))
                                continue;
                            Match voiceMatch = Program.VoiceRegex.Match(line);
                            if (!voiceMatch.Success)
                                continue;

                            string subFileName = voiceMatch.Groups["voice"].Value.Trim();

                            string transcript = reader.ReadLine();
                            if (transcript == null)
                                continue;
                            
                            transcript = transcript.Replace(";", "").Replace("//", "").Trim();

                            if (transcript.StartsWith("@"))
                                continue;

                            bool contains = prevTranslations.TryGetValue(subFileName, out string oldTranscript);
                            if (contains && oldTranscript == transcript)
                                continue;

                            if (sw == null)
                            {
                                if (!Directory.Exists(directoryName))
                                    Directory.CreateDirectory(directoryName);
                                sw = new StreamWriter(File.Open(outputPath, FileMode.Append, FileAccess.Write),
                                                      Encoding.UTF8);
                            }

                            if (!isChangeDateWritten)
                            {
                                sw.WriteLine($"; Changes from {DateTime.Now:yyyy-MM-dd-HHmmss}");
                                isChangeDateWritten = true;
                            }

                            if (!isDescriptionWritten)
                            {
                                sw.WriteLine($"; From {fileSystem.Name}\\{file.Name}");
                                isDescriptionWritten = true;
                            }
                            sw.WriteLine($"{subFileName}\t{transcript}");
                            prevTranslations[subFileName] = transcript;
                        }
                    }
                    sw?.Close();
                    sw?.Dispose();
                }
            }
        }
    }

    public class Program
    {
        public static readonly Logger Logger = new Logger();
        public const string OUTPUT_DIR = "output";
        public const string SCRIPT_EXTENSION = ".ks";
        public const string VOICE_TAG = "voice=";
        public static readonly Regex VoiceRegex = new Regex(@"voice=(?<voice>\S*)", RegexOptions.Compiled);
        private const int MaxThreads = 4;
        public static string ProcessName => Process.GetCurrentProcess().ProcessName;

        public static void Main(string[] args)
        {
            if (args.Length == 0)
            {
                PrintHelp();
                return;
            }

            if (!Directory.Exists(OUTPUT_DIR))
                Directory.CreateDirectory(OUTPUT_DIR);

            Logger.Info("Beginning to write new data");

            DumpWorker[] workers = new DumpWorker[MaxThreads];
            WaitHandle[] doneEvents = new WaitHandle[MaxThreads];

            int filesPerThread = args.Length / MaxThreads;

            if (filesPerThread <= 1)
                RunSingleThread(args);

            int workerid = 0;
            List<string> workList = new List<string>();
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
        }

        private static void RunSingleThread(string[] args)
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