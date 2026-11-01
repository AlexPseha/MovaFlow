using System.Diagnostics;
using System.Drawing;
using System.Reflection;
using System.Windows.Forms;
using Microsoft.Win32;

namespace MovaFlow.Installer;

public static class Program
{
    private static readonly string TargetDir = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "Programs",
        "MovaFlow");

    private static readonly string TargetExe = Path.Combine(TargetDir, "MovaFlow.exe");

    [STAThread]
    public static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();

        if (args.Length > 0 && (args[0] == "--silent" || args[0] == "/S" || args[0] == "-s"))
        {
            bool isLite = args.Any(a => a.Equals("--lite", StringComparison.OrdinalIgnoreCase));
            PerformInstall(isLite, true, true, true, false);
            return;
        }

        if (args.Length > 0 && (args[0] == "--uninstall" || args[0] == "/U"))
        {
            PerformUninstall();
            return;
        }

        Application.Run(new InstallerForm());
    }

    public static bool IsDotNet9Installed()
    {
        try
        {
            string[] searchRoots = new[]
            {
                Environment.GetEnvironmentVariable("DOTNET_ROOT") ?? "",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "dotnet"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "dotnet")
            };

            foreach (var root in searchRoots)
            {
                if (string.IsNullOrEmpty(root) || !Directory.Exists(root)) continue;
                string fxDir = Path.Combine(root, "shared", "Microsoft.WindowsDesktop.App");
                if (Directory.Exists(fxDir))
                {
                    var dirs = Directory.GetDirectories(fxDir);
                    if (dirs.Any(d => Path.GetFileName(d).StartsWith("9.")))
                    {
                        return true;
                    }
                }
            }
        }
        catch { }

        return false;
    }

    public static void PerformInstall(bool isLite, bool createDesktop, bool createStartMenu, bool autostart, bool launchNow)
    {
        // 1. Close existing running instance
        try
        {
            foreach (var proc in Process.GetProcessesByName("MovaFlow"))
            {
                proc.Kill();
                proc.WaitForExit(3000);
            }
        }
        catch { }

        // 2. Extract payload
        Directory.CreateDirectory(TargetDir);
        var asm = Assembly.GetExecutingAssembly();

        string exeResource = isLite ? "Payload.MovaFlow-Lite.exe" : "Payload.MovaFlow-Portable.exe";

        // Fallback if requested resource is not present
        if (!ExtractResource(asm, exeResource, TargetExe))
        {
            if (!ExtractResource(asm, "Payload.MovaFlow-Portable.exe", TargetExe))
            {
                ExtractResource(asm, "Payload.MovaFlow-Lite.exe", TargetExe);
            }
        }

        string icoTarget = Path.Combine(TargetDir, "movaflow.ico");
        ExtractResource(asm, "Payload.movaflow.ico", icoTarget);

        // 3. Shortcuts
        if (createDesktop)
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            CreateShortcut(Path.Combine(desktop, "MovaFlow.lnk"), TargetExe, icoTarget);
        }

        if (createStartMenu)
        {
            string startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs");
            CreateShortcut(Path.Combine(startMenu, "MovaFlow.lnk"), TargetExe, icoTarget);
        }

        // 4. Autostart
        if (autostart)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                key?.SetValue("MovaFlow", $"\"{TargetExe}\"");
            }
            catch { }
        }

        // 5. Windows Add/Remove Programs registration
        RegisterUninstall(icoTarget);

        // 6. Launch
        if (launchNow && File.Exists(TargetExe))
        {
            Process.Start(new ProcessStartInfo(TargetExe) { WorkingDirectory = TargetDir });
        }
    }

    public static void PerformUninstall()
    {
        try
        {
            foreach (var proc in Process.GetProcessesByName("MovaFlow"))
            {
                proc.Kill();
                proc.WaitForExit(2000);
            }
        }
        catch { }

        // Remove shortcuts
        try
        {
            string desktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MovaFlow.lnk");
            if (File.Exists(desktop)) File.Delete(desktop);

            string startMenu = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                @"Microsoft\Windows\Start Menu\Programs\MovaFlow.lnk");
            if (File.Exists(startMenu)) File.Delete(startMenu);
        }
        catch { }

        // Remove Run registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
            key?.DeleteValue("MovaFlow", false);
        }
        catch { }

        // Remove Uninstall registry
        try
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true);
            key?.DeleteSubKeyTree("MovaFlow", false);
        }
        catch { }

        // Remove target files
        try
        {
            if (Directory.Exists(TargetDir))
            {
                Directory.Delete(TargetDir, true);
            }
        }
        catch { }
    }

    private static bool ExtractResource(Assembly asm, string resourceSuffix, string outputPath)
    {
        string? name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));
        if (name == null) return false;

        using var stream = asm.GetManifestResourceStream(name);
        if (stream == null) return false;

        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        stream.CopyTo(fs);
        return true;
    }

    private static void CreateShortcut(string shortcutPath, string exePath, string iconPath)
    {
        try
        {
            Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
            if (shellType != null)
            {
                dynamic shell = Activator.CreateInstance(shellType)!;
                dynamic shortcut = shell.CreateShortcut(shortcutPath);
                shortcut.TargetPath = exePath;
                shortcut.WorkingDirectory = Path.GetDirectoryName(exePath);
                shortcut.Description = "MovaFlow — Розумний та безпечний перемикач розкладки";
                if (File.Exists(iconPath))
                {
                    shortcut.IconLocation = iconPath + ",0";
                }
                shortcut.Save();
            }
        }
        catch { }
    }

    private static void RegisterUninstall(string iconPath)
    {
        try
        {
            using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MovaFlow");
            if (key != null)
            {
                key.SetValue("DisplayName", "MovaFlow");
                key.SetValue("DisplayVersion", "1.0.0");
                key.SetValue("Publisher", "MovaFlow Open Source Contributors");
                key.SetValue("DisplayIcon", iconPath);
                key.SetValue("InstallLocation", TargetDir);
                key.SetValue("UninstallString", $"\"{Application.ExecutablePath}\" --uninstall");
            }
        }
        catch { }
    }
}

public class InstallerForm : Form
{
    private RadioButton _rbStandalone = null!;
    private RadioButton _rbLite = null!;
    private LinkLabel _lblNetStatus = null!;
    private CheckBox _chkDesktop = null!;
    private CheckBox _chkStartMenu = null!;
    private CheckBox _chkAutostart = null!;
    private CheckBox _chkLaunchNow = null!;
    private Button _btnInstall = null!;
    private Button _btnUninstall = null!;
    private ProgressBar _progressBar = null!;
    private Label _lblStatus = null!;

    public InstallerForm()
    {
        InitializeComponents();
    }

    private void InitializeComponents()
    {
        Text = "Встановлення MovaFlow (MovaFlow Setup)";
        Size = new Size(560, 530);
        StartPosition = FormStartPosition.CenterScreen;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        Font = new Font("Segoe UI", 9.5f, FontStyle.Regular);

        // Header Panel with Ukrainian colors
        var header = new Panel
        {
            Dock = DockStyle.Top,
            Height = 85,
            BackColor = Color.FromArgb(0, 91, 187)
        };

        var lblTitle = new Label
        {
            Text = "MovaFlow",
            Font = new Font("Segoe UI", 18f, FontStyle.Bold),
            ForeColor = Color.FromArgb(255, 213, 0),
            Location = new Point(20, 14),
            AutoSize = true
        };

        var lblSubtitle = new Label
        {
            Text = "Розумний та безпечний перемикач розкладки клавіатури для Windows",
            Font = new Font("Segoe UI", 9.5f, FontStyle.Regular),
            ForeColor = Color.White,
            Location = new Point(22, 50),
            AutoSize = true
        };

        header.Controls.AddRange(new Control[] { lblTitle, lblSubtitle });

        // Version Selection Group
        var grpType = new GroupBox
        {
            Text = "Варіант встановлення:",
            Location = new Point(24, 98),
            Size = new Size(495, 125),
            Font = new Font(Font, FontStyle.Bold)
        };

        _rbStandalone = new RadioButton
        {
            Text = "Повна версія (Standalone, ~67 МБ) — Рекомендовано",
            Location = new Point(16, 24),
            AutoSize = true,
            Checked = true,
            Font = new Font(Font, FontStyle.Bold)
        };

        var lblStandaloneHint = new Label
        {
            Text = "Містить вбудований .NET 9. Працює одразу на будь-якому ПК без налаштувань.",
            Location = new Point(36, 46),
            Size = new Size(445, 18),
            ForeColor = Color.DimGray,
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Regular)
        };

        _rbLite = new RadioButton
        {
            Text = "Ультралегка версія (Lite, ~2.8 МБ)",
            Location = new Point(16, 68),
            AutoSize = true,
            Font = new Font(Font, FontStyle.Bold)
        };

        var lblLiteHint = new Label
        {
            Text = "Економить місце. Вимагає встановленого в системі .NET 9 Desktop Runtime.",
            Location = new Point(36, 90),
            Size = new Size(445, 18),
            ForeColor = Color.DimGray,
            Font = new Font(Font.FontFamily, 8.5f, FontStyle.Regular)
        };

        grpType.Controls.AddRange(new Control[]
        {
            _rbStandalone, lblStandaloneHint,
            _rbLite, lblLiteHint
        });

        // .NET 9 Status Indicator
        bool hasDotNet9 = Program.IsDotNet9Installed();
        _lblNetStatus = new LinkLabel
        {
            Location = new Point(26, 230),
            Size = new Size(495, 20),
            Font = new Font(Font.FontFamily, 9.0f, FontStyle.Regular)
        };

        if (hasDotNet9)
        {
            _lblNetStatus.Text = "✔ У системі виявлено .NET 9 Desktop Runtime (Lite-версія готова до роботи)";
            _lblNetStatus.LinkColor = Color.FromArgb(0, 120, 0);
            _lblNetStatus.ActiveLinkColor = Color.FromArgb(0, 100, 0);
        }
        else
        {
            _lblNetStatus.Text = "⚠️ .NET 9 не виявлено в системі. Натисніть тут для завантаження з сайту Microsoft";
            _lblNetStatus.LinkColor = Color.FromArgb(0, 102, 204);
            _lblNetStatus.LinkClicked += (s, e) =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://dotnet.microsoft.com/download/dotnet/9.0") { UseShellExecute = true });
                }
                catch { }
            };
        }

        // Options Checkboxes
        _chkDesktop = new CheckBox
        {
            Text = "Створити ярлик на Робочому столі",
            Location = new Point(26, 260),
            AutoSize = true,
            Checked = true
        };

        _chkStartMenu = new CheckBox
        {
            Text = "Створити ярлик у меню «Пуск»",
            Location = new Point(26, 288),
            AutoSize = true,
            Checked = true
        };

        _chkAutostart = new CheckBox
        {
            Text = "Запускати MovaFlow при вході у Windows",
            Location = new Point(26, 316),
            AutoSize = true,
            Checked = true
        };

        _chkLaunchNow = new CheckBox
        {
            Text = "Запустити MovaFlow після встановлення",
            Location = new Point(26, 344),
            AutoSize = true,
            Checked = true
        };

        _progressBar = new ProgressBar
        {
            Location = new Point(26, 380),
            Size = new Size(495, 12),
            Visible = false
        };

        _lblStatus = new Label
        {
            Location = new Point(26, 398),
            Size = new Size(495, 20),
            Text = "",
            ForeColor = Color.FromArgb(0, 100, 0)
        };

        // Buttons
        _btnInstall = new Button
        {
            Text = "Встановити",
            Size = new Size(130, 38),
            Location = new Point(250, 430),
            BackColor = Color.FromArgb(0, 91, 187),
            ForeColor = Color.White,
            FlatStyle = FlatStyle.Flat,
            Font = new Font(Font, FontStyle.Bold)
        };
        _btnInstall.Click += (s, e) => InstallClicked();

        _btnUninstall = new Button
        {
            Text = "Видалити",
            Size = new Size(110, 38),
            Location = new Point(390, 430),
            FlatStyle = FlatStyle.Flat
        };
        _btnUninstall.Click += (s, e) => UninstallClicked();

        Controls.AddRange(new Control[]
        {
            header, grpType, _lblNetStatus,
            _chkDesktop, _chkStartMenu, _chkAutostart, _chkLaunchNow,
            _progressBar, _lblStatus, _btnInstall, _btnUninstall
        });
    }

    private void InstallClicked()
    {
        bool isLite = _rbLite.Checked;

        if (isLite && !Program.IsDotNet9Installed())
        {
            var res = MessageBox.Show(
                "У вашій системі не виявлено .NET 9 Desktop Runtime, необхідного для роботи Lite-версії.\n\n" +
                "Бажаєте відкрити сторінку завантаження .NET 9 зараз?\n" +
                "(Натисніть 'Ні', щоб встановити повну Standalone-версію, яка не вимагає додаткових компонентів)",
                "Потрібен .NET 9",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (res == DialogResult.Yes)
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://dotnet.microsoft.com/download/dotnet/9.0") { UseShellExecute = true });
                }
                catch { }
                return;
            }
            else if (res == DialogResult.No)
            {
                // Switch to Standalone
                _rbStandalone.Checked = true;
                isLite = false;
            }
            else
            {
                return;
            }
        }

        _btnInstall.Enabled = false;
        _btnUninstall.Enabled = false;
        _progressBar.Visible = true;
        _progressBar.Style = ProgressBarStyle.Marquee;
        _lblStatus.Text = "Встановлення файлів та створення ярликів...";

        try
        {
            Program.PerformInstall(isLite, _chkDesktop.Checked, _chkStartMenu.Checked, _chkAutostart.Checked, _chkLaunchNow.Checked);
            _progressBar.Style = ProgressBarStyle.Blocks;
            _progressBar.Value = 100;
            _lblStatus.Text = "✔ MovaFlow успішно встановлено!";
            MessageBox.Show("MovaFlow успішно встановлено і готовий до роботи!", "Встановлення завершено", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
        catch (Exception ex)
        {
            MessageBox.Show("Помилка встановлення: " + ex.Message, "Помилка", MessageBoxButtons.OK, MessageBoxIcon.Error);
            _btnInstall.Enabled = true;
            _btnUninstall.Enabled = true;
        }
    }

    private void UninstallClicked()
    {
        if (MessageBox.Show("Ви впевнені, що хочете видалити MovaFlow?", "Підтвердження видалення", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
        {
            Program.PerformUninstall();
            MessageBox.Show("MovaFlow успішно видалено з вашого комп'ютера.", "Видалення завершено", MessageBoxButtons.OK, MessageBoxIcon.Information);
            Close();
        }
    }
}
