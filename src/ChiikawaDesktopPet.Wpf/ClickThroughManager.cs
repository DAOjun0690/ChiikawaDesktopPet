// src/ChiikawaDesktopPet.Wpf/ClickThroughManager.cs
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Interop;

namespace ChiikawaDesktopPet.Wpf;

public sealed class ClickThroughManager
{
    public static ClickThroughManager Instance { get; } = new();

    private readonly HashSet<CharacterWindow> _registeredWindows = [];
    private readonly object _lock = new();

    private IntPtr _hookHandle = IntPtr.Zero;
    private NativeMethods.HookProc? _hookProc;

    private ClickThroughManager()
    {
    }

    public void Register(CharacterWindow window)
    {
        lock (_lock)
        {
            _registeredWindows.Add(window);
            if (_hookHandle == IntPtr.Zero)
            {
                InstallHook();
            }
        }
    }

    public void Unregister(CharacterWindow window)
    {
        lock (_lock)
        {
            _registeredWindows.Remove(window);
            if (_registeredWindows.Count == 0 && _hookHandle != IntPtr.Zero)
            {
                UninstallHook();
            }
        }
    }

    private void InstallHook()
    {
        if (_hookHandle != IntPtr.Zero) return;

        _hookProc = MouseHookCallback;
        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        IntPtr moduleHandle = NativeMethods.GetModuleHandle(curModule?.ModuleName);
        _hookHandle = NativeMethods.SetWindowsHookEx(NativeMethods.WH_MOUSE_LL, _hookProc, moduleHandle, 0);
    }

    private void UninstallHook()
    {
        if (_hookHandle == IntPtr.Zero) return;

        NativeMethods.UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
        _hookProc = null;
    }

    private nint MouseHookCallback(int nCode, nint wParam, nint lParam)
    {
        if (nCode >= 0)
        {
            int msg = (int)wParam;
            if (msg == NativeMethods.WM_RBUTTONDOWN || msg == NativeMethods.WM_RBUTTONUP)
            {
                var hookStruct = Marshal.PtrToStructure<NativeMethods.MSLLHOOKSTRUCT>(lParam);
                var pt = hookStruct.pt;
                var hitWindow = FindHitWindow(pt);

                if (hitWindow != null)
                {
                    if (msg == NativeMethods.WM_RBUTTONUP)
                    {
                        hitWindow.Dispatcher.BeginInvoke(() =>
                        {
                            hitWindow.TriggerContextMenuFromHook();
                        });
                    }

                    // Block the right-click from reaching underlying windows
                    return 1;
                }
            }
        }

        return NativeMethods.CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    private CharacterWindow? FindHitWindow(NativeMethods.POINT pt)
    {
        CharacterWindow[] windows;
        lock (_lock)
        {
            windows = _registeredWindows.ToArray();
        }

        // Search through registered windows
        foreach (var window in windows)
        {
            if (window.IsPetHidden || !window.IsLoaded) continue;

            IntPtr hwnd = window.Handle != IntPtr.Zero ? window.Handle : new WindowInteropHelper(window).Handle;
            if (hwnd == IntPtr.Zero) continue;

            if (NativeMethods.GetWindowRect(hwnd, out var rect))
            {
                if (pt.X >= rect.Left && pt.X <= rect.Right && pt.Y >= rect.Top && pt.Y <= rect.Bottom)
                {
                    return window;
                }
            }
        }

        return null;
    }
}
