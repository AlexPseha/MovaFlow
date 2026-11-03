using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using MovaFlow.Core;
using MovaFlow.Input;
using MovaFlow.Localization;
using MovaFlow.Security;

namespace MovaFlow.UI;

public class TrayApplicationContext : ApplicationContext
{
    private readonly NotifyIcon _notifyIcon;
    private readonly ContextMenuStrip _contextMenu;
    private readonly KeyboardHookManager _hookManager;
    private readonly ExclusionManager _exclusionManager;
    private readonly LocalizationManager _loc = LocalizationManager.Instance;
    private string? _pendingExclusionWord;

    // Menu Items
    private readonly ToolStripMenuItem _itemHeader;
    private readonly ToolStripMenuItem _itemStatus;
    private readonly ToolStripMenuItem _itemTogglePause;
    private readonly ToolStripMenuItem _itemLanguageMenu;
    private readonly ToolStripMenuItem _itemLangUk;
    private readonly ToolStripMenuItem _itemLangEn;
    private readonly ToolStripMenuItem _itemModeMenu;
    private readonly ToolStripMenuItem _itemModeBoth;
    private readonly ToolStripMenuItem _itemModeUkToEn;
    private readonly ToolStripMenuItem _itemModeEnToUk;
    private readonly ToolStripMenuItem _itemSwitchLayout;
    private readonly ToolStripMenuItem _itemOnTheFly;
    private readonly ToolStripMenuItem _itemSettings;
    private readonly ToolStripMenuItem _itemStats;
    private readonly ToolStripMenuItem _itemDebugLog;
    private readonly ToolStripMenuItem _itemDonate;
    private readonly ToolStripMenuItem _itemExit;

    public TrayApplicationContext(KeyboardHookManager hookManager, ExclusionManager exclusionManager)
    {
        _hookManager = hookManager;
        _exclusionManager = exclusionManager;

        // Apply saved UI language
        _loc.SetLanguageFromCode(_exclusionManager.Settings.UiLanguage);
        _loc.LanguageChanged += RefreshMenuTexts;

        _contextMenu = new ContextMenuStrip();

        _itemHeader = new ToolStripMenuItem { Enabled = false, Font = new Font(Control.DefaultFont, FontStyle.Bold) };
        _itemStatus = new ToolStripMenuItem { Enabled = false };
        _itemTogglePause = new ToolStripMenuItem();
        _itemTogglePause.Click += (s, e) => TogglePause();

        // Language Submenu
        _itemLanguageMenu = new ToolStripMenuItem();
        _itemLangUk = new ToolStripMenuItem(Strings.LangUkrainian(_loc.CurrentLanguage));
        _itemLangUk.Click += (s, e) => SwitchLanguage(UiLanguage.Uk);
        _itemLangEn = new ToolStripMenuItem(Strings.LangEnglish(_loc.CurrentLanguage));
        _itemLangEn.Click += (s, e) => SwitchLanguage(UiLanguage.En);
        _itemLanguageMenu.DropDownItems.AddRange(new ToolStripItem[] { _itemLangUk, _itemLangEn });

        // Conversion Mode Submenu
        _itemModeMenu = new ToolStripMenuItem();
        _itemModeBoth = new ToolStripMenuItem();
        _itemModeBoth.Click += (s, e) => SetConversionMode("Both");
        _itemModeUkToEn = new ToolStripMenuItem();
        _itemModeUkToEn.Click += (s, e) => SetConversionMode("UkrToEng");
        _itemModeEnToUk = new ToolStripMenuItem();
        _itemModeEnToUk.Click += (s, e) => SetConversionMode("EngToUkr");
        _itemModeMenu.DropDownItems.AddRange(new ToolStripItem[] { _itemModeBoth, _itemModeUkToEn, _itemModeEnToUk });

        // Toggles
        _itemSwitchLayout = new ToolStripMenuItem { CheckOnClick = true };
        _itemSwitchLayout.CheckedChanged += (s, e) =>
        {
            _exclusionManager.Settings.SwitchSystemLayout = _itemSwitchLayout.Checked;
            _exclusionManager.Save();
        };

        _itemOnTheFly = new ToolStripMenuItem { CheckOnClick = true };
        _itemOnTheFly.CheckedChanged += (s, e) =>
        {
            _exclusionManager.Settings.EnableOnTheFly = _itemOnTheFly.Checked;
            _exclusionManager.Save();
        };

        // Open settings form
        _itemSettings = new ToolStripMenuItem();
        _itemSettings.Click += (s, e) => OpenSettingsForm();

        // Stats
        _itemStats = new ToolStripMenuItem { Enabled = false };

        _itemDebugLog = new ToolStripMenuItem();
        _itemDebugLog.Click += (s, e) =>
        {
            try
            {
                if (File.Exists(DebugLogger.LogPath))
                {
                    Process.Start(new ProcessStartInfo(DebugLogger.LogPath) { UseShellExecute = true });
                }
                else
                {
                    MessageBox.Show("Файл журналу ще не створено.", "MovaFlow", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Помилка відкриття журналу: {ex.Message}", "MovaFlow", MessageBoxButtons.OK, MessageBoxIcon.Error);
            }
        };

        // Donate
        _itemDonate = new ToolStripMenuItem();
        _itemDonate.Click += (s, e) => OpenDonationUrl();

        // Exit
        _itemExit = new ToolStripMenuItem();
        _itemExit.Click += (s, e) => ExitApplication();

        // Build Menu
        _contextMenu.Items.AddRange(new ToolStripItem[]
        {
            _itemHeader,
            _itemStatus,
            _itemTogglePause,
            new ToolStripSeparator(),
            _itemLanguageMenu,
            _itemModeMenu,
            _itemSwitchLayout,
            _itemOnTheFly,
            new ToolStripSeparator(),
            _itemSettings,
            _itemStats,
            _itemDebugLog,
            _itemDonate,
            new ToolStripSeparator(),
            _itemExit
        });

        _notifyIcon = new NotifyIcon
        {
            Icon = CreateMovaFlowIcon(),
            ContextMenuStrip = _contextMenu,
            Visible = true
        };
        _notifyIcon.DoubleClick += (s, e) => OpenSettingsForm();
        _notifyIcon.BalloonTipClicked += OnBalloonTipClicked;

        _hookManager.SuggestException += OnSuggestException;

        _hookManager.CorrectionApplied += (count) =>
        {
            if (_contextMenu.InvokeRequired)
            {
                _contextMenu.BeginInvoke(new Action(() => _itemStats.Text = Strings.MenuStats(_loc.CurrentLanguage, count)));
            }
            else
            {
                _itemStats.Text = Strings.MenuStats(_loc.CurrentLanguage, count);
            }
        };

        // Sync initial state
        _itemSwitchLayout.Checked = _exclusionManager.Settings.SwitchSystemLayout;
        _itemOnTheFly.Checked = _exclusionManager.Settings.EnableOnTheFly;
        UpdateModeChecks(_exclusionManager.Settings.ConversionMode);

        RefreshMenuTexts();
        _hookManager.Start();

        try
        {
            _notifyIcon.ShowBalloonTip(2000, "MovaFlow", Strings.AppTitle(_loc.CurrentLanguage) + " активний", ToolTipIcon.Info);
        }
        catch { }
    }

    private void RefreshMenuTexts()
    {
        var lang = _loc.CurrentLanguage;

        _notifyIcon.Text = Strings.AppTitle(lang);
        _itemHeader.Text = Strings.AppTitle(lang);
        _itemStatus.Text = _hookManager.IsPaused ? Strings.StatusPaused(lang) : Strings.StatusActive(lang);
        _itemTogglePause.Text = Strings.TogglePause(lang, _hookManager.IsPaused);

        _itemLanguageMenu.Text = Strings.MenuLanguage(lang);
        _itemLangUk.Checked = lang == UiLanguage.Uk;
        _itemLangEn.Checked = lang == UiLanguage.En;

        _itemModeMenu.Text = Strings.MenuConversionMode(lang);
        _itemModeBoth.Text = Strings.ModeBoth(lang);
        _itemModeUkToEn.Text = Strings.ModeUkrToEng(lang);
        _itemModeEnToUk.Text = Strings.ModeEngToUkr(lang);

        _itemSwitchLayout.Text = Strings.MenuSwitchSystemLayout(lang);
        _itemOnTheFly.Text = Strings.MenuOnTheFly(lang);
        _itemSettings.Text = Strings.MenuOpenSettings(lang);
        _itemStats.Text = Strings.MenuStats(lang, _hookManager.CorrectionsCount);
        _itemDebugLog.Text = lang == UiLanguage.Uk ? "Журнал діагностики..." : "Diagnostic log...";
        _itemDonate.Text = Strings.MenuDonate(lang);
        _itemExit.Text = Strings.MenuExit(lang);
    }

    private void OpenDonationUrl()
    {
        try
        {
            string url = string.IsNullOrWhiteSpace(_exclusionManager.Settings.DonationUrl)
                ? SettingsForm.DefaultDonationUrl
                : _exclusionManager.Settings.DonationUrl;

            Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // Ignore
        }
    }

    private void TogglePause()
    {
        _hookManager.IsPaused = !_hookManager.IsPaused;
        RefreshMenuTexts();
    }

    private void SwitchLanguage(UiLanguage language)
    {
        _loc.SetLanguage(language);
        _exclusionManager.Settings.UiLanguage = _loc.CurrentCode;
        _exclusionManager.Save();
        RefreshMenuTexts();
    }

    private void SetConversionMode(string mode)
    {
        _exclusionManager.Settings.ConversionMode = mode;
        _exclusionManager.Save();
        UpdateModeChecks(mode);
    }

    private void UpdateModeChecks(string mode)
    {
        _itemModeBoth.Checked = string.Equals(mode, "Both", StringComparison.OrdinalIgnoreCase);
        _itemModeUkToEn.Checked = string.Equals(mode, "UkrToEng", StringComparison.OrdinalIgnoreCase);
        _itemModeEnToUk.Checked = string.Equals(mode, "EngToUkr", StringComparison.OrdinalIgnoreCase);
    }

    private void OpenSettingsForm()
    {
        using var form = new SettingsForm(_exclusionManager);
        if (form.ShowDialog() == DialogResult.OK)
        {
            RefreshMenuTexts();
            _itemSwitchLayout.Checked = _exclusionManager.Settings.SwitchSystemLayout;
            _itemOnTheFly.Checked = _exclusionManager.Settings.EnableOnTheFly;
            UpdateModeChecks(_exclusionManager.Settings.ConversionMode);
        }
    }

    private void ExitApplication()
    {
        _notifyIcon.Visible = false;
        _hookManager.Stop();
        Application.Exit();
    }

    private static Icon CreateMovaFlowIcon()
    {
        int sz = Math.Max(16, SystemInformation.SmallIconSize.Width);

        try
        {
            using var bmp = new Bitmap(sz, sz, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
                g.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
                g.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
                g.Clear(Color.Transparent);

                // 1. Full-bleed dark rounded squircle (#18181F)
                float pad = Math.Max(0.5f, sz * 0.03f);
                float w = sz - (2.0f * pad);
                float r = w * 0.26f;
                float d = r * 2.0f;

                using (var path = new System.Drawing.Drawing2D.GraphicsPath())
                {
                    path.AddArc(pad, pad, d, d, 180, 90);
                    path.AddArc(pad + w - d, pad, d, d, 270, 90);
                    path.AddArc(pad + w - d, pad + w - d, d, d, 0, 90);
                    path.AddArc(pad, pad + w - d, d, d, 90, 90);
                    path.CloseFigure();

                    using var brush = new SolidBrush(Color.FromArgb(24, 24, 30));
                    g.FillPath(brush, path);
                }

                // 2. Fluent M: Bold Blue and Yellow Arches
                float penW = Math.Max(2.2f, sz * 0.135f);

                using var penBlue = new Pen(Color.FromArgb(0, 140, 255), penW)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round,
                    LineJoin = System.Drawing.Drawing2D.LineJoin.Round
                };

                using var penYellow = new Pen(Color.FromArgb(255, 215, 0), penW)
                {
                    StartCap = System.Drawing.Drawing2D.LineCap.Round,
                    EndCap = System.Drawing.Drawing2D.LineCap.Round,
                    LineJoin = System.Drawing.Drawing2D.LineJoin.Round
                };

                float cx = sz / 2.0f;
                float cy = sz / 2.0f;
                float s = (sz / 100.0f) * 1.35f;

                var p1 = new PointF(cx + (18.0f - 50.0f) * s, cy + (78.0f - 52.0f) * s);
                var p2 = new PointF(cx + (20.0f - 50.0f) * s, cy + (24.0f - 52.0f) * s);
                var p3 = new PointF(cx + (46.0f - 50.0f) * s, cy + (24.0f - 52.0f) * s);
                var p4 = new PointF(cx + (50.0f - 50.0f) * s, cy + (56.0f - 52.0f) * s);
                g.DrawBezier(penBlue, p1, p2, p3, p4);

                var p5 = new PointF(cx + (50.0f - 50.0f) * s, cy + (56.0f - 52.0f) * s);
                var p6 = new PointF(cx + (54.0f - 50.0f) * s, cy + (24.0f - 52.0f) * s);
                var p7 = new PointF(cx + (80.0f - 50.0f) * s, cy + (24.0f - 52.0f) * s);
                var p8 = new PointF(cx + (82.0f - 50.0f) * s, cy + (78.0f - 52.0f) * s);
                g.DrawBezier(penYellow, p5, p6, p7, p8);
            }

            IntPtr hIcon = bmp.GetHicon();
            return Icon.FromHandle(hIcon);
        }
        catch
        {
            return SystemIcons.Application;
        }
    }

    private void OnSuggestException(string word)
    {
        if (_notifyIcon == null) return;

        string title = _loc.CurrentLanguage == UiLanguage.Uk ? "MovaFlow: Виключення" : "MovaFlow: Exclusion";
        string msg = _loc.CurrentLanguage == UiLanguage.Uk
            ? $"Слово «{word}» було двічі скасовано. Натисніть, щоб додати до виключень."
            : $"Word \"{word}\" was reverted twice. Click to add to exclusions.";

        _pendingExclusionWord = word;
        try
        {
            _notifyIcon.ShowBalloonTip(6000, title, msg, ToolTipIcon.Info);
        }
        catch { }
    }

    private void OnBalloonTipClicked(object? sender, EventArgs e)
    {
        if (!string.IsNullOrEmpty(_pendingExclusionWord))
        {
            string word = _pendingExclusionWord;
            _pendingExclusionWord = null;
            if (!_exclusionManager.Settings.ExcludedWords.Contains(word, StringComparer.OrdinalIgnoreCase))
            {
                _exclusionManager.Settings.ExcludedWords.Add(word);
                _exclusionManager.Save();
            }
        }
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _notifyIcon.Dispose();
            _contextMenu.Dispose();
            _hookManager.Dispose();
        }
        base.Dispose(disposing);
    }
}
