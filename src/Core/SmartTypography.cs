using System;

namespace MovaFlow.Core;

/// <summary>
/// Handles smart typography replacements on-the-fly:
/// - Smart quotes: «» for Ukrainian/Russian, “” for English (supports both " with Shift and ' without Shift)
/// - Smart dashes: " - ", " -- ", or line-initial "- " / "-- " -> " — "
/// - Smart quote + dash closure: «привіт -» or «привіт — » -> «привіт —»
/// - Ellipsis: "..." -> "…"
/// - Symbols: (c) -> ©, (r) -> ®, (tm) -> ™
/// - Stuck Shift: "ПРивіт" -> "Привіт"
/// Uses explicit Unicode escape sequences to ensure immunity to OS codepages.
/// </summary>
public static class SmartTypography
{
    // Quotes
    public const char UkOpeningQuote = '\u00AB'; // «
    public const char UkClosingQuote = '\u00BB'; // »
    public const char EnOpeningQuote = '\u201C'; // “
    public const char EnClosingQuote = '\u201D'; // ”

    // Dash & Ellipsis & Symbols
    public const string EmDashWithSpace = "\u2014 "; // — + space
    public const string Ellipsis = "\u2026"; // …
    public const string SymbolCopyright = "\u00A9"; // ©
    public const string SymbolRegistered = "\u00AE"; // ®
    public const string SymbolTrademark = "\u2122"; // ™

    /// <summary>
    /// Returns the appropriate smart quote character based on the preceding character and active language.
    /// Backward-compatible overload for double quotes.
    /// </summary>
    public static char GetSmartQuote(char previousChar, string? language)
    {
        return TryGetSmartQuote(previousChar, language, isSingleQuote: false, null) ?? (
            (string.IsNullOrEmpty(language) || language.StartsWith("uk", StringComparison.OrdinalIgnoreCase) || language.StartsWith("ru", StringComparison.OrdinalIgnoreCase))
                ? UkOpeningQuote : EnOpeningQuote);
    }

    /// <summary>
    /// Returns the appropriate smart quote character based on the preceding character, active language, and quote type.
    /// If isSingleQuote is true and the quote is inside a word without an open quote (e.g. Ukrainian apostrophe in м'ясо or English in don't),
    /// returns null so that the apostrophe is preserved intact.
    /// </summary>
    public static char? TryGetSmartQuote(char previousChar, string? language, bool isSingleQuote = false, string? recentHistory = null)
    {
        bool isCyrillic = string.IsNullOrEmpty(language)
                       || language.StartsWith("uk", StringComparison.OrdinalIgnoreCase)
                       || language.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        char openQuote = isCyrillic ? UkOpeningQuote : EnOpeningQuote;
        char closeQuote = isCyrillic ? UkClosingQuote : EnClosingQuote;

        // Check whether an opening quote is currently unclosed in recentHistory
        bool hasOpenQuote = !string.IsNullOrEmpty(recentHistory)
                         && recentHistory.LastIndexOf(openQuote) > recentHistory.LastIndexOf(closeQuote);

        // Special handling for single quote / apostrophe (key ' without Shift)
        if (isSingleQuote)
        {
            if (hasOpenQuote)
            {
                // We have an open quote, so this ' closes the open quote
                return closeQuote;
            }

            // If preceded by a letter or digit and NO open quote exists, this is an apostrophe (м'ясо, п'ять, don't)!
            if (char.IsLetterOrDigit(previousChar))
            {
                return null; // Preserve apostrophe!
            }
        }
        else if (hasOpenQuote)
        {
            // For double quotes: if an opening quote is already active in recentHistory,
            // always close it (even after a dash or space, e.g. «привіт —»)
            return closeQuote;
        }

        // Opening if at start of buffer, after whitespace, or after opening punctuation
        bool isOpen = previousChar == '\0'
                   || char.IsWhiteSpace(previousChar)
                   || "([{<\u201E\u00AB\u201C\"'`".IndexOf(previousChar) >= 0;

        // If user immediately types another quote right after an opening quote, produce the closing quote to form pair («» / “”)
        if (previousChar == UkOpeningQuote || previousChar == EnOpeningQuote)
        {
            isOpen = false;
        }

        return isOpen ? openQuote : closeQuote;
    }

    /// <summary>
    /// Computes the exact replacement action (backspaces and text to insert) when a quote key is pressed.
    /// Handles smart dash + closing quote combinations such as:
    /// - "«привіт — " + quote -> backspace 1 (space), insert "»" -> "«привіт —»"
    /// - "«привіт -"  + quote -> backspace 1 ('-'),   insert "—»" -> "«привіт —»"
    /// - "«привіт --" + quote -> backspace 2 ("--"),  insert "—»" -> "«привіт —»"
    /// </summary>
    public static (int backspaces, string replacement, char quoteChar)? GetSmartQuoteAction(
        char previousChar,
        string? language,
        bool isSingleQuote = false,
        string? recentHistory = null,
        bool enableSmartDashes = true)
    {
        char? quote = TryGetSmartQuote(previousChar, language, isSingleQuote, recentHistory);
        if (!quote.HasValue)
        {
            return null;
        }

        char q = quote.Value;
        bool isClosing = (q == UkClosingQuote || q == EnClosingQuote);

        if (isClosing && !string.IsNullOrEmpty(recentHistory))
        {
            if (recentHistory.EndsWith(EmDashWithSpace, StringComparison.Ordinal))
            {
                return (1, q.ToString(), q);
            }

            if (enableSmartDashes)
            {
                if (recentHistory.EndsWith(" --", StringComparison.Ordinal))
                {
                    return (2, "\u2014" + q, q);
                }

                if (recentHistory.EndsWith(" -", StringComparison.Ordinal))
                {
                    return (1, "\u2014" + q, q);
                }
            }
        }

        return (0, q.ToString(), q);
    }

    /// <summary>
    /// Checks for smart dash replacement when Space is typed.
    /// If history ends with " --" or is "--" at start of line -> replaces 2 hyphens with "— " (backspace 2, insert "— ")
    /// If history ends with " -"  or is "-"  at start of line -> replaces 1 hyphen with "— " (backspace 1, insert "— ")
    /// Intercepting "- " at start of line also prevents Windows 11 Notepad from converting "- " into a bullet point (•).
    /// </summary>
    public static (int backspaces, string replacement)? CheckSmartDash(string history)
    {
        if (string.IsNullOrEmpty(history)) return null;

        if (history.EndsWith(" --", StringComparison.Ordinal) ||
            history.Equals("--", StringComparison.Ordinal) ||
            history.EndsWith("\n--", StringComparison.Ordinal) ||
            history.EndsWith("\r--", StringComparison.Ordinal))
        {
            return (2, EmDashWithSpace);
        }

        if (history.EndsWith(" -", StringComparison.Ordinal) ||
            history.Equals("-", StringComparison.Ordinal) ||
            history.EndsWith("\n-", StringComparison.Ordinal) ||
            history.EndsWith("\r-", StringComparison.Ordinal))
        {
            return (1, EmDashWithSpace);
        }

        return null;
    }

    /// <summary>
    /// Checks for ellipsis "..." when dot '.' is typed.
    /// If history ends with ".." and not "...", replaces 2 previous dots with "…" (backspace 2, insert "…").
    /// </summary>
    public static (int backspaces, string replacement)? CheckEllipsis(string history)
    {
        if (string.IsNullOrEmpty(history)) return null;

        if (history.EndsWith("..", StringComparison.Ordinal) && !history.EndsWith("...", StringComparison.Ordinal))
        {
            return (2, Ellipsis);
        }

        return null;
    }

    /// <summary>
    /// Checks for symbol abbreviations like (c) -> ©, (r) -> ®, (tm) -> ™ when ')' is typed.
    /// </summary>
    public static (int backspaces, string replacement)? CheckSymbols(string history)
    {
        if (string.IsNullOrEmpty(history)) return null;

        if (history.EndsWith("(c", StringComparison.OrdinalIgnoreCase))
        {
            return (2, SymbolCopyright);
        }

        if (history.EndsWith("(r", StringComparison.OrdinalIgnoreCase))
        {
            return (2, SymbolRegistered);
        }

        if (history.EndsWith("(tm", StringComparison.OrdinalIgnoreCase))
        {
            return (3, SymbolTrademark);
        }

        return null;
    }

    /// <summary>
    /// Detects and fixes stuck Shift mistakes (e.g. "ПРивіт" -> "Привіт", "ТОму" -> "Тому").
    /// Requires word length >= 3, first two letters uppercase, remaining letters lowercase,
    /// and the corrected word exists in the dictionary.
    /// </summary>
    public static string? TryFixStuckShift(string word, Func<string, bool> isKnownWord)
    {
        if (string.IsNullOrEmpty(word) || word.Length < 3) return null;

        if (char.IsUpper(word[0]) && char.IsUpper(word[1]))
        {
            for (int i = 2; i < word.Length; i++)
            {
                if (!char.IsLetter(word[i]) || !char.IsLower(word[i]))
                    return null;
            }

            string candidate = char.ToUpperInvariant(word[0]) + word.Substring(1).ToLowerInvariant();
            if (isKnownWord(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
