using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SideDock.Interop;

/// <summary>
/// Extracts the icon for a file/executable using the documented Win32
/// <c>SHGetFileInfo</c> API, and converts it into a WPF <see cref="ImageSource"/>
/// with <c>Imaging.CreateBitmapSourceFromHIcon</c> (built into WPF — no extra
/// NuGet package needed).
///
/// Docs: https://learn.microsoft.com/windows/win32/api/shellapi/nf-shellapi-shgetfileinfow
/// </summary>
public static class IconExtractor
{
    private const uint SHGFI_ICON = 0x000000100;      // Retrieve the file's icon (fills hIcon).
    private const uint SHGFI_LARGEICON = 0x000000000; // Ask for the large (usually 32x32) icon.

    // SHFILEINFO receives the icon handle and some text fields. Layout/charset
    // must match the Win32 (Unicode "W") definition exactly.
    // https://learn.microsoft.com/windows/win32/api/shellapi/ns-shellapi-shfileinfow
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;          // Handle to the icon (we must free this).
        public int iIcon;
        public uint dwAttributes;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;
        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(
        string pszPath, uint dwFileAttributes, ref SHFILEINFO psfi, uint cbFileInfo, uint uFlags);

    // DestroyIcon frees the native icon handle that SHGetFileInfo created.
    // https://learn.microsoft.com/windows/win32/api/winuser/nf-winuser-destroyicon
    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    /// <summary>
    /// Returns the file's icon as a WPF image, or <c>null</c> if it can't be
    /// read (e.g. the path doesn't exist).
    /// </summary>
    public static ImageSource? TryGetIcon(string filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return null;

        var info = new SHFILEINFO();
        IntPtr result = SHGetFileInfo(
            filePath,
            0,
            ref info,
            (uint)Marshal.SizeOf<SHFILEINFO>(),
            SHGFI_ICON | SHGFI_LARGEICON);

        // A zero return (or no icon handle) means the call gave us nothing.
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
            return null;

        try
        {
            // Convert the native HICON into a WPF bitmap source.
            ImageSource source = Imaging.CreateBitmapSourceFromHIcon(
                info.hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());

            // Freeze makes it immutable + lightweight (safe to reuse, no leaks).
            source.Freeze();
            return source;
        }
        finally
        {
            // Always release the native icon handle to avoid leaking GDI objects.
            DestroyIcon(info.hIcon);
        }
    }

    /// <summary>
    /// Converts an existing native icon handle (HICON) into a WPF image.
    /// Does NOT destroy the handle — used for window/class icons that are owned
    /// by Windows and must not be freed by us.
    /// </summary>
    public static ImageSource? FromHIcon(IntPtr hIcon)
    {
        if (hIcon == IntPtr.Zero)
            return null;

        try
        {
            ImageSource source = Imaging.CreateBitmapSourceFromHIcon(
                hIcon,
                Int32Rect.Empty,
                BitmapSizeOptions.FromEmptyOptions());
            source.Freeze();
            return source;
        }
        catch
        {
            return null;
        }
    }
}
