# SideDock

A lightweight, **open-source, customizable vertical dock / launcher for Windows 11**,
built with **C# / .NET 8 / WPF**.

SideDock is a **standalone application** — it is its own window and uses only
documented Windows APIs. It does **not** inject into `explorer.exe`, and it does
**not** modify, hide, or replace the system taskbar. To reserve its space on the
screen it uses the official Windows **AppBar API** (`SHAppBarMessage`), the same
supported mechanism the system taskbar itself is built on.

## Goals
- A clean vertical bar docked to a screen edge that **reserves its space**, so
  maximized windows don't overlap it.
- A fast, low-overhead **app launcher** driven by a simple config file.
- Optional **clock**, **system tray**, and a **list of open windows**.
- **Customizable** theme, size, and position.
- Friendly, well-commented code that's easy to learn from and extend.

## Requirements
- Windows 11 (also expected to work on Windows 10).
- **.NET 8 SDK** *(this machine has the .NET 8 desktop runtime plus a newer SDK
  that can build `net8.0-windows` targets).*
- Visual Studio 2022 (Community is fine) **or** the `dotnet` CLI.

## Build & run

### Option A — Visual Studio
1. Open `SideDock.sln`.
2. Set **SideDock** as the startup project (it is the only project).
3. Press **F5** (Debug) or **Ctrl+F5** (Run without debugging).

### Option B — command line
```powershell
dotnet run --project src/SideDock/SideDock.csproj
```

To produce a build without running:
```powershell
dotnet build SideDock.sln -c Release
```

## What you should see
A slim dark vertical bar pinned to a screen edge (Left by default), always on
top, reserving its space so maximized windows don't cover it. On the bar:
**app launcher icons** at the top, a **list of open windows** below a divider,
and a **clock** at the bottom. Click an app icon to launch it; click a window
icon to focus that window. **Right-click the bar** (or use the system-tray icon)
to hide the dock, open the config file, or exit.

## Customizing the dock
SideDock reads `%APPDATA%\SideDock\config.json` and **applies edits live** (no
restart needed). Open it from the dock's right-click menu → *Open config file*.

```json
{
  "Edge": "Left",            // "Left" or "Right"
  "BarThickness": 64,        // bar width in pixels
  "IconSize": 32,            // app icon size in pixels
  "WindowIconSize": 26,      // open-window icon size in pixels
  "ShowClock": true,         // show the clock at the bottom
  "ShowDate": true,          // show the date under the time
  "Use24HourClock": true,    // true = 14:05, false = 2:05 PM
  "ShowSeconds": false,      // include seconds (14:05:09)
  "ShowOpenWindows": true,   // show the list of open windows
  "Theme": {
    "BackgroundTop": "#FF1E1E2E",
    "BackgroundBottom": "#FF11111B",
    "BackgroundOpacity": 0.0,         // 0.0 = fully transparent, 1.0 = solid
    "HoverColor": "#33FFFFFF",
    "PressedColor": "#55FFFFFF",
    "TimeColor": "#FFE6E6F0",
    "DateColor": "#FF9A9AB0",
    "TimeFontSize": 15,
    "DateFontSize": 10,
    "FontFamily": "Segoe UI",
    "DividerColor": "#33FFFFFF",
    "ButtonCornerRadius": 8
  },
  "Apps": [
    { "Name": "Notepad", "Path": "C:\\Windows\\System32\\notepad.exe" }
  ]
}
```
**Transparency:** the bar is **fully transparent by default**
(`BackgroundOpacity: 0.0`) — only the icons and clock show, with the desktop
visible behind. Raise `BackgroundOpacity` toward `1.0` for a solid/tinted bar.
Colors are hex **ARGB** (`#AARRGGBB`). Per-app optional fields: `Arguments` and
`IconPath` (a custom `.ico`/`.png`). Save the file and the dock updates live.

## Roadmap
See [`ROADMAP.md`](ROADMAP.md). Short version:
1. Docked vertical bar (AppBar) ✅
2. App launcher ✅
3. Clock + system tray ✅
4. Open-windows list ✅
5. Customization + performance ✅

## License
[MIT](LICENSE) — free to use, modify, and distribute. Contributions welcome.
