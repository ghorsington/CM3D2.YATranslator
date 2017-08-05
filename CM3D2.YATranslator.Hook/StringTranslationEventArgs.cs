using System;

namespace CM3D2.YATranslator.Hook
{
    public class StringTranslationEventArgs : EventArgs
    {
        public string Text { get; set; }

        public string Translation { get; set; }
    }
}