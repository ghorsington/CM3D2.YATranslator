using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CM3D2.YATranslator.Plugin.Utils;

namespace CM3D2.YATranslator.Plugin.Translation
{
    [Flags]
    public enum MemoryOptimizations
    {
        None = 1 << 0,
        LoadOnLevelChange = 1 << 1,
        LoadOnTranslate = 1 << 2,
        UnloadOnLevelChange = 1 << 3,
        Simple = LoadOnLevelChange | UnloadOnLevelChange,
        Aggresive = LoadOnTranslate | UnloadOnLevelChange,
        LazyLoad = LoadOnLevelChange | LoadOnTranslate
    }

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

        public MemoryOptimizations OptimizationFlags { get; set; }

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
            if (CanLoadResouce(ResourceType.Textures))
                LoadTextureTranslations();
            if (CanLoadResouce(ResourceType.Strings))
                LoadStringTranslations();
        }

        public void LoadAssetTranslations()
        {
            cachedAssetPaths.Clear();
            foreach (string text in Directory.GetFiles(assetsPath, "*.png", SearchOption.AllDirectories))
            {
                string fileNameWithoutExtension = Path.GetFileNameWithoutExtension(text);

                Logger.WriteLine(ResourceType.Assets, $"CacheAsset::{fileNameWithoutExtension}");
                cachedAssetPaths.AddOrSet(fileNameWithoutExtension, text);
            }
            Logger.WriteLine($"CacheAssets::Cached '{cachedAssetPaths.Count}' Assets");
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
                Logger.WriteLine(ResourceType.Strings, $"String::'{original}'->'{text}'");
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
                                 $"String::Skip {original} (is already translated)");
                return null;
            }
            untranslated = untranslated.Replace("\n", "").Trim();

            Logger.WriteLine(ResourceType.Strings, LogLevel.Minor, $"FindString::{untranslated}");

            if (string.IsNullOrEmpty(untranslated))
                return null;

            StringTranslations first, second;
            if (IsOptimizationEnabled(MemoryOptimizations.LoadOnTranslate)
                && activeTranslations != null
                && !activeTranslations.TranslationsLoaded)
            {
                first = globalTranslations;
                second = activeTranslations;
            }
            else
            {
                first = activeTranslations;
                second = globalTranslations;
            }

            if (first != null && first.TryTranslate(untranslated, out string translation))
                return Translate(translation);

            if (second != null && second.TryTranslate(untranslated, out string globalTranslation))
                return Translate(globalTranslation);

            return null;
        }

        public bool TryGetOriginal(string translation, out string original) =>
                translatedStrings.TryGetValue(translation, out original);

        public bool WasTranslated(string translation) => translatedStrings.ContainsKey(translation);

        public string GetTexturePath(string name) =>
                cachedTexturePaths.TryGetValue(name, out string path) ? path : null;

        public string GetAssetPath(string name) => cachedAssetPaths.TryGetValue(name, out string path) ? path : null;

        private bool IsOptimizationEnabled(MemoryOptimizations optimization) => (optimization & OptimizationFlags) != 0;

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
                Logger.WriteLine(LogLevel.Error, $"Directory_Load_Fail::{e}");
            }
        }

        private void LoadTextureTranslations()
        {
            cachedTexturePaths.Clear();

            foreach (string path in Directory.GetFiles(texturesPath, "*.png", SearchOption.AllDirectories))
            {
                string fileName = Path.GetFileNameWithoutExtension(path);
                Logger.WriteLine(ResourceType.Textures, $"CacheTexture::{fileName}");
                cachedTexturePaths.AddOrSet(fileName, path);
            }
            Logger.WriteLine($"CacheTexture::Cached '{cachedTexturePaths.Count}' Textures");
        }

        private void ActivateStringTranslations(int level)
        {
            if (!CanLoadResouce(ResourceType.Strings))
                return;

            if (activeTranslations != null
                && IsOptimizationEnabled(MemoryOptimizations.LazyLoad)
                && IsOptimizationEnabled(MemoryOptimizations.UnloadOnLevelChange))
                activeTranslations.ClearTranslations();

            activeTranslations = null;
            stringGroups.TryGetValue(level, out activeTranslations);

            if (activeTranslations != null && IsOptimizationEnabled(MemoryOptimizations.LoadOnLevelChange))
                activeTranslations.LoadTranslations();

            Logger.WriteLine(IsOptimizationEnabled(MemoryOptimizations.LoadOnTranslate)
                                 ? $"CacheString::Cached '{(activeTranslations?.FileCount ?? 0) + globalTranslations.FileCount}' translation files for Level '{level}'"
                                 : $"CacheString::Cached '{(activeTranslations?.LoadedStringCount ?? 0) + globalTranslations.LoadedStringCount}' Strings and '{(activeTranslations?.LoadedRegexCount ?? 0) + globalTranslations.LoadedRegexCount}' Regexes for Level '{level}'");
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

            bool loadContentsIntoMemory = !IsOptimizationEnabled(MemoryOptimizations.LazyLoad);
            int loadedStrings = 0;
            int loadedFiles = 0;
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
                    Logger.WriteLine($"CacheString::'{fileName}'@'[{levels}]'");
                }

                int prev = 0;
                if (translationLevels.Length == 0)
                {
                    prev = globalTranslations.LoadedTranslationCount;
                    globalTranslations.AddTranslationFile(translationPath, true);
                    loadedStrings += globalTranslations.LoadedTranslationCount - prev;
                    loadedFiles++;
                }
                else
                {
                    bool translationsCounted = false;
                    foreach (int level in translationLevels)
                    {
                        if (!stringGroups.TryGetValue(level, out StringTranslations group))
                        {
                            group = new StringTranslations(level);
                            stringGroups.Add(level, group);
                        }

                        if (!translationsCounted)
                            prev = group.LoadedTranslationCount;

                        group.AddTranslationFile(translationPath, loadContentsIntoMemory);

                        if (!translationsCounted)
                            loadedStrings += group.LoadedTranslationCount - prev;
                        translationsCounted = true;
                        loadedFiles++;
                    }
                }
            }

            Logger.WriteLine(loadContentsIntoMemory
                                 ? $"Strings::Loaded '{loadedStrings}' Translations"
                                 : $"Strings::Pre-cached '{loadedFiles}' Translation files");
        }
    }
}