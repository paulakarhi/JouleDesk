namespace JouleDesk;

/// <summary>Date, time and number layouts shared by the views. Times are Finnish local time.</summary>
internal static class DisplayFormat
{
    public const string DayAndTime = "dd.MM. HH:mm";
    public const string Time = "HH:mm";
    public const string TimeWithSeconds = "HH:mm:ss";

    /// <summary>An amount of euros. An asterisk marks a total that leaves out energy which has no price yet.</summary>
    public static string Euros(double amount, bool partial, int decimals) =>
        amount.ToString("F" + decimals) + " €" + (partial ? " *" : "");

    /// <summary>A day for a chart heading: "Today · 12.11." near the present, the weekday and date otherwise.</summary>
    public static string DayLabel(DateTime date)
    {
        var relative = (date - FinnishTime.Today).Days switch
        {
            -1 => "Yesterday",
            0 => "Today",
            1 => "Tomorrow",
            _ => null
        };
        return relative is null ? date.ToString("dddd d.M.") : $"{Localization.Translate(relative)} · {date:d.M.}";
    }

    /// <summary>A power in watts, or a dash while it is not known.</summary>
    public static string Watts(double? value, string format = "F1") => value.HasValue ? $"{value.Value.ToString(format)} W" : "— W";
}
