# Mac Shortcuts for Windows

Use macOS-style keyboard shortcuts on Windows: the **Alt** key (where ⌘ Cmd sits on a Mac keyboard) acts as Cmd, so **Alt+C** copies, **Alt+V** pastes, **Alt+Tab** still switches apps, and so on.

A tray app with a settings window where every shortcut can be switched on or off.

## Build & run

Requires the [.NET 10 SDK](https://dotnet.microsoft.com/download).

```bash
dotnet run --project src/MacShortcuts
```

Standalone `.exe` (a ~2.5 MB native binary; no .NET install needed on the target machine):

```bash
dotnet publish src/MacShortcuts -c Release -r win-x64 -o publish
```

This uses [Native AOT](https://learn.microsoft.com/dotnet/core/deploying/native-aot/), which also needs the Visual Studio **Desktop development with C++** workload (or the C++ Build Tools) for the linker. With the VS 2019 Build Tools, run it from PowerShell or cmd with `%ProgramFiles(x86)%\Microsoft Visual Studio\Installer` on `PATH`; otherwise the linker lookup fails with "'vswhere.exe' is not recognized".

## Usage

- Left-click the tray icon to open the window; right-click for **Settings**, **Enabled**, **Restart as administrator** and **Exit**.
- The window's rail switches between three pages, with an on/off switch for all remapping at the bottom:
  - **Shortcuts**: every shortcut by category, with a filter box. Tick or untick each one.
  - **Excluded apps**: apps where nothing is remapped (e.g. `mstsc.exe`). Type a process name, pick a running app or browse for an `.exe`.
  - **Settings**: Start with Windows (launches minimized at sign-in), which Alt keys act as ⌘ Cmd, the theme (System follows Windows' light/dark app mode, including live changes) and restarting as administrator.
- Closing the window keeps the app running in the tray.
- Settings are stored in `%APPDATA%\MacShortcuts\settings.json`.

## Notes

- **Admin apps:** Windows blocks non-admin apps from sending keys to elevated windows (Task Manager, admin terminals). Use **Restart as administrator** if you need shortcuts there. Start with Windows always launches without admin.
- **Right Alt / AltGr:** on layouts with AltGr (e.g. German, French), remapping Right Alt replaces AltGr characters like €. Untick **Right Alt acts as ⌘ Cmd** if you need them.
- Tapping Alt on its own still behaves normally (e.g. focuses the menu bar).

## How it works

A low-level keyboard hook (`WH_KEYBOARD_LL`) runs on a dedicated thread. Alt key-downs pass through untouched so unmapped combos keep working. When a mapped key is pressed, the key is swallowed, Alt is released logically (after tapping an unassigned "mask" key so the menu bar doesn't activate), and the replacement keys are injected with `SendInput`. The decision logic is in [RemapEngine.cs](src/MacShortcuts/Remapping/RemapEngine.cs), separate from the Windows hooks so it can be unit tested.

## Development

- `dotnet test`: unit tests for the remapping logic, shortcut catalog and settings.
- `tools/smoke-test.ps1`: with the app running, opens a test window and simulates shortcuts to verify the remapping end to end.
- `tools/generate-icon.ps1`: regenerates `src/MacShortcuts/app.ico`.
