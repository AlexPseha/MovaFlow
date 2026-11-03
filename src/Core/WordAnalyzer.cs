namespace MovaFlow.Core;

public enum ConversionMode
{
    Both,
    UkrToEng,
    EngToUkr
}

public record TransformationResult(
    bool ShouldTransform,
    string OriginalText,
    string TransformedText,
    string TargetLanguage
);

public record WordAnalyzerOptions(
    bool EnableUkrainian = true,
    bool EnableEnglish = true,
    bool EnableRussian = true,
    Func<string, bool>? IsWordExcluded = null
);

public class WordAnalyzer
{
    private readonly TrieDictionary _ukDictionary = new();
    private readonly TrieDictionary _ruDictionary = new();
    private readonly TrieDictionary _enDictionary = new();

    public WordAnalyzer()
    {
        InitializeVocabularies();
    }

    public bool IsKnownWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        string lower = word.Trim().ToLowerInvariant();
        return _ukDictionary.Contains(lower) || _enDictionary.Contains(lower) || _ruDictionary.Contains(lower);
    }

    public TransformationResult Analyze(
        string rawWord,
        ConversionMode mode,
        bool isOnTheFly,
        WordAnalyzerOptions? options = null)
    {
        options ??= new WordAnalyzerOptions();

        if (string.IsNullOrWhiteSpace(rawWord))
        {
            return new TransformationResult(false, rawWord, rawWord, string.Empty);
        }

        // Exclusion check (custom words, acronyms, ignored words)
        if (options.IsWordExcluded != null && options.IsWordExcluded(rawWord))
        {
            return new TransformationResult(false, rawWord, rawWord, string.Empty);
        }

        var script = LayoutMatrix.DetectScript(rawWord);
        if (script is ScriptType.Mixed or ScriptType.Other)
        {
            return new TransformationResult(false, rawWord, rawWord, string.Empty);
        }

        // Scenario 1: User typed in Latin (English letters), but meant Cyrillic (UKR or RUS)
        if (script == ScriptType.Latin && mode is ConversionMode.Both or ConversionMode.EngToUkr)
        {
            if (options.EnableUkrainian || options.EnableRussian)
            {
                var cyrillicCandidate = ChooseCyrillicCandidate(rawWord, isOnTheFly, options);
                if (cyrillicCandidate != null)
                {
                    return cyrillicCandidate;
                }
            }
        }

        // Scenario 2: User typed in Cyrillic, but meant English
        if (script == ScriptType.Cyrillic && mode is ConversionMode.Both or ConversionMode.UkrToEng && options.EnableEnglish)
        {
            string candidateEn = NgramDetector.ContainsRussianSpecificLetters(rawWord)
                ? LayoutMatrix.RuToEn(rawWord)
                : LayoutMatrix.UkToEn(rawWord);

            if (isOnTheFly)
            {
                // For 2-letter words like "зу", "не", "де", "як", only convert if it matches an impossible Cyrillic prefix (e.g. "иг" -> "ug").
                // Never convert 2-letter Cyrillic syllables just because English has a 2-letter word (like "pe", "re", "to")!
                bool isShortWord = rawWord.Length <= 2;
                bool isImpossibleCyrillic = NgramDetector.IsLikelyEnglishMistypedAsCyrillic(rawWord);

                if (isImpossibleCyrillic ||
                    (!isShortWord && !_ukDictionary.HasPrefix(rawWord) && !_ruDictionary.HasPrefix(rawWord) && _enDictionary.HasPrefix(candidateEn)))
                {
                    return new TransformationResult(true, rawWord, candidateEn, "en");
                }
            }
            else
            {
                bool isSlavicWord = _ukDictionary.Contains(rawWord) || _ruDictionary.Contains(rawWord);
                bool isEnWord = _enDictionary.Contains(candidateEn);

                if (!isSlavicWord && isEnWord)
                {
                    // Protection against converting 2-letter Ukrainian syllables into English words (e.g. "зу" -> "pe")
                    if (rawWord.Length <= 2 && !NgramDetector.IsLikelyEnglishMistypedAsCyrillic(rawWord))
                    {
                        return new TransformationResult(false, rawWord, rawWord, string.Empty);
                    }

                    return new TransformationResult(true, rawWord, candidateEn, "en");
                }

                if (!isSlavicWord && NgramDetector.IsLikelyEnglishMistypedAsCyrillic(rawWord))
                {
                    return new TransformationResult(true, rawWord, candidateEn, "en");
                }
            }
        }

        return new TransformationResult(false, rawWord, rawWord, string.Empty);
    }

    private TransformationResult? ChooseCyrillicCandidate(string rawWord, bool isOnTheFly, WordAnalyzerOptions options)
    {
        string candidateUk = LayoutMatrix.EnToUk(rawWord);
        string candidateRu = LayoutMatrix.EnToRu(rawWord);

        bool ukEnabled = options.EnableUkrainian;
        bool ruEnabled = options.EnableRussian;

        if (isOnTheFly)
        {
            bool impossibleEn = NgramDetector.IsLikelyCyrillicMistypedAsEnglish(rawWord);
            bool hasEnPrefix = _enDictionary.HasPrefix(rawWord);

            // Check dictionary prefixes
            bool ukPrefix = ukEnabled && _ukDictionary.HasPrefix(candidateUk);
            bool ruPrefix = ruEnabled && _ruDictionary.HasPrefix(candidateRu);

            if (impossibleEn || !hasEnPrefix)
            {
                if (ruPrefix && !ukPrefix)
                {
                    return new TransformationResult(true, rawWord, candidateRu, "ru");
                }
                if (ukPrefix && !ruPrefix)
                {
                    return new TransformationResult(true, rawWord, candidateUk, "uk");
                }
                if (ukPrefix && ruPrefix)
                {
                    if (ukEnabled) return new TransformationResult(true, rawWord, candidateUk, "uk");
                    if (ruEnabled) return new TransformationResult(true, rawWord, candidateRu, "ru");
                }
                if (impossibleEn)
                {
                    // Fallback preference: only convert if candidateUk / candidateRu has a prefix or word in the dictionary!
                    // Prohibit converting English words/identifiers (like CamelCase "OrdersWithTrips") into gibberish "ЩквукіЦшерЕк"!
                    if (ukEnabled && (_ukDictionary.HasPrefix(candidateUk) || _ukDictionary.Contains(candidateUk)))
                    {
                        return new TransformationResult(true, rawWord, candidateUk, "uk");
                    }
                    if (ruEnabled && (_ruDictionary.HasPrefix(candidateRu) || _ruDictionary.Contains(candidateRu)))
                    {
                        return new TransformationResult(true, rawWord, candidateRu, "ru");
                    }
                }
            }
        }
        else
        {
            bool isEnWord = _enDictionary.Contains(rawWord);
            bool isUkWord = ukEnabled && _ukDictionary.Contains(candidateUk);
            bool isRuWord = ruEnabled && _ruDictionary.Contains(candidateRu);

            if (!isEnWord)
            {
                if (isRuWord && !isUkWord)
                {
                    return new TransformationResult(true, rawWord, candidateRu, "ru");
                }
                if (isUkWord && !isRuWord)
                {
                    return new TransformationResult(true, rawWord, candidateUk, "uk");
                }
                if (isUkWord && isRuWord)
                {
                    return new TransformationResult(true, rawWord, candidateUk, "uk");
                }
                if (NgramDetector.IsLikelyCyrillicMistypedAsEnglish(rawWord))
                {
                    if (ukEnabled && (_ukDictionary.Contains(candidateUk) || _ukDictionary.HasPrefix(candidateUk)))
                        return new TransformationResult(true, rawWord, candidateUk, "uk");
                    if (ruEnabled && (_ruDictionary.Contains(candidateRu) || _ruDictionary.HasPrefix(candidateRu)))
                        return new TransformationResult(true, rawWord, candidateRu, "ru");
                }
            }
        }

        return null;
    }

    private static readonly HashSet<string> RussianOnlyFalseFriends = new(StringComparer.OrdinalIgnoreCase)
    {
        "привет", "спасибо", "пожалуйста", "здравствуйте", "хорошо", "конечно", "сейчас",
        "потому", "только", "здесь", "потом", "почему", "зачем", "сегодня", "вчера",
        "очень", "быстро", "нужно", "будет"
    };

    private void InitializeVocabularies()
    {
        // 1. Load comprehensive frequency wordlists (embedded resources)
        LoadEmbeddedDictionary("MovaFlow.resources.dicts.uk.txt", _ukDictionary, isUkrainian: true);
        LoadEmbeddedDictionary("MovaFlow.resources.dicts.en.txt", _enDictionary);
        LoadEmbeddedDictionary("MovaFlow.resources.dicts.ru.txt", _ruDictionary);

        // Also check filesystem fallback if running during development
        LoadFileDictionary(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "dicts", "uk.txt"), _ukDictionary, isUkrainian: true);
        LoadFileDictionary(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "dicts", "en.txt"), _enDictionary);
        LoadFileDictionary(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "dicts", "ru.txt"), _ruDictionary);

        // 2. High priority common Ukrainian words and stems
        string[] commonUk =
        {
            "це", "не", "де", "як", "ми", "ви", "ти", "он", "то", "чи", "бо", "за", "на", "до", "по", "із", "ще", "їх", "ця", "ці", "от", "ну", "та", "їй", "ся", "би", "же",
            "баг", "баги", "багів", "багом", "буде", "бути", "був", "була", "були", "було", "будуть", "без", "багато", "більше", "бачив", "бачити", "юзер", "юзери",
            "справи", "справа", "справі", "справу", "справою", "справах", "справді", "спроба", "спробувати", "спасибі", "стало", "стати",
            "привіт", "дякую", "будь", "ласка", "доброго", "дня", "ранку", "вечора",
            "робота", "мова", "програма", "клавіатура", "текст", "перемикач", "користувач",
            "система", "вікно", "файл", "папка", "проєкт", "команда", "помилка", "успіх",
            "сьогодні", "вчора", "завтра", "зараз", "також", "тому", "що", "якщо", "коли",
            "де", "куди", "звідки", "чому", "який", "яка", "яке", "які", "хто", "що",
            "мене", "тебе", "його", "її", "нас", "вас", "їх", "свій", "своя", "своє",
            "дуже", "добре", "чудово", "гарно", "швидко", "надійно", "безпечно", "відкрито",
            "може", "можна", "треба", "потрібно", "хочу", "маю", "буде", "було", "є",
            "один", "два", "три", "чотири", "п'ять", "перший", "другий", "новий", "старий",
            "великий", "малий", "гарний", "правильний", "головний", "важливий", "цікавий",
            "робити", "писати", "читати", "знати", "думати", "бачити", "жити", "працювати",
            "розуміти", "говорити", "казати", "брати", "дати", "йти", "їхати", "знайти",
            "привітний", "привід", "привабливий", "приклад", "принцип", "перевірка",
            "повідомлення", "документ", "посилання", "пошук", "налаштування", "ліцензія"
        };

        foreach (var w in commonUk) _ukDictionary.Insert(w);

        // Common Russian words and stems
        string[] commonRu =
        {
            "не", "где", "как", "мы", "вы", "ты", "он", "то", "или", "за", "на", "до", "по", "из", "еще", "их", "от", "ну", "да", "но",
            "привет", "спасибо", "пожалуйста", "здравствуйте", "хорошо", "отлично", "работа",
            "язык", "программа", "клавиатура", "текст", "переключатель", "пользователь", "система",
            "окно", "файл", "папка", "проект", "команда", "ошибка", "успех", "сегодня", "вчера",
            "завтра", "сейчас", "также", "потому", "что", "если", "когда", "где", "куда", "откуда",
            "почему", "какой", "какая", "какое", "какие", "кто", "меня", "тебя", "его", "ее", "нас",
            "вас", "их", "свой", "своя", "свое", "очень", "быстро", "надежно", "безопасно", "открыто",
            "может", "можно", "надо", "нужно", "хочу", "будет", "было", "один", "два", "три", "четыре",
            "пять", "первый", "второй", "новый", "старый", "большой", "маленький", "главный", "важный",
            "делать", "писать", "читать", "знать", "думать", "видеть", "жить", "работать", "понимать",
            "говорить", "сказать", "брать", "дать", "идти", "ехать", "найти", "проверка", "сообщение",
            "документ", "ссылка", "поиск", "настройки", "лицензия", "вы", "мы", "ты", "они", "объем",
            "это", "этот", "эта", "эти", "тот", "только", "уже", "еще", "здесь", "там", "потом"
        };

        foreach (var w in commonRu) _ruDictionary.Insert(w);

        // Common English words and stems
        string[] commonEn =
        {
            "to", "in", "is", "it", "on", "at", "by", "as", "or", "an", "if", "my", "no", "so", "up", "we", "he", "do", "go", "am", "be",
            "bug", "bugs", "debug", "debugging",
            "hello", "thanks", "thank", "you", "please", "good", "morning", "evening",
            "work", "language", "program", "keyboard", "text", "switch", "switcher", "user",
            "system", "window", "file", "folder", "project", "command", "error", "success",
            "today", "yesterday", "tomorrow", "now", "also", "because", "if", "when",
            "where", "why", "which", "what", "who", "whom", "whose", "how", "many",
            "me", "him", "her", "us", "them", "my", "your", "his", "their", "our",
            "very", "well", "great", "nice", "fast", "safe", "secure", "open", "source",
            "can", "could", "should", "would", "must", "need", "want", "have", "will", "is",
            "one", "two", "three", "four", "five", "first", "second", "new", "old",
            "big", "small", "fine", "right", "main", "important", "interesting",
            "make", "write", "read", "know", "think", "see", "live", "build",
            "understand", "speak", "say", "take", "give", "go", "find",
            "check", "input", "output", "screen", "display", "setting", "settings", "license"
        };

        foreach (var w in commonEn) _enDictionary.Insert(w);
    }

    private static void LoadEmbeddedDictionary(string resourceName, TrieDictionary target, bool isUkrainian = false)
    {
        try
        {
            var assembly = typeof(WordAnalyzer).Assembly;
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream == null) return;

            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length >= 2)
                {
                    if (isUkrainian && RussianOnlyFalseFriends.Contains(line))
                    {
                        continue;
                    }
                    target.Insert(line);
                }
            }
        }
        catch { }
    }

    private static void LoadFileDictionary(string filePath, TrieDictionary target, bool isUkrainian = false)
    {
        try
        {
            if (!File.Exists(filePath)) return;
            using var stream = File.OpenRead(filePath);
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            string? line;
            while ((line = reader.ReadLine()) != null)
            {
                line = line.Trim();
                if (line.Length >= 2)
                {
                    if (isUkrainian && RussianOnlyFalseFriends.Contains(line))
                    {
                        continue;
                    }
                    target.Insert(line);
                }
            }
        }
        catch { }
    }
}
