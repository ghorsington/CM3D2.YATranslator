namespace CM3D2.YATranslator.Plugin.Translation
{
    public enum TranslationResult
    {
        Invalid,
        Ok,
        NotFound,
        Translated
    }

    public struct TextTranslation
    {
        public string Text;
        public TranslationResult Result;

        public TextTranslation(string text, TranslationResult result)
        {
            Text = text;
            Result = result;
        }
    }
}