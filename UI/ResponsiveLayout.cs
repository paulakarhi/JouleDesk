namespace JouleDesk;

/// <summary>
/// Positions for the page content at a given width: cards and form fields use one, two or three columns,
/// and forms stack into a single column when the window is narrow. All coordinates are relative to the page.
/// </summary>
internal sealed class ResponsiveLayout
{
    public const int Margin = 20;

    /// <summary>Where the main content starts, below the page header and the row of action buttons.</summary>
    public const int Top = 172;

    public const int ActionRowTop = 122;
    public const int ActionRowHeight = 36;

    public const int MetricHeight = 161;
    public const int FieldHeight = 84;
    public const int FieldInset = 16;
    public const int ButtonHeight = 44;
    public const int ModeButtonHeight = 42;
    public const int OptionHeight = 90;

    /// <summary>Height of the chart card at the top of the price and history pages.</summary>
    public const int ChartHeight = 340;

    private const int Gap = 12;
    private const int MinimumWidth = 240;
    private const int NarrowBelow = 620;
    private const int MetricCount = 6;
    private const int FieldCount = 4;

    public ResponsiveLayout(int surfaceWidth)
    {
        SurfaceWidth = surfaceWidth;
        Width = Math.Max(MinimumWidth, surfaceWidth - Margin * 2);
    }

    public int SurfaceWidth { get; }

    /// <summary>Width of the content area between the margins.</summary>
    public int Width { get; }

    public bool Narrow => Width < NarrowBelow;

    public int MetricColumns => Width >= 800 ? 3 : Width >= 520 ? 2 : 1;

    public int FieldColumns => Narrow ? 1 : 2;

    /// <summary>The <paramref name="index"/>th cell of a grid with equal columns, starting at <paramref name="top"/>.</summary>
    public Rectangle Cell(int index, int columns, int top, int height, int inset = 0)
    {
        var available = Width - inset * 2;
        var cellWidth = (available - (columns - 1) * Gap) / columns;
        return new Rectangle(
            Margin + inset + index % columns * (cellWidth + Gap),
            top + index / columns * (height + Gap),
            cellWidth,
            height);
    }

    /// <summary>A button in the row under the page header; <paramref name="offset"/> is measured from the left margin.</summary>
    public static Rectangle ActionButton(int offset, int width) => new(Margin + offset, ActionRowTop, width, ActionRowHeight);

    public Rectangle MetricCell(int index) => Cell(index, MetricColumns, Top, MetricHeight);

    public Rectangle FieldCell(int index, int top) => Cell(index, FieldColumns, top, FieldHeight, FieldInset);

    public Rectangle ButtonCell(int index, int top, int height = ButtonHeight) => Cell(index, FieldColumns, top, height);

    /// <summary>Where the list below a chart card starts.</summary>
    public const int BelowChart = Top + ChartHeight + Gap;

    /// <summary>The chart card at the top of the page.</summary>
    public Rectangle ChartCard => new(Margin, Top, Width, ChartHeight);

    /// <summary>A full-width control inside a card.</summary>
    public Rectangle CardRow(int top, int height) => new(Margin + FieldInset, top, Width - FieldInset * 2, height);

    // Overview
    public int ChartTop => Top + (MetricCount + MetricColumns - 1) / MetricColumns * (MetricHeight + Gap);

    // Fields of the plan and settings forms
    public int FieldRowsHeight => FieldCount / FieldColumns * (FieldHeight + Gap);

    // Electricity plan
    public int PlanFieldsTop => Top + (Narrow ? 3 : 1) * (OptionHeight + Gap) + 40;

    public int PlanNoteTop => PlanFieldsTop + FieldRowsHeight + 12;

    public int PlanSaveTop => PlanNoteTop + 96;

    public int PlanInfoTop => PlanSaveTop + 68;

    // Settings
    public int SettingsFieldsTop => Top + (Narrow ? 2 : 1) * (ModeButtonHeight + Gap) + 40;

    public int CatalogTop => SettingsFieldsTop + FieldRowsHeight + 36;

    public int SettingsSaveTop => CatalogTop + 182;

    public int StartupTop => SettingsSaveTop + ButtonRowsHeight + 70;

    public int SettingsBottom => StartupTop + ButtonRowsHeight + 76;

    private int ButtonRowsHeight => (Narrow ? 2 : 1) * (ButtonHeight + Gap);
}
