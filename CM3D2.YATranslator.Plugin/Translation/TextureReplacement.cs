namespace CM3D2.YATranslator.Plugin.Translation
{
    public enum TextureType
    {
        None,
        PNG,
        TEX
    }

    public struct TextureReplacement
    {
        public static readonly TextureReplacement None = new TextureReplacement(TextureType.None, string.Empty);

        public TextureType TextureType;
        public string FilePath;

        public TextureReplacement(TextureType type, string path)
        {
            TextureType = type;
            FilePath = path;
        }
    }
}