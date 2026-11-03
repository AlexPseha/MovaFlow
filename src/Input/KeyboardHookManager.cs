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
    private static extern short GetKeyState(int vKey);

    [DllImport("user32.dll")]
    private static extern IntPtr GetForegroundWindow();

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll")]
    private static extern IntPtr GetKeyboardLayout(uint idThread);

    [DllImport("user32.dll")]
    private static extern uint MapVirtualKeyEx(uint uCode, uint uMapType, IntPtr dwhkl);

    private readonly WordAnalyzer _wordAnalyzer;
    private readonly SecurityGuard _securityGuard;
    private readonly ExclusionManager _exclusionManager;

    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookId = IntPtr.Zero;
    private readonly StringBuilder _wordBuffer = new();
    private readonly StringBuilder _recentHistory = new();
    private readonly LanguageContext _languageContext = new(5);
    private readonly ExceptionPromptManager _exceptionPromptManager = new();
    private volatile bool _isReplacing;
    private bool _justCorrected;
    private string? _previousLanguageBeforeCorrection;
    private string? _lastCorrectedWord;

    public bool IsPaused { get; set; }
    public int CorrectionsCount { get; private set; }
    public event Action<int>? CorrectionApplied;
    public event Action<string>? SuggestException;

    public KeyboardHookManager(
        WordAnalyzer wordAnalyzer,
        SecurityGuard securityGuard,
        ExclusionManager exclusionManager)
    {
        _wordAnalyzer = wordAnalyzer;
        _securityGuard = securityGuard;
        _exclusionManager = exclusionManager;
        _proc = HookCallback;
        _exceptionPromptManager.SuggestException += word => SuggestException?.Invoke(word);
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
        // 1. Hotkey: Ctrl+Shift+L converts currently selected text in active window
        if (vk == 0x4C && _exclusionManager.Settings.EnableSelectionConversion) // 'L'
        {
            bool isCtrl = (GetAsyncKeyState(0x11) & 0x8000) != 0;
            bool isShift = (GetAsyncKeyState(0x10) & 0x8000) != 0;
            if (isCtrl && isShift)
            {
                SelectedTextConverter.TryConvertSelectedText(lang =>
                {
                    if (_exclusionManager.Settings.EnableCapsLockIndicator)
                    {
                        CapsLockIndicator.Sync(lang, true);
                    }
                });
                return true; // suppress 'L'
            }
        }

        // 2. Security Check
        if (!_securityGuard.IsInputSecureAndAllowed())
        {
            ResetBuffer();
            _justCorrected = false;
            return false;
        }

        // 3. Navigation / Reset Keys
        if (IsResetKey(vk))
        {
            ResetBuffer();
            _recentHistory.Clear();
            _languageContext.Reset();
            _justCorrected = false;
            return false;
        }

        // 4. Enter key (0x0D): ALWAYS finalize and reset buffer so next line starts clean!
        if (vk == 0x0D)
        {
            if (_wordBuffer.Length > 0)
            {
                HandleDelimiter('\0');
            }
            ResetBuffer();
            _recentHistory.Clear();
            _languageContext.Reset();
            _justCorrected = false;
            return false; // let Windows execute Enter cleanly
        }

        // 5. Backspace
        if (vk == 0x08)
        {
            // Revert layout on immediate Backspace after an auto-correction
            if (_justCorrected)
            {
                if (!string.IsNullOrEmpty(_lastCorrectedWord) && _exclusionManager.Settings.EnableExceptionPrompt)
                {
                    _exceptionPromptManager.RecordRevert(_lastCorrectedWord);
                }

                if (_exclusionManager.Settings.EnableRevertOnBackspace &&
                    _exclusionManager.Settings.SwitchSystemLayout &&
                    !string.IsNullOrEmpty(_previousLanguageBeforeCorrection))
                {
                    SystemLayoutSwitcher.SwitchLanguage(IntPtr.Zero, _previousLanguageBeforeCorrection);
                    if (_exclusionManager.Settings.EnableCapsLockIndicator)
                    {
                        CapsLockIndicator.Sync(_previousLanguageBeforeCorrection, true);
                    }
                }
                _justCorrected = false;
                _previousLanguageBeforeCorrection = null;
                _lastCorrectedWord = null;
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

        if (!string.IsNullOrEmpty(_lastCorrectedWord) && !_justCorrected)
        {
            _exceptionPromptManager.RecordAccepted(_lastCorrectedWord);
            _lastCorrectedWord = null;
        }
        _justCorrected = false;

        // 6. Translate Key to Unicode Char
        bool shiftPressed = (GetAsyncKeyState(0x10) & 0x8000) != 0;
        string activeLang = SystemLayoutSwitcher.GetActiveWindowLanguage();
        char typedChar = GetCharFromKey((uint)vk, (uint)scanCode);

        // Smart Quotes: replace '"' or ''' with language-aware «» or “”
        if (_exclusionManager.Settings.EnableSmartQuotes)
        {
            // Recognize double quote:
            // 1. typedChar == '"'
            // 2. OR vk == 0x32 ('2') with Shift in Cyrillic
            // 3. OR vk == 0xDE ('\'') with Shift in English
            bool isDoubleQuote = (typedChar == '"')
                              || (shiftPressed && vk == 0x32 && activeLang != "en")
                              || (shiftPressed && vk == 0xDE && activeLang == "en");

            // Recognize single quote (without Shift):
            // 1. typedChar == '\''
            // 2. OR vk == 0xDE without Shift in English
            // 3. OR vk == 0xC0 without Shift in Cyrillic (tilde key which types apostrophe in Ukrainian)
            bool isSingleQuote = !shiftPressed && (typedChar == '\'' || (vk == 0xDE && activeLang == "en") || (vk == 0xC0 && activeLang != "en"));

            if (isDoubleQuote || isSingleQuote)
            {
                char prevChar = _recentHistory.Length > 0 ? _recentHistory[_recentHistory.Length - 1] : '\0';
                char? smartQuote = SmartTypography.TryGetSmartQuote(prevChar, activeLang, isSingleQuote, _recentHistory.ToString());

                if (smartQuote.HasValue)
                {
                    if (_wordBuffer.Length > 0)
                    {
                        HandleDelimiter('\0');
                    }

                    _isReplacing = true;
                    try
                    {
                        InputSynthesizer.ReplaceText(0, smartQuote.Value.ToString());
                        AppendHistory(smartQuote.Value);
                        DebugLogger.Log($"[KeyboardHook] Replaced quote: vk=0x{vk:X2}, shift={shiftPressed}, isDouble={isDoubleQuote}, isSingle={isSingleQuote}, lang={activeLang}, prev='{prevChar}', smart='{smartQuote.Value}'");
                        return true;
                    }
                    finally
                    {
                        _isReplacing = false;
                    }
                }
                else
                {
                    DebugLogger.Log($"[KeyboardHook] Preserved apostrophe: vk=0x{vk:X2}, lang={activeLang}, prev='{prevChar}'");
                }
            }
        }

        if (typedChar == '\0')
        {
            return false;
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

        var result = _wordAnalyzer.Analyze(
            currentWord,
            mode,
            isOnTheFly: true,
            GetAnalyzerOptions(),
            _exclusionManager.Settings.EnableLanguageContext ? _languageContext : null);

        if (result.ShouldTransform)
        {
            _isReplacing = true;
            try
            {
                string previousLang = SystemLayoutSwitcher.GetActiveWindowLanguage();
                if (_exclusionManager.Settings.SwitchSystemLayout)
                {
                    SystemLayoutSwitcher.SwitchLanguage(IntPtr.Zero, result.TargetLanguage);
                    if (_exclusionManager.Settings.EnableCapsLockIndicator)
                    {
                        CapsLockIndicator.Sync(result.TargetLanguage, true);
                    }
                }

                // In HookCallback, this triggering key is suppressed (return (IntPtr)1).
                // So in the active window, only (currentWord.Length - 1) characters exist!
                int charsInWindow = Math.Max(0, currentWord.Length - 1);
                InputSynthesizer.ReplaceText(charsInWindow, result.TransformedText);

                // Reset buffer so subsequent delimiters or keys don't re-transform or jump
                _wordBuffer.Clear();

                _previousLanguageBeforeCorrection = previousLang;
                _lastCorrectedWord = currentWord;
                _justCorrected = true;
                _languageContext.AddWord(result.TargetLanguage);

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

            var result = _wordAnalyzer.Analyze(
                currentWord,
                mode,
                isOnTheFly: false,
                GetAnalyzerOptions(),
                _exclusionManager.Settings.EnableLanguageContext ? _languageContext : null);

            if (result.ShouldTransform)
            {
                _isReplacing = true;
                try
                {
                    string previousLang = SystemLayoutSwitcher.GetActiveWindowLanguage();
                    if (_exclusionManager.Settings.SwitchSystemLayout)
                    {
                        SystemLayoutSwitcher.SwitchLanguage(IntPtr.Zero, result.TargetLanguage);
                        if (_exclusionManager.Settings.EnableCapsLockIndicator)
                        {
                            CapsLockIndicator.Sync(result.TargetLanguage, true);
                        }
                    }

                    // Replace typed word. If delimiter is '\0' (e.g. on Enter), do not append delimiter char.
                    string textToInsert = delimiter != '\0' ? result.TransformedText + delimiter : result.TransformedText;
                    InputSynthesizer.ReplaceText(currentWord.Length, textToInsert);

                    _previousLanguageBeforeCorrection = previousLang;
                    _lastCorrectedWord = currentWord;
                    _justCorrected = true;
                    _languageContext.AddWord(result.TargetLanguage);

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
                else
                {
                    string lang = LayoutMatrix.DetectScript(currentWord) == ScriptType.Cyrillic ? "uk" : "en";
                    _languageContext.AddWord(lang);
                }
            }
            else
            {
                string lang = LayoutMatrix.DetectScript(currentWord) == ScriptType.Cyrillic ? "uk" : "en";
                _languageContext.AddWord(lang);
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

        // Track Shift/Ctrl/Alt states accurately including left/right specific keys
        if ((GetAsyncKeyState(0x10) & 0x8000) != 0)
        {
            keyState[0x10] = 0x80; // VK_SHIFT
            if ((GetAsyncKeyState(0xA0) & 0x8000) != 0) keyState[0xA0] = 0x80; // VK_LSHIFT
            if ((GetAsyncKeyState(0xA1) & 0x8000) != 0) keyState[0xA1] = 0x80; // VK_RSHIFT
        }
        if ((GetAsyncKeyState(0x11) & 0x8000) != 0)
        {
            keyState[0x11] = 0x80; // VK_CONTROL
            if ((GetAsyncKeyState(0xA2) & 0x8000) != 0) keyState[0xA2] = 0x80; // VK_LCONTROL
            if ((GetAsyncKeyState(0xA3) & 0x8000) != 0) keyState[0xA3] = 0x80; // VK_RCONTROL
        }
        if ((GetAsyncKeyState(0x12) & 0x8000) != 0)
        {
            keyState[0x12] = 0x80; // VK_MENU
            if ((GetAsyncKeyState(0xA4) & 0x8000) != 0) keyState[0xA4] = 0x80; // VK_LMENU
            if ((GetAsyncKeyState(0xA5) & 0x8000) != 0) keyState[0xA5] = 0x80; // VK_RMENU
        }
        if ((GetKeyState(0x14) & 0x0001) != 0)
        {
            keyState[0x14] = 0x01; // VK_CAPITAL toggle state
        }

        IntPtr hkl = SystemLayoutSwitcher.GetActiveWindowHkl();
        if (hkl == IntPtr.Zero)
        {
            IntPtr hwnd = GetForegroundWindow();
            uint threadId = hwnd != IntPtr.Zero ? GetWindowThreadProcessId(hwnd, out _) : 0;
            hkl = GetKeyboardLayout(threadId);
        }

        uint sc = scanCode != 0 ? scanCode : MapVirtualKeyEx(vk, 0, hkl);

        var sb = new StringBuilder(8);
        int result = ToUnicodeEx(vk, sc, keyState, sb, sb.Capacity, 0, hkl);
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
