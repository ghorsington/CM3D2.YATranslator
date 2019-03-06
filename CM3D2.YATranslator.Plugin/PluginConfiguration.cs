using System;
using System.Linq;
using System.Text.RegularExpressions;
using CM3D2.YATranslator.Plugin.Translation;
using CM3D2.YATranslator.Plugin.Utils;
using UnityEngine;
using Logger = CM3D2.YATranslator.Plugin.Utils.Logger;

namespace CM3D2.YATranslator.Plugin
{
    [ConfigSection("Clipboard")]
    public class ClipboardConfiguration
    {
        public bool Enable = false;
        public float WaitTime = 0.5f;
        public int[] AllowedLevels { get; internal set; } = {-1};

        public string Levels
        {
            get => "-1";

            set => AllowedLevels = value.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
        }
    }


    [ConfigSection("Subtitles")]
    public class SubtitleConfiguration
    {
        private static readonly Regex RgbaPattern =
                new Regex(@"RGBA\(\s*(?<r>\d\.\d+)\s*,\s*(?<g>\d\.\d+)\s*,\s*(?<b>\d\.\d+)\s*,\s*(?<a>\d\.\d+)\s*\)");

        private static readonly Regex Vec2Pattern = new Regex(@"\(\s*(?<x>-?\d+\.\d+)\s*,\s*(?<y>-?\d+\.\d+)\s*\)");

        public bool Enable = true;
        public int FontSize = 20;
        public int FontSizeVR = 40;
        public bool HideWhenClipStops = true;
        public bool Outline = true;
        public float OutlineThickness = 1.0f;

        public bool ShowUntranslatedText = false;
        public TextAnchor Alignment { get; private set; } = TextAnchor.UpperCenter;
        public Color Color { get; private set; } = Color.white;

        public string FontColor
        {
            get => Color.ToString();
            set => Color = ParseColor(value);
        }

        public string FontStyle
        {
            get => Style.ToString();

            set => Style = Extensions.ParseEnum<FontStyle>(value, true);
        }

        public Vector2 Offset { get; private set; } = Vector2.zero;

        public Vector2 OffsetVR { get; private set; } = Vector2.zero;

        public string OutlineColor
        {
            get => TextOutlineColor.ToString();
            set => TextOutlineColor = ParseColor(value);
        }

        public FontStyle Style { get; private set; } = UnityEngine.FontStyle.Bold;

        public string TextAlignment
        {
            get => Alignment.ToString();

            set => Alignment = Extensions.ParseEnum<TextAnchor>(value, true);
        }

        public string TextOffset
        {
            get => Offset.ToString();

            set => Offset = ParseVec2(value);
        }

        public string TextOffsetVR
        {
            get => OffsetVR.ToString();
            set => OffsetVR = ParseVec2(value);
        }

        public Color TextOutlineColor { get; private set; } = Color.black;

        private static Vector2 ParseVec2(string value)
        {
            var m = Vec2Pattern.Match(value);
            if (!m.Success)
                throw new FormatException("Invalid Vec2 format");
            float x = float.Parse(m.Groups["x"].Value);
            float y = float.Parse(m.Groups["y"].Value);

            return new Vector2(x, y);
        }

        private static Color ParseColor(string value)
        {
            var m = RgbaPattern.Match(value);
            if (!m.Success)
                throw new FormatException("Invalid RGBA format");
            float r = float.Parse(m.Groups["r"].Value);
            float g = float.Parse(m.Groups["g"].Value);
            float b = float.Parse(m.Groups["b"].Value);
            float a = float.Parse(m.Groups["a"].Value);

            return new Color(r, g, b, a);
        }
    }

    [ConfigSection("Config")]
    public class PluginConfiguration
    {
        public bool EnableTranslationReload = false;
        private readonly Regex parameterReplacementRegex = new Regex("{([^}]*)}");
        private readonly string[] texTemplateVariables = {"NAME", "HASH", "METADATA", "LEVEL"};

        public int[] AllowedDumpLevels { get; private set; } = {-1};

        public ClipboardConfiguration Clipboard { get; set; } = new ClipboardConfiguration();

        public string Dump
        {
            get => "None";

            set
            {
                var parts = value.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);

                DumpTypes = parts.Select(str => (DumpType) Enum.Parse(typeof(DumpType), str.Trim(), true)).ToArray();
                Logger.DumpTypes = DumpTypes;
            }
        }

        public string DumpLevels
        {
            get => "-1";
            set
            {
                AllowedDumpLevels = value.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries).Select(int.Parse).ToArray();
                Logger.DumpLevels = AllowedDumpLevels;
            }
        }

        public DumpType[] DumpTypes { get; private set; } = new DumpType[0];

        public string Load
        {
            get => ResourceType.All.ToString();

            set
            {
                var parts = value.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
                LoadResourceTypes = parts.Aggregate(ResourceType.None,
                                                    (current, part) =>
                                                            current | (ResourceType) Enum.Parse(typeof(ResourceType), part.Trim(), true));
            }
        }

        public ResourceType LoadResourceTypes { get; private set; } = ResourceType.All;

        public KeyCode ReloadTranslationsKeyCode { get; private set; } = KeyCode.F12;

        public string ReloadTranslationsKey
        {
            get => ReloadTranslationsKeyCode.ToString();

            set => ReloadTranslationsKeyCode = (KeyCode) Enum.Parse(typeof(KeyCode), value, true);
        }

        public string MemoryOptimizations
        {
            get => OptimizationFlags.ToString();

            set => OptimizationFlags = (MemoryOptimizations)Enum.Parse(typeof(MemoryOptimizations), value, true);
        }

        public MemoryOptimizations OptimizationFlags { get; private set; } = Translation.MemoryOptimizations.None;
        public SubtitleConfiguration Subtitles { get; set; } = new SubtitleConfiguration();

        public string TextureNameTemplate
        {
            get => "{NAME}";
            set
            {
                string name = value.Trim();
                if (string.IsNullOrEmpty(name) || !FileUtils.IsValidFilename(name))
                    throw new ArgumentException("Invalid file name");
                name = parameterReplacementRegex.Replace(name,
                                                         m =>
                                                         {
                                                             string template = m.Groups[1].Value;
                                                             int i = Array.IndexOf(texTemplateVariables, template.Trim().ToUpper());
                                                             return i < 0 ? m.Value : $"{{{i.ToString()}}}";
                                                         });
                Logger.TextureNameTemplate = name;
            }
        }

        public string Verbosity
        {
            get => ResourceType.None.ToString();

            set
            {
                var parts = value.Split(new[] {'|'}, StringSplitOptions.RemoveEmptyEntries);
                VerbosityLevel = parts.Aggregate(ResourceType.None,
                                                 (current, part) =>
                                                         current | (ResourceType) Enum.Parse(typeof(ResourceType), part.Trim(), true));
                Logger.Verbosity = VerbosityLevel;
            }
        }

        public ResourceType VerbosityLevel { get; private set; } = ResourceType.None;
    }
}