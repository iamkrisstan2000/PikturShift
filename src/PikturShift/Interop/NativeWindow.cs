using System.Runtime.InteropServices;
using System.Windows.Interop;
using System.Windows.Media;

namespace PikturShift.Interop;

/// <summary>
/// Small, safe Windows 11 interop layer: rounded window corners, native dark caption
/// (so system menus and shadow behave) and optional Mica backdrop behind the frame.
/// Every call is no-op on older Windows builds.
/// </summary>
internal static class NativeWindow
{
    private const int DwmwaUseImmersiveDarkMode = 20;
    private const int DwmwaUseImmersiveDarkModeBefore20H1 = 19;
    private const int DwmwaWindowCornerPreference = 33;
    private const int DwmwaSystemBackdropType = 38;

    private const int CornerRound = 2;
    private const int DwmsbtMainWindow = 2;   // Mica, the Windows 11 wallpaper-tinted backdrop

    [StructLayout(LayoutKind.Sequential)]
    private struct Margins
    {
        public int Left, Right, Top, Bottom;
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmExtendFrameIntoClientArea(IntPtr hwnd, ref Margins margins);

    [DllImport("dwmapi.dll", SetLastError = true)]
    private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attribute, ref int value, int size);

    public static void ApplyRoundedCorners(IntPtr hwnd)
    {
        int preference = CornerRound;
        TrySet(hwnd, DwmwaWindowCornerPreference, preference);
    }

    public static void SetImmersiveDarkMode(IntPtr hwnd, bool dark)
    {
        int value = dark ? 1 : 0;
        if (TrySet(hwnd, DwmwaUseImmersiveDarkMode, value) != 0)
        {
            TrySet(hwnd, DwmwaUseImmersiveDarkModeBefore20H1, value);
        }
    }

    /// <summary>
    /// Turns on the Mica backdrop so translucent surfaces have something real to show.
    /// Returns false on anything older than Windows 11 22H2 (or without composition), in
    /// which case the caller keeps every surface fully opaque.
    /// </summary>
    public static bool TryEnableBackdrop(IntPtr hwnd)
    {
        try
        {
            // Ask for the backdrop first. The attribute is unknown on Windows 10 and on
            // pre-22H2 Windows 11, where it fails - and in that case the frame must NOT be
            // extended, otherwise the client area turns into glass with nothing behind it.
            int backdrop = DwmsbtMainWindow;
            if (TrySet(hwnd, DwmwaSystemBackdropType, backdrop) != 0)
            {
                return false;
            }

            var margins = new Margins { Left = -1, Right = -1, Top = -1, Bottom = -1 };
            DwmExtendFrameIntoClientArea(hwnd, ref margins);
            return true;
        }
        catch (DllNotFoundException)
        {
            return false;
        }
        catch (EntryPointNotFoundException)
        {
            return false;
        }
    }

    /// <summary>
    /// Controls what WPF composites underneath its own (possibly translucent) layers.
    /// Transparent lets the Mica backdrop show through; anything else would be painted over.
    /// </summary>
    public static void SetCompositionBackground(IntPtr hwnd, Color color)
    {
        try
        {
            var source = HwndSource.FromHwnd(hwnd);
            if (source?.CompositionTarget is not null)
            {
                source.CompositionTarget.BackgroundColor = color;
            }
        }
        catch
        {
            // Purely cosmetic; ignore on hosts without a composition target.
        }
    }

    private static int TrySet(IntPtr hwnd, int attribute, int value)
    {
        try
        {
            return DwmSetWindowAttribute(hwnd, attribute, ref value, sizeof(int));
        }
        catch (DllNotFoundException)
        {
            return -1;
        }
        catch (EntryPointNotFoundException)
        {
            return -1;
        }
    }
}
