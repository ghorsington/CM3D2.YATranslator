using System;

namespace CM3D2.YATranslator.Hook
{
    public class SoundEventArgs : EventArgs
    {
        public AudioSourceMgr AudioSourceMgr { get; internal set; }
    }
}