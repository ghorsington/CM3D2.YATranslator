using System.Collections;
using System.IO;
using CM3D2.YATranslator.Plugin.Utils;
using UnityEngine;
using UnityEngine.UI;
using Logger = CM3D2.YATranslator.Plugin.Utils.Logger;

namespace CM3D2.YATranslator.Plugin
{
    public class Subtitles : MonoBehaviour
    {
        public const string AUDIOCLIP_PREFIX = "#";
        private ManagedCoroutine currentAudioTracker = ManagedCoroutine.NoRoutine;
        private SubtitleConfiguration currentConfig;
        private bool hideAfterSound;
        private Outline outline;
        private bool showUntranslatedText;

        private Text subtitleText;
        private GameObject translationCanvas;

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

        public bool Enabled { get; private set; }

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
            Font myFont = (Font) Resources.GetBuiltinResource(typeof(Font), "Arial.ttf");
            subtitleText.font = myFont;
            subtitleText.material = myFont.material;
            subtitleText.text = string.Empty;
            InitText();
        }

        public void DisplayFor(AudioSourceMgr mgr)
        {
            if (!Enabled)
                return;

            currentAudioTracker.Stop();

            string soundName = Path.GetFileNameWithoutExtension(mgr.FileName);
            subtitleText.text = AUDIOCLIP_PREFIX + soundName;

            if (subtitleText.text == soundName)
            {
                if (!showUntranslatedText)
                    subtitleText.text = string.Empty;

                Logger.DumpVoice(soundName, mgr.audiosource.clip);
            }

            IEnumerator TrackSubtitleAudio(AudioSource audio)
            {
                yield return null;
                while (audio != null && audio.isPlaying)
                    yield return new WaitForSeconds(0.1f);

                subtitleText.text = string.Empty;
            }

            if (hideAfterSound)
                currentAudioTracker = this.StartManagedCoroutine(TrackSubtitleAudio(mgr.audiosource));
        }

        private void InitText()
        {
            if (outline == null || subtitleText == null)
                return;

            outline.enabled = currentConfig.Outline;
            outline.effectDistance = new Vector2(currentConfig.OutlineThickness, currentConfig.OutlineThickness);
            outline.effectColor = currentConfig.TextOutlineColor;

            subtitleText.fontSize = currentConfig.FontSize;
            subtitleText.fontStyle = currentConfig.Style;
            subtitleText.material.color = currentConfig.Color;
            subtitleText.alignment = currentConfig.Alignment;
            subtitleText.rectTransform.anchoredPosition = currentConfig.Offset;
        }
    }
}