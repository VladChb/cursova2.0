using System.Collections.Generic;
using System.Text;

namespace MediaVault.Helpers
{
    /// <summary>
    /// Перетворює кирилицю в латиницю для пошуку в iTunes
    /// </summary>
    public static class Transliterator
    {
        private static readonly Dictionary<char, string> Map = new()
        {
            // Українські
            {'А', "A"}, {'Б', "B"}, {'В', "V"}, {'Г', "H"}, {'Ґ', "G"},
            {'Д', "D"}, {'Е', "E"}, {'Є', "Ye"}, {'Ж', "Zh"}, {'З', "Z"},
            {'И', "Y"}, {'І', "I"}, {'Ї', "Yi"}, {'Й', "Y"}, {'К', "K"},
            {'Л', "L"}, {'М', "M"}, {'Н', "N"}, {'О', "O"}, {'П', "P"},
            {'Р', "R"}, {'С', "S"}, {'Т', "T"}, {'У', "U"}, {'Ф', "F"},
            {'Х', "Kh"}, {'Ц', "Ts"}, {'Ч', "Ch"}, {'Ш', "Sh"}, {'Щ', "Shch"},
            {'Ь', ""}, {'Ю', "Yu"}, {'Я', "Ya"},

            // Російські (для сумісності)
            {'Ё', "Yo"}, {'Ы', "Y"}, {'Э', "E"}, {'Ъ', ""},

            // Малі українські
            {'а', "a"}, {'б', "b"}, {'в', "v"}, {'г', "h"}, {'ґ', "g"},
            {'д', "d"}, {'е', "e"}, {'є', "ie"}, {'ж', "zh"}, {'з', "z"},
            {'и', "y"}, {'і', "i"}, {'ї', "i"}, {'й', "i"}, {'к', "k"},
            {'л', "l"}, {'м', "m"}, {'н', "n"}, {'о', "o"}, {'п', "p"},
            {'р', "r"}, {'с', "s"}, {'т', "t"}, {'у', "u"}, {'ф', "f"},
            {'х', "kh"}, {'ц', "ts"}, {'ч', "ch"}, {'ш', "sh"}, {'щ', "shch"},
            {'ь', ""}, {'ю', "iu"}, {'я', "ia"},

            // Малі російські
            {'ё', "yo"}, {'ы', "y"}, {'э', "e"}, {'ъ', ""}
        };

        public static string Transliterate(string input)
        {
            if (string.IsNullOrEmpty(input))
                return input;

            var sb = new StringBuilder();
            foreach (char c in input)
            {
                if (Map.TryGetValue(c, out string replacement))
                    sb.Append(replacement);
                else
                    sb.Append(c);
            }
            return sb.ToString();
        }
    }
}