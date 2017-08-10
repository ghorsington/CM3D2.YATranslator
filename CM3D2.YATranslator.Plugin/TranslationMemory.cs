using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using CM3D2.YATranslator.Plugin.Utils;

namespace CM3D2.YATranslator.Plugin
{
    public class TranslationMemory
    {
        private const string ASSETS_FOLDER = "Assets";
        private static readonly int[] NoLevels = new int[0];
        private const string STRINGS_FOLDER = "Strings";
        private const string TEXTURES_FOLDER = "Textures";

        private readonly Dictionary<Regex, string> activeRegexTranslations;
        private readonly Dictionary<string, string> activeStringTranslations;
        private readonly Dictionary<string, string> cachedAssetPaths;
        private readonly List<StringTranslation> cachedStringTranslations;
        private readonly Dictionary<string, string> cachedTexturePaths;
        private readonly Dictionary<string, string> translatedStrings;

        private string assetsPath;
        private bool isDirectoriesChecked;
        private string stringsPath;
        private string texturesPath;
        private string translationsPath;

        public TranslationMemory(string translationPath)
        {
            TranslationsPath = translationPath;
            translatedStrings = new Dictionary<string, string>();
            cachedStringTranslations = new List<StringTranslation>();
            cachedAssetPaths = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            activeRegexTranslations = new Dictionary<Regex, string>();
            activeStringTranslations = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            cachedTexturePaths = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
        }

        public bool RetranslateText { get; set; }
        public ResourceType LoadResource { get; set; }

        public string TranslationsPath
        {
            get => translationsPath;
            set
            {
                translationsPath = value;
                assetsPath = Path.Combine(translationsPath, ASSETS_FOLDER);
                stringsPath = Path.Combine(translationsPath, STRINGS_FOLDER);
                texturesPath = Path.Combine(translationsPath, TEXTURES_FOLDER);
            }
        }

        private bool CanLoadResouce(ResourceType resourceType) => (resourceType & LoadResource) != 0;

        public void LoadTranslations()
        {
            CheckDirectories();
            if (CanLoadResouce(ResourceType.Assets))
                LoadAssetTranslations();
            if (CanLoadResouce(ResourceType.Strings))
                LoadStringTranslations();
            if (CanLoadResouce(ResourceType.Textures))
                LoadTextureTranslations();
        }

        public void LoadAssetTranslations()
        {
            cachedAssetPaths.Clear();
            foreach (string text in Directory.GetFiles(assetsPath, "*.png", SearchOption.AllDirectories))
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);

                Logger.WriteLine(ResourceType.Assets, $"Translation::CacheAsset::{fileNameWithoutExtension}");
                cachedAssetPaths.AddOrSet(fileNameWithoutExtension, text);
            }
            Logger.WriteLine($"Translation::CacheAssets::Cached '{cachedAssetPaths.Count}' Assets");
        }

        public void ActivateLevelTranslations(int level, bool clearTranslatedCache = true)
        {
            if(clearTranslatedCache)
                translatedStrings.Clear();
            ActivateStringTranslations(level);
        }

        public string GetTextTranslation(string original)
        {
            string Translate(string text)
            {
                Logger.WriteLine(ResourceType.Strings, $"Translation::String::'{original}'->'{text}'");
                translatedStrings.AddIfNotPresent(text, original);
                return text;
            }

            bool wasTranslated = translatedStrings.ContainsKey(original);
            if (RetranslateText)
                original = wasTranslated ? translatedStrings[original] : original;
            else if (wasTranslated)
            {
                Logger.WriteLine(ResourceType.Strings,
                                 LogLevel.Minor,
                                 $"Translation::String::Skip {original} (is already translated)");
                return null;
            }
            original = original.Replace("\n", "").Trim();

            if (string.IsNullOrEmpty(original))
                return null;

            if (activeStringTranslations.TryGetValue(original, out string translation))
                return Translate(translation);

            foreach (KeyValuePair<Regex, string> regexTranslation in activeRegexTranslations)
                if (regexTranslation.Key.IsMatch(original))
                    return Translate(regexTranslation.Key.Replace(original, regexTranslation.Value));

            return RetranslateText ? original : null;
        }

        public bool WasTranslated(string translation) => translatedStrings.ContainsKey(translation);

        public string GetTexturePath(string name) => cachedTexturePaths.TryGetValue(name, out string path)
                                                         ? path
                                                         : null;

        public string GetAssetPath(string name) => cachedAssetPaths.TryGetValue(name, out string path) ? path : null;

        private void CheckDirectories()
        {
            if (isDirectoriesChecked)
                return;
            isDirectoriesChecked = true;

            void InitDir(string dir)
            {
                if (!Directory.Exists(dir))
                    Directory.CreateDirectory(dir);
            }

            try
            {
                InitDir(stringsPath);
                InitDir(texturesPath);
                InitDir(assetsPath);
            }
            catch (Exception e)
            {
                Logger.WriteLine(LogLevel.Error, $"Translation::Directory_Load_Fail::{e}");
            }
        }

        private void LoadTextureTranslations()
        {
            cachedTexturePaths.Clear();

            foreach (string path in Directory.GetFiles(texturesPath, "*.png", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                Logger.WriteLine(ResourceType.Textures, $"Translation::CacheTexture::{fileName}");
                cachedTexturePaths.AddOrSet(fileName, path);
            }
            Logger.WriteLine($"Translation::CacheTexture::Cached '{cachedTexturePaths.Count}' Textures");
        }

        private void ActivateStringTranslations(int level)
        {
            activeStringTranslations.Clear();
            activeRegexTranslations.Clear();
            if (!CanLoadResouce(ResourceType.Strings))
                return;

            foreach (StringTranslation current in cachedStringTranslations)
            {
                if (current.Level.Length != 0 && !current.Level.Contains(level))
                    continue;
                if (current.IsRegex)
                    activeRegexTranslations.AddIfNotPresent(current.RegexTranslation, current.TargetTranslation);
                else
                    activeStringTranslations.AddIfNotPresent(current.ConstTranslation, current.TargetTranslation);
            }

            Logger.WriteLine($"Translation::CacheString::Cached '{activeStringTranslations.Count}' Strings and '{activeRegexTranslations.Count}' Regexes for Level '{level}'");
        }

        private void LoadStringTranslations()
        {
            activeStringTranslations.Clear();
            activeRegexTranslations.Clear();
            cachedStringTranslations.Clear();

            foreach (string translationPath in Directory.GetFiles(stringsPath, "*.txt", SearchOption.AllDirectories))
            {
                int[] translationLevels = NoLevels;
                string fileName = Path.GetFileNameWithoutExtension(translationPath);
                string[] fileNameParts = fileName.Split('.');

                if (fileNameParts.Length != 1)
                {
                    List<int> levelList = new List<int>();
                    foreach (string s in fileNameParts[1].Split('-'))
                        if (int.TryParse(s, out int item))
                            levelList.Add(item);
                    translationLevels = levelList.Distinct().ToArray();
                }

                if (Logger.IsLogging(ResourceType.Strings))
                {
                    string levels = translationLevels.Length > 0
                                        ? string.Join(",", translationLevels.Select(i => i.ToString()).ToArray())
                                        : "";
                    Logger.WriteLine($"Translation::CacheString::'{fileName}'@'[{levels}]'");
                }

                IEnumerable<string> translationLines = File.ReadAllLines(translationPath, Encoding.UTF8)
                                                           .Select(m => m.Trim())
                                                           .Where(m => !m.StartsWith(";",
                                                                                     StringComparison.CurrentCulture));
                foreach (string translationLine in translationLines)
                {
                    string[] textParts = translationLine.Split(new[] {'\t'}, StringSplitOptions.RemoveEmptyEntries);
                    if (textParts.Length < 2)
                        continue;
                    string original = textParts[0].Unescape();
                    string translation = textParts[1].Unescape().Trim();
                    if (string.IsNullOrEmpty(translation))
                        continue;

                    cachedStringTranslations.Add(new StringTranslation(original, translation, translationLevels));
                }
            }

            Logger.WriteLine($"Translation::Strings::Loaded '{cachedStringTranslations.Count}' Translations");
        }
    }
}