using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using CM3D2.YATranslator.Hook;
using CM3D2.YATranslator.Plugin.Features;
using CM3D2.YATranslator.Plugin.Translation;
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
        private const string TEMPLATE_STRING_PREFIX = "\u00a0";

        private string lastFoundAsset;
        private string lastFoundTexture;
        private string lastLoadedAsset;
        private string lastLoadedTexture;

        private Action<UILabel> processAndRequest;

        public PluginConfiguration Settings { get; private set; }

        private Clipboard Clipboard { get; set; }

        private int CurrentLevel { get; set; }

        private TranslationMemory Memory { get; set; }

        private Subtitles Subtitles { get; set; }

        public void Awake()
        {
            DontDestroyOnLoad(this);

            MethodInfo processAndRequestMethod = typeof(UILabel).GetMethod("ProcessAndRequest");

            processAndRequest = label => processAndRequestMethod.Invoke(label, null);

            Memory = new TranslationMemory(DataPath);
            Clipboard = gameObject.AddComponent<Clipboard>();
            Subtitles = gameObject.AddComponent<Subtitles>();

            InitConfig();

            Memory.LoadTranslations();

            TranslationHooks.TranslateText += OnTranslateString;
            TranslationHooks.AssetTextureLoad += OnAssetTextureLoad;
            TranslationHooks.ArcTextureLoad += OnTextureLoad;
            TranslationHooks.SpriteLoad += OnTextureLoad;
            TranslationHooks.ArcTextureLoaded += OnArcTextureLoaded;
            TranslationHooks.TranslateGraphic += OnTranslateGraphic;
            TranslationHooks.PlaySound += OnPlaySound;
            TranslationHooks.GetOppositePair += OnGetOppositePair;
            TranslationHooks.GetOriginalText += OnGetOriginalText;
            TranslationHooks.YotogiKagSubtitleCaptured += OnYotogiSubtitleCapture;
            Logger.WriteLine("Translation::Hooking complete");
        }

        private void OnYotogiSubtitleCapture(object sender, StringTranslationEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
                return;
            string voiceFile = Subtitles.DisplayForLast(e.Text);
            if (voiceFile == null)
                return;
            Logger.WriteLine(ResourceType.Strings, "Translation::Strings::Captured yotogi subtitle from script");
            Logger.DumpLine($"{Subtitles.AUDIOCLIP_PREFIX}{voiceFile} {e.Text}", CurrentLevel);
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
                    Memory.ActivateLevelTranslations(CurrentLevel, false);

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
            TranslationHooks.SpriteLoad -= OnTextureLoad;
            TranslationHooks.ArcTextureLoaded -= OnArcTextureLoaded;
            TranslationHooks.TranslateGraphic -= OnTranslateGraphic;
            TranslationHooks.PlaySound -= OnPlaySound;
            TranslationHooks.GetOppositePair -= OnGetOppositePair;
            TranslationHooks.GetOriginalText -= OnGetOriginalText;
            TranslationHooks.YotogiKagSubtitleCaptured -= OnYotogiSubtitleCapture;

            Logger.Dispose();
        }

        private void OnGetOriginalText(object sender, StringTranslationEventArgs e)
        {
            if (string.IsNullOrEmpty(e.Text))
                return;

            if (Memory.TryGetOriginal(e.Text, out string original))
                e.Translation = original;
        }

        private void OnGetOppositePair(object sender, StringTranslationEventArgs e)
        {
            string text = e.Text;
            if (string.IsNullOrEmpty(text))
                return;
            e.Translation = Memory.TryGetOriginal(text, out string original)
                                ? original
                                : Memory.GetTextTranslation(text);
        }

        private void OnPlaySound(object sender, SoundEventArgs e)
        {
            if (!Settings.Subtitles.Enable || e.AudioSourceMgr.SoundType != AudioSourceMgr.Type.Voice)
                return;

            Logger.WriteLine(ResourceType.Voices, $"Translation::Voices {e.AudioSourceMgr.FileName}");

            Subtitles.DisplayFor(e.AudioSourceMgr);
        }

        private void InitConfig()
        {
            Settings = ConfigurationLoader.LoadConfig<PluginConfiguration>(Preferences);
            SaveConfig();
            Memory.OptimizationFlags = Settings.OptimizationFlags;
            Memory.LoadResource = Settings.LoadResourceTypes;
            Memory.RetranslateText = Settings.EnableStringReload;
            Clipboard.Configuration = Settings.Clipboard;
            Subtitles.Configuration = Settings.Subtitles;
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
                Logger.WriteLine(ResourceType.Assets,
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
                    Logger.WriteLine(ResourceType.Assets, LogLevel.Minor, $"Translation::TryFindAsset::{assetName}");
                }

                string assetPath = Memory.GetAssetPath(assetName);

                if (assetPath == null)
                    continue;
                if (lastLoadedAsset != assetName)
                    Logger.WriteLine(ResourceType.Assets, $"Translation::LoadAsset::{assetName}");
                lastLoadedAsset = assetName;

                e.Data = new TextureResource(1, 1, TextureFormat.ARGB32, File.ReadAllBytes(assetPath));
                return;
            }

            Logger.DumpTexture(DumpType.Assets, e.Name, e.OriginalTexture, true);
        }

        private void OnTranslateString(object sender, StringTranslationEventArgs e)
        {
            string inputText = e.Text;
            if (string.IsNullOrEmpty(inputText))
                return;

            if (inputText.StartsWith(TEMPLATE_STRING_PREFIX))
            {
                e.Translation = inputText.Substring(TEMPLATE_STRING_PREFIX.Length);
                return;
            }

            bool isAudioClipName = inputText.StartsWith(Subtitles.AUDIOCLIP_PREFIX);
            if (isAudioClipName)
                inputText = inputText.Substring(Subtitles.AUDIOCLIP_PREFIX.Length);

            e.Translation = Memory.GetTextTranslation(inputText);

            if (e.Type == StringType.Template && e.Translation != null)
            {
                e.Translation = TEMPLATE_STRING_PREFIX + e.Translation;
                return;
            }

            if (Memory.WasTranslated(e.Translation ?? inputText))
                return;

            if (!isAudioClipName)
            {
                Clipboard.AddText(inputText, CurrentLevel);
                Logger.DumpLine(inputText, CurrentLevel);
            }
            else
            {
                e.Translation = inputText;
                Logger.DumpLine(inputText, CurrentLevel, DumpType.Voices);
            }
        }

        private void OnTextureLoad(object sender, TextureTranslationEventArgs e)
        {
            string textureName = e.Name;

            if (lastFoundTexture != textureName)
            {
                lastFoundTexture = textureName;
                Logger.WriteLine(ResourceType.Textures, LogLevel.Minor, $"Translation::FindTexture::{textureName}");
            }

            string texturePath = Memory.GetTexturePath(textureName);

            if (texturePath == null)
            {
                if (e.OriginalTexture != null)
                    Logger.DumpTexture(DumpType.TexSprites, textureName, e.OriginalTexture, true);
                return;
            }
            if (lastLoadedTexture != textureName)
                Logger.WriteLine(ResourceType.Textures, $"Translation::Texture::{textureName}");
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
}