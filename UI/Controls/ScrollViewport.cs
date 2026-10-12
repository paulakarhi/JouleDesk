using System.Drawing.Drawing2D;
using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// A scrollable area that keeps its content surface no taller than the visible part: instead of one tall panel,
/// the page is painted translated by the scroll offset and the child controls are moved with it.
/// The strip at the right edge draws the scroll bar.
/// </summary>
internal sealed class ScrollViewport : Panel
{
    private const int ScrollBarWidth = 14;
    private const int ThumbWidth = 6;
    private const int MinimumThumbHeight = 32;
    private const int WheelPixelsPerNotch = 72;
    private const int RevealMargin = 8;

    private readonly Dictionary<Control, Rectangle> contentBounds = new();
    private int offset;
    private int contentHeight;
    private int? dragStart;
    private int dragOffset;

    public ScrollViewport()
    {
        DoubleBuffered = true;
        BackColor = Background;
        Surface.BackColor = Background;
        Controls.Add(Surface);
        Surface.MouseWheel += ForwardWheel;
    }

    /// <summary>The panel that pages paint on and add their controls to.</summary>
    public PageSurface Surface { get; } = new();

    public int Offset => offset;

    public int ContentHeight => contentHeight;

    public int MaximumOffset => Math.Max(0, contentHeight - ClientSize.Height);

    /// <summary>Sets the height of the scrollable content. It is never less than the visible height.</summary>
    public void SetExtent(int height)
    {
        contentHeight = Math.Max(ClientSize.Height, height);
        Surface.Size = new Size(Math.Max(1, ClientSize.Width - ScrollBarWidth), ClientSize.Height);
        ScrollTo(offset);
    }

    /// <summary>Adds a control whose bounds are given in page coordinates; it is moved as the page scrolls.</summary>
    public void AddContent(Control control)
    {
        contentBounds.Add(control, control.Bounds);
        control.Disposed += (_, _) => contentBounds.Remove(control);
        Surface.Controls.Add(control);
        AttachWheel(control);
    }

    /// <summary>The bounds a content control was given, before scrolling moved it.</summary>
    public Rectangle GetContentBounds(Control control) => contentBounds[control];

    public void ScrollTo(int value)
    {
        offset = Math.Clamp(value, 0, MaximumOffset);
        foreach (var (control, bounds) in contentBounds)
            control.Bounds = bounds with { Y = bounds.Y - offset };
        Surface.Invalidate();
        Invalidate();
    }

    /// <summary>Scrolls just enough to bring a control fully into view, for keyboard focus.</summary>
    public void Reveal(Control control)
    {
        var bounds = RectangleToClient(control.RectangleToScreen(control.ClientRectangle));
        if (bounds.Top < RevealMargin)
            ScrollTo(offset + bounds.Top - RevealMargin);
        else if (bounds.Bottom > Height - RevealMargin)
            ScrollTo(offset + bounds.Bottom - Height + RevealMargin);
    }

    protected override void OnMouseWheel(MouseEventArgs e)
    {
        ScrollTo(offset - e.Delta * WheelPixelsPerNotch / 120);
        if (e is HandledMouseEventArgs handled)
            handled.Handled = true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        if (MaximumOffset == 0)
            return;
        e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
        e.Graphics.DrawBox(Thumb, SecondaryText, radius: 3);
    }

    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.Button != MouseButtons.Left || MaximumOffset == 0)
            return;

        // Clicking the track pages up or down; dragging moves the thumb.
        if (!Thumb.Contains(e.Location))
            ScrollTo(offset + (e.Y < Thumb.Top ? -Height : Height));
        dragStart = e.Y;
        dragOffset = offset;
        Capture = true;
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if (dragStart is int start)
            ScrollTo(dragOffset + (e.Y - start) * MaximumOffset / Math.Max(1, Height - Thumb.Height));
    }

    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);
        dragStart = null;
        Capture = false;
    }

    protected override bool ProcessCmdKey(ref Message message, Keys keyData)
    {
        if (keyData is Keys.PageDown or Keys.PageUp)
        {
            var page = Math.Max(40, Height - 40);
            ScrollTo(offset + (keyData == Keys.PageDown ? page : -page));
            return true;
        }
        return base.ProcessCmdKey(ref message, keyData);
    }

    private Rectangle Thumb
    {
        get
        {
            var height = Math.Min(Height, Math.Max(MinimumThumbHeight, Height * Height / Math.Max(1, contentHeight)));
            return new Rectangle(Width - ThumbWidth - 4, (Height - height) * offset / Math.Max(1, MaximumOffset), ThumbWidth, height);
        }
    }

    // Wheel events go to the focused or hovered control, so every child forwards them to the viewport.
    private void AttachWheel(Control control)
    {
        control.MouseWheel += ForwardWheel;
        foreach (Control child in control.Controls)
            AttachWheel(child);
    }

    private void ForwardWheel(object? sender, MouseEventArgs e) => OnMouseWheel(e);

    internal sealed class PageSurface : Panel
    {
        public PageSurface() => DoubleBuffered = true;
    }
}
