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
            // A built-in system icon as a placeholder; a custom icon comes in
            // the Phase 5 customization pass.
            Icon = SystemIcons.Application,
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
}
