namespace MovaFlow.Core;

public enum ScriptType
{
    Latin,
    Cyrillic,
    Mixed,
    Other
}

public static class LayoutMatrix
{
    private static readonly Dictionary<char, char> EnToUkMap = new()
    {
        // Lowercase
        { 'q', 'й' }, { 'w', 'ц' }, { 'e', 'у' }, { 'r', 'к' }, { 't', 'е' },
        { 'y', 'н' }, { 'u', 'г' }, { 'i', 'ш' }, { 'o', 'щ' }, { 'p', 'з' },
        { '[', 'х' }, { ']', 'ї' },
        { 'a', 'ф' }, { 's', 'і' }, { 'd', 'в' }, { 'f', 'а' }, { 'g', 'п' },
        { 'h', 'р' }, { 'j', 'о' }, { 'k', 'л' }, { 'l', 'д' }, { ';', 'ж' },
        { '\'', 'є' },
        { 'z', 'я' }, { 'x', 'ч' }, { 'c', 'с' }, { 'v', 'м' }, { 'b', 'и' },
        { 'n', 'т' }, { 'm', 'ь' }, { ',', 'б' }, { '.', 'ю' }, { '/', '.' },
        { '`', '\'' },

        // Uppercase
        { 'Q', 'Й' }, { 'W', 'Ц' }, { 'E', 'У' }, { 'R', 'К' }, { 'T', 'Е' },
        { 'Y', 'Н' }, { 'U', 'Г' }, { 'I', 'Ш' }, { 'O', 'Щ' }, { 'P', 'З' },
        { '{', 'Х' }, { '}', 'Ї' },
        { 'A', 'Ф' }, { 'S', 'І' }, { 'D', 'В' }, { 'F', 'А' }, { 'G', 'П' },
        { 'H', 'Р' }, { 'J', 'О' }, { 'K', 'Л' }, { 'L', 'Д' }, { ':', 'Ж' },
        { '"', 'Є' },
        { 'Z', 'Я' }, { 'X', 'Ч' }, { 'C', 'С' }, { 'V', 'М' }, { 'B', 'И' },
        { 'N', 'Т' }, { 'M', 'Ь' }, { '<', 'Б' }, { '>', 'Ю' }, { '?', ',' },
        { '~', '₴' }, { '@', '"' }, { '#', '№' }, { '$', ';' }, { '^', ':' },
        { '&', '?' }
    };

    private static readonly Dictionary<char, char> UkToEnMap = new();

    private static readonly Dictionary<char, char> EnToRuMap = new();
    private static readonly Dictionary<char, char> RuToEnMap = new();

    static LayoutMatrix()
    {
        foreach (var kvp in EnToUkMap)
        {
            UkToEnMap[kvp.Value] = kvp.Key;
        }

        // Additional Ukrainian special characters
        UkToEnMap['Ґ'] = 'G';
        UkToEnMap['ґ'] = 'g';
        UkToEnMap['’'] = '\'';
        UkToEnMap['‘'] = '\'';

        // Russian layout initialization
        foreach (var kvp in EnToUkMap)
        {
            EnToRuMap[kvp.Key] = kvp.Value;
        }

        // Differences between Russian and Ukrainian layouts
        EnToRuMap['s'] = 'ы';
        EnToRuMap['S'] = 'Ы';
        EnToRuMap[']'] = 'ъ';
        EnToRuMap['}'] = 'Ъ';
        EnToRuMap['\''] = 'э';
        EnToRuMap['"'] = 'Э';
        EnToRuMap['`'] = 'ё';
        EnToRuMap['~'] = 'Ё';
        EnToRuMap['@'] = '"';
        EnToRuMap['#'] = '№';
        EnToRuMap['$'] = ';';
        EnToRuMap['^'] = ':';
        EnToRuMap['&'] = '?';

        foreach (var kvp in EnToRuMap)
        {
            RuToEnMap[kvp.Value] = kvp.Key;
        }
    }

    public static string EnToUk(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var chars = new char[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            chars[i] = EnToUkMap.TryGetValue(text[i], out var ukChar) ? ukChar : text[i];
        }
        return new string(chars);
    }

    public static string UkToEn(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var chars = new char[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            chars[i] = UkToEnMap.TryGetValue(text[i], out var enChar) ? enChar : text[i];
        }
        return new string(chars);
    }

    public static string EnToRu(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var chars = new char[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            chars[i] = EnToRuMap.TryGetValue(text[i], out var ruChar) ? ruChar : text[i];
        }
        return new string(chars);
    }

    public static string RuToEn(string text)
    {
        if (string.IsNullOrEmpty(text)) return text;
        var chars = new char[text.Length];
        for (int i = 0; i < text.Length; i++)
        {
            chars[i] = RuToEnMap.TryGetValue(text[i], out var enChar) ? enChar : text[i];
        }
        return new string(chars);
    }

    public static ScriptType DetectScript(string text)
    {
        if (string.IsNullOrEmpty(text)) return ScriptType.Other;

        int latinCount = 0;
        int cyrillicCount = 0;

        foreach (char c in text)
        {
            if ((c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z'))
            {
                latinCount++;
            }
            else if ((c >= 'а' && c <= 'я') || (c >= 'А' && c <= 'Я') ||
                     c == 'і' || c == 'І' || c == 'ї' || c == 'Ї' ||
                     c == 'є' || c == 'Є' || c == 'ґ' || c == 'Ґ' ||
                     c == 'ё' || c == 'Ё')
            {
                cyrillicCount++;
            }
        }

        if (latinCount > 0 && cyrillicCount == 0) return ScriptType.Latin;
        if (cyrillicCount > 0 && latinCount == 0) return ScriptType.Cyrillic;
        if (latinCount > 0 && cyrillicCount > 0) return ScriptType.Mixed;
        return ScriptType.Other;
    }
}
