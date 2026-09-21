// src/ChiikawaDesktopPet.Wpf/GlobalMouseHook.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChiikawaDesktopPet.Wpf;

public sealed class GlobalMouseHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.HookProc _hookProc;
    private bool _isDisposed;

    public event Action<int, int>? MouseMove;
    public event Action<bool>? MouseDown; // true for left button, false for right/other
    public event Action<bool>? MouseUp;   // true for left button, false for right/other

    public bool IsActive => _hookId != IntPtr.Zero;

    public GlobalMouseHook()
    {
        // Maintain a strong reference to prevent GC from collecting the callback delegate
        _hookProc = HookCallback;
    }

    public void Start()
    {
        if (_isDisposed || _hookId != IntPtr.Zero) return;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr hMod = curModule != null ? NativeMethods.GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;

        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc, hMod, 0);
    }

    public void Stop()
    {
        if (_hookId != IntPtr.Zero)
        {
            NativeMethods.UnhookWindowsHookEx(_hookId);
            _hookId = IntPtr.Zero;
        }
    }

    private nint HookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0 && lParam != IntPtr.Zero)
        {
            var mouseStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);

            switch ((int)wParam)
            {
                case NativeMethods.WM_MOUSEMOVE:
                    MouseMove?.Invoke(mouseStruct.pt.X, mouseStruct.pt.Y);
                    break;

                case NativeMethods.WM_LBUTTONDOWN:
                    MouseDown?.Invoke(true);
                    break;

                case NativeMethods.WM_RBUTTONDOWN:
                    MouseDown?.Invoke(false);
                    break;

                case NativeMethods.WM_LBUTTONUP:
                    MouseUp?.Invoke(true);
                    break;

                case NativeMethods.WM_RBUTTONUP:
                    MouseUp?.Invoke(false);
                    break;
            }
        }

        return NativeMethods.CallNextHookEx(_hookId, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (!_isDisposed)
        {
            Stop();
            _isDisposed = true;
        }
    }
}
