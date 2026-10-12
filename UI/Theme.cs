namespace JouleDesk;

internal enum TextAlign
{
    Left,
    Center,
    Right
}

/// <summary>Font, size and colour of a piece of text. <see cref="Wrap"/> lets it use several lines inside its bounds.</summary>
internal readonly record struct TextStyle(float Size, Color Color, bool Bold = false, bool Wrap = false, TextAlign Align = TextAlign.Left);

internal static class Theme
{
    public static readonly Color Background = Color.FromArgb(14, 20, 25);
    public static readonly Color Sidebar = Color.FromArgb(18, 26, 32);
    public static readonly Color Surface = Color.FromArgb(24, 34, 41);
    public static readonly Color Border = Color.FromArgb(42, 55, 64);
    public static readonly Color Foreground = Color.FromArgb(235, 243, 244);
    public static readonly Color SecondaryText = Color.FromArgb(137, 157, 168);
    public static readonly Color DisabledText = Color.FromArgb(78, 94, 104);
    public static readonly Color Accent = Color.FromArgb(107, 233, 191);
    public static readonly Color AccentText = Color.FromArgb(36, 85, 69);
    public static readonly Color AccentSurface = Color.FromArgb(28, 55, 50);
    public static readonly Color AccentBorder = Color.FromArgb(46, 93, 77);
    public static readonly Color InfoSurface = Color.FromArgb(25, 43, 40);
    public static readonly Color SelectedSurface = Color.FromArgb(33, 58, 51);
    public static readonly Color SelectedBorder = Color.FromArgb(54, 94, 77);
    public static readonly Color HoverSurface = Color.FromArgb(43, 57, 66);
    public static readonly Color DangerSurface = Color.FromArgb(132, 51, 59);
    public static readonly Color Warning = Color.FromArgb(239, 188, 116);
    public static readonly Color Expensive = Color.FromArgb(240, 128, 118);
    public static readonly Color ChartFillTop = Color.FromArgb(50, Accent);
    public static readonly Color ChartFillBottom = Color.FromArgb(0, Accent);

    private static readonly Dictionary<(float Size, bool Bold), Font> Fonts = new();

    /// <summary>Returns a shared font. Fonts are cached for the life of the process and must not be disposed by callers.</summary>
    public static Font GetFont(float size = 10, bool bold = false)
    {
        if (!Fonts.TryGetValue((size, bold), out var font))
        {
            font = new Font("Segoe UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
            Fonts.Add((size, bold), font);
        }
        return font;
    }
}

/// <summary>Text styles that recur across the views.</summary>
internal static class TextStyles
{
    /// <summary>Upper-case labels above a block of content.</summary>
    public static readonly TextStyle Caption = new(9, Theme.SecondaryText, Bold: true);

    public static readonly TextStyle SmallCaption = new(8, Theme.SecondaryText, Bold: true);

    public static readonly TextStyle Axis = new(8, Theme.SecondaryText);

    public static readonly TextStyle Hint = new(9, Theme.SecondaryText, Wrap: true);

    public static readonly TextStyle Note = new(10, Theme.SecondaryText, Wrap: true);
}
