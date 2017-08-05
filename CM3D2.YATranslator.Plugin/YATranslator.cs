using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using CM3D2.YATranslator.Hook;
using CM3D2.YATranslator.Plugin.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityInjector;
using UnityInjector.Attributes;
using Logger = CM3D2.YATranslator.Plugin.Utils.Logger;

namespace CM3D2.YATranslator.Plugin
{
    [PluginName("Yet Another Translator")]
    public class YATranslator : PluginBase
    {
        private string lastFoundAsset;
        private string lastFoundTexture;
        private string lastLoadedAsset;
        private string lastLoadedTexture;

        public PluginConfiguration Settings { get; private set; }

        private int CurrentLevel { get; set; }

        private TranslationMemory Memory { get; set; }

        public void Awake()
        {
            DontDestroyOnLoad(this);

            string dataPath = Path.Combine(Environment.CurrentDirectory, "UnityInjector\\Config");

            Settings = ConfigurationLoader.LoadConfig<PluginConfiguration>(Preferences);
            SaveConfig();
            Logger.Verbosity = Settings.VerbosityLevel;
            Logger.DumpPath = Path.Combine(dataPath, "TranslationDumps");
            Logger.EnableDump = Settings.DumpStrings;

            Memory = new TranslationMemory(dataPath, this);
            Memory.LoadTranslations();

            TranslationHooks.TranslateText += OnTranslateString;
            TranslationHooks.AssetTextureLoad += OnAssetTextureLoad;
            TranslationHooks.ArcTextureLoad += OnArcTextureLoad;
            Logger.WriteLine("Translation::Hooking::TranslateText");
            Logger.WriteLine("Translation::Hooking::AssetLoad");
            Logger.WriteLine("Translation::Hooking::TextureLoad");
        }

        public void OnLevelWasLoaded(int level)
        {
            CurrentLevel = level;
            Memory.ActivateLevelTranslations(level);
            TranslateExisting();
        }

        public void Update()
        {
            if (Input.GetKeyDown(KeyCode.F12))
            {
                Logger.WriteLine("Reloading config");
                ReloadConfig();
                Settings = ConfigurationLoader.LoadConfig<PluginConfiguration>(Preferences);
                Logger.Verbosity = Settings.VerbosityLevel;
                Logger.EnableDump = Settings.DumpStrings;
                SaveConfig();
                if (Settings.EnableStringReload)
                {
                    Logger.WriteLine("Reloading translations");
                    Memory.LoadTranslations();
                    Memory.ActivateLevelTranslations(CurrentLevel);

                    TranslateExisting();
                }
            }
        }

        public void OnDestroy()
        {
            TranslationHooks.TranslateText -= OnTranslateString;
            TranslationHooks.AssetTextureLoad -= OnAssetTextureLoad;
            TranslationHooks.ArcTextureLoad -= OnArcTextureLoad;
            Logger.Dispose();
        }

        private void OnAssetTextureLoad(object sender, TextureTranslationEventArgs e)
        {
            if (lastFoundAsset != e.Name)
            {
                lastFoundAsset = e.Name;
                Logger.WriteLine(VerbosityLevel.Assets,
                                 LogLevel.Minor,
                                 $"Translation::FindAsset::{e.Name} [{e.Meta}::{e.CompoundHash}]");
            }

            string[] namePossibilities =
            {
                e.CompoundHash + "@" + SceneManager.GetActiveScene().buildIndex,
                e.Name + "@" + SceneManager.GetActiveScene().buildIndex,
                e.CompoundHash,
                e.Name
            };

            foreach (string assetName in namePossibilities)
            {
                if (lastFoundAsset != assetName)
                {
                    lastFoundAsset = assetName;
                    Logger.WriteLine(VerbosityLevel.Assets, LogLevel.Minor, $"Translation::TryFindAsset::{assetName}");
                }

                string assetPath = Memory.GetAssetPath(assetName);

                if (assetPath == null)
                    continue;
                if (lastLoadedAsset != assetName)
                    Logger.WriteLine($"Translation::LoadAsset::{assetName}");
                lastLoadedAsset = assetName;

                e.Data = new TextureResource(1, 1, TextureFormat.ARGB32, File.ReadAllBytes(assetPath));
                return;
            }

            Logger
                    .DumpLine($"[ASSET][HASH {e.CompoundHash}][BUILDINDEX {SceneManager.GetActiveScene().buildIndex}] {e.Name}");
        }

        private void OnTranslateString(object sender, StringTranslationEventArgs e)
        {
            string inputText = e.Text;
            if (string.IsNullOrEmpty(inputText))
                return;

            Logger.WriteLine(VerbosityLevel.Strings, LogLevel.Minor, $"Translation::FindString::{inputText}");

            e.Translation = Memory.GetTextTranslation(inputText);

            if (Settings.DumpStrings && !Memory.WasTranslated(inputText))
                Logger.DumpLine($"[STRING][LEVEL {CurrentLevel}] {inputText}");
        }

        private void OnArcTextureLoad(object sender, TextureTranslationEventArgs e)
        {
            string textureName = e.Name;

            if (lastFoundTexture != textureName)
            {
                lastFoundTexture = textureName;
                Logger.WriteLine(VerbosityLevel.Textures, LogLevel.Minor, $"Translation::FindTexture::{textureName}");
            }

            string texturePath = Memory.GetTexturePath(textureName);

            if (texturePath == null)
            {
                Logger.DumpLine($"[TEXTURE] {textureName}");
                return;
            }
            if (lastLoadedTexture != textureName)
                Logger.WriteLine($"Translation::Texture::{textureName}");
            lastLoadedTexture = textureName;

            e.Data = new TextureResource(1, 1, TextureFormat.ARGB32, File.ReadAllBytes(texturePath));
        }

        private static void TranslateExisting()
        {
            HashSet<string> processedTextures = new HashSet<string>();
            foreach (UIWidget widget in FindObjectsOfType<UIWidget>())
                if (widget is UILabel label)
                    typeof(UILabel).GetMethod("ProcessAndRequest").Invoke(label, null);
                else
                {
                    string texName = widget.mainTexture?.name;
                    if (string.IsNullOrEmpty(texName) || processedTextures.Contains(texName))
                        continue;
                    processedTextures.Add(texName);
                    TranslationHooks.OnAssetTextureLoad(1, widget);
                }
        }
    }

    public class PluginConfiguration
    {
        public bool DumpStrings = false;
        public bool EnableStringReload = false;
        public bool LoadAssets = true;
        public bool LoadStrings = true;
        public bool LoadTextures = true;

        public string Verbosity
        {
            get => "None";

            set
            {
                string[] parts = value.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
                VerbosityLevel level = parts.Aggregate(VerbosityLevel.None,
                                                       (current, part) => current
                                                                          | (VerbosityLevel) Enum
                                                                                  .Parse(typeof(VerbosityLevel), part));
                VerbosityLevel = level;
            }
        }

        public VerbosityLevel VerbosityLevel { get; private set; }
    }
}