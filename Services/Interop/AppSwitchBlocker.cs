using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;

namespace StudentDesktop.Services.Interop;

// SDA-01: best-effort blocking of OS-level app-switching shortcuts (Alt+Tab, the
// Windows key, Ctrl+Esc) while a class session is active, via a low-level keyboard
// hook (WH_KEYBOARD_LL). This is a normal, non-elevated technique used by kiosk-mode
// apps and only works on Windows.
//
// Known limitations (documented rather than overclaimed):
//   - Cannot intercept Ctrl+Alt+Del or the Secure Attention Sequence — that is
//     handled by Winlogon below any user-mode hook, by design, on every Windows
//     version. No non-elevated process can block it.
//   - A user with local admin rights (or Task Manager access) can still kill this
//     process outright, which removes the hook immediately.
//   - This raises the bar against casual app-switching during class; it is not a
//     kiosk-grade security boundary. True OS-level lockdown would need Windows
//     Assigned Access / shell replacement, which is out of scope for a normal
//     desktop app.
//   - No equivalent is implemented for Linux/macOS; the full-screen + topmost +
//     re-focus behavior in MainWindow is the only enforcement on those platforms.
[SupportedOSPlatform("windows")]
public sealed class AppSwitchBlocker : IDisposable
{
    private const int WH_KEYBOARD_LL = 13;
    private const int WM_KEYDOWN = 0x0100;
    private const int WM_SYSKEYDOWN = 0x0104;

    private const int VK_TAB = 0x09;
    private const int VK_ESCAPE = 0x1B;
    private const int VK_LWIN = 0x5B;
    private const int VK_RWIN = 0x5C;
    private const int VK_MENU = 0x12; // Alt
    private const int VK_CONTROL = 0x11;

    private delegate IntPtr LowLevelKeyboardProc(int nCode, IntPtr wParam, IntPtr lParam);

    // Keep a reference alive for the lifetime of the hook so the delegate isn't
    // garbage-collected while native code still holds a pointer to it.
    private readonly LowLevelKeyboardProc _proc;
    private IntPtr _hookHandle = IntPtr.Zero;

    public AppSwitchBlocker()
    {
        _proc = HookCallback;
    }

    public bool IsActive => _hookHandle != IntPtr.Zero;

    public void Start()
    {
        if (_hookHandle != IntPtr.Zero)
        {
            return;
        }

        using var curProcess = Process.GetCurrentProcess();
        using var curModule = curProcess.MainModule;
        _hookHandle = SetWindowsHookEx(WH_KEYBOARD_LL, _proc, GetModuleHandle(curModule?.ModuleName), 0);
    }

    public void Stop()
    {
        if (_hookHandle == IntPtr.Zero)
        {
            return;
        }
        UnhookWindowsHookEx(_hookHandle);
        _hookHandle = IntPtr.Zero;
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            var message = wParam.ToInt32();
            if (message is WM_KEYDOWN or WM_SYSKEYDOWN)
            {
                var vkCode = Marshal.ReadInt32(lParam);
                var altDown = (GetAsyncKeyState(VK_MENU) & 0x8000) != 0;
                var ctrlDown = (GetAsyncKeyState(VK_CONTROL) & 0x8000) != 0;

                var blockAltTab = vkCode == VK_TAB && altDown;
                var blockCtrlEsc = vkCode == VK_ESCAPE && ctrlDown;
                var blockWinKey = vkCode is VK_LWIN or VK_RWIN;

                if (blockAltTab || blockCtrlEsc || blockWinKey)
                {
                    return (IntPtr)1;
                }
            }
        }

        return CallNextHookEx(_hookHandle, nCode, wParam, lParam);
    }

    public void Dispose() => Stop();

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr SetWindowsHookEx(int idHook, LowLevelKeyboardProc lpfn, IntPtr hMod, uint dwThreadId);

    [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool UnhookWindowsHookEx(IntPtr hhk);

    [DllImport("user32.dll", CharSet = CharSet.Auto, CallingConvention = CallingConvention.StdCall)]
    private static extern IntPtr CallNextHookEx(IntPtr hhk, int nCode, IntPtr wParam, IntPtr lParam);

    [DllImport("kernel32.dll", CharSet = CharSet.Auto, SetLastError = true)]
    private static extern IntPtr GetModuleHandle(string? lpModuleName);

    [DllImport("user32.dll")]
    private static extern short GetAsyncKeyState(int vKey);
}
