namespace JouleDesk;

/// <summary>Vertical scale of a chart: always includes zero, with round grid line values.</summary>
internal readonly record struct ChartAxis(double Min, double Max, double Step)
{
    private const int TargetGridLines = 4;
    private const int LabelWidth = 36;
    private const int LabelGap = 8;

    /// <param name="lowest">The smallest value to show; the axis reaches below zero only for negative values.</param>
    /// <param name="highest">The largest value to show.</param>
    /// <param name="minimumRange">The smallest span the axis may have, so tiny values do not fill the chart.</param>
    public static ChartAxis For(double lowest, double highest, double minimumRange)
    {
        var low = Math.Min(0, lowest);
        var high = Math.Max(0, highest);
        if (high - low < minimumRange)
            high = low + minimumRange;

        var step = RoundStep((high - low) / TargetGridLines);
        return new ChartAxis(Math.Floor(low / step) * step, Math.Ceiling(high / step) * step, step);
    }

    public float YOf(double value, Rectangle plot) => plot.Bottom - (float)((value - Min) / (Max - Min)) * plot.Height;

    /// <summary>
    /// Draws a horizontal line and its value for every grid step, with the unit above the values.
    /// The zero line stands out when the axis reaches below it.
    /// </summary>
    public void PaintGrid(Graphics g, Rectangle plot, string unit)
    {
        using var gridPen = new Pen(Theme.Border);
        using var zeroPen = new Pen(Theme.SecondaryText);

        g.DrawLabel(unit, new Rectangle(plot.Left - LabelWidth - LabelGap, plot.Top - 22, 120, 16), TextStyles.Axis);
        foreach (var value in Ticks())
        {
            var y = (int)YOf(value, plot);
            g.DrawLine(value == 0 && Min < 0 ? zeroPen : gridPen, plot.Left, y, plot.Right, y);
            g.DrawLabel(Format(value), new Rectangle(plot.Left - LabelWidth - LabelGap, y - 8, LabelWidth, 20),
                TextStyles.Axis with { Align = TextAlign.Right });
        }
    }

    private IEnumerable<double> Ticks()
    {
        var count = (int)Math.Round((Max - Min) / Step);
        for (var index = 0; index <= count; index++)
            yield return Math.Round(Min + index * Step, 6);
    }

    private string Format(double value) => value.ToString(Step >= 1 ? "F0" : Step >= 0.1 ? "F1" : "F2");

    // 1, 2 or 5 times a power of ten, whichever is closest above the wanted step.
    private static double RoundStep(double wanted)
    {
        var magnitude = Math.Pow(10, Math.Floor(Math.Log10(wanted)));
        var fraction = wanted / magnitude;
        return magnitude * (fraction <= 1 ? 1 : fraction <= 2 ? 2 : fraction <= 5 ? 5 : 10);
    }
}
