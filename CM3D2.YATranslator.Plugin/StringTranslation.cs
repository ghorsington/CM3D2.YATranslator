using System;
using System.Text.RegularExpressions;

namespace CM3D2.YATranslator.Plugin {
    public class StringTranslation
    {
        public StringTranslation(string original, string translation, int[] levels)
        {
            Level = levels;
            TargetTranslation = translation;

            if (original.StartsWith("$", StringComparison.CurrentCulture))
                RegexTranslation = new Regex(original.Substring(1), RegexOptions.Compiled);
            else
                ConstTranslation = original;
        }

        public string ConstTranslation { get; set; }

        public bool HasToken { get; set; }

        public bool IsRegex => RegexTranslation != null;

        public int[] Level { get; set; }

        public Regex RegexTranslation { get; set; }

        public string TargetTranslation { get; set; }
    }
}