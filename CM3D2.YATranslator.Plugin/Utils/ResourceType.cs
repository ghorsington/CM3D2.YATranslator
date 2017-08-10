using System;

namespace CM3D2.YATranslator.Plugin.Utils
{
    [Flags]
    public enum ResourceType
    {
        None = 0,
        Strings = 1 << 0,
        Textures = 1 << 1,
        Assets = 1 << 2,
        Voices = 1 << 3,
        All = Strings | Textures | Assets | Voices
    }
}