using System.Diagnostics;
using System.Drawing;
using System.Windows.Forms;
using MovaFlow.Localization;
using MovaFlow.Security;

namespace MovaFlow.UI;

public class SettingsForm : Form
{
    private readonly ExclusionManager _exclusionManager;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;

    // Controls
    private TabControl _tabControl = null!;
    private TabPage _tabGeneral = null!;
    private TabPage _tabExclusions = null!;
    private TabPage _tabSecurity = null!;

    // General Tab
    private GroupBox _grpLang = null!;
    private RadioButton _rbLangUk = null!;
    private RadioButton _rbLangEn = null!;

    private GroupBox _grpLayouts = null!;
    private CheckBox _chkUkLayout = null!;
    private CheckBox _chkEnLayout = null!;
    private CheckBox _chkRuLayout = null!;

    private GroupBox _grpMode = null!;
    private RadioButton _rbModeBoth = null!;
    private RadioButton _rbModeUkToEn = null!;
    private RadioButton _rbModeEnToUk = null!;

    private CheckBox _chkSwitchLayout = null!;
    private CheckBox _chkOnTheFly = null!;
    private CheckBox _chkRevertOnBackspace = null!;
    private CheckBox _chkSmartQuotes = null!;
    private CheckBox _chkSmartDashes = null!;
    private CheckBox _chkSmartTypography = null!;
    private CheckBox _chkStuckShiftFix = null!;
    private CheckBox _chkLanguageContext = null!;
    private CheckBox _chkSelectionConversion = null!;
    private CheckBox _chkExceptionPrompt = null!;
    private CheckBox _chkCapsLockIndicator = null!;
    private CheckBox _chkAutostart = null!;

    // Exclusions Tab
    private ListBox _listApps = null!;
    private Button _btnAddApp = null!;
    private Button _btnRemoveApp = null!;
    private ListBox _listSites = null!;
    private TextBox _txtNewSite = null!;
    private Button _btnAddSite = null!;
    private Button _btnRemoveSite = null!;
    private ListBox _listWords = null!;
    private TextBox _txtNewWord = null!;
    private Button _btnAddWord = null!;
    private Button _btnRemoveWord = null!;
    private Label _lblApps = null!;
    private Label _lblSites = null!;
    private Label _lblWords = null!;

    // Security & Donate Tab
    public const string DefaultDonationUrl = "https://www.privat24.ua/send/k6jqc";
    private CheckBox _chkPasswordProtection = null!;
    private LinkLabel _lnkDonate = null!;
    private Label _lblDonateDesc = null!;

    // Bottom
    private Button _btnSave = null!;
    private Button _btnCancel = null!;

    public SettingsForm(ExclusionManager exclusionManager)
    {
        _exclusionManager = exclusionManager;
        InitializeComponents();
        LoadValues();
        ApplyLocalization();
    }

    private void InitializeComponents()
    {
        Text = Strings.SettingsTitle(_loc.CurrentLanguage);
        ClientSize = new Size(760, 660);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        try
        {
            string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "resources", "movaflow.ico");
            if (File.Exists(iconPath))
            {
                Icon = new Icon(iconPath);
            }
        }
        catch
        {
            // Ignore
        }

        _tabControl = new TabControl
        {
            Dock = DockStyle.Fill,
            Padding = new Point(12, 6)
        };

        // 1. Tab General
        _tabGeneral = new TabPage();
        BuildGeneralTab(_tabGeneral);

        // 2. Tab Exclusions
        _tabExclusions = new TabPage();
        BuildExclusionsTab(_tabExclusions);

        // 3. Tab Security & Donate
        _tabSecurity = new TabPage();
        BuildSecurityTab(_tabSecurity);

        _tabControl.TabPages.AddRange(new[] { _tabGeneral, _tabExclusions, _tabSecurity });

        // Bottom panel
        var bottomPanel = new Panel
        {
            Dock = DockStyle.Bottom,
            Height = 60
        };

        _btnSave = new Button
        {
            Text = Strings.BtnSave(_loc.CurrentLanguage),
            DialogResult = DialogResult.OK,
            Size = new Size(110, 36),
            Location = new Point(500, 12),
            BackColor = Color.FromArgb(0, 91, 187),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat
        };
        _btnSave.Click += (s, e) => SaveValues();

        _btnCancel = new Button
        {
            Text = Strings.BtnCancel(_loc.CurrentLanguage),
            DialogResult = DialogResult.Cancel,
            Size = new Size(100, 36),
            Location = new Point(620, 12),
            FlatStyle = FlatStyle.Flat
        };
        _btnCancel.Click += (s, e) => Close();

        bottomPanel.Controls.AddRange(new Control[] { _btnSave, _btnCancel });

        Controls.Add(bottomPanel);
        Controls.Add(_tabControl);
        AcceptButton = _btnSave;
        CancelButton = _btnCancel;
    }

    private void BuildGeneralTab(TabPage page)
    {
        page.Padding = new Padding(16);

        // UI Language Group
        _grpLang = new GroupBox
        {
            Text = Strings.GroupUiLanguage(_loc.CurrentLanguage),
            Location = new Point(16, 10),
            Size = new Size(620, 65)
        };
        _rbLangUk = new RadioButton { Text = Strings.LangUkrainian(_loc.CurrentLanguage), Location = new Point(20, 25), AutoSize = true };
        _rbLangEn = new RadioButton { Text = Strings.LangEnglish(_loc.CurrentLanguage), Location = new Point(200, 25), AutoSize = true };
        _rbLangUk.CheckedChanged += (s, e) => { if (_rbLangUk.Checked) OnLanguageSelected(UiLanguage.Uk); };
        _rbLangEn.CheckedChanged += (s, e) => { if (_rbLangEn.Checked) OnLanguageSelected(UiLanguage.En); };
        _grpLang.Controls.AddRange(new Control[] { _rbLangUk, _rbLangEn });

        // Active Layouts Group
        _grpLayouts = new GroupBox
        {
            Text = Strings.GroupEnabledLayouts(_loc.CurrentLanguage),
            Location = new Point(16, 80),
            Size = new Size(620, 65)
        };
        _chkUkLayout = new CheckBox { Text = Strings.LayoutUkrainian(_loc.CurrentLanguage), Location = new Point(20, 25), AutoSize = true };
        _chkEnLayout = new CheckBox { Text = Strings.LayoutEnglish(_loc.CurrentLanguage), Location = new Point(200, 25), AutoSize = true };
        _chkRuLayout = new CheckBox { Text = Strings.LayoutRussian(_loc.CurrentLanguage), Location = new Point(360, 25), AutoSize = true };
        _grpLayouts.Controls.AddRange(new Control[] { _chkUkLayout, _chkEnLayout, _chkRuLayout });

        // Mode Group
        _grpMode = new GroupBox
        {
            Text = Strings.GroupConversionMode(_loc.CurrentLanguage),
            Location = new Point(16, 150),
            Size = new Size(620, 100)
        };
        _rbModeBoth = new RadioButton { Text = Strings.ModeBoth(_loc.CurrentLanguage), Location = new Point(20, 22), AutoSize = true };
        _rbModeUkToEn = new RadioButton { Text = Strings.ModeUkrToEng(_loc.CurrentLanguage), Location = new Point(20, 47), AutoSize = true };
        _rbModeEnToUk = new RadioButton { Text = Strings.ModeEngToUkr(_loc.CurrentLanguage), Location = new Point(20, 72), AutoSize = true };
        _grpMode.Controls.AddRange(new Control[] { _rbModeBoth, _rbModeUkToEn, _rbModeEnToUk });

        // Options
        _chkSwitchLayout = new CheckBox
        {
            Text = Strings.CheckSwitchLayout(_loc.CurrentLanguage),
            Location = new Point(20, 255),
            AutoSize = true
        };
        _chkOnTheFly = new CheckBox
        {
            Text = Strings.CheckOnTheFly(_loc.CurrentLanguage),
            Location = new Point(20, 278),
            AutoSize = true
        };
        _chkRevertOnBackspace = new CheckBox
        {
            Text = Strings.CheckRevertOnBackspace(_loc.CurrentLanguage),
            Location = new Point(20, 301),
            AutoSize = true
        };
        _chkSmartQuotes = new CheckBox
        {
            Text = Strings.CheckSmartQuotes(_loc.CurrentLanguage),
            Location = new Point(20, 324),
            AutoSize = true
        };
        _chkSmartDashes = new CheckBox
        {
            Text = Strings.CheckSmartDashes(_loc.CurrentLanguage),
            Location = new Point(20, 347),
            AutoSize = true
        };
        _chkSmartTypography = new CheckBox
        {
            Text = Strings.CheckSmartTypography(_loc.CurrentLanguage),
            Location = new Point(20, 370),
            AutoSize = true
        };
        _chkStuckShiftFix = new CheckBox
        {
            Text = Strings.CheckStuckShiftFix(_loc.CurrentLanguage),
            Location = new Point(20, 393),
            AutoSize = true
        };
        _chkLanguageContext = new CheckBox
        {
            Text = Strings.CheckLanguageContext(_loc.CurrentLanguage),
            Location = new Point(20, 416),
            AutoSize = true
        };
        _chkSelectionConversion = new CheckBox
        {
            Text = Strings.CheckSelectionConversion(_loc.CurrentLanguage),
            Location = new Point(20, 439),
            AutoSize = true
        };
        _chkExceptionPrompt = new CheckBox
        {
            Text = Strings.CheckExceptionPrompt(_loc.CurrentLanguage),
            Location = new Point(20, 462),
            AutoSize = true
        };
        _chkCapsLockIndicator = new CheckBox
        {
            Text = Strings.CheckCapsLockIndicator(_loc.CurrentLanguage),
            Location = new Point(20, 485),
            AutoSize = true
        };
        _chkAutostart = new CheckBox
        {
            Text = Strings.CheckAutostart(_loc.CurrentLanguage),
            Location = new Point(20, 508),
            AutoSize = true
        };

        page.Controls.AddRange(new Control[]
        {
            _grpLang, _grpLayouts, _grpMode,
            _chkSwitchLayout, _chkOnTheFly, _chkRevertOnBackspace,
            _chkSmartQuotes, _chkSmartDashes, _chkSmartTypography, _chkStuckShiftFix,
            _chkLanguageContext, _chkSelectionConversion, _chkExceptionPrompt, _chkCapsLockIndicator,
            _chkAutostart
        });
    }

    private void BuildExclusionsTab(TabPage page)
    {
        page.Padding = new Padding(12);

        // Column 1: Apps (X = 16, Width = 224)
        _lblApps = new Label { Text = Strings.GroupApps(_loc.CurrentLanguage), Location = new Point(16, 12), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        _listApps = new ListBox { Location = new Point(16, 36), Size = new Size(224, 270) };
        _btnAddApp = new Button { Text = Strings.BtnAddApp(_loc.CurrentLanguage), Location = new Point(16, 355), Size = new Size(108, 32) };
        _btnAddApp.Click += (s, e) => AddAppClicked();
        _btnRemoveApp = new Button { Text = Strings.BtnRemove(_loc.CurrentLanguage), Location = new Point(132, 355), Size = new Size(108, 32) };
        _btnRemoveApp.Click += (s, e) => RemoveSelected(_listApps);

        // Column 2: Sites (X = 256, Width = 224)
        _lblSites = new Label { Text = Strings.GroupSites(_loc.CurrentLanguage), Location = new Point(256, 12), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        _listSites = new ListBox { Location = new Point(256, 36), Size = new Size(224, 270) };
        _txtNewSite = new TextBox { Location = new Point(256, 316), Size = new Size(224, 28), PlaceholderText = "example.com" };
        _btnAddSite = new Button { Text = Strings.BtnAddSite(_loc.CurrentLanguage), Location = new Point(256, 355), Size = new Size(108, 32) };
        _btnAddSite.Click += (s, e) => AddSiteClicked();
        _btnRemoveSite = new Button { Text = Strings.BtnRemove(_loc.CurrentLanguage), Location = new Point(372, 355), Size = new Size(108, 32) };
        _btnRemoveSite.Click += (s, e) => RemoveSelected(_listSites);

        // Column 3: Words (X = 496, Width = 224)
        _lblWords = new Label { Text = Strings.GroupWords(_loc.CurrentLanguage), Location = new Point(496, 12), AutoSize = true, Font = new Font(Font, FontStyle.Bold) };
        _listWords = new ListBox { Location = new Point(496, 36), Size = new Size(224, 270) };
        _txtNewWord = new TextBox { Location = new Point(496, 316), Size = new Size(224, 28), PlaceholderText = Strings.WordPlaceholder(_loc.CurrentLanguage) };
        _btnAddWord = new Button { Text = Strings.BtnAddWord(_loc.CurrentLanguage), Location = new Point(496, 355), Size = new Size(108, 32) };
        _btnAddWord.Click += (s, e) => AddWordClicked();
        _btnRemoveWord = new Button { Text = Strings.BtnRemove(_loc.CurrentLanguage), Location = new Point(612, 355), Size = new Size(108, 32) };
        _btnRemoveWord.Click += (s, e) => RemoveSelected(_listWords);

        page.Controls.AddRange(new Control[]
        {
            _lblApps, _listApps, _btnAddApp, _btnRemoveApp,
            _lblSites, _listSites, _txtNewSite, _btnAddSite, _btnRemoveSite,
            _lblWords, _listWords, _txtNewWord, _btnAddWord, _btnRemoveWord
        });
    }

    private void BuildSecurityTab(TabPage page)
    {
        page.Padding = new Padding(16);

        _chkPasswordProtection = new CheckBox
        {
            Text = Strings.CheckPasswordProtection(_loc.CurrentLanguage),
            Location = new Point(20, 20),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        };

        _lnkDonate = new LinkLabel
        {
            Text = _loc.CurrentLanguage == UiLanguage.En
                ? "💙💛 Support MovaFlow development (Privat24)"
                : "💙💛 Підтримати розвиток проєкту MovaFlow (Privat24)",
            Location = new Point(20, 80),
            AutoSize = true,
            Font = new Font(Font.FontFamily, 10.5f, FontStyle.Bold),
            LinkColor = Color.FromArgb(0, 102, 204),
            ActiveLinkColor = Color.FromArgb(0, 80, 160),
            Cursor = Cursors.Hand
        };
        _lnkDonate.LinkClicked += (s, e) =>
        {
            try
            {
                Process.Start(new ProcessStartInfo(DefaultDonationUrl) { UseShellExecute = true });
            }
            catch { }
        };

        _lblDonateDesc = new Label
        {
            Text = _loc.CurrentLanguage == UiLanguage.En
                ? "Your support helps keep the project active and fuels future updates. Thank you!"
                : "Ваша підтримка допомагає розвивати проєкт та надихає на нові оновлення. Дякуємо!",
            Location = new Point(20, 110),
            Size = new Size(580, 45),
            ForeColor = Color.DimGray
        };

        var lblAbout = new Label
        {
            Text = "MovaFlow v1.0.0 (Open Source, MIT License)\n100% локальна приватність: робота офлайн, без реклами та телеметрії.",
            Location = new Point(20, 200),
            AutoSize = true,
            ForeColor = Color.Gray
        };

        page.Controls.AddRange(new Control[]
        {
            _chkPasswordProtection,
            _lnkDonate, _lblDonateDesc,
            lblAbout
        });
    }

    private void LoadValues()
    {
        var s = _exclusionManager.Settings;

        if (s.UiLanguage == "en") _rbLangEn.Checked = true;
        else _rbLangUk.Checked = true;

        _chkUkLayout.Checked = s.EnableUkrainianLayout;
        _chkEnLayout.Checked = s.EnableEnglishLayout;
        _chkRuLayout.Checked = s.EnableRussianLayout;

        if (s.ConversionMode == "UkrToEng") _rbModeUkToEn.Checked = true;
        else if (s.ConversionMode == "EngToUkr") _rbModeEnToUk.Checked = true;
        else _rbModeBoth.Checked = true;

        _chkSwitchLayout.Checked = s.SwitchSystemLayout;
        _chkOnTheFly.Checked = s.EnableOnTheFly;
        _chkRevertOnBackspace.Checked = s.EnableRevertOnBackspace;
        _chkSmartQuotes.Checked = s.EnableSmartQuotes;
        _chkSmartDashes.Checked = s.EnableSmartDashes;
        _chkSmartTypography.Checked = s.EnableSmartTypography;
        _chkStuckShiftFix.Checked = s.EnableStuckShiftFix;
        _chkLanguageContext.Checked = s.EnableLanguageContext;
        _chkSelectionConversion.Checked = s.EnableSelectionConversion;
        _chkExceptionPrompt.Checked = s.EnableExceptionPrompt;
        _chkCapsLockIndicator.Checked = s.EnableCapsLockIndicator;
        _chkAutostart.Checked = AutostartHelper.IsAutostartEnabled();
        _chkPasswordProtection.Checked = s.EnablePasswordProtection;

        _listApps.Items.Clear();
        foreach (var app in s.ExcludedApplications) _listApps.Items.Add(app);

        _listSites.Items.Clear();
        foreach (var site in s.ExcludedWebsites) _listSites.Items.Add(site);

        _listWords.Items.Clear();
        foreach (var word in s.ExcludedWords) _listWords.Items.Add(word);
    }

    private void SaveValues()
    {
        var s = _exclusionManager.Settings;

        s.UiLanguage = _rbLangEn.Checked ? "en" : "uk";
        _loc.SetLanguageFromCode(s.UiLanguage);

        s.EnableUkrainianLayout = _chkUkLayout.Checked;
        s.EnableEnglishLayout = _chkEnLayout.Checked;
        s.EnableRussianLayout = _chkRuLayout.Checked;

        if (_rbModeUkToEn.Checked) s.ConversionMode = "UkrToEng";
        else if (_rbModeEnToUk.Checked) s.ConversionMode = "EngToUkr";
        else s.ConversionMode = "Both";

        s.SwitchSystemLayout = _chkSwitchLayout.Checked;
        s.EnableOnTheFly = _chkOnTheFly.Checked;
        s.EnableRevertOnBackspace = _chkRevertOnBackspace.Checked;
        s.EnableSmartQuotes = _chkSmartQuotes.Checked;
        s.EnableSmartDashes = _chkSmartDashes.Checked;
        s.EnableSmartTypography = _chkSmartTypography.Checked;
        s.EnableStuckShiftFix = _chkStuckShiftFix.Checked;
        s.EnableLanguageContext = _chkLanguageContext.Checked;
        s.EnableSelectionConversion = _chkSelectionConversion.Checked;
        s.EnableExceptionPrompt = _chkExceptionPrompt.Checked;
        s.EnableCapsLockIndicator = _chkCapsLockIndicator.Checked;
        s.EnablePasswordProtection = _chkPasswordProtection.Checked;
        s.DonationUrl = DefaultDonationUrl;

        AutostartHelper.SetAutostart(_chkAutostart.Checked, Application.ExecutablePath);

        s.ExcludedApplications = _listApps.Items.Cast<string>().ToList();
        s.ExcludedWebsites = _listSites.Items.Cast<string>().ToList();
        s.ExcludedWords = _listWords.Items.Cast<string>().ToList();

        _exclusionManager.Save();
        Close();
    }

    private void OnLanguageSelected(UiLanguage lang)
    {
        _loc.SetLanguage(lang);
        ApplyLocalization();
    }

    private void ApplyLocalization()
    {
        var lang = _loc.CurrentLanguage;
        Text = Strings.SettingsTitle(lang);
        _tabGeneral.Text = Strings.TabGeneral(lang);
        _tabExclusions.Text = Strings.TabExclusions(lang);
        _tabSecurity.Text = Strings.TabSecurityDonate(lang);

        _grpLang.Text = Strings.GroupUiLanguage(lang);
        _grpLayouts.Text = Strings.GroupEnabledLayouts(lang);
        _chkUkLayout.Text = Strings.LayoutUkrainian(lang);
        _chkEnLayout.Text = Strings.LayoutEnglish(lang);
        _chkRuLayout.Text = Strings.LayoutRussian(lang);

        _grpMode.Text = Strings.GroupConversionMode(lang);
        _rbModeBoth.Text = Strings.ModeBoth(lang);
        _rbModeUkToEn.Text = Strings.ModeUkrToEng(lang);
        _rbModeEnToUk.Text = Strings.ModeEngToUkr(lang);

        _chkSwitchLayout.Text = Strings.CheckSwitchLayout(lang);
        _chkOnTheFly.Text = Strings.CheckOnTheFly(lang);
        _chkRevertOnBackspace.Text = Strings.CheckRevertOnBackspace(lang);
        _chkSmartQuotes.Text = Strings.CheckSmartQuotes(lang);
        _chkSmartDashes.Text = Strings.CheckSmartDashes(lang);
        _chkSmartTypography.Text = Strings.CheckSmartTypography(lang);
        _chkStuckShiftFix.Text = Strings.CheckStuckShiftFix(lang);
        _chkLanguageContext.Text = Strings.CheckLanguageContext(lang);
        _chkSelectionConversion.Text = Strings.CheckSelectionConversion(lang);
        _chkExceptionPrompt.Text = Strings.CheckExceptionPrompt(lang);
        _chkCapsLockIndicator.Text = Strings.CheckCapsLockIndicator(lang);
        _chkAutostart.Text = Strings.CheckAutostart(lang);
        _chkPasswordProtection.Text = Strings.CheckPasswordProtection(lang);

        _lblApps.Text = Strings.GroupApps(lang);
        _lblSites.Text = Strings.GroupSites(lang);
        _lblWords.Text = Strings.GroupWords(lang);

        _btnAddApp.Text = Strings.BtnAddApp(lang);
        _btnRemoveApp.Text = Strings.BtnRemove(lang);
        _btnAddSite.Text = Strings.BtnAddSite(lang);
        _btnRemoveSite.Text = Strings.BtnRemove(lang);
        _btnAddWord.Text = Strings.BtnAddWord(lang);
        _btnRemoveWord.Text = Strings.BtnRemove(lang);
        _txtNewWord.PlaceholderText = Strings.WordPlaceholder(lang);

        _lnkDonate.Text = lang == UiLanguage.En
            ? "💙💛 Support MovaFlow development (Privat24)"
            : "💙💛 Підтримати розвиток проєкту MovaFlow (Privat24)";
        _lblDonateDesc.Text = lang == UiLanguage.En
            ? "Your support helps keep the project active and fuels future updates. Thank you!"
            : "Ваша підтримка допомагає розвивати проєкт та надихає на нові оновлення. Дякуємо!";

        _btnSave.Text = Strings.BtnSave(lang);
        _btnCancel.Text = Strings.BtnCancel(lang);
    }

    private void AddAppClicked()
    {
        using var ofd = new OpenFileDialog
        {
            Filter = "Executables (*.exe)|*.exe|All files (*.*)|*.*",
            Title = Strings.BtnAddApp(_loc.CurrentLanguage)
        };
        if (ofd.ShowDialog() == DialogResult.OK)
        {
            string fileName = Path.GetFileName(ofd.FileName);
            if (!_listApps.Items.Contains(fileName))
            {
                _listApps.Items.Add(fileName);
            }
        }
    }

    private void AddSiteClicked()
    {
        string raw = _txtNewSite.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;

        string clean = raw.Replace("https://", "").Replace("http://", "").TrimEnd('/');
        if (!_listSites.Items.Contains(clean))
        {
            _listSites.Items.Add(clean);
            _txtNewSite.Clear();
        }
    }

    private void AddWordClicked()
    {
        string raw = _txtNewWord.Text.Trim();
        if (string.IsNullOrWhiteSpace(raw)) return;

        if (!_listWords.Items.Contains(raw))
        {
            _listWords.Items.Add(raw);
            _txtNewWord.Clear();
        }
    }

    private static void RemoveSelected(ListBox listBox)
    {
        if (listBox.SelectedIndex >= 0)
        {
            listBox.Items.RemoveAt(listBox.SelectedIndex);
        }
    }
}
