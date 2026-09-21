// src/ChiikawaDesktopPet.Wpf/GlobalKeyboardHook.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace ChiikawaDesktopPet.Wpf;

public sealed class GlobalKeyboardHook : IDisposable
{
    private IntPtr _hookId = IntPtr.Zero;
    private readonly NativeMethods.HookProc _hookProc;
    private bool _isDisposed;

    public event Action<int>? KeyDown;
    public event Action<int>? KeyUp;

    public bool IsActive => _hookId != IntPtr.Zero;

    public GlobalKeyboardHook()
    {
        // Maintain a strong reference to prevent GC collecting the delegate
        _hookProc = HookCallback;
    }

    public void Start()
    {
        if (_isDisposed || _hookId != IntPtr.Zero) return;

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr hMod = curModule != null ? NativeMethods.GetModuleHandle(curModule.ModuleName) : IntPtr.Zero;

        _hookId = NativeMethods.SetWindowsHookEx(NativeMethods.WH_KEYBOARD_LL, _hookProc, hMod, 0);
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
            var kbd = Marshal.PtrToStructure<NativeMethods.KBDLLHOOKSTRUCT>(lParam);
            int vkCode = (int)kbd.vkCode;

            if (wParam == NativeMethods.WM_KEYDOWN || wParam == NativeMethods.WM_SYSKEYDOWN)
            {
                KeyDown?.Invoke(vkCode);
            }
            else if (wParam == NativeMethods.WM_KEYUP || wParam == NativeMethods.WM_SYSKEYUP)
            {
                KeyUp?.Invoke(vkCode);
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
