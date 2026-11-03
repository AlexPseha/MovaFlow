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
            exclusion.Save();

            var reloadedExclusion = new ExclusionManager(tempSettings);
            Assert(reloadedExclusion.IsWordExcluded("НОВЕСЛОВО"), "ExclusionManager: newly saved word persists across instances");
            Assert(!reloadedExclusion.Settings.EnableRevertOnBackspace, "ExclusionManager: EnableRevertOnBackspace flag persists across instances");
            Assert(reloadedExclusion.Settings.EnableSmartQuotes, "ExclusionManager: EnableSmartQuotes persists");
            Assert(reloadedExclusion.Settings.EnableSmartDashes, "ExclusionManager: EnableSmartDashes persists");
            Assert(reloadedExclusion.Settings.ExcludedWords.Contains("НОВЕСЛОВО"), "ExclusionManager: Settings.ExcludedWords contains newly added word");
        }
        finally
        {
            if (File.Exists(tempSettings)) File.Delete(tempSettings);
        }

        // 6. Smart Typography Tests
        // Quotes
        Assert(SmartTypography.GetSmartQuote('\0', "uk") == '«', "SmartTypography: opening quote at start for UK");
        Assert(SmartTypography.GetSmartQuote(' ', "uk") == '«', "SmartTypography: opening quote after space for UK");
        Assert(SmartTypography.GetSmartQuote('(', "uk") == '«', "SmartTypography: opening quote after bracket for UK");
        Assert(SmartTypography.GetSmartQuote('а', "uk") == '»', "SmartTypography: closing quote after letter for UK");
        Assert(SmartTypography.GetSmartQuote('«', "uk") == '»', "SmartTypography: closing quote right after opening quote makes «»");
        Assert(SmartTypography.GetSmartQuote('\0', "en") == '“', "SmartTypography: opening quote for EN");
        Assert(SmartTypography.GetSmartQuote('a', "en") == '”', "SmartTypography: closing quote for EN");

        // Dashes
        var dash1 = SmartTypography.CheckSmartDash("привіт -");
        Assert(dash1.HasValue && dash1.Value.backspaces == 1 && dash1.Value.replacement == "— ", "SmartTypography: ' -' replaced with '— '");
        var dash2 = SmartTypography.CheckSmartDash("привіт --");
        Assert(dash2.HasValue && dash2.Value.backspaces == 2 && dash2.Value.replacement == "— ", "SmartTypography: ' --' replaced with '— '");
        Assert(SmartTypography.CheckSmartDash("--flag") == null, "SmartTypography: '--flag' not converted to dash");
        Assert(SmartTypography.CheckSmartDash("-item") == null, "SmartTypography: '-item' not converted to dash");

        // Ellipsis
        var ell = SmartTypography.CheckEllipsis("зачекай..");
        Assert(ell.HasValue && ell.Value.backspaces == 2 && ell.Value.replacement == "…", "SmartTypography: '..' + '.' replaced with '…'");
        Assert(SmartTypography.CheckEllipsis("зачекай...") == null, "SmartTypography: '...' + '.' does not double replace");

        // Symbols
        var symC = SmartTypography.CheckSymbols("MovaFlow (c");
        Assert(symC.HasValue && symC.Value.backspaces == 2 && symC.Value.replacement == "©", "SmartTypography: (c) -> ©");
        var symR = SmartTypography.CheckSymbols("Brand (r");
        Assert(symR.HasValue && symR.Value.backspaces == 2 && symR.Value.replacement == "®", "SmartTypography: (r) -> ®");
        var symTM = SmartTypography.CheckSymbols("Nexus (tm");
        Assert(symTM.HasValue && symTM.Value.backspaces == 3 && symTM.Value.replacement == "™", "SmartTypography: (tm) -> ™");

        // Stuck Shift
        Assert(analyzer.IsKnownWord("привіт"), "WordAnalyzer: knows word 'привіт'");
        Assert(SmartTypography.TryFixStuckShift("ПРивіт", w => analyzer.IsKnownWord(w)) == "Привіт", "SmartTypography: 'ПРивіт' -> 'Привіт'");
        Assert(SmartTypography.TryFixStuckShift("ТОму", w => analyzer.IsKnownWord(w)) == "Тому", "SmartTypography: 'ТОму' -> 'Тому'");
        Assert(SmartTypography.TryFixStuckShift("ПРИВІТ", w => analyzer.IsKnownWord(w)) == null, "SmartTypography: All-caps 'ПРИВІТ' stays unchanged");
        Assert(SmartTypography.TryFixStuckShift("Привіт", w => analyzer.IsKnownWord(w)) == null, "SmartTypography: normal 'Привіт' stays unchanged");

        // 7. Localization Tests
        Assert(Strings.AppTitle(UiLanguage.Uk).Contains("Розумний"), "Localization: UK title");
        Assert(Strings.AppTitle(UiLanguage.En).Contains("Smart"), "Localization: EN title");

        Console.WriteLine($"\n=== Test Results: Passed: {passed}, Failed: {failed} ===");
        return failed == 0 ? 0 : 1;
    }
}
