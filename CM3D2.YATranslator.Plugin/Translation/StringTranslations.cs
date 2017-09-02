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

        private bool translationsLoaded;

        public StringTranslations(int level)
        {
            Level = level;

            translationFilePaths = new HashSet<string>();
            loadedRegexTranslations = new Dictionary<Regex, string>();
            loadedStringTranslations = new Dictionary<string, string>();
        }

        public int Level { get; }

        public int LoadedTranslationCount => loadedStringTranslations.Count + loadedRegexTranslations.Count;

        public int LoadedStringCount => loadedStringTranslations.Count;

        public int LoadedRegexCount => loadedRegexTranslations.Count;

        public bool TryTranslate(string original, out string result)
        {
            if (!translationsLoaded)
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

            if(load)
                LoadFromFile(filePath);
        }

        public void ClearFilePaths()
        {
            translationFilePaths.Clear();
        }

        public void ClearTranslations()
        {
            loadedRegexTranslations.Clear();
            loadedStringTranslations.Clear();
            translationsLoaded = false;
        }

        public bool LoadTranslations()
        {
            if (translationsLoaded)
                return true;

            foreach (string filePath in translationFilePaths)
                LoadFromFile(filePath);

            translationsLoaded = true;
            return true;
        }

        private void LoadFromFile(string filePath)
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

            if (translated == 0)
                translationFilePaths.Remove(filePath);
        }
    }
}