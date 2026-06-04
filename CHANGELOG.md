# Changelog

All notable changes to SideDock are documented in this file.

The format is based on [Keep a Changelog](https://keepachangelog.com/en/1.1.0/),
and this project adheres to [Semantic Versioning](https://semver.org/spec/v2.0.0.html).

## [Unreleased]
### Added
- **Single instance:** a second launch exits quietly instead of registering a
  duplicate AppBar/tray icon.
- **Start with Windows:** optional `StartWithWindows` config flag that adds/removes
  a per-user `Run` registry entry.
- **Full-screen auto-hide:** the dock drops out of "topmost" when a full-screen
  app (game/video) opens, and returns on top when it closes
  (handles `ABN_FULLSCREENAPP`).
- **Custom application icon** for the executable, window, and tray icon (replaces
  the generic Windows icon).
- **Continuous integration:** a GitHub Actions workflow builds the solution on
  Windows for every push and pull request.

## [0.1.0] - 2026-06-04
First public version. A standalone vertical dock for Windows 11 that uses only
documented APIs (no process injection, no modification of the system taskbar).

### Added
- **Docked bar (AppBar):** borderless, always-on-top vertical bar docked to the
  Left or Right edge that reserves its screen space via `SHAppBarMessage`.
- **App launcher:** app icons read from `%APPDATA%\SideDock\config.json`; click
  to launch (`Process.Start`). Optional per-app `Arguments` and `IconPath`.
- **Clock:** 12/24-hour, optional seconds, optional date.
- **System tray icon** (WinForms `NotifyIcon`) and a **right-click menu on the
  bar** to hide the dock, open the config, or exit.
- **Open-windows list:** taskbar-like list via `EnumWindows` (filtered to real
  app windows); click to focus/restore. Minimized windows are dimmed.
- **Customization & live reload:** theme colors, transparency
  (`BackgroundOpacity`), bar thickness, icon sizes, clock fonts/sizes, button
  corner radius, and docked edge — all in `config.json`, applied live via a
  `FileSystemWatcher`. The config file is auto-created and upgraded with new
  options on launch.

### Performance & robustness
- Clock and window-poll timers pause while the dock is hidden.
- Window icons fetched lazily (no cross-process `WM_GETICON` on idle polls).
- Window list rebuilt only when the set of windows actually changes.
- Frozen brushes for cheaper rendering.
- Idempotent AppBar (re)registration.
- Clean shutdown frees the AppBar reservation first; a global crash handler
  releases it too, so the screen strip is never left reserved.
- Best-effort config saves (a locked/unwritable file won't crash startup).

### Known limitations
- Only Left/Right (vertical) edges; no Top/Bottom yet.
- Sized/placed using primary-monitor metrics; multi-monitor / mixed-DPI is not
  specially handled.
- Uses the generic Windows application icon (no custom icon yet).

[Unreleased]: https://example.com/compare/v0.1.0...HEAD
[0.1.0]: https://example.com/releases/tag/v0.1.0
