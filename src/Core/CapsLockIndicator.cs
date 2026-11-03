using System;
using System.Runtime.InteropServices;

namespace MovaFlow.Core;

/// <summary>
/// Hardware Caps Lock LED indication for active keyboard layout.
/// When enabled, lights the Caps Lock LED when Ukrainian is active,
/// and turns it off when English is active.
/// Inspired by Klavko's CapsLockLight architecture.
/// </summary>
public static class CapsLockIndicator
{
    private const int VK_CAPITAL = 0x14;
    private const uint KEYEVENTF_EXTENDEDKEY = 0x0001;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    [DllImport("user32.dll")]
    private static extern short GetKeyState(int nVirtKey);

    [DllImport("user32.dll")]
    private static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, UIntPtr dwExtraInfo);

    public static bool IsCapsLockOn()
    {
        return (GetKeyState(VK_CAPITAL) & 0x0001) != 0;
    }

    /// <summary>
    /// Synchronizes Caps Lock indicator according to the active language:
    /// On for Ukrainian, Off for English/Other.
    /// </summary>
    public static void Sync(string? language, bool enabled)
    {
        if (!enabled || string.IsNullOrEmpty(language)) return;

        bool shouldBeOn = language.StartsWith("uk", StringComparison.OrdinalIgnoreCase);
        bool currentlyOn = IsCapsLockOn();

        if (shouldBeOn != currentlyOn)
        {
            // Simulate CapsLock press to toggle state
            keybd_event(VK_CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY, UIntPtr.Zero);
            keybd_event(VK_CAPITAL, 0x45, KEYEVENTF_EXTENDEDKEY | KEYEVENTF_KEYUP, UIntPtr.Zero);
        }
    }
}
