namespace Blah.Win32;

internal static class Constants
{
    // Keyboard hook constants
    public const int WH_KEYBOARD_LL = 13;
    public const int WM_KEYDOWN = 0x0100;
    public const int WM_SYSKEYDOWN = 0x0104;
    public const int WM_KEYUP = 0x0101;

    // Virtual key codes
    public const int VK_LWIN = 0x5B;
    public const int VK_RWIN = 0x5C;
    public const int VK_RETURN = 0x0D;
    public const int VK_ESCAPE = 0x1B;

    // Input constants
    public const uint INPUT_KEYBOARD = 1;
    public const uint KEYEVENTF_UNICODE = 0x0004;
    public const uint KEYEVENTF_KEYUP = 0x0002;

    // Window style constants
    public const uint WS_EX_LAYERED = 0x00080000;
    public const uint WS_EX_TRANSPARENT = 0x00000020;
    public const uint WS_EX_TOPMOST = 0x00000008;
    public const uint WS_EX_TOOLWINDOW = 0x00000080;
    public const uint WS_EX_NOACTIVATE = 0x08000000;
    public const uint WS_POPUP = 0x80000000;

    // Show window constants
    public const int SW_SHOW = 5;
    public const int SW_HIDE = 0;

    // UpdateLayeredWindow constants
    public const uint ULW_ALPHA = 0x00000002;
    public const byte AC_SRC_OVER = 0x00;
    public const byte AC_SRC_ALPHA = 0x01;

    // SetLayeredWindowAttributes constants
    public const uint LWA_COLORKEY = 0x00000001;
    public const uint LWA_ALPHA = 0x00000002;

    // System metrics constants
    public const int SM_CXSCREEN = 0;
    public const int SM_CYSCREEN = 1;

    // Polygon fill mode
    public const int WINDING = 2;

    // Window class constants
    public const int GCL_HBRBACKGROUND = -10;

    // SetWindowPos constants
    public const uint SWP_NOMOVE = 0x0002;
    public const uint SWP_NOSIZE = 0x0001;
    public const uint SWP_NOZORDER = 0x0004;
    public const uint SWP_NOACTIVATE = 0x0010;

    // Color constants (BGR format)
    public const uint COLOR_RED = 0x000000FF;
    public const uint COLOR_YELLOW = 0x0000FFFF;
    public const uint COLOR_GREEN = 0x0000FF00;
    public const uint COLOR_BLUE = 0x00EBCE87; // Sky blue (BGR format of 0x87CEEB)
}
