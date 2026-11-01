using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MovaFlow.Core;
using MovaFlow.Input;
using MovaFlow.Security;
using MovaFlow.UI;

namespace MovaFlow;

public static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (args.Length > 0 && (args[0] == "--test" || args[0] == "--self-test"))
        {
            return MovaFlow.Tests.UnitTests.RunAll();
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appDir = Path.Combine(appData, "MovaFlow");
        Directory.CreateDirectory(appDir);
        string crashLog = Path.Combine(appDir, "crash.log");

        try
        {
            // Reliable single instance check based on active running processes
            var currentProc = Process.GetCurrentProcess();
            var existing = Process.GetProcessesByName(currentProc.ProcessName)
                                  .FirstOrDefault(p => p.Id != currentProc.Id);
            if (existing != null)
            {
                var ask = MessageBox.Show(
                    "MovaFlow вже запущено в системному треї (біля годинника).\n\nБажаєте перезапустити додаток?",
                    "MovaFlow",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);

                if (ask == DialogResult.Yes)
                {
                    try
                    {
                        existing.Kill();
                        existing.WaitForExit(2000);
                    }
                    catch { }
                }
                else
                {
                    return 0;
                }
            }

            ApplicationConfiguration.Initialize();

            string settingsPath = Path.Combine(appDir, "settings.json");

            // Migrate local settings if existing
            string localSettings = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "settings.json");
            if (File.Exists(localSettings) && !File.Exists(settingsPath))
            {
                try { File.Copy(localSettings, settingsPath, true); } catch { }
            }

            var exclusionManager = new ExclusionManager(settingsPath);
            var securityGuard = new SecurityGuard(exclusionManager);
            var wordAnalyzer = new WordAnalyzer();

            using var hookManager = new KeyboardHookManager(wordAnalyzer, securityGuard, exclusionManager);
            using var trayContext = new TrayApplicationContext(hookManager, exclusionManager);

            Application.Run(trayContext);
            return 0;
        }
        catch (Exception ex)
        {
            try
            {
                File.WriteAllText(crashLog, $"{DateTime.Now}: {ex}");
            }
            catch { }
            Console.Error.WriteLine(ex);
            return 1;
        }
    }
}
