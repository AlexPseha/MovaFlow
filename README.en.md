# MovaFlow

[🇺🇦 Українська](README.md) · 🇬🇧 **English**

A free, lightweight, and privacy-focused typing assistant for Windows - automatic keyboard layout switching, frequency dictionaries, password protection, and 100% offline security.

MovaFlow automatically switches the keyboard layout when a word was typed in the wrong one (`ghbdsn` → `привіт`, `cghfdb` → `справи`, `rfr ltkf` → `как дела`), synchronizes the system language in active Windows 10/11 windows (Notepad, Chrome, Telegram, VS Code, etc.), and supports both on-the-fly and delimiter-based corrections. Everything runs locally on your PC: no accounts, API keys, external servers, or telemetry - 100% offline.

---

## Download

**[⬇︎ Latest version](../../releases/latest)**

* **`MovaFlow-Setup.exe`** — complete setup wizard (Dual Payload: includes both Portable and Lite payloads) with Desktop and Start Menu shortcuts, and autostart configuration.
* **`MovaFlow-Portable.exe`** — standalone portable build (Single-File, ~71 MB): zero dependencies, requires no runtime installation, runs straight from any folder or USB drive.
* **`MovaFlow-Lite.exe`** — lightweight build (~1.5 MB): instant startup and compact footprint for PCs with .NET 9 Desktop Runtime already installed.

> **Requirements:** Windows 10 or Windows 11 (64-bit). The .NET 9.0 Desktop Runtime is embedded in the Portable and Setup releases.

---

## Features

### Layout & Typing Intelligence
* **On-the-fly correction:** by the 3rd or 4th character, MovaFlow detects that a word is being typed in the wrong layout (`ghb…`) and seamlessly switches it while you type.
* **Line Context (LanguageContext):** remembers the language of the last 3–5 words on the current line, creating language inertia that prevents accidental layout flips during fluent typing.
* **Short Words Whitelist (ShortWords):** 1–3 letter words are evaluated strictly against a curated whitelist of common short words (inspired by Klavko). This permanently prevents false conversions like `зу` → `pe`, `де` → `lt`, or `ми` → `vb`.
* **Convert Selected Text (Ctrl+Shift+L):** quickly converts and toggles the layout of any currently selected text fragment in any Windows application.
* **Automatic Exception Prompt (ExceptionPrompt):** reverting an auto-correction twice consecutively with Backspace prompts a balloon notification offering to add the word to exclusions with one click.
* **Hardware Caps Lock LED Indicator:** optional feature allowing your keyboard's Caps Lock LED to serve as a hardware indicator for the active Ukrainian layout.
* **Delimiter-based correction:** words not recognized during typing are evaluated after pressing Space, Tab, Enter, or punctuation: `ghbdsn?` instantly becomes `привіт,`.
* **Revert layout on Backspace:** pressing `Backspace` immediately after an auto-correction automatically reverts the Windows keyboard layout to the previous language, breaking unwanted correction loops.
* **CamelCase & PascalCase Protection:** code identifiers like `OrdersWithTrips` are preserved intact and never falsely converted to Cyrillic.
* **Embedded frequency dictionaries:** 25,000 top words for Ukrainian (UKR), English (ENG), and Russian (RUS) from the HermitDave corpus. Prefix tree (Trie) search takes less than 1 microsecond.
* **Full alphabet support:** handles unique letters like `і`, `ї`, `є`, `ґ`, apostrophe, as well as `б` and `ю` without false delimiter triggers.
* **Reliable Windows 11 system layout switching:** guaranteed synchronization of the active system language across all modern and legacy windows (WinUI 3, UWP, Chromium, Electron, Win32).
* **Flexible conversion modes:**
  * *Both directions:* automatic detection across all enabled layouts.
  * *Ukrainian to English only.*
  * *English to Ukrainian only.*

### Smart Typography & Text Formatting
* **Smart Quotes:** typing `"` automatically inserts the correct quotation marks based on the active layout:
  * Cyrillic (Ukrainian/Russian): guillemets `«` and `»` (e.g., `«MovaFlow»`). Typing `""` produces a pair `«»`.
  * English: curly quotes `“` and `”`.
* **Smart Dashes:** typing ` - ` or ` -- ` (hyphens surrounded by spaces) automatically converts into an em-dash ` — ` (command-line flags like `--help` remain untouched).
* **Ellipsis:** three dots `...` are automatically transformed into a single ellipsis character `…`.
* **Special Symbols:** `(c)` or `(C)` → `©`, `(r)` or `(R)` → `®`, `(tm)` or `(TM)` → `™`.
* **Stuck Shift Fix:** automatically corrects words typed with an accidental double uppercase letter (e.g., `ПРивіт` → `Привіт`, `THis` → `This`).

### Security, Privacy & Exclusions (Security-First)
* 🛡️ **Zero Password Exposure:** detects password fields automatically via Windows UI Automation (`IsPassword == true`) and native `ES_PASSWORD` styles. Key tracking is suspended immediately and memory buffers are zeroed out.
* 📝 **Custom Excluded Words:** user-defined list of stop-words, acronyms, and technical jargon (`NNG`, `API`, `SQL`). MovaFlow ignores them across mirror layouts.
* 🚫 **Process Exclusions:** automatically pauses in command terminals (`cmd.exe`, `powershell.exe`, Windows Terminal), IDEs, and password managers (KeePass, 1Password, Bitwarden).
* 🌐 **Website Domain Exclusions:** safe typing on banking websites (`privat24.ua`, `monobank.ua`) — analysis pauses automatically when the respective browser tab is active.
* 🔒 **100% Offline & Private:** no disk logging, no network requests, zero telemetry. Text exists only in a short in-memory ring buffer (up to 64 chars) and is erased immediately after word completion or pressing Enter/Escape.

### Interface & Controls
* **System Tray:** clean icon near the clock, operational status, correction counter, quick pause/resume, and layout toggles.
* **Flat Fluent M-Flow Vector Icon:** high-resolution full-bleed design optimized for both dark and light Windows 11 taskbars.
* **Settings Window:**
  * Single-click interface language switching (Ukrainian / English).
  * Toggle enabled layouts (UKR, ENG, RUS).
  * Independent switches for typography features: smart quotes, smart dashes, ellipsis/symbols, stuck Shift fix.
  * Manage excluded applications, websites, and custom words.
  * Configurable autostart with Windows.

---

## Controls & Actions

| Action | How to perform |
|---|---|
| **Convert selected text** | Select any text fragment and press `Ctrl+Shift+L` |
| **Revert layout on Backspace** | Press `Backspace` immediately after an auto-correction |
| **Pause / Resume** | Click the tray icon or choose "Pause / Resume" from the context menu |
| **Open Settings** | Right-click tray icon → "Settings..." |
| **Change Conversion Mode** | Tray menu → "Mode" → choose preferred mode |
| **Switch UI Language** | Tray menu → "UI Language (Українська / English)" |
| **Exit Application** | Tray menu → "Exit" |

---

## Privacy

MovaFlow is built with a strict privacy-first foundation:
1. **Zero Data Collection:** the application never sends any network packets.
2. **Zero Keystroke Logging:** your typing history is never written to disk or the registry.
3. **RAM-Only Isolation:** transient input data exists only in process memory and is erased after each correction.
4. **Open Source:** every line of code is available for audit in this repository.

---

## For Developers

The project is written in **C# (.NET 9.0 Windows Forms / Win32 API)** with a modular architecture:

```bash
# Clone the repository
git clone https://github.com/AlexPseha/MovaFlow.git
cd MovaFlow

# Run the automated unit test suite (78 tests)
dotnet run -- --test

# Build release packages (MovaFlow-Portable.exe, MovaFlow-Lite.exe, MovaFlow-Setup.exe)
powershell -ExecutionPolicy Bypass -File .\build_and_publish.ps1
```

### Code Structure
* `src/Core/` — statistical language detection, N-gram scoring, Trie dictionaries, character mapping, `SmartTypography.cs` engine.
* `src/Input/` — low-level keyboard hook (`WH_KEYBOARD_LL`), Win32 input synthesis, system layout switching.
* `src/Security/` — UI Automation password field detection, app, website, and custom word exclusion lists.
* `src/Localization/` — bilingual UI strings (Ukrainian & English).
* `src/UI/` — Windows system tray context menu (`TrayApplicationContext`) and settings window (`SettingsForm`).
* `installer/` — standalone setup wizard project (`MovaFlow-Setup`, Dual Payload).
* `resources/` — frequency dictionaries and multi-resolution `movaflow.ico`.
* `tests/` — built-in test suite (`UnitTests.cs`) covering dictionaries, exclusions, typography, and localization.

---

## Credits & Sources

* Frequency Dictionaries: [HermitDave / FrequencyWords](https://github.com/hermitdave/FrequencyWords) (25k most frequent tokens for UK, EN, RU).
* Framework: [.NET 9](https://dotnet.microsoft.com/) and Win32 / UI Automation by Microsoft.

---

## License

This project is licensed under the [MIT License](LICENSE). You are free to use, modify, and distribute the code.

© 2026 MovaFlow Authors.
