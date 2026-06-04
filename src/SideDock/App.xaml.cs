using System.Windows;
using System.Windows.Threading;

namespace SideDock;

/// <summary>
/// Application entry point. WPF wires this up automatically from App.xaml
/// (StartupUri points at MainWindow.xaml, so the window opens on launch).
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Safety net: if an unhandled exception bubbles up (e.g. from a timer
        // callback), free the AppBar reservation and tray icon before the app
        // terminates — otherwise a crash would leave a strip of screen space
        // reserved until the user signs out.
        DispatcherUnhandledException += OnUnhandledException;
    }

    private void OnUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        (MainWindow as MainWindow)?.ReleaseDockResources();
        // e.Handled stays false: we've cleaned up the OS-level resources, and
        // let the process terminate rather than continue in a bad state.
    }
}
