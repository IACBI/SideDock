using System.Diagnostics;
using SideDock.Models;

namespace SideDock.Services;

/// <summary>
/// Launches an <see cref="AppEntry"/> as a normal child process — the same as
/// double-clicking it in Explorer. No injection, no elevation tricks.
/// </summary>
public static class AppLauncher
{
    public static void Launch(AppEntry app)
    {
        var startInfo = new ProcessStartInfo
        {
            FileName = app.Path,
            Arguments = app.Arguments ?? string.Empty,
            // UseShellExecute = true lets the OS shell resolve and launch the
            // target (handles file associations, Store-app stubs like calc.exe,
            // working directory, etc.) — i.e. exactly like double-clicking it.
            UseShellExecute = true,
        };

        Process.Start(startInfo);
    }
}
