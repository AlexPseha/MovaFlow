namespace MovaFlow.Core;

/// <summary>
/// Handles smart typography replacements on-the-fly:
/// - Smart quotes: «» for Ukrainian/Russian, “” for English
/// - Smart dashes: " - " or " -- " -> " — "
/// - Ellipsis: "..." -> "…"
/// - Symbols: (c) -> ©, (r) -> ®, (tm) -> ™
/// - Stuck Shift: "ПРивіт" -> "Привіт"
/// </summary>
public static class SmartTypography
{
    /// <summary>
    /// Returns the appropriate smart quote character based on the preceding character and active language.
    /// </summary>
    public static char GetSmartQuote(char previousChar, string? language)
    {
        bool isCyrillic = string.IsNullOrEmpty(language)
                       || language.StartsWith("uk", StringComparison.OrdinalIgnoreCase)
                       || language.StartsWith("ru", StringComparison.OrdinalIgnoreCase);

        // Opening if at start of buffer, after whitespace, or after opening punctuation
        bool isOpen = previousChar == '\0' || char.IsWhiteSpace(previousChar) || "([{<„«“".Contains(previousChar);

        // If user immediately types another quote right after an opening quote, produce the closing quote to form pair («» / “”)
        if (previousChar is '«' or '“')
        {
            isOpen = false;
        }

        if (isCyrillic)
        {
            return isOpen ? '«' : '»';
        }
        else
        {
            return isOpen ? '“' : '”';
        }
    }

    /// <summary>
    /// Checks for smart dash replacement when Space is typed.
    /// If history ends with " --" -> replaces 2 hyphens with "— " (backspace 2, insert "— ")
    /// If history ends with " -"  -> replaces 1 hyphen with "— " (backspace 1, insert "— ")
    /// </summary>
    public static (int backspaces, string replacement)? CheckSmartDash(string history)
    {
        if (string.IsNullOrEmpty(history)) return null;

        if (history.EndsWith(" --"))
        {
            return (2, "— ");
        }

        if (history.EndsWith(" -"))
        {
            return (1, "— ");
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

        if (history.EndsWith("..") && !history.EndsWith("..."))
        {
            return (2, "…");
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
            return (2, "©");
        }

        if (history.EndsWith("(r", StringComparison.OrdinalIgnoreCase))
        {
            return (2, "®");
        }

        if (history.EndsWith("(tm", StringComparison.OrdinalIgnoreCase))
        {
            return (3, "™");
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
