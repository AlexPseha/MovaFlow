using System;
using System.IO;

namespace MovaFlow.Core;

public static class DebugLogger
{
    private static readonly object _lock = new();
    private static readonly string _logPath;
    private static bool _enabled = true;

    static DebugLogger()
    {
        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string dir = Path.Combine(appData, "MovaFlow");
        Directory.CreateDirectory(dir);
        _logPath = Path.Combine(dir, "debug.log");
    }

    public static string LogPath => _logPath;

    public static bool Enabled
    {
        get => _enabled;
        set => _enabled = value;
    }

    public static void Log(string message)
    {
        if (!_enabled) return;

        try
        {
            lock (_lock)
            {
                // Roll log file if it exceeds 1 MB
                if (File.Exists(_logPath) && new FileInfo(_logPath).Length > 1024 * 1024)
                {
                    string old = _logPath + ".old";
                    File.Delete(old);
                    File.Move(_logPath, old);
                }

                string line = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] {message}{Environment.NewLine}";
                File.AppendAllText(_logPath, line);
            }
        }
        catch
        {
            // Logging must never crash the application
        }
    }
}
