using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using CM3D2.YATranslator.Hook;
using CM3D2.YATranslator.Plugin.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
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

        private Action<UILabel> processAndRequest;

        public PluginConfiguration Settings { get; private set; }

        private int CurrentLevel { get; set; }

        private TranslationMemory Memory { get; set; }

        public void Awake()
        {
            DontDestroyOnLoad(this);

            MethodInfo processAndRequestMethod = typeof(UILabel).GetMethod("ProcessAndRequest");

            processAndRequest = label => processAndRequestMethod.Invoke(label, null);

            InitConfig();

            Memory = new TranslationMemory(DataPath, this);
            Memory.LoadTranslations();

            TranslationHooks.TranslateText += OnTranslateString;
            TranslationHooks.AssetTextureLoad += OnAssetTextureLoad;
            TranslationHooks.ArcTextureLoad += OnTextureLoad;
            TranslationHooks.SpriteLoad += OnTextureLoad;
            TranslationHooks.ArcTextureLoaded += OnArcTextureLoaded;
            TranslationHooks.TranslateGraphic += OnTranslateGraphic;
            Logger.WriteLine("Translation::Hooking complete");
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
                InitConfig();
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
            Logger.WriteLine("Translation::Removing hooks");
            TranslationHooks.TranslateText -= OnTranslateString;
            TranslationHooks.AssetTextureLoad -= OnAssetTextureLoad;
            TranslationHooks.ArcTextureLoad -= OnTextureLoad;
            TranslationHooks.ArcTextureLoaded -= OnArcTextureLoaded;
            TranslationHooks.SpriteLoad -= OnTextureLoad;
            TranslationHooks.TranslateGraphic -= OnTranslateGraphic;

            Logger.Dispose();
        }

        private void InitConfig()
        {
            Settings = ConfigurationLoader.LoadConfig<PluginConfiguration>(Preferences);
            SaveConfig();
            Logger.DumpPath = Path.Combine(DataPath, "TranslationDumps");
        }

        private void OnTranslateGraphic(object sender, GraphicTranslationEventArgs e)
        {
            if (e.Graphic == null)
                return;

            switch (e.Graphic)
            {
                case Image img:
                    if (img.sprite == null)
                        return;
                    Sprite currentSprite = img.sprite;
                    img.sprite = currentSprite;
                    break;
                case Text text:
                    if (text.text == null)
                        return;
                    string str = text.text;
                    text.text = str;
                    break;
            }
        }

        private void OnArcTextureLoaded(object sender, TextureTranslationEventArgs e)
        {
            if (Logger.CanDump(DumpType.Textures))
                Logger.DumpTexture(DumpType.Textures, e.Name, e.Data.CreateTexture2D(), false);
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
            Logger.DumpTexture(DumpType.Assets, e.Name, e.OriginalTexture, true);
        }

        private void OnTranslateString(object sender, StringTranslationEventArgs e)
        {
            string inputText = e.Text;
            if (string.IsNullOrEmpty(inputText))
                return;

            Logger.WriteLine(VerbosityLevel.Strings, LogLevel.Minor, $"Translation::FindString::{inputText}");

            e.Translation = Memory.GetTextTranslation(inputText);

            if (!Memory.WasTranslated(inputText))
                Logger.DumpLine($"[STRING][LEVEL {CurrentLevel}] {inputText}");
        }

        private void OnTextureLoad(object sender, TextureTranslationEventArgs e)
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
                if (e.OriginalTexture != null)
                    Logger.DumpTexture(DumpType.TexSprites, textureName, e.OriginalTexture, true);
                Logger.DumpLine($"[TEXTURE] {textureName}");
                return;
            }
            if (lastLoadedTexture != textureName)
                Logger.WriteLine($"Translation::Texture::{textureName}");
            lastLoadedTexture = textureName;

            e.Data = new TextureResource(1, 1, TextureFormat.ARGB32, File.ReadAllBytes(texturePath));
        }

        private void TranslateExisting()
        {
            HashSet<string> processedTextures = new HashSet<string>();
            foreach (UIWidget widget in FindObjectsOfType<UIWidget>())
                if (widget is UILabel label)
                    processAndRequest(label);
                else
                {
                    string texName = widget.mainTexture?.name;
                    if (string.IsNullOrEmpty(texName) || processedTextures.Contains(texName))
                        continue;
                    processedTextures.Add(texName);
                    TranslationHooks.OnAssetTextureLoad(1, widget);
                }

            foreach (MaskableGraphic graphic in FindObjectsOfType<MaskableGraphic>())
            {
                if (graphic is Image img && img.sprite != null)
                    if (img.sprite.name.StartsWith("!"))
                        img.sprite.name = img.sprite.name.Substring(1);
                TranslationHooks.OnTranslateGraphic(graphic);
            }
        }
    }

    public class PluginConfiguration
    {
        public bool EnableStringReload = false;
        public bool LoadAssets = true;
        public bool LoadStrings = true;
        public bool LoadTextures = true;

        public string Dump
        {
            get => string.Empty;

            set
            {
                string[] parts = value.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);

                DumpTypes = parts.Select(str => (DumpType) Enum.Parse(typeof(DumpType), str.Trim(), true)).ToArray();
                Logger.DumpTypes = DumpTypes;
            }
        }

        public DumpType[] DumpTypes { get; private set; }

        public string Verbosity
        {
            get => "None";

            set
            {
                string[] parts = value.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
                VerbosityLevel = parts.Aggregate(VerbosityLevel.None,
                                                 (current, part) => current
                                                                    | (VerbosityLevel) Enum
                                                                            .Parse(typeof(VerbosityLevel),
                                                                                   part.Trim(),
                                                                                   true));
                Logger.Verbosity = VerbosityLevel;
            }
        }

        public VerbosityLevel VerbosityLevel { get; private set; }
    }
}