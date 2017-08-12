using System;
using UnityEngine;

namespace CM3D2.YATranslator.Hook
{
    public class StringTranslationEventArgs : EventArgs
    {
        public string Text { get; internal set; }
        public MonoBehaviour TextContainer { get; internal set; }

        public string Translation { get; set; }

        public StringType Type { get; internal set; }
    }
}