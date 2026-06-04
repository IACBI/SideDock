using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Media;

namespace SideDock.Interop;

/// <summary>One open top-level window we want to show in the dock.</summary>
public sealed class OpenWindowInfo
{
    public IntPtr Handle { get; init; }
    public string Title { get; init; } = "";
    public bool IsMinimized { get; init; }
    // Note: the icon is intentionally NOT fetched here. Icon retrieval uses a
    // cross-process WM_GETICON message, so we fetch it lazily (only when a
    // button is actually built) via WindowEnumerator.GetIcon — not on every poll.
}

/// <summary>
/// Lists the user's open application windows and activates them, using only
/// documented Win32 APIs. This reads window state from the outside; it does NOT
/// inject into or subclass any other process.
///
/// NOTE: The *Ptr entry points (GetWindowLongPtrW, GetClassLongPtrW) assume a
/// 64-bit process, which is the case on modern Windows 11.
/// </summary>
public static class WindowEnumerator
{
    // ---- EnumWindows + its callback -----------------------------------------
    // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-enumwindows
    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    // IsIconic = is the window minimized?
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool IsIconic(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowTextLength(IntPtr hWnd);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

    // GetWindow with GW_OWNER tells us if a window is owned by another (e.g. a
    // dialog) so we can skip those and keep only top-level app windows.
    [DllImport("user32.dll")]
    private static extern IntPtr GetWindow(IntPtr hWnd, uint uCmd);

    // Reads the extended window styles (to detect tool/app windows).
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    // DWM "cloaked" attribute lets us skip hidden UWP/virtual-desktop ghosts.
    // https://learn.microsoft.com/windows/win32/api/dwmapi/nf-dwmapi-dwmgetwindowattribute
    [DllImport("dwmapi.dll")]
    private static extern int DwmGetWindowAttribute(
        IntPtr hWnd, int dwAttribute, out int pvAttribute, int cbAttribute);

    // WM_GETICON asks a window for its icon; SendMessageTimeout avoids hanging
    // if the target window is not responding.
    [DllImport("user32.dll", CharSet = CharSet.Auto)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam,
        uint fuFlags, uint uTimeout, out IntPtr lpdwResult);

    [DllImport("user32.dll", EntryPoint = "GetClassLongPtrW")]
    private static extern IntPtr GetClassLongPtr(IntPtr hWnd, int nIndex);

    // Activation APIs.
    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

    // ---- Constants ----------------------------------------------------------
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x00000080; // small floating toolbox window
    private const long WS_EX_APPWINDOW = 0x00040000;  // forces a taskbar button
    private const uint GW_OWNER = 4;
    private const int DWMWA_CLOAKED = 14;

    private const uint WM_GETICON = 0x007F;
    private const int ICON_BIG = 1;
    private const int ICON_SMALL2 = 2;
    private const int GCLP_HICON = -14;
    private const int GCLP_HICONSM = -34;
    private const uint SMTO_ABORTIFHUNG = 0x0002;

    private const int SW_RESTORE = 9;

    /// <summary>
    /// Returns the list of open app windows, excluding our own dock window.
    /// </summary>
    public static List<OpenWindowInfo> GetOpenWindows(IntPtr selfHandle)
    {
        var results = new List<OpenWindowInfo>();

        // EnumWindows calls our callback once per top-level window.
        EnumWindows((hWnd, _) =>
        {
            if (hWnd == selfHandle)         return true; // skip ourselves
            if (!IsAppWindow(hWnd))         return true; // skip non-app windows

            string title = GetTitle(hWnd);
            if (title.Length == 0)          return true; // skip untitled windows

            results.Add(new OpenWindowInfo
            {
                Handle = hWnd,
                Title = title,
                IsMinimized = IsIconic(hWnd),
            });

            return true; // return true to keep enumerating
        }, IntPtr.Zero);

        return results;
    }

    /// <summary>Brings a window to the foreground, restoring it if minimized.</summary>
    public static void Activate(IntPtr hWnd)
    {
        if (IsIconic(hWnd))
            ShowWindow(hWnd, SW_RESTORE);

        SetForegroundWindow(hWnd);
    }

    // Heuristic for "a window that should appear in a taskbar": visible, not
    // cloaked, not a tool window, and either top-level (no owner) or explicitly
    // flagged as an app window. This mirrors the common alt-tab rules.
    private static bool IsAppWindow(IntPtr hWnd)
    {
        if (!IsWindowVisible(hWnd)) return false;
        if (IsCloaked(hWnd))        return false;

        long exStyle = GetWindowLongPtr(hWnd, GWL_EXSTYLE).ToInt64();
        bool isToolWindow = (exStyle & WS_EX_TOOLWINDOW) != 0;
        bool isAppWindow = (exStyle & WS_EX_APPWINDOW) != 0;

        if (isToolWindow) return false;

        bool hasOwner = GetWindow(hWnd, GW_OWNER) != IntPtr.Zero;
        return !hasOwner || isAppWindow;
    }

    private static bool IsCloaked(IntPtr hWnd)
    {
        // DwmGetWindowAttribute returns S_OK (0) on success; non-zero "cloaked"
        // means the window is hidden by DWM (e.g. on another virtual desktop).
        int result = DwmGetWindowAttribute(hWnd, DWMWA_CLOAKED, out int cloaked, sizeof(int));
        return result == 0 && cloaked != 0;
    }

    private static string GetTitle(IntPtr hWnd)
    {
        int length = GetWindowTextLength(hWnd);
        if (length == 0) return string.Empty;

        var buffer = new StringBuilder(length + 1);
        GetWindowText(hWnd, buffer, buffer.Capacity);
        return buffer.ToString();
    }

    /// <summary>
    /// Fetches a window's icon (lazily — only call this when actually building a
    /// button, since it sends a cross-process WM_GETICON message).
    /// </summary>
    public static ImageSource? GetIcon(IntPtr hWnd)
    {
        IntPtr hIcon = IntPtr.Zero;

        // Ask the window for its big icon (200 ms timeout so a hung app can't
        // freeze the dock).
        SendMessageTimeout(hWnd, WM_GETICON, (IntPtr)ICON_BIG, IntPtr.Zero,
            SMTO_ABORTIFHUNG, 200, out IntPtr result);
        if (result != IntPtr.Zero) hIcon = result;

        if (hIcon == IntPtr.Zero)
        {
            SendMessageTimeout(hWnd, WM_GETICON, (IntPtr)ICON_SMALL2, IntPtr.Zero,
                SMTO_ABORTIFHUNG, 200, out result);
            if (result != IntPtr.Zero) hIcon = result;
        }

        // Fall back to the window class icon.
        if (hIcon == IntPtr.Zero) hIcon = GetClassLongPtr(hWnd, GCLP_HICON);
        if (hIcon == IntPtr.Zero) hIcon = GetClassLongPtr(hWnd, GCLP_HICONSM);

        // These icons are owned by the window/class, so we must NOT destroy them.
        return IconExtractor.FromHIcon(hIcon);
    }
}
