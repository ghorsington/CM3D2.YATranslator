using System.IO;
using System.Text.RegularExpressions;

namespace CM3D2.YATranslator.Plugin.Utils
{
    public static class FileUtils
    {
        private static readonly Regex CharRegex;

        static FileUtils()
        {
            CharRegex = new Regex($"[{Regex.Escape(new string(Path.GetInvalidFileNameChars()))}]");
        }

        public static bool IsValidFilename(string filename)
        {
            return !CharRegex.IsMatch(filename);
        }
    }
}