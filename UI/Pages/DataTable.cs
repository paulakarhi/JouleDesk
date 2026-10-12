using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// One row of a table: a name, a value, and a line of description made of a caption and a detail.
/// Every text is a translation key or already translated text.
/// </summary>
internal readonly record struct TableRow(string Name, string Value, string Caption, string Detail, bool Highlight = false);

/// <summary>
/// Paints a list of rows as a card. Only the rows inside the visible part of the page are drawn,
/// so long histories stay cheap.
/// </summary>
internal static class DataTable
{
    public const int RowHeight = 112;
    private const int HeaderHeight = 64;
    private const int Padding = 16;

    /// <summary>Height of the card for the given number of rows; an empty table still has room for its message.</summary>
    public static int HeightFor(int rowCount) => HeaderHeight + Math.Max(1, rowCount) * RowHeight;

    /// <summary>
    /// Draws the table with its top edge at <paramref name="top"/> and returns the position of its bottom edge.
    /// The visible part of the page, in page coordinates, decides which rows are drawn.
    /// </summary>
    public static int Paint(Graphics g, ResponsiveLayout layout, int top, IReadOnlyList<TableRow> rows,
        string heading, string valueHeading, int visibleTop, int visibleHeight)
    {
        var x = ResponsiveLayout.Margin;
        var width = layout.Width;
        var bottom = top + HeightFor(rows.Count);
        var innerWidth = width - Padding * 2;

        g.DrawBox(new Rectangle(x, top, width, bottom - top), Surface);
        g.DrawLabel(heading, new Rectangle(x + Padding, top + 14, innerWidth, 22), TextStyles.SmallCaption);
        g.DrawLabel(valueHeading, new Rectangle(x + Padding, top + 36, innerWidth, 22), TextStyles.SmallCaption with { Align = TextAlign.Right });
        if (rows.Count == 0)
            g.DrawLabel("No data yet. Start monitoring or fetch prices.", new Rectangle(x + Padding, top + 74, innerWidth, 60),
                new TextStyle(11, SecondaryText, Wrap: true));

        var first = Math.Max(0, (visibleTop - top - HeaderHeight) / RowHeight);
        var last = Math.Min(rows.Count, (visibleTop + visibleHeight - top) / RowHeight + 1);
        using var divider = new Pen(Border);
        for (var index = first; index < last; index++)
        {
            var row = rows[index];
            var y = top + HeaderHeight + index * RowHeight;
            var valueColor = row.Highlight ? Accent : Foreground;

            g.DrawLine(divider, x + Padding, y, x + width - Padding, y);
            g.DrawLabel(row.Name, new Rectangle(x + Padding, y + 8, innerWidth, 24), new TextStyle(11, Foreground, Bold: true));
            g.DrawLabel(row.Value, new Rectangle(x + Padding, y + 36, innerWidth, 28), new TextStyle(15, valueColor, Bold: true));
            g.DrawLabel($"{Localization.Translate(row.Caption)} · {Localization.Translate(row.Detail)}",
                new Rectangle(x + Padding, y + 70, innerWidth, 38), new TextStyle(8, row.Highlight ? Accent : SecondaryText, Wrap: true));
        }
        return bottom;
    }
}
