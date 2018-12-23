using System;
using UnityEngine;

namespace CM3D2.YATranslator.Hook
{
    public class TextureTranslationEventArgs : EventArgs
    {
        public TextureTranslationEventArgs(string name, string meta)
        {
            Name = name.Replace('.', '-');
            Meta = meta;
            CompoundHash = string.IsNullOrEmpty(Meta) || Meta.Equals(Name, StringComparison.Ordinal)
                                   ? GetMetaHash(Name).ToString("X16")
                                   : GetMetaHash(Meta + ":" + Name).ToString("X16");
        }

        public string CompoundHash { get; }

        public TextureResource Data { get; set; }

        public string Meta { get; }

        public string Name { get; }

        public Texture2D OriginalTexture { get; internal set; }

        internal static ulong GetMetaHash(string s)
        {
            ulong num = 3074457345618258791uL;
            foreach (char t in s)
            {
                num += t;
                num *= 3074457345618258799uL;
            }

            return num;
        }
    }
}