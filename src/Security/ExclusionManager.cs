using System.IO;
using System.Text.Json;
using MovaFlow.Core;

namespace MovaFlow.Security;

public class AppSettingsModel
{
    public string UiLanguage { get; set; } = "uk";
    public string ConversionMode { get; set; } = "Both";
    public bool EnableUkrainianLayout { get; set; } = true;
    public bool EnableEnglishLayout { get; set; } = true;
    public bool EnableRussianLayout { get; set; } = true;
    public bool SwitchSystemLayout { get; set; } = true;
    public bool EnableOnTheFly { get; set; } = true;
    public bool EnableRevertOnBackspace { get; set; } = true;
    public bool EnableSmartQuotes { get; set; } = true;
    public bool EnableSmartDashes { get; set; } = true;
    public bool EnableSmartTypography { get; set; } = true;
    public bool EnableStuckShiftFix { get; set; } = true;
    public bool EnablePasswordProtection { get; set; } = true;
    public bool AutostartWithWindows { get; set; } = false;
    public string DonationUrl { get; set; } = "https://send.monobank.ua/";
    public List<string> ExcludedApplications { get; set; } = new();
    public List<string> ExcludedWebsites { get; set; } = new();
    public List<string> ExcludedWords { get; set; } = new();
}

public class ExclusionManager
{
    private readonly HashSet<string> _excludedApps = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedDomains = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedWords = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _settingsPath;

    public AppSettingsModel Settings { get; private set; } = new();

    public ExclusionManager(string settingsPath)
    {
        _settingsPath = settingsPath;
        Load();
    }

    public void Load()
    {
        try
        {
            if (File.Exists(_settingsPath))
            {
                string json = File.ReadAllText(_settingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettingsModel>(json);
                if (loaded != null)
                {
                    Settings = loaded;
                }
            }
        }
        catch
        {
            // Fallback to default in case of IO error
            Settings = new AppSettingsModel();
        }

        _excludedApps.Clear();
        foreach (var app in Settings.ExcludedApplications) _excludedApps.Add(app);

        _excludedDomains.Clear();
        foreach (var domain in Settings.ExcludedWebsites) _excludedDomains.Add(domain);

        _excludedWords.Clear();
        foreach (var word in Settings.ExcludedWords) _excludedWords.Add(word);
    }

    public void Save()
    {
        try
        {
            // Synchronize internal HashSets with Settings lists (e.g. if modified via SettingsForm)
            _excludedApps.Clear();
            foreach (var app in Settings.ExcludedApplications) _excludedApps.Add(app);

            _excludedDomains.Clear();
            foreach (var domain in Settings.ExcludedWebsites) _excludedDomains.Add(domain);

            _excludedWords.Clear();
            foreach (var word in Settings.ExcludedWords) _excludedWords.Add(word);

            // Re-assign distinct normalized lists to Settings
            Settings.ExcludedApplications = _excludedApps.ToList();
            Settings.ExcludedWebsites = _excludedDomains.ToList();
            Settings.ExcludedWords = _excludedWords.ToList();

            var options = new JsonSerializerOptions { WriteIndented = true };
            string json = JsonSerializer.Serialize(Settings, options);
            File.WriteAllText(_settingsPath, json);
        }
        catch
        {
            // Ignore file write errors gracefully
        }
    }

    public bool IsAppExcluded(string processName)
    {
        if (string.IsNullOrWhiteSpace(processName)) return false;
        string clean = processName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)
            ? processName
            : processName + ".exe";

        return _excludedApps.Contains(clean) || _excludedApps.Contains(processName);
    }

    public bool IsDomainExcluded(string domain)
    {
        if (string.IsNullOrWhiteSpace(domain)) return false;
        string clean = domain.Trim().ToLowerInvariant();

        foreach (var excluded in _excludedDomains)
        {
            if (clean.Contains(excluded.ToLowerInvariant(), StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public bool IsWordExcluded(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        string clean = word.Trim();

        if (_excludedWords.Contains(clean)) return true;

        // Check mirror layouts (e.g. if user excluded "ННГ", also match "yyu", or vice versa)
        string ukMirror = LayoutMatrix.EnToUk(clean);
        if (_excludedWords.Contains(ukMirror)) return true;

        string enMirror = LayoutMatrix.UkToEn(clean);
        if (_excludedWords.Contains(enMirror)) return true;

        string ruMirror = LayoutMatrix.EnToRu(clean);
        if (_excludedWords.Contains(ruMirror)) return true;

        return false;
    }

    public IReadOnlyCollection<string> GetExcludedWords() => _excludedWords;

    public bool AddExcludedWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        bool added = _excludedWords.Add(word.Trim());
        if (added)
        {
            Settings.ExcludedWords = _excludedWords.ToList();
        }
        return added;
    }

    public bool RemoveExcludedWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return false;
        bool removed = _excludedWords.Remove(word.Trim());
        if (removed)
        {
            Settings.ExcludedWords = _excludedWords.ToList();
        }
        return removed;
    }
}
