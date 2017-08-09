using System;

namespace CM3D2.YATranslator.Hook
{
    public class StringTranslationEventArgs : EventArgs
    {
        public string Text { get; internal set; }

        public string Translation { get; set; }

        public UILabel Label { get; internal set; }
    }
}