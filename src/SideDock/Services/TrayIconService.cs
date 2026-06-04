using System.Drawing;          // SystemIcons (System.Drawing.Common, via WinForms)
using System.Windows.Forms;    // NotifyIcon, ContextMenuStrip

namespace SideDock.Services;

/// <summary>
/// Owns the notification-area (system tray) icon using the standard
/// <see cref="NotifyIcon"/> from Windows Forms. The icon gives the user a way
/// to show/hide the dock, open the config file, and exit cleanly.
///
/// The caller passes in the actions to run for each menu item, so this class
/// stays decoupled from the window itself.
/// </summary>
public sealed class TrayIconService : IDisposable
{
    private readonly NotifyIcon _notifyIcon;

    public TrayIconService(Action onToggleVisibility, Action onOpenConfig, Action onExit)
    {
        // Right-click menu for the tray icon.
        var menu = new ContextMenuStrip();
        menu.Items.Add("Show / Hide dock", null, (_, _) => onToggleVisibility());
        menu.Items.Add("Open config file", null, (_, _) => onOpenConfig());
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => onExit());

        _notifyIcon = new NotifyIcon
        {
            Icon = LoadAppIcon(),
            Text = "SideDock",         // tooltip shown on hover
            Visible = true,
            ContextMenuStrip = menu,
        };

        // Double-clicking the tray icon also toggles the dock.
        _notifyIcon.DoubleClick += (_, _) => onToggleVisibility();
    }

    public void Dispose()
    {
        // Hide before disposing so the icon disappears immediately instead of
        // lingering until the user hovers over the tray.
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
    }

    // Loads the app's own .ico (bundled as a WPF resource), falling back to a
    // built-in system icon if it can't be loaded.
    private static Icon LoadAppIcon()
    {
        try
        {
            var info = System.Windows.Application.GetResourceStream(
                new Uri("pack://application:,,,/SideDock.ico"));
            if (info is not null)
            {
                using var stream = info.Stream;
                return new Icon(stream);
            }
        }
        catch
        {
            // Fall back below.
        }
        return SystemIcons.Application;
    }
}
