# MovaFlow

[🇺🇦 Українська](README.md) · 🇬🇧 **English**

A free, lightweight, and privacy-focused typing assistant for Windows - automatic keyboard layout switching, frequency dictionaries, password protection, and 100% offline security.

MovaFlow automatically switches the keyboard layout when a word was typed in the wrong one (`ghbdsn` → `привіт`, `cghfdb` → `справи`, `rfr ltkf` → `как дела`), synchronizes the system language in active Windows 10/11 windows (Notepad, Chrome, Telegram, VS Code, etc.), and supports both on-the-fly and delimiter-based corrections. Everything runs locally on your PC: no accounts, API keys, external servers, or telemetry - 100% offline.

---

## Download

**[⬇︎ Latest version](../../releases/latest)**

* **`MovaFlow-Setup.exe`** - quick installer with Desktop shortcuts, Start Menu integration, and optional autostart.
* **`MovaFlow.exe`** - portable version (Single-File): runs directly without installation.

> **Requirements:** Windows 10 or Windows 11 (64-bit). The .NET 9.0 Desktop Runtime is embedded in the release.

---

## Features

### Layout & Typing Intelligence
* **On-the-fly correction:** by the 3rd or 4th character, MovaFlow detects that a word is being typed in the wrong layout (`ghb…`) and seamlessly switches it while you type.
* **Delimiter-based correction:** words not recognized during typing are evaluated after pressing Space, Tab, Enter, or punctuation: `ghbdsn?` instantly becomes `привіт,`.
* **Embedded frequency dictionaries:** 25,000 top words for Ukrainian (UKR), English (ENG), and Russian (RUS) from the HermitDave corpus. Prefix tree (Trie) search takes less than 1 microsecond.
* **Full alphabet support:** handles unique letters like `і`, `ї`, `є`, `ґ`, apostrophe, as well as `б` and `ю` without false delimiter triggers.
* **Reliable Windows 11 system layout switching:** guaranteed synchronization of the active system language across all modern and legacy windows (WinUI 3, UWP, Chromium, Electron, Win32).
* **Flexible conversion modes:**
  * *Both directions:* automatic detection across all enabled layouts.
  * *Ukrainian to English only.*
  * *English to Ukrainian only.*

### Security & Password Protection (Security-First)
* 🛡️ **Zero Password Exposure:** detects password fields automatically via Windows UI Automation (`IsPassword == true`) and native `ES_PASSWORD` styles. Key tracking is suspended immediately and memory buffers are zeroed out.
* 🚫 **Process Exclusions:** automatically pauses in command terminals (`cmd.exe`, `powershell.exe`, Windows Terminal), IDEs, and password managers (KeePass, 1Password, Bitwarden).
* 🌐 **Website Domain Exclusions:** safe typing on banking websites (`privat24.ua`, `monobank.ua`) - analysis pauses automatically when the respective browser tab is active.
* 🔒 **100% Offline & Private:** no disk logging, no network requests, zero telemetry. Text exists only in a short in-memory ring buffer (up to 64 chars) and is erased immediately after word completion or pressing Enter/Escape.

### Interface & Controls
* **System Tray:** clean icon near the clock, operational status, correction counter, quick pause/resume, and layout toggles.
* **Flat Fluent M-Flow Vector Icon:** high-resolution full-bleed design optimized for both dark and light Windows 11 taskbars.
* **Settings Window:**
  * Single-click interface language switching (Ukrainian / English).
  * Toggle enabled layouts (UKR, ENG, RUS).
  * Manage excluded applications and websites.
  * Configurable autostart with Windows.

---

## Controls & Actions

| Action | How to perform |
|---|---|
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

# Run the automated unit test suite (41 tests)
dotnet run -- --test

# Build release binaries (MovaFlow.exe and MovaFlow-Setup.exe)
powershell -ExecutionPolicy Bypass -File .\build_and_publish.ps1
```

### Code Structure
* `src/Core/` - statistical language detection, N-gram scoring, Trie dictionaries, character mapping.
* `src/Input/` - low-level keyboard hook (`WH_KEYBOARD_LL`), Win32 input synthesis, system layout switching.
* `src/Security/` - UI Automation password field detection, app and website exclusion lists.
* `src/Localization/` - bilingual UI strings (Ukrainian & English).
* `src/UI/` - Windows system tray context menu (`TrayApplicationContext`) and settings window (`SettingsForm`).
* `installer/` - standalone setup wizard project (`MovaFlow-Setup`).
* `resources/` - frequency dictionaries and multi-resolution `movaflow.ico`.
* `tests/` - built-in test suite (`UnitTests.cs`) covering dictionaries, exclusions, and localization.

---

## Credits & Sources

* Frequency Dictionaries: [HermitDave / FrequencyWords](https://github.com/hermitdave/FrequencyWords) (25k most frequent tokens for UK, EN, RU).
* Framework: [.NET 9](https://dotnet.microsoft.com/) and Win32 / UI Automation by Microsoft.

---

## License

This project is licensed under the [MIT License](LICENSE). You are free to use, modify, and distribute the code.

© 2026 MovaFlow Authors.
