using System;
using UnityEngine;
using UnityEngine.UI;

namespace CM3D2.YATranslator.Hook
{
    public class TranslationHooks
    {
        private static readonly byte[] EmptyTextureData = new byte[0];

        public static event EventHandler<TextureTranslationEventArgs> ArcTextureLoad;
        public static event EventHandler<TextureTranslationEventArgs> ArcTextureLoaded;
        public static event EventHandler<TextureTranslationEventArgs> AssetTextureLoad;
        public static event EventHandler<GraphicTranslationEventArgs> TranslateGraphic;
        public static event EventHandler<TextureTranslationEventArgs> SpriteLoad;
        public static event EventHandler<StringTranslationEventArgs> TranslateText;

        public static void OnTranslateGraphic(MaskableGraphic graphic)
        {
            GraphicTranslationEventArgs args = new GraphicTranslationEventArgs
            {
                Graphic = graphic
            };

            TranslateGraphic?.Invoke(null, args);
        }

        public static void OnTranslateSprite(ref Sprite sprite)
        {
            if (sprite == null)
                return;

            string spriteName = sprite.name;
            bool previouslyTranslated;
            if (string.IsNullOrEmpty(spriteName) || (previouslyTranslated = spriteName.StartsWith("!")))
                return;

            TextureTranslationEventArgs args = new TextureTranslationEventArgs(sprite.name, "SPRITE")
            {
                OriginalTexture = sprite.texture
            };

            SpriteLoad?.Invoke(null, args);

            if (args.Data == null)
                return;

            Sprite newSprite = Sprite.Create(args.Data.CreateTexture2D(), sprite.rect, sprite.pivot);
            newSprite.name = previouslyTranslated ? spriteName : "!" + spriteName;
            sprite = newSprite;
        }

        public static void OnTranslateInfoText(ref int nightWorkId, ref string info)
        {
            OnTranslateConstText(ref info);
        }

        public static TextureResource OnArcTextureLoaded(TextureResource resource, string name)
        {
            TextureTranslationEventArgs args = new TextureTranslationEventArgs(name, "ARC")
            {
                Data = resource
            };

            ArcTextureLoaded?.Invoke(null, args);

            return resource;
        }

        public static void OnTranslateConstText(ref string text)
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
            bool previouslyTranslated;
            if (string.IsNullOrEmpty(textureName) || (previouslyTranslated = textureName.StartsWith("!")) && !force)
                return;

            TextureTranslationEventArgs args = new TextureTranslationEventArgs(textureName, im.name)
            {
                OriginalTexture = texture2D
            };

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
                Text = label.text,
                Label = label
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