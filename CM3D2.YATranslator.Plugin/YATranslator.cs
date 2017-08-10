using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
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

        private Coroutine CurrentSubtitleAudioTracker { get; set; }

        private TranslationMemory Memory { get; set; }

        private Text SubtitleText { get; set; }

        private Outline TextOutline { get; set; }

        private GameObject TranslationCanvas { get; set; }

        public void Awake()
        {
            DontDestroyOnLoad(this);

            MethodInfo processAndRequestMethod = typeof(UILabel).GetMethod("ProcessAndRequest");

            processAndRequest = label => processAndRequestMethod.Invoke(label, null);

            Memory = new TranslationMemory(DataPath);

            InitConfig();

            Memory.LoadTranslations();

            TranslationHooks.TranslateText += OnTranslateString;
            TranslationHooks.AssetTextureLoad += OnAssetTextureLoad;
            TranslationHooks.ArcTextureLoad += OnTextureLoad;
            TranslationHooks.SpriteLoad += OnTextureLoad;
            TranslationHooks.ArcTextureLoaded += OnArcTextureLoaded;
            TranslationHooks.TranslateGraphic += OnTranslateGraphic;
            TranslationHooks.PlaySound += OnPlaySound;
            Logger.WriteLine("Translation::Hooking complete");
        }

        public void Start()
        {
            CreateSubtitleOverlay();
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
                InitText();
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
            TranslationHooks.ArcTextureLoaded -= OnArcTextureLoaded;
            TranslationHooks.SpriteLoad -= OnTextureLoad;
            TranslationHooks.TranslateGraphic -= OnTranslateGraphic;
            TranslationHooks.PlaySound -= OnPlaySound;

            Logger.Dispose();
        }

        private void OnPlaySound(object sender, SoundEventArgs e)
        {
            if (!Settings.Subtitles.Enable || e.AudioSourceMgr.SoundType != AudioSourceMgr.Type.Voice)
                return;

            Logger.WriteLine(ResourceType.Voices, $"Translation::Voice {e.AudioSourceMgr.FileName}");

            if (CurrentSubtitleAudioTracker != null)
                StopCoroutine(CurrentSubtitleAudioTracker);

            string soundName = Path.GetFileNameWithoutExtension(e.AudioSourceMgr.FileName);
            SubtitleText.text = soundName;

            bool hideUntranslated = !Logger.IsLogging(ResourceType.Voices);

            if (SubtitleText.text == soundName)
            {
                if (hideUntranslated)
                    SubtitleText.text = string.Empty;

                Logger.DumpVoice(soundName, e.AudioSourceMgr.audiosource.clip);
            }

            IEnumerator TrackSubtitleAudio(AudioSource audio)
            {
                yield return null;
                while (audio.isPlaying)
                    yield return new WaitForSeconds(0.1f);

                SubtitleText.text = string.Empty;
                CurrentSubtitleAudioTracker = null;
            }

            if(hideUntranslated)
                CurrentSubtitleAudioTracker = StartCoroutine(TrackSubtitleAudio(e.AudioSourceMgr.audiosource));
        }

        private void CreateSubtitleOverlay()
        {
            TranslationCanvas = new GameObject
            {
                name = "TranslationCanvas"
            };
            GameObject panel = new GameObject("Panel");
            DontDestroyOnLoad(TranslationCanvas);
            DontDestroyOnLoad(panel);

            panel.transform.parent = TranslationCanvas.transform;

            Canvas canvas = TranslationCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 0f);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);

            TextOutline = panel.AddComponent<Outline>();

            SubtitleText = panel.AddComponent<Text>();
            SubtitleText.transform.SetParent(panel.transform, false);
            Font myFont = (Font) Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
            SubtitleText.font = myFont;
            SubtitleText.material = myFont.material;
            SubtitleText.text = string.Empty;
            InitText();
        }

        private void InitText()
        {
            TextOutline.enabled = Settings.Subtitles.Outline;
            TextOutline.effectDistance = new Vector2(Settings.Subtitles.OutlineThickness, Settings.Subtitles.OutlineThickness);
            TextOutline.effectColor = Settings.Subtitles.OutlineColor;

            SubtitleText.fontSize = Settings.Subtitles.FontSize;
            SubtitleText.fontStyle = Settings.Subtitles.Style;
            SubtitleText.material.color = Settings.Subtitles.Color;
            SubtitleText.alignment = Settings.Subtitles.Alignment;
            SubtitleText.rectTransform.anchoredPosition = Settings.Subtitles.Offset;
        }

        private void InitConfig()
        {
            Settings = ConfigurationLoader.LoadConfig<PluginConfiguration>(Preferences);
            SaveConfig();
            Memory.LoadResource = Settings.LoadResourceTypes;
            Memory.RetranslateText = Settings.EnableStringReload;
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

            Logger.WriteLine(ResourceType.Strings, LogLevel.Minor, $"Translation::FindString::{inputText}");

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