using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using MovaFlow.Core;
using MovaFlow.Security;

namespace MovaFlow.Input;

public class KeyboardHookManager : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;
    private const uint LLKHF_INJECTED = 0x00000010;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    [StructLayout(LayoutKind.Sequential)]
    private struct KBDLLHOOKSTRUCT
    {
        public uint vkCode;
        public uint scanCode;
        public uint flags;
        public uint time;
        public IntPtr dwExtraInfo;
    }

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll")]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern int ToUnicodeEx(
        uint wVirtKey,
        uint wScanCode,
        byte[] lpKeyState,
        [Out, MarshalAs(UnmanagedType.LPWStr)] StringBuilder pwszBuff,
        int cchBuff,
        uint wFlags,
        IntPtr dwhkl);

    [DllImport("user32.dll")]
    private static extern bool GetKeyboardState(byte[] lpKeyState);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    private readonly WordAnalyzer _wordAnalyzer;
    private readonly SecurityGuard _securityGuard;
    private readonly ExclusionManager _exclusionManager;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly StringBuilder _wordBuffer = new();
    private readonly StringBuilder _recentHistory = new();
    private volatile bool _isReplacing;
    private bool _justCorrected;
    private string? _previousLanguageBeforeCorrection;

    public bool IsPaused { get; set; }
    public int CorrectionsCount { get; private set; }
    public event Action<int>? CorrectionApplied;

    public KeyboardHookManager(
        WordAnalyzer wordAnalyzer,
        SecurityGuard securityGuard,
        ExclusionManager exclusionManager)
    {
        _wordAnalyzer = wordAnalyzer;
        _securityGuard = securityGuard;
        _exclusionManager = exclusionManager;
        _proc = HookCallback;
    }

    public void Start()
    {
        if (_hookId != IntPtr.Zero) return;

        IntPtr hModule = IntPtr.Zero;
        try
        {
            using var curProcess = Process.GetCurrentProcess();
            using var curModule = curProcess.MainModule;
            if (curModule != null)
            {
                hModule = GetModuleHandle(curModule.ModuleName);
            }
        }
        catch
        {
            // Ignore
        }

        if (hModule == IntPtr.Zero)
        {
            hModule = GetModuleHandle(null);
        }

        _hookId = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, hModule, 0);
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
        ResetBuffer();
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0 && (wParam == (IntPtr)WM_KEYDOWN || wParam == (IntPtr)WM_SYSKEYDOWN))
        {
            var kbd = Marshal.PtrToStructure<KBDLLHOOKSTRUCT>(lParam);

            // Ignore synthetic keystrokes sent exclusively by our own application via InjectedSignature
            if (kbd.dwExtraInfo == InputSynthesizer.InjectedSignature ||
                kbd.dwExtraInfo == SystemLayoutSwitcher.InjectedSignature)
            {
                return CallNextHookEx(_hookId, nCode, wParam, lParam);
            }

            if (!IsPaused && !_isReplacing)
            {
                bool handled = ProcessKey((int)kbd.vkCode, (int)kbd.scanCode);
                if (handled)
                {
                    // Suppress this key so Windows doesn't echo it in the new layout!
                    return (IntPtr)1;
                }
            }
        }

        return CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    private bool ProcessKey(int vk, int scanCode)
    {
        // 1. Security Check
        if (!_securityGuard.IsInputSecureAndAllowed())
        {
            ResetBuffer();
            _justCorrected = false;
            return false;
        }

        // 2. Navigation / Reset Keys
        if (IsResetKey(vk))
        {
            ResetBuffer();
            _recentHistory.Clear();
            _justCorrected = false;
            return false;
        }

        // 3. Enter key (0x0D): ALWAYS finalize and reset buffer so next line starts clean!
        if (vk == 0x0D)
        {
            if (_wordBuffer.Length > 0)
            {
                HandleDelimiter('\0');
            }
            ResetBuffer();
            _recentHistory.Clear();
            _justCorrected = false;
            return false; // let Windows execute Enter cleanly
        }

        // 4. Backspace
        if (vk == 0x08)
        {
            // Revert layout on immediate Backspace after an auto-correction
            if (_justCorrected)
            {
                if (_exclusionManager.Settings.EnableRevertOnBackspace &&
                    _exclusionManager.Settings.SwitchSystemLayout &&
                    !string.IsNullOrEmpty(_previousLanguageBeforeCorrection))
                {
                    SystemLayoutSwitcher.SwitchLanguage(IntPtr.Zero, _previousLanguageBeforeCorrection);
                }
                _justCorrected = false;
                _previousLanguageBeforeCorrection = null;
            }

            if (_wordBuffer.Length > 0)
            {
                _wordBuffer.Length--;
            }
            if (_recentHistory.Length > 0)
            {
                _recentHistory.Length--;
            }
            return false;
        }

        _justCorrected = false;

        // 5. Translate Key to Unicode Char
        char typedChar = GetCharFromKey((uint)vk, (uint)scanCode);
        if (typedChar == '\0')
        {
            return false;
        }

        // Smart Quotes: replace '"' with language-aware «» or “”
        if (typedChar == '"' && _exclusionManager.Settings.EnableSmartQuotes)
        {
            if (_wordBuffer.Length > 0)
            {
                HandleDelimiter('\0');
            }

            char prevChar = _recentHistory.Length > 0 ? _recentHistory[_recentHistory.Length - 1] : '\0';
            string activeLang = SystemLayoutSwitcher.GetActiveWindowLanguage();
            char smartQuote = SmartTypography.GetSmartQuote(prevChar, activeLang);

            _isReplacing = true;
            try
            {
                InputSynthesizer.ReplaceText(0, smartQuote.ToString());
                AppendHistory(smartQuote);
                return true;
            }
            finally
            {
                _isReplacing = false;
            }
        }

        // Smart Typography: Ellipsis "..."
        if (typedChar == '.' && _exclusionManager.Settings.EnableSmartTypography)
        {
            var ellipsis = SmartTypography.CheckEllipsis(_recentHistory.ToString());
            if (ellipsis != null)
            {
                _isReplacing = true;
                try
                {
                    InputSynthesizer.ReplaceText(ellipsis.Value.backspaces, ellipsis.Value.replacement);
                    if (_recentHistory.Length >= ellipsis.Value.backspaces)
                    {
                        _recentHistory.Length -= ellipsis.Value.backspaces;
                    }
                    _recentHistory.Append(ellipsis.Value.replacement);
                    _wordBuffer.Clear();
                    return true;
                }
                finally
                {
                    _isReplacing = false;
                }
            }
        }

        // Smart Typography: Symbols like (c) -> ©, (r) -> ®, (tm) -> ™
        if (typedChar == ')' && _exclusionManager.Settings.EnableSmartTypography)
        {
            var symbol = SmartTypography.CheckSymbols(_recentHistory.ToString());
            if (symbol != null)
            {
                _isReplacing = true;
                try
                {
                    InputSynthesizer.ReplaceText(symbol.Value.backspaces, symbol.Value.replacement);
                    if (_recentHistory.Length >= symbol.Value.backspaces)
                    {
                        _recentHistory.Length -= symbol.Value.backspaces;
                    }
                    _recentHistory.Append(symbol.Value.replacement);
                    _wordBuffer.Clear();
                    return true;
                }
                finally
                {
                    _isReplacing = false;
                }
            }
        }

        // Smart Dashes: " - " or " -- " -> " — "
        if (typedChar == ' ' && _exclusionManager.Settings.EnableSmartDashes)
        {
            var dash = SmartTypography.CheckSmartDash(_recentHistory.ToString());
            if (dash != null)
            {
                _isReplacing = true;
                try
                {
                    InputSynthesizer.ReplaceText(dash.Value.backspaces, dash.Value.replacement);
                    if (_recentHistory.Length >= dash.Value.backspaces)
                    {
                        _recentHistory.Length -= dash.Value.backspaces;
                    }
                    _recentHistory.Append(dash.Value.replacement);
                    _wordBuffer.Clear();
                    return true;
                }
                finally
                {
                    _isReplacing = false;
                }
            }
        }

        AppendHistory(typedChar);

        // 6. Delimiters (Space, Tab, Punctuation)
        if (IsWordDelimiter(typedChar))
        {
            return HandleDelimiter(typedChar);
        }

        // 7. Regular letter typed: append to buffer
        _wordBuffer.Append(typedChar);

        // 8. On-The-Fly Analysis (minimum 2 characters for short words like "це", "не", "де", "як", "ба")
        if (_exclusionManager.Settings.EnableOnTheFly && _wordBuffer.Length >= 2)
        {
            return CheckOnTheFly();
        }

        return false;
    }

    private bool CheckOnTheFly()
    {
        string currentWord = _wordBuffer.ToString();
        var mode = ParseMode(_exclusionManager.Settings.ConversionMode);

        var result = _wordAnalyzer.Analyze(currentWord, mode, isOnTheFly: true, GetAnalyzerOptions());
        if (result.ShouldTransform)
        {
            _isReplacing = true;
            try
            {
                string previousLang = SystemLayoutSwitcher.GetActiveWindowLanguage();
                if (_exclusionManager.Settings.SwitchSystemLayout)
                {
                    SystemLayoutSwitcher.SwitchLanguage(IntPtr.Zero, result.TargetLanguage);
                }

                // In HookCallback, this triggering key is suppressed (return (IntPtr)1).
                // So in the active window, only (currentWord.Length - 1) characters exist!
                int charsInWindow = Math.Max(0, currentWord.Length - 1);
                InputSynthesizer.ReplaceText(charsInWindow, result.TransformedText);

                // Reset buffer so subsequent delimiters or keys don't re-transform or jump
                _wordBuffer.Clear();

                _previousLanguageBeforeCorrection = previousLang;
                _justCorrected = true;

                CorrectionsCount++;
                CorrectionApplied?.Invoke(CorrectionsCount);
                return true; // Key was consumed and replaced into text
            }
            finally
            {
                _isReplacing = false;
            }
        }
        return false;
    }

    private bool HandleDelimiter(char delimiter)
    {
        bool handled = false;
        if (_wordBuffer.Length > 0)
        {
            string currentWord = _wordBuffer.ToString();
            var mode = ParseMode(_exclusionManager.Settings.ConversionMode);

            var result = _wordAnalyzer.Analyze(currentWord, mode, isOnTheFly: false, GetAnalyzerOptions());
            if (result.ShouldTransform)
            {
                _isReplacing = true;
                try
                {
                    string previousLang = SystemLayoutSwitcher.GetActiveWindowLanguage();
                    if (_exclusionManager.Settings.SwitchSystemLayout)
                    {
                        SystemLayoutSwitcher.SwitchLanguage(IntPtr.Zero, result.TargetLanguage);
                    }

                    // Replace typed word. If delimiter is '\0' (e.g. on Enter), do not append delimiter char.
                    string textToInsert = delimiter != '\0' ? result.TransformedText + delimiter : result.TransformedText;
                    InputSynthesizer.ReplaceText(currentWord.Length, textToInsert);

                    _previousLanguageBeforeCorrection = previousLang;
                    _justCorrected = true;

                    CorrectionsCount++;
                    CorrectionApplied?.Invoke(CorrectionsCount);
                    handled = (delimiter != '\0');
                }
                finally
                {
                    _isReplacing = false;
                }
            }
            else if (_exclusionManager.Settings.EnableStuckShiftFix)
            {
                string? fixedWord = SmartTypography.TryFixStuckShift(currentWord, w => _wordAnalyzer.IsKnownWord(w));
                if (fixedWord != null)
                {
                    _isReplacing = true;
                    try
                    {
                        string textToInsert = delimiter != '\0' ? fixedWord + delimiter : fixedWord;
                        InputSynthesizer.ReplaceText(currentWord.Length, textToInsert);

                        CorrectionsCount++;
                        CorrectionApplied?.Invoke(CorrectionsCount);
                        handled = (delimiter != '\0');
                    }
                    finally
                    {
                        _isReplacing = false;
                    }
                }
            }
        }
        ResetBuffer();
        return handled;
    }

    private void AppendHistory(char c)
    {
        if (_recentHistory.Length >= 32)
        {
            _recentHistory.Remove(0, 1);
        }
        _recentHistory.Append(c);
    }

    private WordAnalyzerOptions GetAnalyzerOptions() => new(
        EnableUkrainian: _exclusionManager.Settings.EnableUkrainianLayout,
        EnableEnglish: _exclusionManager.Settings.EnableEnglishLayout,
        EnableRussian: _exclusionManager.Settings.EnableRussianLayout,
        IsWordExcluded: _exclusionManager.IsWordExcluded
    );

    private void ResetBuffer()
    {
        _wordBuffer.Clear();
    }

    private static bool IsWordDelimiter(char c)
    {
        // Note: ',' and '.' are intentionally omitted because on QWERTY keyboard they correspond
        // to Ukrainian letters 'б' (OEM_COMMA) and 'ю' (OEM_PERIOD).
        return char.IsWhiteSpace(c) || c is '!' or '?' or ';' or ':' or '/' or '\\' or '\n' or '\r';
    }

    private static bool IsResetKey(int vk)
    {
        return vk switch
        {
            0x1B or // ESC
            0x25 or 0x26 or 0x27 or 0x28 or // Arrows
            0x24 or 0x23 or // Home, End
            0x21 or 0x22 or // PgUp, PgDn
            0x2E or // Delete
            >= 0x70 and <= 0x87 => true, // F1 - F24
            _ => false
        };
    }

    private static ConversionMode ParseMode(string? modeStr) => modeStr?.ToLowerInvariant() switch
    {
        "ukrtoeng" => ConversionMode.UkrToEng,
        "engtoukr" => ConversionMode.EngToUkr,
        _ => ConversionMode.Both
    };

    private static char GetCharFromKey(uint vk, uint scanCode)
    {
        byte[] keyState = new byte[256];
        if (!GetKeyboardState(keyState))
        {
            Array.Clear(keyState, 0, keyState.Length);
        }

        // Track Shift/Ctrl states
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0) keyState[0x10] = 0x80; // Shift
        if ((GetAsyncKeyState(0x11) & 0x8000) != 0) keyState[0x11] = 0x80; // Ctrl
        if ((GetAsyncKeyState(0x12) & 0x8000) != 0) keyState[0x12] = 0x80; // Alt

        IntPtr hwnd = GetForegroundWindow();
        uint threadId = hwnd != IntPtr.Zero ? GetWindowThreadProcessId(hwnd, out _) : 0;
        IntPtr hkl = GetKeyboardLayout(threadId);

        var sb = new StringBuilder(8);
        int result = ToUnicodeEx(vk, scanCode, keyState, sb, sb.Capacity, 0, hkl);
        if (result > 0 && sb.Length > 0)
        {
            return sb[0];
        }
        return '\0';
    }

    public void Dispose()
    {
        Stop();
        GC.SuppressFinalize(this);
    }
}
