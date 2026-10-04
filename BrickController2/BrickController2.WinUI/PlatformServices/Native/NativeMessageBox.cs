#if DEBUG
using System;
using System.Runtime.InteropServices;

namespace BrickController2.Windows.PlatformServices.Native;

internal static class NativeMessageBox
{
    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int MessageBoxW(IntPtr hWnd, string text, string caption, uint type);

    private const uint MB_ICONERROR = 0x00000010;
    private const uint MB_TOPMOST = 0x00040000;

    public static void Show(string title, string text)
        => MessageBoxW(IntPtr.Zero, text, title, MB_ICONERROR | MB_TOPMOST);
}
#endif