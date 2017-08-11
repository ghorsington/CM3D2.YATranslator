using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;
using Logger = CM3D2.YATranslator.Plugin.Utils.Logger;
using WindowsClipboard = System.Windows.Forms.Clipboard;

namespace CM3D2.YATranslator.Plugin
{
    public class Clipboard : MonoBehaviour
    {
        private readonly HashSet<int> allowedLevels;
        private readonly StringBuilder clipboardContents;
        private readonly HashSet<string> clipboardStrings;

        private bool allowAll;
        private float currWaitTime;
        private bool isSendingToClipboard;
        private float maxWaitTime = 0.5f;

        public Clipboard()
        {
            clipboardStrings = new HashSet<string>();
            allowedLevels = new HashSet<int>();
            clipboardContents = new StringBuilder(256);
        }

        public ClipboardConfiguration Configuration
        {
            set => InitConfig(value);
        }

        public bool Enabled { get; private set; }

        public void Awake()
        {
            DontDestroyOnLoad(this);
        }

        public void AddText(string str, int level)
        {
            if (!Enabled)
                return;
            if (string.IsNullOrEmpty(str) || !allowAll && !allowedLevels.Contains(level))
                return;
            if (clipboardStrings.Contains(str))
                return;
            Logger.WriteLine($"Clipboard::Adding {str}");
            clipboardStrings.Add(str);
            clipboardContents.Append($"{str}.");
            currWaitTime = 0.0f;
            if (!isSendingToClipboard)
                StartCoroutine(SendToClipboard());
        }

        private void InitConfig(ClipboardConfiguration config)
        {
            allowedLevels.Clear();

            maxWaitTime = config.WaitTime;
            Enabled = config.Enable;
            if (!Enabled)
                return;

            foreach (int level in config.AllowedLevels)
            {
                if (level == -1)
                {
                    allowAll = true;
                    return;
                }

                allowedLevels.Add(level);
            }
            Enabled = allowedLevels.Count != 0;
            allowAll = false;
        }

        private IEnumerator SendToClipboard()
        {
            isSendingToClipboard = true;
            while (currWaitTime < maxWaitTime)
            {
                yield return new WaitForSeconds(maxWaitTime);
                currWaitTime += maxWaitTime;
            }
            WindowsClipboard.SetText(clipboardContents.ToString());
            clipboardStrings.Clear();
            clipboardContents.Length = 0;
            clipboardContents.Capacity = 256;
            currWaitTime = 0.0f;
            isSendingToClipboard = false;
        }
    }
}