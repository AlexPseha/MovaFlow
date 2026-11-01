using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;

namespace MovaFlow.Security;

public class BrowserSiteDetector
{
    private static readonly HashSet<string> KnownBrowserProcesses = new(StringComparer.OrdinalIgnoreCase)
    {
        "chrome", "msedge", "firefox", "brave", "opera", "vivaldi", "browser"
    };

    [DllImport("user32.dll")]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

    // Cache: HWND -> (Domain, Timestamp)
    private IntPtr _cachedHwnd = IntPtr.Zero;
    private string _cachedDomain = string.Empty;
    private long _lastCheckTick = 0;
    private const int CacheTtlMs = 1000;

    public string GetActiveBrowserDomain(IntPtr foregroundHwnd, string processName)
    {
        if (foregroundHwnd == IntPtr.Zero || string.IsNullOrWhiteSpace(processName))
        {
            return string.Empty;
        }

        string cleanProcess = processName.Replace(".exe", "", StringComparison.OrdinalIgnoreCase);
        if (!KnownBrowserProcesses.Contains(cleanProcess))
        {
            return string.Empty;
        }

        long now = Environment.TickCount64;
        if (foregroundHwnd == _cachedHwnd && (now - _lastCheckTick) < CacheTtlMs)
        {
            return _cachedDomain;
        }

        _cachedHwnd = foregroundHwnd;
        _lastCheckTick = now;
        _cachedDomain = string.Empty;

        // Instant, non-blocking window title check (0.01 ms)
        var titleSb = new StringBuilder(512);
        if (GetWindowText(foregroundHwnd, titleSb, 512) > 0)
        {
            string title = titleSb.ToString().ToLowerInvariant();
            _cachedDomain = ExtractDomainFromTitle(title);
        }

        return _cachedDomain;
    }

    private static string ExtractDomainFromTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return string.Empty;

        string[] parts = title.Split(new[] { " - ", " — ", " | " }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts)
        {
            if (part.Contains('.'))
            {
                var candidate = CleanUrlToDomain(part);
                if (!string.IsNullOrEmpty(candidate)) return candidate;
            }
        }
        return string.Empty;
    }

    private static string CleanUrlToDomain(string raw)
    {
        try
        {
            if (!raw.StartsWith("http://", StringComparison.OrdinalIgnoreCase) &&
                !raw.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
            {
                raw = "https://" + raw;
            }

            if (Uri.TryCreate(raw, UriKind.Absolute, out var uri))
            {
                return uri.Host.ToLowerInvariant();
            }
        }
        catch
        {
            // Ignore parse errors
        }
        return string.Empty;
    }
}
