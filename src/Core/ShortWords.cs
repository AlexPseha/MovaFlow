using System;
using System.Collections.Generic;

namespace MovaFlow.Core;

/// <summary>
/// Curated white-list of common 1 to 3 letter words for Ukrainian, English, and Russian.
/// Short wrong-layout words cannot be judged by the 25k dictionary:
/// "ye" (= "ну"), "wt" (= "це"), "db" (= "вз") are all valid in an unabridged English dictionary.
/// A short word is converted ONLY into a common short word of the target language,
/// and ONLY if what was typed is not a common short word itself (e.g. "зу" never converts to "pe").
/// Inspired by Klavko's ShortWords architecture.
/// </summary>
public static class ShortWords
{
    public const int MaxLength = 3;

    private static readonly HashSet<string> CommonUk = new(StringComparer.OrdinalIgnoreCase)
    {
        "а", "б", "в", "ж", "з", "є", "і", "й", "о", "у", "я",
        "аж", "би", "бо", "ви", "де", "до", "же", "за", "її", "із", "їй", "їм", "їх", "як", "ми", "на",
        "не", "ні", "ну", "ой", "ок", "от", "по", "та", "ти", "то", "ті", "це", "ці", "чи", "ще", "що",
        "ага", "або", "але", "без", "вам", "вас", "вже", "він", "від", "все", "вся", "всі", "для", "ось",
        "мій", "між", "моє", "мої", "над", "нам", "нас", "наш", "ним", "них", "ніж", "під", "при", "про",
        "сам", "так", "там", "теж", "тим", "тих", "тут", "хто", "чим", "чий", "якщо", "щоб"
    };

    private static readonly HashSet<string> CommonEn = new(StringComparer.OrdinalIgnoreCase)
    {
        "a", "i",
        "am", "an", "as", "at", "be", "by", "do", "go", "he", "hi", "if", "in", "is", "it", "me", "my",
        "no", "of", "oh", "ok", "on", "or", "so", "to", "up", "us", "we", "yo",
        "ago", "all", "and", "any", "are", "ask", "bad", "big", "box", "boy", "bug", "bus", "but", "can", "car", "day", "did", "few", "fix", "for", "get",
        "got", "had", "has", "her", "hey", "him", "his", "how", "its", "let", "lol", "man", "may", "new",
        "not", "now", "off", "old", "one", "our", "out", "own", "per", "put", "run", "say", "see", "set",
        "she", "the", "too", "top", "try", "two", "use", "via", "was", "way", "who", "why", "wow", "yes",
        "yet", "you"
    };

    private static readonly HashSet<string> CommonRu = new(StringComparer.OrdinalIgnoreCase)
    {
        "а", "в", "и", "к", "о", "с", "у", "я",
        "бы", "во", "да", "до", "ее", "ей", "ею", "его", "ему", "же", "за", "из", "их", "ли", "мы", "на",
        "не", "ни", "но", "ну", "он", "по", "со", "та", "те", "то", "ты", "уж",
        "без", "был", "вам", "вас", "вот", "все", "всю", "где", "для", "две", "дня", "еще",
        "или", "как", "кто", "мне", "мог", "моя", "мои", "мое", "над", "нам", "нас", "наш", "ней", "нем",
        "нет", "них", "она", "они", "оно", "под", "при", "про", "раз", "сам", "так", "там", "тем", "тех",
        "тот", "три", "тут", "уже", "чем", "что", "это", "эти", "эту"
    };

    /// <summary>
    /// Checks if a word is in the curated short-words whitelist for the given language.
    /// </summary>
    public static bool IsCommon(string word, string language)
    {
        if (string.IsNullOrEmpty(word)) return false;
        string lang = language.ToLowerInvariant();

        if (lang.StartsWith("uk"))
        {
            return CommonUk.Contains(word);
        }
        if (lang.StartsWith("en"))
        {
            return CommonEn.Contains(word);
        }
        if (lang.StartsWith("ru"))
        {
            return CommonRu.Contains(word);
        }

        return false;
    }

    /// <summary>
    /// Decides whether a short word (<= 3 letters) should be converted into candidate word of target language.
    /// </summary>
    public static bool ShouldConvertShort(
        string typedWord,
        string typedLang,
        string candidateWord,
        string targetLang,
        LanguageContext? context = null)
    {
        // 1. Target candidate MUST be a recognized common short word
        if (!IsCommon(candidateWord, targetLang))
        {
            return false;
        }

        // 2. If recent line context explicitly favors target language, convert it
        if (context != null && context.Favors(targetLang, typedLang))
        {
            return true;
        }

        // 3. If the typed text is already a common short word in its own language, DO NOT convert!
        // (Protects "зу", "не", "де", "ми", "як", "to", "in", "on", "no")
        if (IsCommon(typedWord, typedLang))
        {
            return false;
        }

        // 4. In a single-letter case without context, it is too ambiguous unless context favors target
        if (candidateWord.Length == 1 && typedWord.Length == 1)
        {
            return false;
        }

        return true;
    }
}
