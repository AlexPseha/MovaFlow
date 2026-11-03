using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows.Forms;
using MovaFlow.Input;

namespace MovaFlow.Core;

/// <summary>
/// Converts currently selected text in any Windows application via Hotkey (Ctrl+Shift+L or configured hotkey).
/// Automates: Copy selection -> Detect language -> Transliterate/Switch layout -> Paste replacement -> Switch Windows layout.
/// </summary>
public static class SelectedTextConverter
{
    private const int VK_CONTROL = 0x11;
    private const int VK_C = 0x43;
    private const int VK_V = 0x56;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern uint SendInput(uint nInputs, INPUT[] pInputs, int cbSize);

    [StructLayout(LayoutKind.Sequential)]
    private struct INPUT
    {
        public uint type;
        public KEYBDINPUT ki;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct KEYBDINPUT
    {
        public ushort wVk;
        public ushort wScan;
        public uint dwFlags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    private const uint INPUT_KEYBOARD = 1;
    private const uint KEYEVENTF_KEYUP = 0x0002;

    public static bool TryConvertSelectedText(Action<string>? onConverted = null)
    {
        string? originalClipboard = null;
        try
        {
            if (Clipboard.ContainsText())
            {
                originalClipboard = Clipboard.GetText();
            }
        }
        catch
        {
            // Clipboard might be locked by another process
        }

        try
        {
            // 1. Clear clipboard sentinel
            Clipboard.Clear();

            // 2. Simulate Ctrl+C
            SendCtrlKey(VK_C);

            // 3. Wait for selection text to arrive
            string? selectedText = null;
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(20);
                try
                {
                    if (Clipboard.ContainsText())
                    {
                        selectedText = Clipboard.GetText();
                        if (!string.IsNullOrEmpty(selectedText)) break;
                    }
                }
                catch
                {
                    // Retry
                }
            }

            if (string.IsNullOrEmpty(selectedText))
            {
                // Restore original clipboard if nothing was selected
                if (!string.IsNullOrEmpty(originalClipboard))
                {
                    Clipboard.SetText(originalClipboard);
                }
                return false;
            }

            // 4. Detect whether text is primarily Cyrillic or Latin
            int cyrillicCount = 0;
            int latinCount = 0;
            foreach (char c in selectedText)
            {
                if (c is (>= '\u0400' and <= '\u04FF') or '\u0500' or 'ї' or 'є' or 'і' or 'ґ' or 'Ї' or 'Є' or 'І' or 'Ґ')
                {
                    cyrillicCount++;
                }
                else if (c is (>= 'a' and <= 'z') or (>= 'A' and <= 'Z'))
                {
                    latinCount++;
                }
            }

            string targetLang;
            string convertedText;
            if (cyrillicCount >= latinCount && cyrillicCount > 0)
            {
                // Convert Cyrillic -> Latin
                convertedText = LayoutMatrix.UkToEn(selectedText);
                targetLang = "en";
            }
            else
            {
                // Convert Latin -> Ukrainian
                convertedText = LayoutMatrix.EnToUk(selectedText);
                targetLang = "uk";
            }

            if (string.Equals(convertedText, selectedText, StringComparison.Ordinal))
            {
                return false;
            }

            // 5. Put converted text in clipboard and Paste (Ctrl+V)
            Clipboard.SetText(convertedText);
            SendCtrlKey(VK_V);

            // 6. Switch system layout to match converted text
            Thread.Sleep(30);
            SystemLayoutSwitcher.SwitchLanguage(IntPtr.Zero, targetLang);

            onConverted?.Invoke(targetLang);
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static void SendCtrlKey(ushort keyVk)
    {
        var inputs = new INPUT[4];

        // Ctrl DOWN
        inputs[0] = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = 0, dwExtraInfo = InputSynthesizer.InjectedSignature }
        };
        // Key DOWN
        inputs[1] = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT { wVk = keyVk, dwFlags = 0, dwExtraInfo = InputSynthesizer.InjectedSignature }
        };
        // Key UP
        inputs[2] = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT { wVk = keyVk, dwFlags = KEYEVENTF_KEYUP, dwExtraInfo = InputSynthesizer.InjectedSignature }
        };
        // Ctrl UP
        inputs[3] = new INPUT
        {
            type = INPUT_KEYBOARD,
            ki = new KEYBDINPUT { wVk = VK_CONTROL, dwFlags = KEYEVENTF_KEYUP, dwExtraInfo = InputSynthesizer.InjectedSignature }
        };

        SendInput((uint)inputs.Length, inputs, Marshal.SizeOf(typeof(INPUT)));
    }
}
