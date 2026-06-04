# SideDock

> A lightweight, **open-source, customizable vertical dock / launcher for Windows 11**, built with **C# / .NET 8 / WPF**.

**Version:** 0.1.0 · **License:** MIT

SideDock is a **standalone application** — it is its own window and uses only
documented Windows APIs. It does **not** inject into `explorer.exe`, and it does
**not** modify, hide, or replace the system taskbar. To reserve its space on the
screen it uses the official Windows **AppBar API** (`SHAppBarMessage`), the same
supported mechanism the system taskbar itself is built on.

## About
SideDock is a small vertical dock that sits along the side of your screen for
quick access to your favorite apps, a clock, and your open windows. It's built
as a friendly, well-commented open-source project — easy to read, tweak, and
learn from. Because it uses only documented Windows APIs and never touches the
system taskbar, it stays out of the way and plays nicely with Windows.

## Features
- **Edge-docked bar** (Left or Right) that **reserves its screen space** via the
  documented AppBar API, so maximized windows don't cover it.
- **App launcher** — icons read from a JSON config; click to launch.
- **Clock** (12/24-hour, optional seconds, optional date) at the bottom.
- **System-tray icon** + a **right-click menu on the bar** to hide, open the
  config, or exit.
- **Open-windows list** (taskbar-like) via documented Win32 calls; click to
  focus/restore a window. Minimized windows are dimmed.
- **Fully customizable & live-reloaded**: theme colors, transparency, sizes,
  fonts, docked edge — edit the config file and the dock updates without a
  restart.
- **Lightweight & well-commented**: timers pause while hidden, icons are fetched
  lazily, brushes are frozen; every Win32 call is documented in the source.

## Requirements
- **Windows 11** (expected to work on Windows 10 as well).
- **.NET 8 SDK** — or a newer .NET SDK that can build the `net8.0-windows`
  target. (The .NET 8 **Windows Desktop runtime** is required to run.)
- **Visual Studio 2022** (Community is fine) **or** the **`dotnet` CLI**.

## Build & run

### Option A — Visual Studio
1. Open `SideDock.sln`.
2. Set **SideDock** as the startup project (it is the only project).
3. Press **F5** (Debug) or **Ctrl+F5** (Run without debugging).

### Option B — command line
```powershell
# run
dotnet run --project src/SideDock/SideDock.csproj

# or build only
dotnet build SideDock.sln -c Release
```

## Usage
On launch you get a slim vertical bar pinned to the left edge (by default),
always on top, reserving its space. On the bar, top to bottom:

1. **App launcher icons** — click to launch.
2. A **divider**, then the **open-windows list** — click an icon to focus that
   window (it un-minimizes if needed).
3. The **clock**.

To **hide**, **open the config**, or **exit**: right-click anywhere on the bar,
or use the **SideDock system-tray icon** (Windows 11 may tuck it into the `^`
overflow flyout). When hidden, bring it back from the tray icon.

## Configuration
SideDock reads `%APPDATA%\SideDock\config.json` and **applies edits live** (no
restart). Open it via the right-click menu → *Open config file*. The file is
created with defaults on first run and upgraded with any new options on launch.

### Example
```json
{
  "Edge": "Left",
  "BarThickness": 64,
  "IconSize": 32,
  "WindowIconSize": 26,
  "ShowClock": true,
  "ShowDate": true,
  "Use24HourClock": true,
  "ShowSeconds": false,
  "ShowOpenWindows": true,
  "Theme": {
    "BackgroundTop": "#FF1E1E2E",
    "BackgroundBottom": "#FF11111B",
    "BackgroundOpacity": 0.0,
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
    { "Name": "Notepad", "Path": "C:\\Windows\\System32\\notepad.exe", "Arguments": null, "IconPath": null }
  ]
}
```

### Options reference
| Key | Type | Default | Description |
|---|---|---|---|
| `Edge` | string | `"Left"` | Docked edge: `"Left"` or `"Right"`. |
| `BarThickness` | number | `64` | Bar width in pixels. |
| `IconSize` | number | `32` | App icon size in pixels. |
| `WindowIconSize` | number | `26` | Open-window icon size in pixels. |
| `ShowClock` | bool | `true` | Show the clock. |
| `ShowDate` | bool | `true` | Show the date under the time. |
| `Use24HourClock` | bool | `true` | `true` → `14:05`; `false` → `2:05 PM`. |
| `ShowSeconds` | bool | `false` | Include seconds in the time. |
| `ShowOpenWindows` | bool | `true` | Show the open-windows list. |
| `Theme.BackgroundTop` / `BackgroundBottom` | ARGB | dark | Background gradient colors. |
| `Theme.BackgroundOpacity` | number | `0.0` | `0.0` = fully transparent, `1.0` = solid. |
| `Theme.HoverColor` / `PressedColor` | ARGB | white-ish | Button highlight colors. |
| `Theme.TimeColor` / `DateColor` | ARGB | light | Clock text colors. |
| `Theme.TimeFontSize` / `DateFontSize` | number | `15` / `10` | Clock font sizes. |
| `Theme.FontFamily` | string | `"Segoe UI"` | Clock font family. |
| `Theme.DividerColor` | ARGB | faint | Divider line color. |
| `Theme.ButtonCornerRadius` | number | `8` | Roundness of the button highlight. |
| `Apps[]` | array | 3 built-ins | `Name`, `Path`, optional `Arguments`, optional `IconPath`. |

**Colors** are hex **ARGB** (`#AARRGGBB`) — the first two digits are alpha
(`FF` = opaque, `00` = invisible). **Transparency:** the bar is fully
transparent by default; raise `Theme.BackgroundOpacity` for a solid/tinted bar.
If you enter invalid JSON or a bad color, the dock keeps its current look until
you fix the file.

## Roadmap
See [`ROADMAP.md`](ROADMAP.md). All five planned phases are complete:
1. Docked vertical bar (AppBar) ✅
2. App launcher ✅
3. Clock + system tray ✅
4. Open-windows list ✅
5. Customization + performance ✅

## Contributing
Contributions are welcome — see [`CONTRIBUTING.md`](CONTRIBUTING.md). Core rule:
SideDock stays a standalone app using only documented APIs (no process
injection, no modifying the system taskbar or other system components).

## Changelog
See [`CHANGELOG.md`](CHANGELOG.md).

## License
[MIT](LICENSE) © 2026 A.C.B — free to use, modify, and distribute.
