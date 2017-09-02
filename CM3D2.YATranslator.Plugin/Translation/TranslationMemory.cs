using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CM3D2.YATranslator.Plugin.Utils;

namespace CM3D2.YATranslator.Plugin.Translation
{
    public class TranslationMemory
    {
        private const string ASSETS_FOLDER = "Assets";
        private static readonly int[] NoLevels = new int[0];
        private const string STRINGS_FOLDER = "Strings";
        private const string TEXTURES_FOLDER = "Textures";
        private readonly Dictionary<string, string> cachedAssetPaths;
        private readonly Dictionary<string, string> cachedTexturePaths;
        private readonly StringTranslations globalTranslations;
        private readonly Dictionary<int, StringTranslations> stringGroups;
        private readonly Dictionary<string, string> translatedStrings;

        private StringTranslations activeTranslations;

        private string assetsPath;
        private bool isDirectoriesChecked;
        private string stringsPath;
        private string texturesPath;
        private string translationsPath;

        public TranslationMemory(string translationPath)
        {
            TranslationsPath = translationPath;
            translatedStrings = new Dictionary<string, string>();
            cachedAssetPaths = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            stringGroups = new Dictionary<int, StringTranslations>();
            cachedTexturePaths = new Dictionary<string, string>(StringComparer.InvariantCultureIgnoreCase);
            globalTranslations = new StringTranslations(-1);
        }

        public ResourceType LoadResource { get; set; }

        public bool RetranslateText { get; set; }

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
            if (clearTranslatedCache)
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
            string untranslated = original;
            if (RetranslateText)
                untranslated = wasTranslated ? translatedStrings[untranslated] : untranslated;
            else if (wasTranslated)
            {
                Logger.WriteLine(ResourceType.Strings,
                                 LogLevel.Minor,
                                 $"Translation::String::Skip {original} (is already translated)");
                return null;
            }
            untranslated = untranslated.Replace("\n", "").Trim();

            Logger.WriteLine(ResourceType.Strings, LogLevel.Minor, $"Translation::FindString::{untranslated}");

            if (string.IsNullOrEmpty(untranslated))
                return null;

            if (activeTranslations != null && activeTranslations.TryTranslate(untranslated, out string translation))
                return Translate(translation);

            if (globalTranslations.TryTranslate(untranslated, out string globalTranslation))
                return Translate(globalTranslation);

            return null;
        }

        public bool TryGetOriginal(string translation, out string original) =>
                translatedStrings.TryGetValue(translation, out original);

        public bool WasTranslated(string translation) => translatedStrings.ContainsKey(translation);

        public string GetTexturePath(string name) =>
                cachedTexturePaths.TryGetValue(name, out string path) ? path : null;

        public string GetAssetPath(string name) => cachedAssetPaths.TryGetValue(name, out string path) ? path : null;

        private bool CanLoadResouce(ResourceType resourceType) => (resourceType & LoadResource) != 0;

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
            if (!CanLoadResouce(ResourceType.Strings))
                return;

            activeTranslations = null;
            stringGroups.TryGetValue(level, out activeTranslations);

            int loadedConstCount = (activeTranslations != null ? activeTranslations.LoadedStringCount : 0)
                                   + globalTranslations.LoadedStringCount;
            int loadedRegexCount = (activeTranslations != null ? activeTranslations.LoadedRegexCount : 0)
                                   + globalTranslations.LoadedRegexCount;

            Logger.WriteLine($"Translation::CacheString::Cached '{loadedConstCount}' Strings and '{loadedRegexCount}' Regexes for Level '{level}'");
        }

        private void LoadStringTranslations()
        {
            globalTranslations.ClearTranslations();
            globalTranslations.ClearFilePaths();

            foreach (KeyValuePair<int, StringTranslations> group in stringGroups)
            {
                group.Value.ClearTranslations();
                group.Value.ClearFilePaths();
            }

            int loadedStrings = 0;
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

                int prev = 0;
                if (translationLevels.Length == 0)
                {
                    prev = globalTranslations.LoadedTranslationCount;
                    globalTranslations.AddTranslationFile(translationPath, true);
                    loadedStrings += globalTranslations.LoadedTranslationCount - prev;
                }
                else
                {
                    bool translationsCounted = false;
                    foreach (int level in translationLevels)
                    {
                        StringTranslations group;
                        if (!stringGroups.TryGetValue(level, out group))
                        {
                            group = new StringTranslations(level);
                            stringGroups.Add(level, group);
                        }

                        if (!translationsCounted)
                            prev = group.LoadedTranslationCount;

                        group.AddTranslationFile(translationPath, true);

                        if (!translationsCounted)
                            loadedStrings += group.LoadedTranslationCount - prev;
                        translationsCounted = true;
                    }
                }
            }

            Logger.WriteLine($"Translation::Strings::Loaded '{loadedStrings}' Translations");
        }
    }
}