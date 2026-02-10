using System.Runtime.InteropServices;
using Blah.Win32;

namespace Blah.UI;

internal class IndicatorWindow
{
    private const int INDICATOR_SIZE_SMALL = 10;
    private const int INDICATOR_SIZE_LARGE = 15;
    private const int INSET = 3; // Pixel inset from screen edge
    private IntPtr _hwnd = IntPtr.Zero;
    private IntPtr _currentBrush = IntPtr.Zero;
    private int _currentSize = INDICATOR_SIZE_SMALL;
    private User32.WndProc? _wndProc; // Keep reference to prevent GC
    private System.Threading.Timer? _visibilityTimer; // Timer to restore visibility periodically

    public void Create()
    {
        _wndProc = WndProcCallback;
        
        // Start with red brush
        _currentBrush = Gdi32.CreateSolidBrush(Constants.COLOR_RED);
        
        WNDCLASSEX wndClass = new WNDCLASSEX
        {
            cbSize = (uint)Marshal.SizeOf(typeof(WNDCLASSEX)),
            style = 0,
            lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
            cbClsExtra = 0,
            cbWndExtra = 0,
            hInstance = Kernel32.GetModuleHandle(null!),
            hIcon = IntPtr.Zero,
            hCursor = IntPtr.Zero,
            hbrBackground = _currentBrush,
            lpszMenuName = null!,
            lpszClassName = "IndicatorWindowClass",
            hIconSm = IntPtr.Zero
        };

        ushort atom = User32.RegisterClassEx(ref wndClass);
        if (atom == 0)
        {
            Console.WriteLine("Failed to register indicator window class");
            return;
        }

        int screenWidth = User32.GetSystemMetrics(Constants.SM_CXSCREEN);
        int screenHeight = User32.GetSystemMetrics(Constants.SM_CYSCREEN);
        
        // Position in bottom-right corner with inset
        int x = screenWidth - _currentSize - INSET;
        int y = screenHeight - _currentSize - INSET;

        _hwnd = User32.CreateWindowEx(
            Constants.WS_EX_LAYERED | Constants.WS_EX_TRANSPARENT | Constants.WS_EX_TOPMOST | Constants.WS_EX_TOOLWINDOW | Constants.WS_EX_NOACTIVATE,
            "IndicatorWindowClass",
            "Indicator",
            Constants.WS_POPUP,
            x, y, _currentSize, _currentSize,
            IntPtr.Zero,
            IntPtr.Zero,
            Kernel32.GetModuleHandle(null!),
            IntPtr.Zero
        );

        if (_hwnd == IntPtr.Zero)
        {
            Console.WriteLine("Failed to create indicator window");
            return;
        }

        // Create triangular region and apply to window
        UpdateRegion();

        // Make window more translucent (50% opacity)
        User32.SetLayeredWindowAttributes(_hwnd, 0, 128, Constants.LWA_ALPHA);
        
        // Start timer to periodically restore visibility (every 1 second)
        _visibilityTimer = new System.Threading.Timer(_ => EnsureVisible(), null, 1000, 1000);
    }

    private void UpdateRegion()
    {
        if (_hwnd == IntPtr.Zero) return;
        
        // Create right-angled triangle in bottom-right corner
        // Points form a triangle with hypotenuse from top-right to bottom-left
        POINT[] trianglePoints = new POINT[3]
        {
            new POINT { x = 0, y = _currentSize },          // Bottom-left
            new POINT { x = _currentSize, y = _currentSize }, // Bottom-right
            new POINT { x = _currentSize, y = 0 }            // Top-right
        };
        
        IntPtr hRgn = Gdi32.CreatePolygonRgn(trianglePoints, 3, Constants.WINDING);
        User32.SetWindowRgn(_hwnd, hRgn, true);
    }

    private IntPtr WndProcCallback(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        return User32.DefWindowProc(hWnd, msg, wParam, lParam);
    }

    public void Show()
    {
        if (_hwnd != IntPtr.Zero)
        {
            User32.ShowWindow(_hwnd, Constants.SW_SHOW);
        }
    }

    public void SetSize(int size)
    {
        if (_hwnd == IntPtr.Zero) return;

        _currentSize = size;

        // Calculate new position (keep bottom-right alignment with inset)
        int screenWidth = User32.GetSystemMetrics(Constants.SM_CXSCREEN);
        int screenHeight = User32.GetSystemMetrics(Constants.SM_CYSCREEN);
        int x = screenWidth - _currentSize - INSET;
        int y = screenHeight - _currentSize - INSET;

        // Resize and reposition window
        User32.SetWindowPos(_hwnd, IntPtr.Zero, x, y, _currentSize, _currentSize, 
            Constants.SWP_NOZORDER | Constants.SWP_NOACTIVATE);

        // Update the triangular region to match new size
        UpdateRegion();
    }

    public void SetSmall()
    {
        SetSize(INDICATOR_SIZE_SMALL);
    }

    public void SetLarge()
    {
        SetSize(INDICATOR_SIZE_LARGE);
    }

    public void SetColor(uint color)
    {
        if (_hwnd == IntPtr.Zero) return;

        // Create new brush with the specified color
        IntPtr newBrush = Gdi32.CreateSolidBrush(color);
        
        // Set the new background brush
        User32.SetClassLongPtr(_hwnd, Constants.GCL_HBRBACKGROUND, newBrush);
        
        // Delete old brush if it exists
        if (_currentBrush != IntPtr.Zero)
        {
            Gdi32.DeleteObject(_currentBrush);
        }
        
        _currentBrush = newBrush;
        
        // Force window to redraw
        User32.InvalidateRect(_hwnd, IntPtr.Zero, true);
    }

    public void SetIdle()
    {
        SetSmall();
        SetColor(Constants.COLOR_BLUE);
        Show();
    }

    public void EnsureVisible()
    {
        if (_hwnd != IntPtr.Zero)
        {
            // Always force the window to restore its proper state
            // Show Desktop might move it off-screen or resize it rather than hiding it
            
            // Recalculate proper position
            int screenWidth = User32.GetSystemMetrics(Constants.SM_CXSCREEN);
            int screenHeight = User32.GetSystemMetrics(Constants.SM_CYSCREEN);
            int x = screenWidth - _currentSize - INSET;
            int y = screenHeight - _currentSize - INSET;
            
            // Force reposition and show
            User32.SetWindowPos(_hwnd, new IntPtr(-1), x, y, _currentSize, _currentSize,
                Constants.SWP_NOACTIVATE);
            
            User32.ShowWindow(_hwnd, Constants.SW_SHOW);
        }
    }

    public void Destroy()
    {
        // Stop the visibility timer
        _visibilityTimer?.Dispose();
        _visibilityTimer = null;
        
        if (_hwnd != IntPtr.Zero)
        {
            User32.DestroyWindow(_hwnd);
            _hwnd = IntPtr.Zero;
        }
    }
}
