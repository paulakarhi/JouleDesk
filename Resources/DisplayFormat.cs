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

    /// <summary>A power in watts, or a dash while it is not known.</summary>
    public static string Watts(double? value, string format = "F1") => value.HasValue ? $"{value.Value.ToString(format)} W" : "— W";
}
