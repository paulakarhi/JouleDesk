namespace JouleDesk;

internal static class AtomicFile
{
    /// <summary>Replaces a file in one step, so an interrupted write cannot leave a truncated file behind.</summary>
    public static void Write(string path, string content)
    {
        var temporaryPath = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporaryPath, content);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }
}
