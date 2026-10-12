namespace JouleDesk;

internal static class AppIcon
{
    /// <summary>Loads the embedded icon as a standalone copy that does not depend on the resource stream.</summary>
    public static Icon Load()
    {
        using var stream = EmbeddedResources.Open(EmbeddedResources.Icon);
        using var icon = new Icon(stream);
        return (Icon)icon.Clone();
    }
}
