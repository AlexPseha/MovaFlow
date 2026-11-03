using System.Runtime.InteropServices;

namespace MovaFlow.Input;

public static class SystemLayoutSwitcher
{
    private const uint WM_INPUTLANGCHANGEREQUEST = 0x0050;
    private const uint KLF_ACTIVATE = 0x00000001;

    public static readonly IntPtr InjectedSignature = new(0x4D4F5641); // "MOVA"

    [StructLayout(LayoutKind.Sequential)]
    private struct GUITHREADINFO
    {
        public int cbSize;
        public int flags;
        public IntPtr hwndActive;
        public IntPtr hwndFocus;
        public IntPtr hwndCapture;
        public IntPtr hwndMenuOwner;
        public IntPtr hwndMoveSize;
        public IntPtr hwndCaret;
        public System.Drawing.Rectangle rcCaret;
    }

    [DllImport("user32.dll")]
    private static extern bool GetGUIThreadInfo(uint idThread, ref GUITHREADINFO lpgui);

    [DllImport("user32.dll")]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern uint GetKeyboardLayoutList(int nBuff, [Out] IntPtr[]? lpList);

    [DllImport("user32.dll")]
    private static extern IntPtr ActivateKeyboardLayout(IntPtr hkl, uint Flags);

    public static IntPtr GetActiveWindowHkl()
    {
        IntPtr hwnd = GetForegroundWindow();
        if (hwnd == IntPtr.Zero) return IntPtr.Zero;

        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        var gui = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (GetGUIThreadInfo(threadId, ref gui) && gui.hwndFocus != IntPtr.Zero)
        {
            uint focusThread = GetWindowThreadProcessId(gui.hwndFocus, out _);
            IntPtr hklFocus = GetKeyboardLayout(focusThread);
            if (hklFocus != IntPtr.Zero) return hklFocus;
        }

        return GetKeyboardLayout(threadId);
    }

    public static string GetActiveWindowLanguage()
    {
        IntPtr hkl = GetActiveWindowHkl();
        if (hkl == IntPtr.Zero) return "uk";

        int langId = (int)((long)hkl & 0xFFFF);
        return langId switch
        {
            0x0422 => "uk",
            0x0419 => "ru",
            _ => "en"
        };
    }

    public static IntPtr GetInstalledHkl(string languageCode)
    {
        uint targetLangId = languageCode?.ToLowerInvariant() switch
        {
            "uk" => 0x0422u,
            "ru" => 0x0419u,
            _ => 0x0409u
        };

        var list = new IntPtr[16];
        uint count = GetKeyboardLayoutList(16, list);
        for (int i = 0; i < count; i++)
        {
            if (((uint)list[i].ToInt64() & 0xFFFF) == targetLangId)
            {
                return list[i];
            }
        }
        return targetLangId switch
        {
            0x0422u => (IntPtr)0x04220422,
            0x0419u => (IntPtr)0x04190419,
            _ => (IntPtr)0x04090409
        };
    }

    public static void SwitchLanguage(IntPtr hwnd, string targetLanguage)
    {
        if (string.IsNullOrEmpty(targetLanguage)) return;

        if (hwnd == IntPtr.Zero)
        {
            hwnd = GetForegroundWindow();
        }
        if (hwnd == IntPtr.Zero) return;

        IntPtr targetHkl = GetInstalledHkl(targetLanguage);

        // 1. Classical Win32 WM_INPUTLANGCHANGEREQUEST (fast, non-blocking)
        // Pass IntPtr.Zero as wParam to accept any character set without restriction
        PostMessage(hwnd, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, targetHkl);

        // 2. Also post directly to focused control within target window thread if distinct
        uint threadId = GetWindowThreadProcessId(hwnd, out _);
        var gui = new GUITHREADINFO { cbSize = Marshal.SizeOf<GUITHREADINFO>() };
        if (GetGUIThreadInfo(threadId, ref gui) && gui.hwndFocus != IntPtr.Zero && gui.hwndFocus != hwnd)
        {
            PostMessage(gui.hwndFocus, WM_INPUTLANGCHANGEREQUEST, IntPtr.Zero, targetHkl);
        }

        // 3. Activate for calling process context
        ActivateKeyboardLayout(targetHkl, KLF_ACTIVATE);
    }
}
