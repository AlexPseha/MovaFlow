namespace MovaFlow.Core;

public class TrieNode
{
    public Dictionary<char, TrieNode> Children { get; } = new();
    public bool IsEndOfWord { get; set; }
}

public class TrieDictionary
{
    private readonly TrieNode _root = new();

    public void Insert(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return;
        var current = _root;
        string normalized = word.Trim().ToLowerInvariant();

        foreach (char c in normalized)
        {
            if (!current.Children.TryGetValue(c, out var next))
            {
                next = new TrieNode();
                current.Children[c] = next;
            }
            current = next;
        }
        current.IsEndOfWord = true;
    }

    public bool Contains(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        var current = _root;
        string normalized = word.Trim().ToLowerInvariant();

        foreach (char c in normalized)
        {
            if (!current.Children.TryGetValue(c, out var next))
            {
                return false;
            }
            current = next;
        }
        return current.IsEndOfWord;
    }

    public bool HasPrefix(string prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix)) return false;
        var current = _root;
        string normalized = prefix.Trim().ToLowerInvariant();

        foreach (char c in normalized)
        {
            if (!current.Children.TryGetValue(c, out var next))
            {
                return false;
            }
            current = next;
        }
        return true;
    }
}
