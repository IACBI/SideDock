# CLAUDE.md — Guidance for Claude working on this project

## What this project is
**SideDock** is an open-source, customizable **vertical dock / launcher for Windows 11**,
written in **C# / .NET 8 / WPF**.

It is a **standalone application**. This is a hard rule:

- It is **its own normal top-level window**.
- It does **NOT** inject code into `explorer.exe` or any other process.
- It does **NOT** modify, hide, replace, or subclass the system taskbar or any
  other system component.
- The only OS integration it uses is **documented, supported public Win32 APIs**
  (e.g. the AppBar API `SHAppBarMessage`) called through normal P/Invoke.

If a feature cannot be done with documented APIs from our own process, we do not
do it.

## How we work (process rules)
- **Build incrementally, one phase at a time.** See `ROADMAP.md`.
- **Finish the current phase, then STOP.** Do not begin the next phase until the
  user explicitly confirms the current one works (the user will say something
  like "it works" / "confirmed" / "next").
- After each phase, give **exact build/run instructions** and a clear description
  of **what the user should see**.

## Coding conventions
- Target **.NET 8** (`net8.0-windows`), WPF, C# with nullable + implicit usings on.
- Keep code **clean and well-commented**, written for a beginner-to-intermediate
  C# developer to follow.
- **Only use real, documented APIs** — real .NET BCL APIs and documented Win32
  APIs via P/Invoke. **Never invent** library names, NuGet packages, methods, or
  Win32 calls.
- **Every P/Invoke / Win32 call must have a comment** explaining what it does,
  and ideally a link to its Microsoft Learn documentation.
- If you are **not sure** whether an API exists or behaves a certain way, say
  **"I'm not sure"** and verify — do not guess.
- Prefer small, focused files. Interop (P/Invoke) code lives under
  `src/SideDock/Interop/`.

## Project layout
```
sidetaskbar/
├─ CLAUDE.md            ← this file
├─ README.md
├─ ROADMAP.md
├─ LICENSE              ← MIT
├─ .gitignore
├─ SideDock.sln
└─ src/
   └─ SideDock/
      ├─ SideDock.csproj
      ├─ App.xaml / App.xaml.cs
      ├─ MainWindow.xaml / MainWindow.xaml.cs
      ├─ Models/
      │  └─ DockConfig.cs       ← config model (AppEntry list)
      ├─ Services/
      │  ├─ ConfigService.cs    ← load/save JSON config (%APPDATA%\SideDock)
      │  ├─ AppLauncher.cs      ← Process.Start wrapper
      │  └─ TrayIconService.cs  ← WinForms NotifyIcon (system tray)
      └─ Interop/
         ├─ AppBarManager.cs     ← SHAppBarMessage wrapper (AppBar API)
         ├─ IconExtractor.cs     ← SHGetFileInfo / HICON → WPF ImageSource
         └─ WindowEnumerator.cs  ← EnumWindows list + SetForegroundWindow activate
```

## Current status
- **Phase 1: DONE & confirmed.** Empty styled vertical bar docked left via AppBar API.
- **Phase 2: DONE & confirmed.** App launcher: icons read from
  `%APPDATA%\SideDock\config.json`, click to launch.
- **Phase 3: DONE & confirmed.** Clock (DispatcherTimer) + system tray
  (WinForms NotifyIcon) + right-click context menu on the bar itself.
- **Phase 4: DONE & confirmed.** Taskbar-like list of open windows via
  EnumWindows (filtered: visible, titled, not tool window, no owner, not
  DWM-cloaked); click to activate via SetForegroundWindow.
- **Phase 5: in progress / awaiting user confirmation.** Config-driven theme
  (colors), size (BarThickness/IconSize), and edge (Left/Right) in
  `config.json`; live reload via FileSystemWatcher (debounced); perf: timers
  pause while hidden, brushes frozen, window list rebuilt only on change.

## Notes / gotchas
- `UseWindowsForms` injects `global using System.Drawing;` + `System.Windows.Forms;`
  which clash with WPF's `Application`/`Button`. We `<Using Remove>` both in the
  csproj and use WinForms only inside `TrayIconService.cs` with explicit usings.
- **Transparency:** the user wants a fully transparent bar (only icons/clock
  visible, desktop showing through). We use WPF per-pixel transparency
  (`AllowsTransparency="True"`, `Background="Transparent"`); the `Theme`
  gradient is a tint scaled by `BackgroundOpacity` (default 0.0 = fully clear).
  NOTE: a DWM acrylic/Mica backdrop (`DwmSetWindowAttribute`) was tried but it
  CANNOT coexist with `AllowsTransparency=True`, so it was removed. If glass is
  ever wanted again, AllowsTransparency must be decided at window-create time.
- **Live reload must not re-dock on theme-only edits.** `ApplyConfig` only
  touches the AppBar (which moves/resizes the window) when `Edge` or
  `BarThickness` changes; otherwise changing a color/opacity used to shift the
  window. Tracked via `_currentEdge` / `_currentThickness`.
