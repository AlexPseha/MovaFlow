using System.IO;
using MovaFlow.Core;
using MovaFlow.Localization;
using MovaFlow.Security;

namespace MovaFlow.Tests;

public static class UnitTests
{
    public static int RunAll()
    {
        int passed = 0;
        int failed = 0;

        void Assert(bool condition, string testName)
        {
            if (condition)
            {
                Console.WriteLine($"[PASS] {testName}");
                passed++;
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine($"[FAIL] {testName}");
                Console.ResetColor();
                failed++;
            }
        }

        Console.WriteLine("=== Running MovaFlow Self-Tests ===");

        // 1. LayoutMatrix Tests (Ukrainian)
        Assert(LayoutMatrix.EnToUk("ghbdsn") == "привіт", "LayoutMatrix: 'ghbdsn' -> 'привіт'");
        Assert(LayoutMatrix.UkToEn("руддщ") == "hello", "LayoutMatrix: 'руддщ' -> 'hello'");
        Assert(LayoutMatrix.EnToUk("Ckfdf erhf]ys!") == "Слава україні!", "LayoutMatrix: Special letters 'і', 'ї' & punctuation");

        // 2. LayoutMatrix Tests (Russian)
        Assert(LayoutMatrix.EnToRu("ghbdtn") == "привет", "LayoutMatrix: 'ghbdtn' -> 'привет' (RU)");
        Assert(LayoutMatrix.RuToEn("привет") == "ghbdtn", "LayoutMatrix: 'привет' -> 'ghbdtn' (RU)");
        Assert(LayoutMatrix.EnToRu("s]'`") == "ыъэё", "LayoutMatrix: RU special letters 'ы', 'ъ', 'э', 'ё'");

        // 3. NgramDetector Tests
        Assert(NgramDetector.IsLikelyCyrillicMistypedAsEnglish("ghbd"), "NgramDetector: 'ghbd' detected as mistyped Cyrillic");
        Assert(NgramDetector.IsLikelyEnglishMistypedAsCyrillic("рудд"), "NgramDetector: 'рудд' detected as mistyped ENG");
        Assert(!NgramDetector.IsLikelyCyrillicMistypedAsEnglish("hello"), "NgramDetector: 'hello' is not mistyped");
        Assert(!NgramDetector.IsLikelyEnglishMistypedAsCyrillic("привіт"), "NgramDetector: 'привіт' is not mistyped");
        Assert(NgramDetector.ContainsRussianSpecificLetters("это"), "NgramDetector: 'это' has RU specific letters");
        Assert(NgramDetector.ContainsUkrainianSpecificLetters("привіт"), "NgramDetector: 'привіт' has UK specific letters");

        // 4. WordAnalyzer Tests
        var analyzer = new WordAnalyzer();
        var res1 = analyzer.Analyze("ghbdsn", ConversionMode.Both, isOnTheFly: false);
        Assert(res1.ShouldTransform && res1.TransformedText == "привіт" && res1.TargetLanguage == "uk", "WordAnalyzer: delimiter 'ghbdsn' -> 'привіт' (uk)");

        var resRu = analyzer.Analyze("ghbdtn", ConversionMode.Both, isOnTheFly: false);
        Assert(resRu.ShouldTransform && resRu.TransformedText == "привет" && resRu.TargetLanguage == "ru", "WordAnalyzer: delimiter 'ghbdtn' -> 'привет' (ru)");

        var res2 = analyzer.Analyze("руддщ", ConversionMode.Both, isOnTheFly: false);
        Assert(res2.ShouldTransform && res2.TransformedText == "hello", "WordAnalyzer: delimiter 'руддщ' -> 'hello'");

        var res3 = analyzer.Analyze("ghbd", ConversionMode.Both, isOnTheFly: true);
        Assert(res3.ShouldTransform && res3.TransformedText == "прив", "WordAnalyzer: on-the-fly 'ghbd' -> 'прив'");

        var res4 = analyzer.Analyze("hello", ConversionMode.Both, isOnTheFly: false);
        Assert(!res4.ShouldTransform, "WordAnalyzer: valid word 'hello' must not be changed");

        // 2-Letter Words Tests
        var resWtFly = analyzer.Analyze("wt", ConversionMode.Both, isOnTheFly: true);
        Assert(resWtFly.ShouldTransform && resWtFly.TransformedText == "це", "WordAnalyzer: on-the-fly 'wt' -> 'це'");

        var resWtDelim = analyzer.Analyze("wt", ConversionMode.Both, isOnTheFly: false);
        Assert(resWtDelim.ShouldTransform && resWtDelim.TransformedText == "це", "WordAnalyzer: delimiter 'wt' -> 'це'");

        var resYtFly = analyzer.Analyze("yt", ConversionMode.Both, isOnTheFly: true);
        Assert(resYtFly.ShouldTransform && resYtFly.TransformedText == "не", "WordAnalyzer: on-the-fly 'yt' -> 'не'");

        var resLtFly = analyzer.Analyze("lt", ConversionMode.Both, isOnTheFly: true);
        Assert(resLtFly.ShouldTransform && resLtFly.TransformedText == "де", "WordAnalyzer: on-the-fly 'lt' -> 'де'");

        var resZrFly = analyzer.Analyze("zr", ConversionMode.Both, isOnTheFly: true);
        Assert(resZrFly.ShouldTransform && resZrFly.TransformedText == "як", "WordAnalyzer: on-the-fly 'zr' -> 'як'");

        var resZrDelim = analyzer.Analyze("zr", ConversionMode.Both, isOnTheFly: false);
        Assert(resZrDelim.ShouldTransform && resZrDelim.TransformedText == "як", "WordAnalyzer: delimiter 'zr' -> 'як'");

        var resSpravyDelim = analyzer.Analyze("cghfdb", ConversionMode.Both, isOnTheFly: false);
        Assert(resSpravyDelim.ShouldTransform && resSpravyDelim.TransformedText == "справи", "WordAnalyzer: delimiter 'cghfdb' -> 'справи'");

        var resSpravdiDelim = analyzer.Analyze("cghfdls", ConversionMode.Both, isOnTheFly: false);
        Assert(resSpravdiDelim.ShouldTransform && resSpravdiDelim.TransformedText == "справді", "WordAnalyzer: delimiter 'cghfdls' -> 'справді'");

        // HermitDave Frequency Dictionary integration test
        var resFreqUk = analyzer.Analyze("df;kbdt", ConversionMode.Both, isOnTheFly: false);
        Assert(resFreqUk.ShouldTransform && resFreqUk.TransformedText == "важливе", "WordAnalyzer: frequency dict 'df;kbdt' -> 'важливе'");

        // Valid 2-letter English words protection
        var resTo = analyzer.Analyze("to", ConversionMode.Both, isOnTheFly: false);
        Assert(!resTo.ShouldTransform, "WordAnalyzer: English 'to' must not be changed");

        var resIn = analyzer.Analyze("in", ConversionMode.Both, isOnTheFly: false);
        Assert(!resIn.ShouldTransform, "WordAnalyzer: English 'in' must not be changed");

        var resIs = analyzer.Analyze("is", ConversionMode.Both, isOnTheFly: false);
        Assert(!resIs.ShouldTransform, "WordAnalyzer: English 'is' must not be changed");

        // Words starting with ',' ('б') and '.' ('ю')
        var resBag = analyzer.Analyze(",fu", ConversionMode.Both, isOnTheFly: false);
        Assert(resBag.ShouldTransform && resBag.TransformedText == "баг", "WordAnalyzer: delimiter ',fu' -> 'баг'");

        var resBude = analyzer.Analyze(",elt", ConversionMode.Both, isOnTheFly: false);
        Assert(resBude.ShouldTransform && resBude.TransformedText == "буде", "WordAnalyzer: delimiter ',elt' -> 'буде'");

        var resUser = analyzer.Analyze(".pth", ConversionMode.Both, isOnTheFly: false);
        Assert(resUser.ShouldTransform && resUser.TransformedText == "юзер", "WordAnalyzer: delimiter '.pth' -> 'юзер'");

        // Reverse: Cyrillic mistyped as English
        var resBug = analyzer.Analyze("игп", ConversionMode.Both, isOnTheFly: false);
        Assert(resBug.ShouldTransform && resBug.TransformedText == "bug", "WordAnalyzer: delimiter 'игп' -> 'bug'");

        // Mode filtering
        var resUkrOnly = analyzer.Analyze("ghbdsn", ConversionMode.UkrToEng, isOnTheFly: false);
        Assert(!resUkrOnly.ShouldTransform, "WordAnalyzer: UkrToEng mode ignores Latin mistypes");

        // CamelCase / PascalCase Identifier Protection Tests
        var resOrders = analyzer.Analyze("OrdersWithTrips", ConversionMode.Both, isOnTheFly: false);
        Assert(!resOrders.ShouldTransform, "WordAnalyzer: CamelCase 'OrdersWithTrips' must not convert to Cyrillic");

        var resOrdersTr = analyzer.Analyze("OrdersWithTr", ConversionMode.Both, isOnTheFly: true);
        Assert(!resOrdersTr.ShouldTransform, "WordAnalyzer: On-the-fly 'OrdersWithTr' must not convert to Cyrillic");

        // Short Cyrillic Syllable Protection ("зу" must not become "pe")
        var resZuDelim = analyzer.Analyze("зу", ConversionMode.Both, isOnTheFly: false);
        Assert(!resZuDelim.ShouldTransform, "WordAnalyzer: 'зу' must not convert to 'pe' on delimiter");

        var resZuFly = analyzer.Analyze("зу", ConversionMode.Both, isOnTheFly: true);
        Assert(!resZuFly.ShouldTransform, "WordAnalyzer: 'зу' must not convert to 'pe' on-the-fly");

        // 5. ExclusionManager Tests
        string tempSettings = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempSettings, """
            {
              "ExcludedApplications": ["cmd.exe", "powershell.exe"],
              "ExcludedWebsites": ["privat24.ua", "bank.gov.ua"],
              "ExcludedWords": ["ННГ", "API", "customterm"]
            }
            """);

            var exclusion = new ExclusionManager(tempSettings);
            Assert(exclusion.IsAppExcluded("cmd.exe"), "ExclusionManager: 'cmd.exe' is excluded");
            Assert(exclusion.IsAppExcluded("cmd"), "ExclusionManager: 'cmd' (without .exe) is excluded");
            Assert(!exclusion.IsAppExcluded("notepad.exe"), "ExclusionManager: 'notepad.exe' is allowed");
            Assert(exclusion.IsDomainExcluded("https://privat24.ua/login"), "ExclusionManager: 'privat24.ua' is excluded");
            Assert(!exclusion.IsDomainExcluded("github.com"), "ExclusionManager: 'github.com' is allowed");

            // Excluded words tests
            Assert(exclusion.IsWordExcluded("ННГ"), "ExclusionManager: 'ННГ' is excluded");
            Assert(exclusion.IsWordExcluded("ннг"), "ExclusionManager: 'ннг' (case-insensitive) is excluded");
            Assert(exclusion.IsWordExcluded("yyu"), "ExclusionManager: 'yyu' (mirror layout of 'ННГ') is excluded");
            Assert(exclusion.IsWordExcluded("api"), "ExclusionManager: 'api' is excluded");
            Assert(!exclusion.IsWordExcluded("привіт"), "ExclusionManager: regular word is not excluded");

            // WordAnalyzer with IsWordExcluded option
            var optionsWithExclusion = new WordAnalyzerOptions(IsWordExcluded: exclusion.IsWordExcluded);
            var resNng = analyzer.Analyze("ННГ", ConversionMode.Both, isOnTheFly: false, optionsWithExclusion);
            Assert(!resNng.ShouldTransform, "WordAnalyzer: excluded word 'ННГ' must not transform");

            var resYyu = analyzer.Analyze("yyu", ConversionMode.Both, isOnTheFly: false, optionsWithExclusion);
            Assert(!resYyu.ShouldTransform, "WordAnalyzer: mirror 'yyu' of excluded word 'ННГ' must not transform");

            // Test saving and reloading excluded words via Settings.ExcludedWords (SettingsForm flow)
            exclusion.Settings.ExcludedWords = new List<string> { "ННГ", "API", "НОВЕСЛОВО" };
            exclusion.Settings.EnableRevertOnBackspace = false;
            exclusion.Settings.EnableSmartQuotes = true;
            exclusion.Settings.EnableSmartDashes = true;
            exclusion.Settings.EnableSmartTypography = true;
            exclusion.Settings.EnableStuckShiftFix = true;
            exclusion.Settings.EnableLanguageContext = true;
            exclusion.Settings.EnableSelectionConversion = true;
            exclusion.Settings.EnableExceptionPrompt = true;
            exclusion.Settings.EnableCapsLockIndicator = false;
            exclusion.Save();

            var reloadedExclusion = new ExclusionManager(tempSettings);
            Assert(reloadedExclusion.IsWordExcluded("НОВЕСЛОВО"), "ExclusionManager: newly saved word persists across instances");
            Assert(!reloadedExclusion.Settings.EnableRevertOnBackspace, "ExclusionManager: EnableRevertOnBackspace flag persists across instances");
            Assert(reloadedExclusion.Settings.EnableSmartQuotes, "ExclusionManager: EnableSmartQuotes persists");
            Assert(reloadedExclusion.Settings.EnableSmartDashes, "ExclusionManager: EnableSmartDashes persists");
            Assert(reloadedExclusion.Settings.EnableLanguageContext, "ExclusionManager: EnableLanguageContext persists");
            Assert(reloadedExclusion.Settings.EnableSelectionConversion, "ExclusionManager: EnableSelectionConversion persists");
            Assert(reloadedExclusion.Settings.EnableExceptionPrompt, "ExclusionManager: EnableExceptionPrompt persists");
            Assert(!reloadedExclusion.Settings.EnableCapsLockIndicator, "ExclusionManager: EnableCapsLockIndicator persists");
            Assert(reloadedExclusion.Settings.ExcludedWords.Contains("НОВЕСЛОВО"), "ExclusionManager: Settings.ExcludedWords contains newly added word");
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }

        // 6. Smart Typography Tests
        // Quotes (using explicit Unicode escapes for cross-platform encoding safety)
        Assert(SmartTypography.GetSmartQuote('\0', "uk") == '\u00AB', "SmartTypography: opening quote at start for UK is \u00AB");
        Assert(SmartTypography.GetSmartQuote(' ', "uk") == '\u00AB', "SmartTypography: opening quote after space for UK is \u00AB");
        Assert(SmartTypography.GetSmartQuote('(', "uk") == '\u00AB', "SmartTypography: opening quote after bracket for UK is \u00AB");
        Assert(SmartTypography.GetSmartQuote('а', "uk") == '\u00BB', "SmartTypography: closing quote after letter for UK is \u00BB");
        Assert(SmartTypography.GetSmartQuote('\u00AB', "uk") == '\u00BB', "SmartTypography: closing quote right after opening quote makes \u00AB\u00BB");
        Assert(SmartTypography.GetSmartQuote('\0', "en") == '\u201C', "SmartTypography: opening quote for EN is \u201C");
        Assert(SmartTypography.GetSmartQuote('a', "en") == '\u201D', "SmartTypography: closing quote for EN is \u201D");
        Assert(SmartTypography.GetSmartQuote('\u201C', "en") == '\u201D', "SmartTypography: closing quote after open makes \u201C\u201D");

        // Dashes
        var dash1 = SmartTypography.CheckSmartDash("привіт -");
        Assert(dash1.HasValue && dash1.Value.backspaces == 1 && dash1.Value.replacement == "\u2014 ", "SmartTypography: ' -' replaced with em-dash and space");
        var dash2 = SmartTypography.CheckSmartDash("привіт --");
        Assert(dash2.HasValue && dash2.Value.backspaces == 2 && dash2.Value.replacement == "\u2014 ", "SmartTypography: ' --' replaced with em-dash and space");
        Assert(SmartTypography.CheckSmartDash("--flag") == null, "SmartTypography: '--flag' not converted to dash");
        Assert(SmartTypography.CheckSmartDash("-item") == null, "SmartTypography: '-item' not converted to dash");

        // Ellipsis
        var ell = SmartTypography.CheckEllipsis("зачекай..");
        Assert(ell.HasValue && ell.Value.backspaces == 2 && ell.Value.replacement == "\u2026", "SmartTypography: '..' + '.' replaced with ellipsis \u2026");
        Assert(SmartTypography.CheckEllipsis("зачекай...") == null, "SmartTypography: '...' + '.' does not double replace");

        // Symbols
        var symC = SmartTypography.CheckSymbols("MovaFlow (c");
        Assert(symC.HasValue && symC.Value.backspaces == 2 && symC.Value.replacement == "\u00A9", "SmartTypography: (c) -> \u00A9");
        var symR = SmartTypography.CheckSymbols("Brand (r");
        Assert(symR.HasValue && symR.Value.backspaces == 2 && symR.Value.replacement == "\u00AE", "SmartTypography: (r) -> \u00AE");
        var symTM = SmartTypography.CheckSymbols("Nexus (tm");
        Assert(symTM.HasValue && symTM.Value.backspaces == 3 && symTM.Value.replacement == "\u2122", "SmartTypography: (tm) -> \u2122");

        // Stuck Shift
        Assert(analyzer.IsKnownWord("привіт"), "WordAnalyzer: knows word 'привіт'");
        Assert(SmartTypography.TryFixStuckShift("ПРивіт", w => analyzer.IsKnownWord(w)) == "Привіт", "SmartTypography: 'ПРивіт' -> 'Привіт'");
        Assert(SmartTypography.TryFixStuckShift("ТОму", w => analyzer.IsKnownWord(w)) == "Тому", "SmartTypography: 'ТОму' -> 'Тому'");
        Assert(SmartTypography.TryFixStuckShift("ПРИВІТ", w => analyzer.IsKnownWord(w)) == null, "SmartTypography: All-caps 'ПРИВІТ' stays unchanged");
        Assert(SmartTypography.TryFixStuckShift("Привіт", w => analyzer.IsKnownWord(w)) == null, "SmartTypography: normal 'Привіт' stays unchanged");

        // 7. ShortWords Tests
        Assert(ShortWords.IsCommon("не", "uk"), "ShortWords: 'не' is common UK short word");
        Assert(ShortWords.IsCommon("це", "uk"), "ShortWords: 'це' is common UK short word");
        Assert(ShortWords.IsCommon("де", "uk"), "ShortWords: 'де' is common UK short word");
        Assert(ShortWords.IsCommon("як", "uk"), "ShortWords: 'як' is common UK short word");
        Assert(ShortWords.IsCommon("ми", "uk"), "ShortWords: 'ми' is common UK short word");
        Assert(ShortWords.IsCommon("the", "en"), "ShortWords: 'the' is common EN short word");
        Assert(ShortWords.IsCommon("you", "en"), "ShortWords: 'you' is common EN short word");
        Assert(!ShortWords.IsCommon("pe", "en"), "ShortWords: 'pe' is NOT a common EN short word");
        Assert(!ShortWords.IsCommon("wt", "en"), "ShortWords: 'wt' is NOT a common EN short word");
        Assert(!ShortWords.IsCommon("yt", "en"), "ShortWords: 'yt' is NOT a common EN short word");

        // ShortWords Layout Switching Protection
        var options = new WordAnalyzerOptions();
        var resZu = analyzer.Analyze("зу", ConversionMode.Both, isOnTheFly: false, options);
        Assert(!resZu.ShouldTransform, "ShortWords: 'зу' NEVER converts to 'pe' on delimiter");
        var resZuOtf = analyzer.Analyze("зу", ConversionMode.Both, isOnTheFly: true, options);
        Assert(!resZuOtf.ShouldTransform, "ShortWords: 'зу' NEVER converts to 'pe' on-the-fly");

        var resNe = analyzer.Analyze("не", ConversionMode.Both, isOnTheFly: false, options);
        Assert(!resNe.ShouldTransform, "ShortWords: 'не' NEVER converts to 'yt'");
        var resDe = analyzer.Analyze("де", ConversionMode.Both, isOnTheFly: false, options);
        Assert(!resDe.ShouldTransform, "ShortWords: 'де' NEVER converts to 'lt'");
        var resYak = analyzer.Analyze("як", ConversionMode.Both, isOnTheFly: false, options);
        Assert(!resYak.ShouldTransform, "ShortWords: 'як' NEVER converts to 'zr'");

        var resYt = analyzer.Analyze("yt", ConversionMode.Both, isOnTheFly: false, options);
        Assert(resYt.ShouldTransform && resYt.TransformedText == "не", "ShortWords: 'yt' converts to 'не'");
        var resWt = analyzer.Analyze("wt", ConversionMode.Both, isOnTheFly: false, options);
        Assert(resWt.ShouldTransform && resWt.TransformedText == "це", "ShortWords: 'wt' converts to 'це'");
        var resGhb = analyzer.Analyze("ghb", ConversionMode.Both, isOnTheFly: false, options);
        Assert(resGhb.ShouldTransform && resGhb.TransformedText == "при", "ShortWords: 'ghb' converts to 'при'");
        var resThe = analyzer.Analyze("еру", ConversionMode.Both, isOnTheFly: false, options);
        Assert(resThe.ShouldTransform && resThe.TransformedText == "the", "ShortWords: Cyrillic 'еру' converts to 'the'");

        // 8. LanguageContext Tests
        var ctx = new LanguageContext(5);
        Assert(ctx.WordCount == 0, "LanguageContext: initially empty");
        ctx.AddWord("uk");
        ctx.AddWord("uk");
        Assert(ctx.Favors("uk", "en"), "LanguageContext: 2 UK words favor UK over EN");
        Assert(!ctx.Favors("en", "uk"), "LanguageContext: does not favor EN");
        ctx.Reset();
        Assert(ctx.WordCount == 0, "LanguageContext: empty after Reset");
        Assert(!ctx.Favors("uk", "en"), "LanguageContext: does not favor UK after Reset");

        // 9. ExceptionPromptManager Tests
        var epm = new ExceptionPromptManager();
        string? promptedWord = null;
        epm.SuggestException += w => promptedWord = w;

        epm.RecordRevert("тест");
        Assert(promptedWord == null, "ExceptionPrompt: not prompted on first revert");
        epm.RecordRevert("тест");
        Assert(promptedWord == "тест", "ExceptionPrompt: prompted on second consecutive revert of 'тест'");

        // 10. Localization Tests
        Assert(Strings.AppTitle(UiLanguage.Uk).Contains("Розумний"), "Localization: UK title");
        Assert(Strings.AppTitle(UiLanguage.En).Contains("Smart"), "Localization: EN title");
        Assert(!string.IsNullOrEmpty(Strings.CheckLanguageContext(UiLanguage.Uk)), "Localization: CheckLanguageContext UK exists");
        Assert(!string.IsNullOrEmpty(Strings.CheckSelectionConversion(UiLanguage.Uk)), "Localization: CheckSelectionConversion UK exists");

        Console.WriteLine($"\n=== Test Results: Passed: {passed}, Failed: {failed} ===");
        return failed == 0 ? 0 : 1;
    }
}
