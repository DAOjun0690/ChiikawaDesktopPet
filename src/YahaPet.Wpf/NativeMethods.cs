// src/YahaPet.Wpf/NativeMethods.cs
using System;
using System.Runtime.InteropServices;

namespace YahaPet.Wpf;

internal static class NativeMethods
{
    private const int GWL_EXSTYLE = -20;
    private const int WS_EX_TOOLWINDOW = 0x00000080;
    private const int WS_EX_APPWINDOW = 0x00040000;

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int GetWindowLong(IntPtr hWnd, int nIndex);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern int SetWindowLong(IntPtr hWnd, int nIndex, int dwNewLong);

    /// Removes the window from the taskbar and Alt-Tab list, matching the original's
    /// Qt.WindowType.Tool flag. WPF's ShowInTaskbar=False alone does not fully replicate this.
    public static void MakeToolWindow(IntPtr hwnd)
    {
        int exStyle = GetWindowLong(hwnd, GWL_EXSTYLE);
        exStyle = (exStyle | WS_EX_TOOLWINDOW) & ~WS_EX_APPWINDOW;
        SetWindowLong(hwnd, GWL_EXSTYLE, exStyle);
    }
}
