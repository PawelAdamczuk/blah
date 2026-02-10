using Blah.Win32;

namespace Blah.Core;

internal static class MessageLoop
{
    public static void Run()
    {
        while (User32.GetMessage(out MSG msg, IntPtr.Zero, 0, 0))
        {
            User32.TranslateMessage(ref msg);
            User32.DispatchMessage(ref msg);
        }
    }
}
