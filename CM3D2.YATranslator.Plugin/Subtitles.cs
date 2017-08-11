using System.Collections;
using System.IO;
using CM3D2.YATranslator.Hook;
using CM3D2.YATranslator.Plugin.Utils;
using UnityEngine;
using UnityEngine.UI;
using Logger = CM3D2.YATranslator.Plugin.Utils.Logger;

namespace CM3D2.YATranslator.Plugin
{
    public class Subtitles : MonoBehaviour
    {
        private Text subtitleText;
        private Outline outline;
        private GameObject translationCanvas;
        private Coroutine currentAudioTracker;
        public bool Enabled { get; private set; }
        private bool hideAfterSound;
        private bool showUntranslatedText;
        private SubtitleConfiguration currentConfig;

        public SubtitleConfiguration Configuration
        {
            set
            {
                currentConfig = value;

                Enabled = value.Enable;
                hideAfterSound = value.HideWhenClipStops;
                showUntranslatedText = value.ShowUntranslatedText;
                InitText();
            }
        }

        public void Awake()
        {
            DontDestroyOnLoad(this);
        }

        public void Start()
        {
            translationCanvas = new GameObject("TranslationCanvas");

            GameObject panel = new GameObject("Panel");
            DontDestroyOnLoad(translationCanvas);
            DontDestroyOnLoad(panel);

            panel.transform.parent = translationCanvas.transform;

            Canvas canvas = translationCanvas.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;

            RectTransform rect = panel.AddComponent<RectTransform>();
            rect.sizeDelta = new Vector2(0f, 0f);
            rect.anchorMin = new Vector2(0f, 0f);
            rect.anchorMax = new Vector2(1f, 1f);

            outline = panel.AddComponent<Outline>();

            subtitleText = panel.AddComponent<Text>();
            subtitleText.transform.SetParent(panel.transform, false);
            Font myFont = (Font)Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
            subtitleText.font = myFont;
            subtitleText.material = myFont.material;
            subtitleText.text = string.Empty;
            InitText();
        }

        private void InitText()
        {
            if (outline == null || subtitleText == null)
                return;

            outline.enabled = currentConfig.Outline;
            outline.effectDistance = new Vector2(currentConfig.OutlineThickness, currentConfig.OutlineThickness);
            outline.effectColor = currentConfig.OutlineColor;

            subtitleText.fontSize = currentConfig.FontSize;
            subtitleText.fontStyle = currentConfig.Style;
            subtitleText.material.color = currentConfig.Color;
            subtitleText.alignment = currentConfig.Alignment;
            subtitleText.rectTransform.anchoredPosition = currentConfig.Offset;
        }

        public void DisplayFor(AudioSourceMgr mgr)
        {
            if (!Enabled)
                return;

            if (currentAudioTracker != null)
                StopCoroutine(currentAudioTracker);

            string soundName = Path.GetFileNameWithoutExtension(mgr.FileName);
            subtitleText.text = soundName;

            if (subtitleText.text == soundName)
            {
                if (!showUntranslatedText)
                    subtitleText.text = string.Empty;

                Logger.DumpVoice(soundName, mgr.audiosource.clip);
            }

            IEnumerator TrackSubtitleAudio(AudioSource audio)
            {
                yield return null;
                while (audio.isPlaying)
                    yield return new WaitForSeconds(0.1f);

                subtitleText.text = string.Empty;
                currentAudioTracker = null;
            }

            if (hideAfterSound)
                currentAudioTracker = StartCoroutine(TrackSubtitleAudio(mgr.audiosource));
        }
    }
}