using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace IT_viddil_monitoring;

internal static class NativeWindowTheme
{
    public static void Apply(Window window, bool light)
    {
        if (!OperatingSystem.IsWindows()) return;
        var handle = new WindowInteropHelper(window).Handle;
        if (handle == IntPtr.Zero) return;
        try
        {
            if (SystemParameters.HighContrast)
            {
                var systemColor = unchecked((int)0xFFFFFFFF);
                DwmSetWindowAttribute(handle, 35, ref systemColor, sizeof(int));
                DwmSetWindowAttribute(handle, 36, ref systemColor, sizeof(int));
                return;
            }
            var dark = light ? 0 : 1;
            DwmSetWindowAttribute(handle, 20, ref dark, sizeof(int));
            var caption = light ? 0x00FBF7F6 : 0x00221816;
            var text = light ? 0x001F1D1D : 0x00F7F5F5;
            DwmSetWindowAttribute(handle, 35, ref caption, sizeof(int));
            DwmSetWindowAttribute(handle, 36, ref text, sizeof(int));
            var rounded = 2;
            DwmSetWindowAttribute(handle, 33, ref rounded, sizeof(int));
        }
        catch (DllNotFoundException) { }
        catch (EntryPointNotFoundException) { }
    }

    [DllImport("dwmapi.dll")]
    private static extern int DwmSetWindowAttribute(IntPtr window, int attribute, ref int value, int size);
}
