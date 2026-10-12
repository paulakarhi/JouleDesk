using System.Drawing.Drawing2D;
using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>A caption above a bordered text box, with an optional unit at the right edge.</summary>
internal sealed class LabeledTextField : Control
{
    private const int CaptionHeight = 38;
    private const int BoxTop = 39;
    private const int EditorTop = 49;
    private const int EditorHeight = 26;
    private const int EditorLeft = 14;
    private const int UnitWidth = 66;

    private readonly TextBox editor;
    private readonly string unit;

    public LabeledTextField(string label, string value, string unit)
    {
        Label = label;
        this.unit = unit;
        Height = 70;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer |
                 ControlStyles.SupportsTransparentBackColor, true);
        BackColor = Color.Transparent;

        editor = new TextBox
        {
            Text = value,
            BorderStyle = BorderStyle.None,
            BackColor = Background,
            ForeColor = Foreground,
            Font = GetFont(12),
            AccessibleName = Localization.Translate(label)
        };
        Controls.Add(editor);
        editor.GotFocus += (_, _) => Invalidate();
        editor.LostFocus += (_, _) => Invalidate();
        Resize += (_, _) => editor.Bounds = new Rectangle(EditorLeft, EditorTop, Math.Max(20, Width - EditorRightInset), EditorHeight);
    }

    /// <summary>The caption as a translation key.</summary>
    public string Label { get; }

    public string TextValue
    {
        get => editor.Text;
        set => editor.Text = value;
    }

    private int EditorRightInset => unit.Length == 0 ? 28 : 95;

    public void FocusEditor() => editor.Focus();

    protected override void OnPaint(PaintEventArgs e)
    {
        var g = e.Graphics;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.DrawLabel(Label, new Rectangle(0, 0, Width, CaptionHeight), new TextStyle(9, Enabled ? SecondaryText : DisabledText, Wrap: true));
        g.DrawBox(new Rectangle(0, BoxTop, Width - 1, Height - BoxTop - 1), Background, editor.Focused ? Accent : Border, 8);
        if (unit.Length > 0)
            g.DrawLabel(unit, new Rectangle(Width - UnitWidth - 12, EditorTop + 2, UnitWidth, 24), new TextStyle(9, SecondaryText, Align: TextAlign.Right));
    }
}
