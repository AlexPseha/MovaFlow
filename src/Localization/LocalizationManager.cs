namespace MovaFlow.Localization;

public class LocalizationManager
{
    private static LocalizationManager? _instance;
    public static LocalizationManager Instance => _instance ??= new LocalizationManager();

    public UiLanguage CurrentLanguage { get; private set; } = UiLanguage.Uk;

    public event Action? LanguageChanged;

    public void SetLanguage(UiLanguage language)
    {
        if (CurrentLanguage == language) return;
        CurrentLanguage = language;
        LanguageChanged?.Invoke();
    }

    public void SetLanguageFromCode(string? code)
    {
        if (string.Equals(code, "en", StringComparison.OrdinalIgnoreCase))
        {
            SetLanguage(UiLanguage.En);
        }
        else
        {
            SetLanguage(UiLanguage.Uk);
        }
    }

    public string CurrentCode => CurrentLanguage == UiLanguage.En ? "en" : "uk";
}
