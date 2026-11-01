using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Windows.Automation;

namespace MovaFlow.Security;

public class SecurityGuard : IDisposable
{
    private readonly ExclusionManager _exclusionManager;
    private readonly BrowserSiteDetector _siteDetector = new();

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    // Cache to prevent lag on typing
    private IntPtr _lastHwnd = IntPtr.Zero;
    private string _lastProcessName = string.Empty;
    private bool _isCurrentAppExcluded = false;
    private bool _isCurrentSiteExcluded = false;
    private long _lastAppCheckTime = 0;
    private const int AppCacheTtlMs = 1000;

    private volatile bool _isCurrentFocusPassword = false;
    private readonly AutomationFocusChangedEventHandler _focusHandler;

    public SecurityGuard(ExclusionManager exclusionManager)
    {
        _exclusionManager = exclusionManager;
        _focusHandler = OnFocusChanged;

        try
        {
            Automation.AddAutomationFocusChangedEventHandler(_focusHandler);
        }
        catch
        {
            // Fallback gracefully if UI Automation is unavailable
        }
    }

    private void OnFocusChanged(object sender, AutomationFocusChangedEventArgs e)
    {
        try
        {
            if (sender is AutomationElement element)
            {
                object isPassword = element.GetCurrentPropertyValue(AutomationElement.IsPasswordProperty, true);
                _isCurrentFocusPassword = isPassword is bool b && b;
            }
        }
        catch
        {
            _isCurrentFocusPassword = false;
        }
    }

    public bool IsInputSecureAndAllowed()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return false;

        long now = Environment.TickCount64;
        if (hwnd != _lastHwnd || (now - _lastAppCheckTime) > AppCacheTtlMs)
        {
            _lastHwnd = hwnd;
            _lastAppCheckTime = now;

            _lastProcessName = GetProcessName(hwnd);
            _isCurrentAppExcluded = _exclusionManager.IsAppExcluded(_lastProcessName);

            string domain = _siteDetector.GetActiveBrowserDomain(hwnd, _lastProcessName);
            _isCurrentSiteExcluded = !string.IsNullOrEmpty(domain) && _exclusionManager.IsDomainExcluded(domain);
        }

        // If the application or website is excluded, reject immediately
        if (_isCurrentAppExcluded || _isCurrentSiteExcluded)
        {
            return false;
        }

        // If password protection is enabled, check non-blocking flag
        if (_exclusionManager.Settings.EnablePasswordProtection && _isCurrentFocusPassword)
        {
            return false;
        }

        return true;
    }

    private static string GetProcessName(IntPtr hwnd)
    {
        try
        {
            GetWindowThreadProcessId(hwnd, out uint pid);
            if (pid != 0)
            {
                using var proc = Process.GetProcessById((int)pid);
                return proc.ProcessName;
            }
        }
        catch
        {
            // Ignore access denied on system processes
        }
        return string.Empty;
    }

    public void Dispose()
    {
        try
        {
            Automation.RemoveAutomationFocusChangedEventHandler(_focusHandler);
        }
        catch
        {
            // Ignore
        }
        GC.SuppressFinalize(this);
    }
}
