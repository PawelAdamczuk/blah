using System.Runtime.InteropServices;
using Blah.Win32;

namespace Blah.Input;

internal class KeyboardHook : IDisposable
{
    private readonly User32.LowLevelKeyboardProc _hookCallback;
    private IntPtr _hookID = IntPtr.Zero;
    private bool _winKeyPressed = false;

    public event Action? ToggleRecordingRequested;
    public event Action? StopRequested;

    public KeyboardHook()
    {
        _hookCallback = HookCallback;
    }

    public bool Install()
    {
        _hookID = SetHook(_hookCallback);
        return _hookID != IntPtr.Zero;
    }

    private IntPtr SetHook(User32.LowLevelKeyboardProc proc)
    {
        using (var curProcess = System.Diagnostics.Process.GetCurrentProcess())
        using (var curModule = curProcess.MainModule)
        {
            return User32.SetWindowsHookEx(
                Constants.WH_KEYBOARD_LL, 
                proc, 
                Kernel32.GetModuleHandle(curModule!.ModuleName), 
                0);
        }
    }

    private IntPtr HookCallback(int nCode, IntPtr wParam, IntPtr lParam)
    {
        if (nCode >= 0)
        {
            int vkCode = Marshal.ReadInt32(lParam);
            
            if (wParam == (IntPtr)Constants.WM_KEYDOWN || wParam == (IntPtr)Constants.WM_SYSKEYDOWN)
            {
                if (vkCode == Constants.VK_LWIN || vkCode == Constants.VK_RWIN)
                {
                    _winKeyPressed = true;
                }
                else if (vkCode == Constants.VK_RETURN && _winKeyPressed)
                {
                    // Verify Win key is actually pressed right now
                    bool winActuallyPressed = (User32.GetAsyncKeyState(Constants.VK_LWIN) & 0x8000) != 0 || 
                                              (User32.GetAsyncKeyState(Constants.VK_RWIN) & 0x8000) != 0;
                    
                    if (winActuallyPressed)
                    {
                        ToggleRecordingRequested?.Invoke();
                    }
                    else
                    {
                        _winKeyPressed = false;
                    }
                }
                else if (vkCode == Constants.VK_ESCAPE && _winKeyPressed)
                {
                    // Verify Win key is actually pressed right now
                    bool winActuallyPressed = (User32.GetAsyncKeyState(Constants.VK_LWIN) & 0x8000) != 0 || 
                                              (User32.GetAsyncKeyState(Constants.VK_RWIN) & 0x8000) != 0;
                    
                    if (winActuallyPressed)
                    {
                        StopRequested?.Invoke();
                    }
                    else
                    {
                        _winKeyPressed = false;
                    }
                }
            }
            else if (wParam == (IntPtr)Constants.WM_KEYUP)
            {
                if (vkCode == Constants.VK_LWIN || vkCode == Constants.VK_RWIN)
                {
                    _winKeyPressed = false;
                }
            }
        }
        
        return User32.CallNextHookEx(_hookID, nCode, wParam, lParam);
    }

    public void Dispose()
    {
        if (_hookID != IntPtr.Zero)
        {
            User32.UnhookWindowsHookEx(_hookID);
            _hookID = IntPtr.Zero;
        }
    }
}
