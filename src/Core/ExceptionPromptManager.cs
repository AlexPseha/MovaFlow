using System;

namespace MovaFlow.Core;

/// <summary>
/// Monitors user reverts via Backspace.
/// If the user reverts an auto-correction for the same word twice consecutively,
/// triggers a non-intrusive suggestion to add that word to the exclusions list (ExcludedWords).
/// Inspired by Klavko's user feedback and exception prompting architecture.
/// </summary>
public class ExceptionPromptManager
{
    private string? _lastRevertedWord;
    private int _consecutiveReverts;
    private DateTime _lastRevertTime = DateTime.MinValue;

    /// <summary>
    /// Fired when a word has been reverted twice consecutively within a reasonable time window (e.g. 30 seconds).
    /// </summary>
    public event Action<string>? SuggestException;

    public void RecordRevert(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;

        var now = DateTime.UtcNow;
        if (string.Equals(_lastRevertedWord, word, StringComparison.OrdinalIgnoreCase) &&
            (now - _lastRevertTime).TotalSeconds < 30)
        {
            _consecutiveReverts++;
            if (_consecutiveReverts >= 2)
            {
                SuggestException?.Invoke(word);
                _consecutiveReverts = 0;
                _lastRevertedWord = null;
                return;
            }
        }
        else
        {
            _lastRevertedWord = word;
            _consecutiveReverts = 1;
        }

        _lastRevertTime = now;
    }

    public void RecordAccepted(string word)
    {
        if (string.Equals(_lastRevertedWord, word, StringComparison.OrdinalIgnoreCase))
        {
            _lastRevertedWord = null;
            _consecutiveReverts = 0;
        }
    }

    public void Reset()
    {
        _lastRevertedWord = null;
        _consecutiveReverts = 0;
    }
}
