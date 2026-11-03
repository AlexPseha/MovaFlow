using System;
using System.Collections.Generic;

namespace MovaFlow.Core;

/// <summary>
/// Maintains line-level language context for the last 3-6 words typed in the current line.
/// Provides language inertia so fast typing of words in Ukrainian does not get interrupted
/// by short ambiguous words erroneously switching to English.
/// Inspired by Klavko's LanguageContext architecture.
/// </summary>
public class LanguageContext
{
    private readonly int _limit;
    private readonly Queue<string> _recentWordLanguages = new();
    private readonly Dictionary<string, int> _counts = new(StringComparer.OrdinalIgnoreCase);

    public LanguageContext(int limit = 5)
    {
        _limit = limit;
    }

    /// <summary>
    /// Total recognized words in the current line context.
    /// </summary>
    public int WordCount => _recentWordLanguages.Count;

    /// <summary>
    /// Records a word and its recognized language ("uk", "en", "ru").
    /// </summary>
    public void AddWord(string? language)
    {
        if (string.IsNullOrEmpty(language)) return;

        string lang = language.ToLowerInvariant();
        if (lang.StartsWith("uk")) lang = "uk";
        else if (lang.StartsWith("en")) lang = "en";
        else if (lang.StartsWith("ru")) lang = "ru";
        else return;

        if (_recentWordLanguages.Count >= _limit)
        {
            string oldest = _recentWordLanguages.Dequeue();
            if (_counts.TryGetValue(oldest, out int count))
            {
                if (count <= 1) _counts.Remove(oldest);
                else _counts[oldest] = count - 1;
            }
        }

        _recentWordLanguages.Enqueue(lang);
        _counts[lang] = _counts.GetValueOrDefault(lang, 0) + 1;
    }

    /// <summary>
    /// Returns the number of words in the recent line context belonging to the specified language.
    /// </summary>
    public int Count(string language)
    {
        if (string.IsNullOrEmpty(language)) return 0;
        string lang = language.ToLowerInvariant();
        if (lang.StartsWith("uk")) lang = "uk";
        else if (lang.StartsWith("en")) lang = "en";
        else if (lang.StartsWith("ru")) lang = "ru";

        return _counts.GetValueOrDefault(lang, 0);
    }

    /// <summary>
    /// Checks whether the recent line context has strictly more words in 'language' than in 'other'.
    /// </summary>
    public bool Favors(string language, string overOther)
    {
        return Count(language) > Count(overOther);
    }

    /// <summary>
    /// Clears the context (e.g. on Enter, Esc, or window focus change).
    /// </summary>
    public void Reset()
    {
        _recentWordLanguages.Clear();
        _counts.Clear();
    }
}
