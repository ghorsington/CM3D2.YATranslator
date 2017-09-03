using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CM3D2.YATranslator.Plugin.Utils;

namespace CM3D2.YATranslator.Plugin.Translation
{
    public class StringTranslations
    {
        private readonly Dictionary<Regex, string> loadedRegexTranslations;
        private readonly Dictionary<string, string> loadedStringTranslations;
        private readonly HashSet<string> translationFilePaths;

        public StringTranslations(int level)
        {
            Level = level;

            translationFilePaths = new HashSet<string>();
            loadedRegexTranslations = new Dictionary<Regex, string>();
            loadedStringTranslations = new Dictionary<string, string>();
        }

        public int FileCount => translationFilePaths.Count;

        public int Level { get; }

        public int LoadedRegexCount => loadedRegexTranslations.Count;

        public int LoadedStringCount => loadedStringTranslations.Count;

        public int LoadedTranslationCount => loadedStringTranslations.Count + loadedRegexTranslations.Count;

        public bool TranslationsLoaded { get; private set; }

        public bool TryTranslate(string original, out string result)
        {
            if (!TranslationsLoaded)
                LoadTranslations();

            if (loadedStringTranslations.TryGetValue(original, out result))
                return true;

            foreach (KeyValuePair<Regex, string> regexTranslation in loadedRegexTranslations)
                if (regexTranslation.Key.IsMatch(original))
                {
                    result = regexTranslation.Key.Replace(original, regexTranslation.Value);
                    return true;
                }

            return false;
        }

        public void AddTranslationFile(string filePath, bool load = false)
        {
            translationFilePaths.Add(filePath);

            if (!load)
                return;

            if (LoadFromFile(filePath))
                TranslationsLoaded = true;
        }

        public void ClearFilePaths()
        {
            translationFilePaths.Clear();
        }

        public void ClearTranslations()
        {
            loadedRegexTranslations.Clear();
            loadedStringTranslations.Clear();
            TranslationsLoaded = false;

            Logger.WriteLine(ResourceType.Strings,
                             $"Translation::StringTranslations::Unloaded translations for level {Level}");
        }

        public bool LoadTranslations()
        {
            if (TranslationsLoaded)
                return true;

            bool loadedValidTranslations = false;
            foreach (string path in translationFilePaths)
                if (LoadFromFile(path))
                    loadedValidTranslations = true;

            TranslationsLoaded = loadedValidTranslations;

            if (loadedValidTranslations)
                Logger.WriteLine(ResourceType.Strings,
                                 $"Translation::StringTranslations::Loaded {LoadedStringCount} Strings and {LoadedRegexCount} RegExes for level {Level}");

            return loadedValidTranslations;
        }

        private bool LoadFromFile(string filePath)
        {
            IEnumerable<string> translationLines = File.ReadAllLines(filePath, Encoding.UTF8).Select(m => m.Trim())
                                                       .Where(m => !m.StartsWith(";",
                                                                                 StringComparison.CurrentCulture));
            int translated = 0;
            foreach (string translationLine in translationLines)
            {
                string[] textParts = translationLine.Split(new[] {'\t'}, StringSplitOptions.RemoveEmptyEntries);
                if (textParts.Length < 2)
                    continue;
                string original = textParts[0].Unescape();
                string translation = textParts[1].Unescape().Trim();
                if (string.IsNullOrEmpty(translation))
                    continue;

                if (original.StartsWith("$", StringComparison.CurrentCulture))
                {
                    loadedRegexTranslations.AddIfNotPresent(new Regex(original.Substring(1), RegexOptions.Compiled),
                                                            translation);
                    translated++;
                }
                else
                {
                    loadedStringTranslations.AddIfNotPresent(original, translation);
                    translated++;
                }
            }

            if (translated != 0)
                return true;
            translationFilePaths.Remove(filePath);
            return false;
        }
    }
}