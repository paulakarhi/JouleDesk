using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// The app's button, drawn by hand. <see cref="Control.Text"/> is a translation key; a line break
/// splits it into a bold title and a smaller description line.
/// </summary>
internal sealed class ThemedButton : Control
{
    private readonly Action action;
    private bool hovered;
    private bool selected;

    public ThemedButton(string text, Action action)
    {
        Text = text;
        this.action = action;
        Cursor = Cursors.Hand;
        TabStop = true;
        AccessibleName = Localization.Translate(text);
        AccessibleRole = AccessibleRole.PushButton;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;
    }

    public bool Selected
    {
        get => selected;
        set
        {
            selected = value;
            Invalidate();
        }
    }

    /// <summary>A side-rail entry: no fill until hovered or selected, with a number in front of the label.</summary>
    public bool IsNavigation { get; set; }

    public string Marker { get; set; } = "";

    /// <summary>Blends into the background until hovered; used for window caption buttons.</summary>
    public bool Quiet { get; set; }

    /// <summary>Turns red when hovered.</summary>
    public bool Danger { get; set; }

    /// <summary>Lets the window frame handle mouse hit testing over this button, as Windows expects for the maximize button.</summary>
    public bool CaptionHitTarget { get; set; }

    /// <summary>Size of a single-line label; defaults to the larger caption-button size for <see cref="Quiet"/> buttons.</summary>
    public float? GlyphSize { get; set; }

    /// <summary>Activates the button as if it was clicked. Does nothing while disabled.</summary>
    public void PerformAction()
    {
        if (Enabled && !IsDisposed)
            OnClick(EventArgs.Empty);
    }

    protected override AccessibleObject CreateAccessibilityInstance() => new ButtonAccessibility(this);

    protected override void WndProc(ref Message message)
    {
        if (CaptionHitTarget && message.Msg == WindowMessages.NcHitTest)
        {
            message.Result = (IntPtr)(int)HitTarget.Transparent;
            return;
        }
        base.WndProc(ref message);
    }

    protected override void OnClick(EventArgs e)
    {
        base.OnClick(e);
        action();
    }

    protected override void OnMouseEnter(EventArgs e)
    {
        base.OnMouseEnter(e);
        hovered = true;
        Invalidate();
    }

    protected override void OnMouseLeave(EventArgs e)
    {
        base.OnMouseLeave(e);
        hovered = false;
        Invalidate();
    }

    protected override void OnGotFocus(EventArgs e)
    {
        base.OnGotFocus(e);
        Invalidate();
    }

    protected override void OnLostFocus(EventArgs e)
    {
        base.OnLostFocus(e);
        Invalidate();
    }

    protected override void OnEnabledChanged(EventArgs e)
    {
        base.OnEnabledChanged(e);
        Invalidate();
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        if (e.KeyCode is Keys.Space or Keys.Enter)
        {
            e.Handled = true;
            e.SuppressKeyPress = true;
            PerformAction();
        }
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        g.DrawBox(new Rectangle(0, 0, Width - 1, Height - 1), FillColor, BorderColor, 9);

        var textColor = TextColor;
        var lines = Localization.Translate(Text).Split('\n');
        if (IsNavigation)
        {
            g.DrawLabel(Marker, new Rectangle(12, 13, 28, 22), new TextStyle(8, Selected && Enabled ? Accent : SecondaryText));
            g.DrawLabel(Text, new Rectangle(43, 11, Width - 49, 25), new TextStyle(10, textColor, Selected));
        }
        else if (lines.Length > 1)
        {
            g.DrawLabel(lines[0], new Rectangle(16, 16, Width - 30, 30), new TextStyle(11, textColor, Bold: true));
            g.DrawLabel(lines[1], new Rectangle(16, 51, Width - 30, 34),
                new TextStyle(9, Selected ? AccentText : SecondaryText, Wrap: true));
        }
        else
        {
            var font = Theme.GetFont(GlyphSize ?? (Quiet ? 16 : 10), Selected);
            TextRenderer.DrawText(g, lines[0], font, ClientRectangle, textColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.SingleLine | TextFormatFlags.EndEllipsis);
        }
    }

    private Color FillColor
    {
        get
        {
            if (Danger && hovered)
                return DangerSurface;
            if (Selected)
                return IsNavigation ? SelectedSurface : Accent;
            if (hovered && Enabled)
                return HoverSurface;
            if (IsNavigation)
                return Sidebar;
            return Quiet ? Background : Surface;
        }
    }

    private Color? BorderColor
    {
        get
        {
            if (Focused)
                return Accent;
            return Selected && IsNavigation ? SelectedBorder : null;
        }
    }

    private Color TextColor
    {
        get
        {
            if (!Enabled)
                return DisabledText;
            if (Selected)
                return IsNavigation ? Accent : Background;
            return Foreground;
        }
    }

    private sealed class ButtonAccessibility(ThemedButton owner) : ControlAccessibleObject(owner)
    {
        public override string? DefaultAction => Localization.Translate("Press");

        public override void DoDefaultAction() => owner.PerformAction();
    }
}
