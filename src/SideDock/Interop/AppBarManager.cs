using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace SideDock.Interop;

/// <summary>Which screen edge the dock attaches to.</summary>
public enum DockEdge
{
    Left,
    Right,
}

/// <summary>
/// Wraps the Windows <b>AppBar API</b> (<c>SHAppBarMessage</c>) so a WPF window
/// can reserve a strip of screen space along an edge — exactly the supported,
/// documented mechanism the system taskbar itself uses.
///
/// This does NOT touch, hide, subclass, or inject into explorer.exe or the real
/// taskbar. We only register our OWN window as an appbar.
///
/// Docs: https://learn.microsoft.com/windows/win32/shell/application-desktop-toolbars
///       https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shappbarmessage
/// </summary>
public sealed class AppBarManager
{
    // ---- dwMessage values for SHAppBarMessage --------------------------------
    private const int ABM_NEW = 0x00000000;      // Register a new appbar.
    private const int ABM_REMOVE = 0x00000001;   // Unregister an appbar.
    private const int ABM_QUERYPOS = 0x00000002; // Ask the shell for an allowed rectangle.
    private const int ABM_SETPOS = 0x00000003;   // Commit the final rectangle (reserve the space).

    // ---- uEdge values (which screen edge to dock to) -------------------------
    private const int ABE_LEFT = 0;
    private const int ABE_TOP = 1;
    private const int ABE_RIGHT = 2;
    private const int ABE_BOTTOM = 3;

    // ---- GetSystemMetrics indices (primary monitor size, physical pixels) ----
    private const int SM_CXSCREEN = 0;
    private const int SM_CYSCREEN = 1;

    // A private window-message id the shell uses to notify our window about
    // appbar events (e.g. a full-screen app appearing). Any value >= WM_USER
    // is valid for an application-defined message. We register the value now;
    // handling these notifications is a refinement for a later phase.
    private const int APPBAR_CALLBACK = 0x0400 + 5; // WM_USER + 5

    // RECT as Win32 defines it: left/top/right/bottom in pixels.
    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int left, top, right, bottom;
    }

    // APPBARDATA is the struct passed to SHAppBarMessage. Field layout must
    // match the Win32 definition exactly.
    // https://learn.microsoft.com/windows/win32/api/shellapi/ns-shellapi-appbardata
    [StructLayout(LayoutKind.Sequential)]
    private struct APPBARDATA
    {
        public int cbSize;            // Size of this struct, in bytes.
        public IntPtr hWnd;           // Our appbar window handle.
        public uint uCallbackMessage; // App-defined notification message id.
        public uint uEdge;            // Which edge (ABE_*).
        public RECT rc;               // The bounding rectangle, in physical pixels.
        public IntPtr lParam;         // Message-specific value (unused here).
    }

    // SHAppBarMessage: sends an appbar message to the system.
    // https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shappbarmessage
    [DllImport("shell32.dll", CallingConvention = CallingConvention.StdCall)]
    private static extern uint SHAppBarMessage(int dwMessage, ref APPBARDATA pData);

    // GetSystemMetrics: returns various system measurements; we use it for the
    // primary monitor width/height in physical pixels.
    // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-getsystemmetrics
    [DllImport("user32.dll")]
    private static extern int GetSystemMetrics(int nIndex);

    // MoveWindow: moves/resizes a window using physical-pixel coordinates.
    // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-movewindow
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool MoveWindow(
        IntPtr hWnd, int X, int Y, int nWidth, int nHeight, bool bRepaint);

    private readonly Window _window;
    private IntPtr _hwnd;
    private bool _registered;

    /// <summary>The edge to dock to. Read each time we (re)position.</summary>
    public DockEdge Edge { get; set; } = DockEdge.Left;

    public AppBarManager(Window window) => _window = window;

    /// <summary>
    /// Registers the window as a left-edge appbar and positions it. Call this
    /// once the window's HWND exists (e.g. from OnSourceInitialized).
    /// </summary>
    public void Register()
    {
        // Grab the Win32 handle for our WPF window.
        _hwnd = new WindowInteropHelper(_window).Handle;

        // Idempotent: if we're already registered, remove the previous
        // registration first so we never issue a duplicate ABM_NEW for the same
        // window (which can happen if the edge changes while the dock is hidden).
        if (_registered)
            Unregister();

        var data = new APPBARDATA
        {
            cbSize = Marshal.SizeOf<APPBARDATA>(),
            hWnd = _hwnd,
            uCallbackMessage = APPBAR_CALLBACK,
        };

        // Step 1: ABM_NEW — tell the shell "this window is now an appbar".
        SHAppBarMessage(ABM_NEW, ref data);
        _registered = true;

        // Step 2+3: choose and commit the rectangle.
        SetPosition();
    }

    private void SetPosition()
    {
        // The AppBar API works in PHYSICAL pixels, but WPF sizes are in
        // device-independent units (1/96"). Convert using this window's DPI
        // scale so the bar is the right thickness on high-DPI displays.
        var source = (HwndSource?)PresentationSource.FromVisual(_window);
        double dpiScaleX = source?.CompositionTarget?.TransformToDevice.M11 ?? 1.0;
        int widthPx = (int)Math.Round(_window.Width * dpiScaleX);

        // Full size of the primary monitor, in physical pixels.
        int screenWidthPx = GetSystemMetrics(SM_CXSCREEN);
        int screenHeightPx = GetSystemMetrics(SM_CYSCREEN);

        bool right = Edge == DockEdge.Right;

        var data = new APPBARDATA
        {
            cbSize = Marshal.SizeOf<APPBARDATA>(),
            hWnd = _hwnd,
            uEdge = (uint)(right ? ABE_RIGHT : ABE_LEFT), // dock LEFT or RIGHT
        };

        // Propose a full-height strip down the chosen side.
        data.rc.top = 0;
        data.rc.bottom = screenHeightPx;
        if (right)
        {
            data.rc.left = screenWidthPx - widthPx;
            data.rc.right = screenWidthPx;
        }
        else
        {
            data.rc.left = 0;
            data.rc.right = widthPx;
        }

        // Step 2: ABM_QUERYPOS — the shell adjusts our proposed rectangle so it
        // doesn't collide with other appbars (such as the real taskbar). It may
        // change top/bottom (and the docked edge) on return.
        SHAppBarMessage(ABM_QUERYPOS, ref data);

        // Re-apply our desired thickness relative to the (possibly adjusted)
        // edge the shell handed back.
        if (right)
            data.rc.left = data.rc.right - widthPx;
        else
            data.rc.right = data.rc.left + widthPx;

        // Step 3: ABM_SETPOS — commit. From now on the shell keeps maximized
        // windows out of this rectangle.
        SHAppBarMessage(ABM_SETPOS, ref data);

        // Finally, move our actual window into the reserved rectangle. These are
        // physical pixels, which is exactly what MoveWindow expects.
        MoveWindow(
            _hwnd,
            data.rc.left,
            data.rc.top,
            data.rc.right - data.rc.left,
            data.rc.bottom - data.rc.top,
            bRepaint: true);
    }

    /// <summary>
    /// Removes the appbar reservation. Must be called before/at window close so
    /// the shell stops reserving our strip of screen space.
    /// </summary>
    public void Unregister()
    {
        if (!_registered) return;

        var data = new APPBARDATA
        {
            cbSize = Marshal.SizeOf<APPBARDATA>(),
            hWnd = _hwnd,
        };

        // ABM_REMOVE — give the space back to the system.
        SHAppBarMessage(ABM_REMOVE, ref data);
        _registered = false;
    }
}
