using System.Runtime.InteropServices;
using MovaFlow.Core;

namespace MovaFlow.Input;

public static class InputSynthesizer
{
    public static readonly IntPtr InjectedSignature = new(0x4D4F5641); // "MOVA" in ASCII

    private const int INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;
    private const uint KEYEVENTF_UNICODE = 0x0004;
    private const ushort VK_BACK = 0x08;
    private const ushort VK_SHIFT = 0x10;

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [StructLayout(LayoutKind.Explicit, Size = 40)]
    private struct INPUT
    {
        [FieldOffset(0)] public int type;
        [FieldOffset(8)] public KEYBDINPUT ki;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    public static void ReplaceText(int backspaceCount, string newText)
    {
        if (backspaceCount <= 0 && string.IsNullOrEmpty(newText)) return;

        bool shiftDown = (GetAsyncKeyState(VK_SHIFT) & 0x8000) != 0;

        var inputs = new List<INPUT>();

        // 0. If Shift is physically pressed (e.g. user typed Shift+2 or Shift+'),
        // temporarily release Shift before sending Backspace and Unicode characters.
        // This prevents target apps (Telegram, Chrome, Word, RichEdit) from rejecting VK_PACKET.
        if (shiftDown)
        {
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = VK_SHIFT,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    dwExtraInfo = InjectedSignature
                }
            });
        }

        // 1. Simulate Backspace presses
        for (int i = 0; i < backspaceCount; i++)
        {
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = VK_BACK,
                    wScan = 0,
                    dwFlags = 0,
                    dwExtraInfo = InjectedSignature
                }
            });
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = VK_BACK,
                    wScan = 0,
                    dwFlags = KEYEVENTF_KEYUP,
                    dwExtraInfo = InjectedSignature
                }
            });
        }

        // 2. Simulate Unicode typing
        foreach (char c in newText)
        {
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE,
                    dwExtraInfo = InjectedSignature
                }
            });
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = 0,
                    wScan = c,
                    dwFlags = KEYEVENTF_UNICODE | KEYEVENTF_KEYUP,
                    dwExtraInfo = InjectedSignature
                }
            });
        }

        // 3. Restore Shift state if it was physically down
        if (shiftDown)
        {
            inputs.Add(new INPUT
            {
                type = INPUT_KEYBOARD,
                ki = new KEYBDINPUT
                {
                    wVk = VK_SHIFT,
                    wScan = 0,
                    dwFlags = 0,
                    dwExtraInfo = InjectedSignature
                }
            });
        }

        var array = inputs.ToArray();
        uint sent = SendInput((uint)array.Length, array, Marshal.SizeOf(typeof(INPUT)));
        if (sent != array.Length)
        {
            int err = Marshal.GetLastWin32Error();
            DebugLogger.Log($"[InputSynthesizer] SendInput warning: sent {sent}/{array.Length}, err={err}, shiftWasDown={shiftDown}");
        }
        else
        {
            DebugLogger.Log($"[InputSynthesizer] SendInput success: backspaces={backspaceCount}, text='{newText}', shiftWasDown={shiftDown}");
        }
    }
}
