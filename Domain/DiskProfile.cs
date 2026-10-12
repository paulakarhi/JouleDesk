using System.Text.Json.Serialization;

namespace JouleDesk;

/// <summary>Manufacturer power figures for one drive model. A missing value in the catalog is an error, never zero.</summary>
internal sealed record DiskProfile(
    [property: JsonRequired] string Model,
    [property: JsonRequired] double IdleWatts,
    [property: JsonRequired] double ReadWatts,
    [property: JsonRequired] double WriteWatts,
    [property: JsonRequired] double TypicalWatts,
    [property: JsonRequired] string Source)
{
    /// <summary>Assumption used for drives that have no catalog entry.</summary>
    public static DiskProfile Generic(string model) => new(model, IdleWatts: 1, ReadWatts: 6, WriteWatts: 8, TypicalWatts: 3, Source: "");
}
