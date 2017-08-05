using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

namespace CM3D2.YATranslator.Hook
{
    public class TranslationHooks
    {
        private static readonly byte[] EmptyTextureData = new byte[0];

        public static event EventHandler<TextureTranslationEventArgs> ArcTextureLoad;
        public static event EventHandler<TextureTranslationEventArgs> AssetTextureLoad;
        public static event EventHandler<StringTranslationEventArgs> TranslateText;

        public static void OnTranslateInfoText(ref int nightWorkId, ref string info)
        {
            OnTranslateTaggedText(ref info);
        }

        public static void OnTranslateTaggedText(ref string text)
        {
            StringTranslationEventArgs args = new StringTranslationEventArgs
            {
                Text = text
            };

            TranslateText?.Invoke(null, args);

            if (!string.IsNullOrEmpty(args.Translation))
                text = args.Translation;
        }

        public static bool OnArcTextureLoad(out TextureResource result, string name)
        {
            name = name.Replace(".tex", "");

            TextureTranslationEventArgs args = new TextureTranslationEventArgs(name, "ARC");

            ArcTextureLoad?.Invoke(null, args);

            result = args.Data;
            return args.Data != null;
        }

        public static void OnAssetTextureLoad(int forceTag, UIWidget im)
        {
            bool force = forceTag != 0;
            Texture2D texture2D;
            if ((texture2D = im.material?.mainTexture as Texture2D) == null)
                return;

            string textureName = texture2D.name;
            bool previouslyTranslated = textureName.StartsWith("!");
            if (string.IsNullOrEmpty(textureName) || previouslyTranslated && !force)
                return;

            TextureTranslationEventArgs args = new TextureTranslationEventArgs(textureName, im.name);

            AssetTextureLoad?.Invoke(im, args);

            if (args.Data == null)
                return;

            if (!previouslyTranslated)
                texture2D.name = "!" + textureName;
            texture2D.LoadImage(EmptyTextureData);
            texture2D.LoadImage(args.Data.data);
        }

        public static void OnTranslateText(UILabel label, ref string text)
        {
            StringTranslationEventArgs args = new StringTranslationEventArgs
            {
                Text = label.text
            };

            TranslateText?.Invoke(null, args);

            if (string.IsNullOrEmpty(args.Translation))
                return;
            text = args.Translation;
            label.useFloatSpacing = false;
            label.spacingX = -1;
        }
    }
}
