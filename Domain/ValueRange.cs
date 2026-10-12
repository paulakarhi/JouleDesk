namespace JouleDesk;

/// <summary>Inclusive range of acceptable values. Non-finite numbers are never inside a range.</summary>
internal readonly record struct ValueRange(double Min, double Max)
{
    public bool Contains(double value) => double.IsFinite(value) && value >= Min && value <= Max;
}
