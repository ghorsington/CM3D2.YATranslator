using System;
using UnityEngine.UI;

namespace CM3D2.YATranslator.Hook
{
    public class GraphicTranslationEventArgs : EventArgs
    {
        public MaskableGraphic Graphic { get; internal set; }
    }
}