namespace MovaFlow.Core;

public static class NgramDetector
{
    // English letter combinations that have 0 frequency at word start or inside regular words,
    // but frequently represent mistyped Ukrainian/Russian letters.
    private static readonly HashSet<string> ImpossibleEnglishPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        // 2-letter mistyped words
        "wt", // це
        "yt", // не
        "lt", // де
        "xb", // чи
        "ot", // ще
        "wz", // ця
        "ws", // ці
        "][", // їх
        "zr", // як
        ",f", // ба
        ",u", // бг
        ",e", // бу
        ",k", // бл
        ",h", // бр
        ",p", // бз
        ".p", // юз
        ".h", // юр
        ".c", // юс
        ".l", // юд

        // 3-letter & 4-letter prefixes
        "cgh", "cghf", "cghj", "cgfc", "cgb", "cge", "cv", "cnh", "cnk",
        "vty", "nt,", "rjve", "xfv", "xbn", "gjx", "plh", "plk", "gjl",
        "ghb", "ghbd", "ghbr", "ghjv", "ghjc", "ghjb", "ghby", "ghbl", "ghbg",
        "ntcn", "ntrc", "jlyj", "gjxt", "gjlz", "gtht", "pfhf", "pyf", "pyft",
        "rkfd", "vjdf", "xevf", "rjve", "rjkf", "ljcb", "ljpe", "ljgj", "cnfh",
        "cgfc", "ckfd", "bcnf", "vj;k", "xtht", "rhfp", "rfhn", "cthl", "ctht",
        "lkz", "dckb", "dsly", "dflf", "dfvf", "kblf", "pfdn", "pdth", "pdfr",
        "nthv", "njve", "jnyj", "jpyf", "jclb", "jndj", "pljh", "lytx", "dcev",
        "pfgb", "pfge", "pfgh", "pfpl", "pfck", "pfcn", "pflf", "pfrj", "pfrh",
        "ghbdtn", "rfr", "xtuj", "njkm", "ctqx", "njulf", "rulf", "rnj", "xnj",
        "gtth", "vjq", "ndjq", "cdjq", "rjhj", "rhfc", "ckjd", "yjdj", "cnfh"
    };

    // Cyrillic letter combinations that never occur in real Slavic words,
    // but represent mistyped English words (e.g. рудд = hell, цщкд = worl, игп = bug).
    private static readonly HashSet<string> ImpossibleCyrillicPrefixes = new(StringComparer.OrdinalIgnoreCase)
    {
        "рудд", "цщкд", "сруск", "інзге", "іуду", "вуду", "ашду", "іву",
        "цуди", "суду", "туче", "тучт", "цшер", "црше", "гзук", "вщцт",
        "ідщц", "цщгі", "пуе", "зукщ", "іусг", "вшер", "ерші", "ерун",
        "іщьу", "вщеу", "ащгт", "сщву", "вщсі", "еуые", "екгу", "афді",
        "ыуду", "ыусг", "ыщьу", "ышер", "афды", "вщсы", "игп", "иг", "ир", "иф", "ие", "ин"
    };

    public static bool ContainsUkrainianSpecificLetters(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            if (c is 'і' or 'І' or 'ї' or 'Ї' or 'є' or 'Є' or 'ґ' or 'Ґ' or '\'')
                return true;
        }
        return false;
    }

    public static bool ContainsRussianSpecificLetters(string text)
    {
        if (string.IsNullOrEmpty(text)) return false;
        foreach (char c in text)
        {
            if (c is 'ы' or 'Ы' or 'э' or 'Э' or 'ъ' or 'Ъ' or 'ё' or 'Ё')
                return true;
        }
        return false;
    }

    public static bool IsLikelyCyrillicMistypedAsEnglish(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2) return false;
        string lower = text.ToLowerInvariant();

        // If it starts with comma or period (representing Ukrainian 'б' or 'ю')
        // followed by a letter, it is 100% mistyped Ukrainian (e.g. ,fu = баг, ,elt = буде, .pth = юзер)
        if (lower[0] == ',' || lower[0] == '.')
        {
            if (lower.Length >= 2 && char.IsLetter(lower[1]))
                return true;
        }

        // Check 2-letter impossible prefixes (like wt = це, yt = не, lt = де)
        if (lower.Length >= 2 && ImpossibleEnglishPrefixes.Contains(lower[..2]))
            return true;

        if (text.Length >= 4 && ImpossibleEnglishPrefixes.Contains(lower[..4]))
            return true;
        if (text.Length >= 3 && ImpossibleEnglishPrefixes.Contains(lower[..3]))
            return true;

        // Phonotactic heuristic: 4 or more consecutive consonants without vowels
        if (HasConsecutiveConsonants(text, 4, isEnglish: true))
            return true;

        return false;
    }

    public static bool IsLikelyEnglishMistypedAsCyrillic(string text)
    {
        if (string.IsNullOrWhiteSpace(text) || text.Length < 2) return false;
        string lower = text.ToLowerInvariant();

        if (text.Length >= 4 && ImpossibleCyrillicPrefixes.Contains(lower[..4]))
            return true;
        if (text.Length >= 3 && ImpossibleCyrillicPrefixes.Contains(lower[..3]))
            return true;
        if (ImpossibleCyrillicPrefixes.Contains(lower[..2]))
            return true;

        // Consonant clustering check
        if (HasConsecutiveConsonants(text, 4, isEnglish: false))
            return true;

        return false;
    }

    private static bool HasConsecutiveConsonants(string word, int limit, bool isEnglish)
    {
        int count = 0;
        char prevChar = '\0';
        for (int i = 0; i < word.Length; i++)
        {
            char rawC = word[i];
            // In CamelCase/PascalCase (e.g. OrdersWithTrips), an uppercase letter after a lowercase letter
            // indicates a new sub-word boundary. Reset the consecutive consonant count!
            if (i > 0 && char.IsUpper(rawC) && char.IsLower(prevChar))
            {
                count = 0;
            }
            prevChar = rawC;

            char c = char.ToLowerInvariant(rawC);
            if (isEnglish)
            {
                if ("bcdfghjklmnpqrstvwxyz".Contains(c))
                {
                    count++;
                    if (count >= limit) return true;
                }
                else
                {
                    count = 0;
                }
            }
            else
            {
                if ("бвгґджзйклмнпрстфхцчшщ".Contains(c))
                {
                    count++;
                    if (count >= limit) return true;
                }
                else
                {
                    count = 0;
                }
            }
        }
        return false;
    }
}
