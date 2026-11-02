namespace MovaFlow.Localization;

public enum UiLanguage
{
    Uk,
    En
}

public static class Strings
{
    public static string AppTitle(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "MovaFlow — Smart Keyboard Switcher",
        _ => "MovaFlow — Розумний перемикач розкладки"
    };

    public static string StatusActive(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Status: Active",
        _ => "Статус: Працює"
    };

    public static string StatusPaused(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Status: Paused",
        _ => "Статус: Призупинено"
    };

    public static string TogglePause(UiLanguage lang, bool isPaused) => lang switch
    {
        UiLanguage.En => isPaused ? "Resume" : "Pause",
        _ => isPaused ? "Відновити роботу" : "Призупинити"
    };

    public static string MenuLanguage(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "UI Language",
        _ => "Мова інтерфейсу"
    };

    public static string LangUkrainian(UiLanguage lang) => "Українська";
    public static string LangEnglish(UiLanguage lang) => "English";

    public static string MenuConversionMode(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Conversion Mode",
        _ => "Режим конвертації"
    };

    public static string ModeBoth(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Both directions",
        _ => "Обидва напрямки"
    };

    public static string ModeUkrToEng(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Only Cyrillic → ENG",
        _ => "Тільки Кирилиця → ENG"
    };

    public static string ModeEngToUkr(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Only ENG → Cyrillic",
        _ => "Тільки ENG → Кирилиця"
    };

    public static string MenuSwitchSystemLayout(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Switch Windows Layout",
        _ => "Змінювати розкладку Windows"
    };

    public static string MenuOnTheFly(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Fix words on-the-fly",
        _ => "Виправляти на льоту"
    };

    public static string MenuOpenSettings(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Settings...",
        _ => "Налаштування..."
    };

    public static string SettingsTitle(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "MovaFlow — Settings",
        _ => "MovaFlow — Налаштування"
    };

    public static string TabGeneral(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "General",
        _ => "Загальні"
    };

    public static string TabExclusions(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Exclusions",
        _ => "Виключення"
    };

    public static string TabSecurityDonate(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Security & Donate",
        _ => "Безпека та Донат"
    };

    public static string GroupUiLanguage(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "UI Language",
        _ => "Мова інтерфейсу"
    };

    public static string GroupEnabledLayouts(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Active Keyboard Layouts",
        _ => "Активні розкладки клавіатури"
    };

    public static string LayoutUkrainian(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Ukrainian (UKR)",
        _ => "Українська (УКР)"
    };

    public static string LayoutEnglish(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "English (ENG)",
        _ => "English (ENG)"
    };

    public static string LayoutRussian(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Russian (RUS)",
        _ => "Російська (РОС)"
    };

    public static string GroupConversionMode(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Auto-Correction Mode",
        _ => "Режим автовиправлення"
    };

    public static string CheckSwitchLayout(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Automatically switch Windows keyboard layout (ENG ↔ UKR ↔ RUS)",
        _ => "Автоматично перемикати розкладку Windows (ENG ↔ УКР ↔ РОС)"
    };

    public static string CheckOnTheFly(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Fix words on-the-fly (after typing 3–4 letters)",
        _ => "Виправляти слова на льоту (після перших 3–4 літер)"
    };

    public static string CheckRevertOnBackspace(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Revert layout on Backspace after auto-correction",
        _ => "Повертати розкладку при Backspace після автозаміни"
    };

    public static string CheckAutostart(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Launch MovaFlow on Windows startup",
        _ => "Запускати MovaFlow разом із запуском Windows"
    };

    public static string CheckPasswordProtection(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Pause monitoring in password fields (Zero Password Exposure)",
        _ => "Призупиняти роботу в полях паролів (Zero Password Exposure)"
    };

    public static string GroupApps(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Excluded Applications:",
        _ => "Виключені програми:"
    };

    public static string GroupSites(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Excluded Websites:",
        _ => "Виключені сайти:"
    };

    public static string GroupWords(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Excluded Words:",
        _ => "Виключені слова:"
    };

    public static string WordPlaceholder(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "e.g. NNG, API, SQL",
        _ => "напр. ННГ, API, SQL"
    };

    public static string BtnAddApp(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Add Program...",
        _ => "Додати..."
    };

    public static string BtnAddSite(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Add Site",
        _ => "Додати"
    };

    public static string BtnAddWord(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Add Word",
        _ => "Додати"
    };

    public static string BtnRemove(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Remove",
        _ => "Видалити"
    };

    public static string LabelDonateUrl(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Developer Donation URL (Monobank jar, Buy Me a Coffee, Patreon):",
        _ => "Посилання для підтримки розробника (Банка Monobank, Buy Me a Coffee, Patreon):"
    };

    public static string BtnTestDonate(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Open Link",
        _ => "Перевірити посилання"
    };

    public static string BtnSave(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Save",
        _ => "Зберегти"
    };

    public static string BtnCancel(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Cancel",
        _ => "Скасувати"
    };

    public static string MenuStats(UiLanguage lang, int count) => lang switch
    {
        UiLanguage.En => $"Corrections: {count}",
        _ => $"Виправлено слів: {count}"
    };

    public static string MenuDonate(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Support Developer (Donate) ☕",
        _ => "Підтримати розробника (Донат) ☕"
    };

    public static string MenuExit(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "Exit",
        _ => "Вихід"
    };

    public static string ProtectedField(UiLanguage lang) => lang switch
    {
        UiLanguage.En => "[MovaFlow] Password field detected: monitoring paused.",
        _ => "[MovaFlow] Виявлено поле пароля: моніторинг вимкнено."
    };

    public static string ExcludedSite(UiLanguage lang, string domain) => lang switch
    {
        UiLanguage.En => $"[MovaFlow] Excluded site ({domain}): monitoring paused.",
        _ => $"[MovaFlow] Виключений сайт ({domain}): моніторинг вимкнено."
    };
}
