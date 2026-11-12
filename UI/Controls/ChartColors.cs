using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>Colours shared by the bar charts.</summary>
internal static class ChartColors
{
    /// <summary>The colour of a value within a range: mint at the lowest, amber in the middle and coral at the highest.</summary>
    public static Color Level(double value, double lowest, double highest)
    {
        if (highest - lowest < 0.01)
            return Accent;

        var position = Math.Clamp((value - lowest) / (highest - lowest), 0, 1);
        return position < 0.5 ? Blend(Accent, Warning, position * 2) : Blend(Warning, Expensive, (position - 0.5) * 2);
    }

    /// <summary>The colour that is <paramref name="amount"/> of the way from <paramref name="from"/> to <paramref name="to"/>.</summary>
    public static Color Blend(Color from, Color to, double amount) => Color.FromArgb(
        (int)Math.Round(from.R + (to.R - from.R) * amount),
        (int)Math.Round(from.G + (to.G - from.G) * amount),
        (int)Math.Round(from.B + (to.B - from.B) * amount));
}
