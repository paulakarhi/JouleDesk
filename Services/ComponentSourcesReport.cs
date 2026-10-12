namespace JouleDesk;

/// <summary>A plain-text list of where each component's power figure comes from.</summary>
internal static class ComponentSourcesReport
{
    public static string Build(IEnumerable<ComponentEstimate> components)
    {
        var lines = new List<string> { Localization.Translate("Power data sources"), "" };
        foreach (var component in components)
        {
            lines.Add(component.Name);
            lines.Add(Localization.Translate(component.Kind.Label()));
            lines.Add(component.Source.Length > 0
                ? component.Source
                : Localization.Translate("Adjustable general estimate; no model-specific measurement."));
            lines.Add("");
        }
        lines.Add(Localization.Translate("Drive estimates use activity-based heuristics. Manufacturer idle power depends on power-saving states."));
        return string.Join(Environment.NewLine, lines) + Environment.NewLine;
    }
}
