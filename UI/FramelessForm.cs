using System.Runtime.InteropServices;

namespace JouleDesk;

/// <summary>Win32 window messages handled by the custom frame.</summary>
internal static class WindowMessages
{
    public const int GetMinMaxInfo = 0x0024;
    public const int CancelMode = 0x001F;
    public const int NcCalcSize = 0x0083;
    public const int NcHitTest = 0x0084;
    public const int NcPaint = 0x0085;
    public const int NcActivate = 0x0086;
    public const int NcLeftButtonDown = 0x00A1;
    public const int NcLeftButtonUp = 0x00A2;
    public const int LeftButtonUp = 0x0202;
}

/// <summary>Result of a non-client hit test (the HT* values of the Win32 API).</summary>
internal enum HitTarget
{
    Transparent = -1,
    Client = 1,
    Caption = 2,
    MaximizeButton = 9,
    Left = 10,
    Right = 11,
    Top = 12,
    TopLeft = 13,
    TopRight = 14,
    Bottom = 15,
    BottomLeft = 16,
    BottomRight = 17
}

/// <summary>
/// A window that draws its own title bar but keeps the native behaviour of a normal one:
/// resizing from the edges, Aero Snap, and the Windows 11 snap layouts flyout on the maximize button.
/// Derived forms decide which part of the client area acts as caption or maximize button.
/// Windows owns the mouse handling over the maximize button, so a click there is turned into <see cref="ToggleMaximized"/> here.
/// </summary>
internal abstract class FramelessForm : Form
{
    // WS_CAPTION | WS_SYSMENU | WS_THICKFRAME | WS_MINIMIZEBOX | WS_MAXIMIZEBOX
    private const int OverlappedWindowStyle = 0x00CF0000;

    private const int ResizeBorderDips = 6;

    private bool maximizePressed;

    protected FramelessForm() => FormBorderStyle = FormBorderStyle.None;

    protected override CreateParams CreateParams
    {
        get
        {
            // Without the caption and sizing frame styles Windows would not offer snapping or edge resizing.
            var parameters = base.CreateParams;
            parameters.Style |= OverlappedWindowStyle;
            return parameters;
        }
    }

    internal HitTarget HitTest(Point clientPoint)
    {
        if (WindowState == FormWindowState.Normal && HitResizeBorder(clientPoint) is { } border)
            return border;
        return HitTestContent(clientPoint);
    }

    /// <summary>Classifies a point inside the window's own content, away from the resize border.</summary>
    protected abstract HitTarget HitTestContent(Point clientPoint);

    protected void ToggleMaximized() =>
        WindowState = WindowState == FormWindowState.Maximized ? FormWindowState.Normal : FormWindowState.Maximized;

    protected override void WndProc(ref Message message)
    {
        switch (message.Msg)
        {
            case WindowMessages.NcPaint or WindowMessages.NcActivate:
                message.Result = (IntPtr)1;
                return;

            case WindowMessages.NcCalcSize when message.WParam != IntPtr.Zero:
                // The whole window is client area. A maximized window is limited to the work area so it keeps clear of the taskbar.
                if (WindowState == FormWindowState.Maximized)
                    Marshal.StructureToPtr(NativeRectangle.From(Screen.FromHandle(Handle).WorkingArea), message.LParam, false);
                message.Result = IntPtr.Zero;
                return;

            case WindowMessages.NcHitTest:
                message.Result = (IntPtr)(int)HitTest(PointToClient(PointFromLParam(message.LParam)));
                return;

            case WindowMessages.NcLeftButtonDown when message.WParam == (IntPtr)(int)HitTarget.MaximizeButton:
                maximizePressed = true;
                message.Result = IntPtr.Zero;
                return;

            case WindowMessages.NcLeftButtonUp when message.WParam == (IntPtr)(int)HitTarget.MaximizeButton:
                if (maximizePressed)
                    ToggleMaximized();
                maximizePressed = false;
                message.Result = IntPtr.Zero;
                return;

            case WindowMessages.CancelMode or WindowMessages.LeftButtonUp or WindowMessages.NcLeftButtonUp:
                maximizePressed = false;
                break;
        }

        base.WndProc(ref message);

        if (message.Msg == WindowMessages.GetMinMaxInfo)
            LimitMaximizedBounds(message.LParam);
    }

    private HitTarget? HitResizeBorder(Point point)
    {
        var border = Math.Max(ResizeBorderDips, DeviceDpi * ResizeBorderDips / 96);
        bool left = point.X < border, right = point.X >= ClientSize.Width - border;
        bool top = point.Y < border, bottom = point.Y >= ClientSize.Height - border;

        if (top)
            return left ? HitTarget.TopLeft : right ? HitTarget.TopRight : HitTarget.Top;
        if (bottom)
            return left ? HitTarget.BottomLeft : right ? HitTarget.BottomRight : HitTarget.Bottom;
        if (left)
            return HitTarget.Left;
        return right ? HitTarget.Right : null;
    }

    private void LimitMaximizedBounds(IntPtr minMaxInfoPointer)
    {
        var info = Marshal.PtrToStructure<MinMaxInfo>(minMaxInfoPointer);
        var screen = Screen.FromHandle(Handle);
        info.MaxPosition = new Point(screen.WorkingArea.Left - screen.Bounds.Left, screen.WorkingArea.Top - screen.Bounds.Top);
        info.MaxSize = screen.WorkingArea.Size;
        Marshal.StructureToPtr(info, minMaxInfoPointer, false);
    }

    // The screen position is packed into LParam as two signed 16-bit values.
    private static Point PointFromLParam(IntPtr lParam) =>
        new(unchecked((short)(long)lParam), unchecked((short)((long)lParam >> 16)));

    [StructLayout(LayoutKind.Sequential)]
    private struct NativeRectangle
    {
        public int Left, Top, Right, Bottom;

        public static NativeRectangle From(Rectangle rectangle) =>
            new() { Left = rectangle.Left, Top = rectangle.Top, Right = rectangle.Right, Bottom = rectangle.Bottom };
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct MinMaxInfo
    {
        public Point Reserved;
        public Size MaxSize;
        public Point MaxPosition;
        public Size MinTrackSize;
        public Size MaxTrackSize;
    }
}
