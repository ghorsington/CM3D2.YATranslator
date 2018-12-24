using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace CM3D2.YATranslator.Plugin.Utils
{
    public static class Extensions
    {
        public static string Template(this string template, Func<string, string> templateFunc)
        {
            var sb = new StringBuilder(template.Length);
            var sbTemplate = new StringBuilder();

            bool insideTemplate = false;
            bool bracedTemplate = false;
            for (int i = 0; i < template.Length; i++)
            {
                char c = template[i];
                switch (c)
                {
                    case '\\':
                        if (i + 1 < template.Length && template[i + 1] == '$')
                        {
                            sb.Append('$');
                            i++;
                            continue;
                        }

                        break;
                    case '$':
                        insideTemplate = true;
                        continue;
                    case '{':
                        if (insideTemplate)
                        {
                            bracedTemplate = true;
                            continue;
                        }

                        break;
                    case '}':
                        if (insideTemplate && sbTemplate.Length > 0)
                        {
                            sb.Append(templateFunc(sbTemplate.ToString()));
                            sbTemplate.Length = 0;
                            insideTemplate = false;
                            bracedTemplate = false;
                            continue;
                        }

                        break;
                }

                if (insideTemplate && !bracedTemplate && !char.IsDigit(c))
                {
                    sb.Append(templateFunc(sbTemplate.ToString()));
                    sbTemplate.Length = 0;
                    insideTemplate = false;
                }

                if (insideTemplate)
                    sbTemplate.Append(c);
                else
                    sb.Append(c);
            }

            return sb.ToString();
        }

        public static ManagedCoroutine StartManagedCoroutine(this MonoBehaviour self, IEnumerator coroutine)
        {
            return new ManagedCoroutine(self, coroutine).Start();
        }

        public static T ParseEnum<T>(string value, bool ignoreCase = false)
        {
            return (T) Enum.Parse(typeof(T), value, ignoreCase);
        }

        public static string Escape(this string txt)
        {
            var stringBuilder = new StringBuilder(txt.Length + 2);
            foreach (char c in txt)
                switch (c)
                {
                    case '\0':
                        stringBuilder.Append(@"\0");
                        break;
                    case '\a':
                        stringBuilder.Append(@"\a");
                        break;
                    case '\b':
                        stringBuilder.Append(@"\b");
                        break;
                    case '\t':
                        stringBuilder.Append(@"\t");
                        break;
                    case '\n':
                        stringBuilder.Append(@"\n");
                        break;
                    case '\v':
                        stringBuilder.Append(@"\v");
                        break;
                    case '\f':
                        stringBuilder.Append(@"\f");
                        break;
                    case '\r':
                        stringBuilder.Append(@"\r");
                        break;
                    case '\'':
                        stringBuilder.Append(@"\'");
                        break;
                    case '\\':
                        stringBuilder.Append(@"\");
                        break;
                    case '\"':
                        stringBuilder.Append(@"\""");
                        break;
                    default:
                        stringBuilder.Append(c);
                        break;
                }
            return stringBuilder.ToString();
        }

        public static string Unescape(this string txt)
        {
            if (string.IsNullOrEmpty(txt))
                return txt;
            var stringBuilder = new StringBuilder(txt.Length);
            for (int i = 0; i < txt.Length;)
            {
                int num = txt.IndexOf('\\', i);
                if (num < 0 || num == txt.Length - 1)
                    num = txt.Length;
                stringBuilder.Append(txt, i, num - i);
                if (num >= txt.Length)
                    break;
                char c = txt[num + 1];
                switch (c)
                {
                    case '0':
                        stringBuilder.Append('\0');
                        break;
                    case 'a':
                        stringBuilder.Append('\a');
                        break;
                    case 'b':
                        stringBuilder.Append('\b');
                        break;
                    case 't':
                        stringBuilder.Append('\t');
                        break;
                    case 'n':
                        stringBuilder.Append('\n');
                        break;
                    case 'v':
                        stringBuilder.Append('\v');
                        break;
                    case 'f':
                        stringBuilder.Append('\f');
                        break;
                    case 'r':
                        stringBuilder.Append('\r');
                        break;
                    case '\'':
                        stringBuilder.Append('\'');
                        break;
                    case '\"':
                        stringBuilder.Append('\"');
                        break;
                    case '\\':
                        stringBuilder.Append('\\');
                        break;
                    default:
                        stringBuilder.Append('\\').Append(c);
                        break;
                }

                i = num + 2;
            }

            return stringBuilder.ToString();
        }
    }
}