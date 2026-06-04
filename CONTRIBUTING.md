# Contributing to SideDock

Thanks for your interest in improving SideDock! Contributions of all kinds are
welcome — bug reports, ideas, docs, and code.

## Golden rule
SideDock is a **standalone application** and must stay that way:

- **No process injection.** Do not inject code into `explorer.exe` or any other
  process.
- **No modifying system components.** Do not hide, replace, subclass, or hook
  the system taskbar or other parts of the shell.
- **Documented APIs only.** Use real .NET BCL APIs and **documented** Win32 APIs
  via P/Invoke. No undocumented/internal calls, no invented packages.

Any PR that violates these will be declined regardless of how useful it is.

## Project layout
```
src/SideDock/
├─ App.xaml / .cs              app entry point + global styles + crash handler
├─ MainWindow.xaml / .cs       the dock window and its logic
├─ Models/DockConfig.cs        config model (maps 1:1 to config.json)
├─ Services/                   ConfigService, AppLauncher, TrayIconService
└─ Interop/                    AppBarManager, IconExtractor, WindowEnumerator
```
See [`CLAUDE.md`](CLAUDE.md) for design notes and gotchas.

## Building
- Requires the **.NET 8 SDK** (or a newer SDK that can target `net8.0-windows`)
  and Windows.
- Build: `dotnet build SideDock.sln -c Debug`
- Run: `dotnet run --project src/SideDock/SideDock.csproj`
- A change is "done" when the solution builds with **0 warnings, 0 errors** and
  you've manually verified the affected behavior.

## Coding conventions
- Match the existing style: clear, **well-commented** code aimed at a
  beginner-to-intermediate C# reader.
- **Comment every P/Invoke / Win32 call** with what it does, ideally with a link
  to its Microsoft Learn page.
- Keep interop in `Interop/`, config logic in `Services/`.
- Nullable reference types and implicit usings are enabled.
- If you're unsure whether an API exists or behaves a certain way, verify it —
  don't guess.

## Commits & pull requests
1. Create a feature branch.
2. Keep commits focused, with clear messages explaining the *why*.
3. Make sure the build is green and the app runs.
4. Open a PR describing the change, how you tested it, and any screenshots for
   visual changes.
5. Update [`CHANGELOG.md`](CHANGELOG.md) under "Unreleased" when your change is
   user-visible.

## Reporting bugs
Open an issue with:
- Windows version (e.g. Windows 11 23H2, build number),
- what you did, what happened, what you expected,
- your `config.json` if the issue is configuration-related,
- screenshots if it's visual.

## License
By contributing, you agree your contributions are licensed under the project's
[MIT License](LICENSE).
