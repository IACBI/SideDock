using Microsoft.Win32;

namespace SideDock.Services;

/// <summary>
/// Enables/disables "start with Windows" by adding or removing a value under the
/// per-user Run key (HKCU\Software\Microsoft\Windows\CurrentVersion\Run). This is
/// the documented, standard auto-start mechanism and only touches the current
/// user's own registry — no system-wide or admin changes.
/// </summary>
public static class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "SideDock";

    /// <summary>
    /// Makes the Run-key entry match <paramref name="enabled"/>. Best-effort:
    /// failures (e.g. policy-locked registry) are swallowed so they can't crash
    /// the app.
    /// </summary>
    public static void Apply(bool enabled)
    {
        try
        {
            // Path to our own executable, quoted in case it contains spaces.
            string? exePath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(exePath))
                return;

            using RegistryKey key = Registry.CurrentUser.CreateSubKey(RunKeyPath);

            if (enabled)
            {
                key.SetValue(ValueName, $"\"{exePath}\"");
            }
            else if (key.GetValue(ValueName) is not null)
            {
                key.DeleteValue(ValueName, throwOnMissingValue: false);
            }
        }
        catch
        {
            // Best-effort only.
        }
    }
}
