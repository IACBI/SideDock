namespace SideDock.Models;

/// <summary>
/// One launchable item shown in the dock. These properties map 1:1 to the
/// JSON config file, so the field names are also the JSON keys.
/// </summary>
public sealed class AppEntry
{
    /// <summary>Display name, shown as the button tooltip.</summary>
    public string Name { get; set; } = "";

    /// <summary>
    /// Full path to the executable (or a shell target) to launch.
    /// Example: C:\Windows\System32\notepad.exe
    /// </summary>
    public string Path { get; set; } = "";

    /// <summary>Optional command-line arguments passed when launching.</summary>
    public string? Arguments { get; set; }

    /// <summary>
    /// Optional path to a custom icon (.ico/.png). If omitted, the icon is
    /// extracted automatically from the executable at <see cref="Path"/>.
    /// </summary>
    public string? IconPath { get; set; }
}

/// <summary>
/// Colors used to style the dock. Each value is a hex ARGB string like
/// "#FF1E1E2E" (alpha, red, green, blue).
/// </summary>
public sealed class ThemeSettings
{
    /// <summary>Top color of the bar's vertical background gradient (ARGB).</summary>
    public string BackgroundTop { get; set; } = "#FF1E1E2E";

    /// <summary>Bottom color of the bar's vertical background gradient (ARGB).</summary>
    public string BackgroundBottom { get; set; } = "#FF11111B";

    /// <summary>
    /// How opaque the bar background is, 0.0 (fully see-through — only the icons
    /// and clock show, desktop visible behind) to 1.0 (solid). This only affects
    /// the background, not the icons/text. Default is fully transparent.
    /// </summary>
    public double BackgroundOpacity { get; set; } = 0.0;

    /// <summary>Highlight color shown when hovering a button.</summary>
    public string HoverColor { get; set; } = "#33FFFFFF";

    /// <summary>Highlight color shown while a button is pressed.</summary>
    public string PressedColor { get; set; } = "#55FFFFFF";

    /// <summary>Clock (time) text color.</summary>
    public string TimeColor { get; set; } = "#FFE6E6F0";

    /// <summary>Clock (date) text color.</summary>
    public string DateColor { get; set; } = "#FF9A9AB0";

    /// <summary>Font size of the time text, in points.</summary>
    public double TimeFontSize { get; set; } = 15;

    /// <summary>Font size of the date text, in points.</summary>
    public double DateFontSize { get; set; } = 10;

    /// <summary>Font family used for the clock/date (e.g. "Segoe UI").</summary>
    public string FontFamily { get; set; } = "Segoe UI";

    /// <summary>Color of the divider between apps and open windows.</summary>
    public string DividerColor { get; set; } = "#33FFFFFF";

    /// <summary>Corner roundness of the button hover/press highlight, in pixels.</summary>
    public double ButtonCornerRadius { get; set; } = 8;
}

/// <summary>
/// Root object of the JSON config file: the apps to launch plus the dock's
/// appearance and placement. Hand-editing this file updates the dock live.
/// </summary>
public sealed class DockConfig
{
    /// <summary>Which screen edge to dock to: "Left" or "Right".</summary>
    public string Edge { get; set; } = "Left";

    /// <summary>Bar thickness (width for a vertical dock), in pixels.</summary>
    public double BarThickness { get; set; } = 64;

    /// <summary>Icon size for the app launcher buttons, in pixels.</summary>
    public double IconSize { get; set; } = 32;

    /// <summary>Icon size for the open-window buttons, in pixels.</summary>
    public double WindowIconSize { get; set; } = 26;

    /// <summary>Show the clock at the bottom of the bar.</summary>
    public bool ShowClock { get; set; } = true;

    /// <summary>Show the date under the time (only if the clock is shown).</summary>
    public bool ShowDate { get; set; } = true;

    /// <summary>Use a 24-hour clock (true) or 12-hour with AM/PM (false).</summary>
    public bool Use24HourClock { get; set; } = true;

    /// <summary>Include seconds in the time (e.g. 14:05:09).</summary>
    public bool ShowSeconds { get; set; } = false;

    /// <summary>Show the list of currently open windows.</summary>
    public bool ShowOpenWindows { get; set; } = true;

    /// <summary>Launch SideDock automatically when the user signs in.</summary>
    public bool StartWithWindows { get; set; } = false;

    /// <summary>Colors / theme.</summary>
    public ThemeSettings Theme { get; set; } = new();

    /// <summary>The apps shown as launch buttons.</summary>
    public List<AppEntry> Apps { get; set; } = new();
}
