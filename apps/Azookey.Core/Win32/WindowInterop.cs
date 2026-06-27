using System.Runtime.InteropServices;
using WinRT.Interop;

namespace Azookey.Core.Win32;

public static partial class WindowInterop
{
    public readonly record struct MonitorWorkArea(int Left, int Top, int Right, int Bottom);
    public readonly record struct ScreenRect(int Left, int Top, int Right, int Bottom);

    private const int GwlStyle = -16;
    private const int GwlExStyle = -20;
    private const int WsPopup = unchecked((int)0x80000000);
    private const int WsCaption = 0x00C00000;
    private const int WsThickFrame = 0x00040000;
    private const int WsSysMenu = 0x00080000;
    private const int WsMinimizeBox = 0x00020000;
    private const int WsMaximizeBox = 0x00010000;
    private const int WsExToolWindow = 0x00000080;
    private const int WsExTopmost = 0x00000008;
    private const int WsExNoActivate = 0x08000000;
    private const int SwShownoactivate = 4;
    private const uint SwpNoActivate = 0x0010;
    private const uint SwpNoMove = 0x0002;
    private const uint SwpNoSize = 0x0001;
    private const uint SwpFrameChanged = 0x0020;
    private const uint SwpShowWindow = 0x0040;
    private const uint MonitorDefaulttonearest = 0x00000002;
    private const uint SpiGetworkarea = 0x0030;
    private static readonly IntPtr HwndTopmost = new(-1);

    internal static uint ShowNoActivateFlags => SwpNoActivate | SwpShowWindow | SwpNoMove | SwpNoSize;

    internal static uint MoveNoActivateFlags => SwpNoActivate;

    public static IntPtr GetHwnd(object window) => WindowNative.GetWindowHandle(window);

    public static void MakeImeToolWindow(object window)
    {
        IntPtr hwnd = GetHwnd(window);
        IntPtr currentExStyle = GetWindowLongPtr(hwnd, GwlExStyle);
        IntPtr currentStyle = GetWindowLongPtr(hwnd, GwlStyle);
        SetWindowLongPtr(hwnd, GwlExStyle, ComposeImeToolWindowExStyle(currentExStyle));
        SetWindowLongPtr(hwnd, GwlStyle, ComposeImeToolWindowStyle(currentStyle));
        SetWindowPos(hwnd, IntPtr.Zero, 0, 0, 0, 0, SwpNoMove | SwpNoSize | SwpNoActivate | SwpFrameChanged);
    }

    public static void ShowNoActivate(object window)
    {
        IntPtr hwnd = GetHwnd(window);
        ShowWindow(hwnd, SwShownoactivate);
        SetWindowPos(hwnd, HwndTopmost, 0, 0, 0, 0, ShowNoActivateFlags);
    }

    public static void MoveNoActivate(object window, int x, int y, int width, int height)
    {
        SetWindowPos(GetHwnd(window), HwndTopmost, x, y, width, height, MoveNoActivateFlags);
    }

    internal static IntPtr ComposeImeToolWindowStyle(IntPtr currentStyle) =>
        new((currentStyle.ToInt64() & ~ImeToolWindowChromeStyleMask) | WsPopup);

    internal static IntPtr ComposeImeToolWindowExStyle(IntPtr currentExStyle) =>
        new(currentExStyle.ToInt64() | WsExToolWindow | WsExNoActivate | WsExTopmost);

    private static long ImeToolWindowChromeStyleMask =>
        WsCaption | WsThickFrame | WsSysMenu | WsMinimizeBox | WsMaximizeBox;

    public static MonitorWorkArea GetWorkArea(int left, int top, int right, int bottom)
    {
        var rect = new Rect
        {
            Left = left,
            Top = top,
            Right = right,
            Bottom = bottom
        };

        IntPtr monitor = MonitorFromRect(ref rect, MonitorDefaulttonearest);
        if (monitor != IntPtr.Zero)
        {
            var monitorInfo = new MonitorInfo { MonitorInfoSize = Marshal.SizeOf<MonitorInfo>() };
            if (GetMonitorInfo(monitor, ref monitorInfo))
            {
                return new MonitorWorkArea(
                    monitorInfo.WorkArea.Left,
                    monitorInfo.WorkArea.Top,
                    monitorInfo.WorkArea.Right,
                    monitorInfo.WorkArea.Bottom);
            }
        }

        if (SystemParametersInfo(SpiGetworkarea, 0, ref rect, 0))
        {
            return new MonitorWorkArea(rect.Left, rect.Top, rect.Right, rect.Bottom);
        }

        return new MonitorWorkArea(0, 0, 1920, 1080);
    }

    public static ScreenRect GetFallbackInputRect()
    {
        if (TryGetForegroundCaretRect(out ScreenRect caretRect))
        {
            return caretRect;
        }

        if (GetCursorPos(out Point cursor))
        {
            return NormalizeFallbackRect(new ScreenRect(cursor.X, cursor.Y, cursor.X + 1, cursor.Y + 24));
        }

        return new ScreenRect(0, 0, 1, 24);
    }

    private static bool TryGetForegroundCaretRect(out ScreenRect screenRect)
    {
        var guiThreadInfo = new GuiThreadInfo
        {
            Size = Marshal.SizeOf<GuiThreadInfo>()
        };

        if (!GetGUIThreadInfo(0, ref guiThreadInfo) || guiThreadInfo.CaretWindow == IntPtr.Zero)
        {
            screenRect = default;
            return false;
        }

        var topLeft = new Point(guiThreadInfo.CaretRect.Left, guiThreadInfo.CaretRect.Top);
        var bottomRight = new Point(guiThreadInfo.CaretRect.Right, guiThreadInfo.CaretRect.Bottom);
        if (!ClientToScreen(guiThreadInfo.CaretWindow, ref topLeft) ||
            !ClientToScreen(guiThreadInfo.CaretWindow, ref bottomRight))
        {
            screenRect = default;
            return false;
        }

        screenRect = NormalizeFallbackRect(new ScreenRect(topLeft.X, topLeft.Y, bottomRight.X, bottomRight.Y));
        return true;
    }

    private static ScreenRect NormalizeFallbackRect(ScreenRect rect)
    {
        int left = Math.Min(rect.Left, rect.Right);
        int top = Math.Min(rect.Top, rect.Bottom);
        int right = Math.Max(rect.Left, rect.Right);
        int bottom = Math.Max(rect.Top, rect.Bottom);

        if (right <= left)
        {
            right = left + 1;
        }

        if (bottom <= top)
        {
            bottom = top + 24;
        }

        return new ScreenRect(left, top, right, bottom);
    }

    [LibraryImport("user32.dll", EntryPoint = "SetWindowLongPtrW")]
    private static partial IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

    [LibraryImport("user32.dll", EntryPoint = "GetWindowLongPtrW")]
    private static partial IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ShowWindow(IntPtr hWnd, int nCmdShow);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint flags);

    [LibraryImport("user32.dll")]
    private static partial IntPtr MonitorFromRect(ref Rect lprc, uint dwFlags);

    [LibraryImport("user32.dll", EntryPoint = "GetMonitorInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetMonitorInfo(IntPtr hMonitor, ref MonitorInfo lpmi);

    [LibraryImport("user32.dll", EntryPoint = "SystemParametersInfoW")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool SystemParametersInfo(uint uiAction, uint uiParam, ref Rect pvParam, uint fWinIni);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetGUIThreadInfo(uint idThread, ref GuiThreadInfo guiThreadInfo);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool ClientToScreen(IntPtr hWnd, ref Point point);

    [LibraryImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static partial bool GetCursorPos(out Point point);

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MonitorInfo
    {
        public int MonitorInfoSize;
        public Rect MonitorArea;
        public Rect WorkArea;
        public uint Flags;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Point
    {
        public Point(int x, int y)
        {
            X = x;
            Y = y;
        }

        public int X;
        public int Y;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct GuiThreadInfo
    {
        public int Size;
        public uint Flags;
        public IntPtr ActiveWindow;
        public IntPtr FocusWindow;
        public IntPtr CaptureWindow;
        public IntPtr MenuOwnerWindow;
        public IntPtr MoveSizeWindow;
        public IntPtr CaretWindow;
        public Rect CaretRect;
    }
}
