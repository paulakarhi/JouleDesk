namespace JouleDesk;

/// <summary>How a power figure was obtained. The label is a translation key.</summary>
internal enum EstimateKind
{
    Sensor,
    ModelBased,
    General,
    GeneralFallback
}

internal static class EstimateKindExtensions
{
    public static string Label(this EstimateKind kind) => kind switch
    {
        EstimateKind.Sensor => "Sensor reading",
        EstimateKind.ModelBased => "Model-based estimate",
        EstimateKind.General => "General estimate",
        _ => "General estimate · fallback"
    };
}

/// <summary>Estimated power draw of one component. <paramref name="Source"/> is a reference URL, empty for general assumptions.</summary>
internal sealed record ComponentEstimate(string Name, double Watts, EstimateKind Kind, string Detail, string Source);
