using System.IO;
using System.Text.Json;

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
    public bool EnablePasswordProtection { get; set; } = true;
    public bool AutostartWithWindows { get; set; } = false;
    public string DonationUrl { get; set; } = "https://send.monobank.ua/";
    public List<string> ExcludedApplications { get; set; } = new();
    public List<string> ExcludedWebsites { get; set; } = new();
}

public class ExclusionManager
{
    private readonly HashSet<string> _excludedApps = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _excludedDomains = new(StringComparer.OrdinalIgnoreCase);
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
    }

    public void Save()
    {
        try
        {
            Settings.ExcludedApplications = _excludedApps.ToList();
            Settings.ExcludedWebsites = _excludedDomains.ToList();

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
}
