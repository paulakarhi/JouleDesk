namespace JouleDesk;

/// <summary>Reading and writing the files of the data folder.</summary>
internal static class DataFile
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

    /// <summary>Reads a whole text file, refusing one larger than <paramref name="maxBytes"/> instead of loading it.</summary>
    public static string Read(string path, long maxBytes)
    {
        if (new FileInfo(path).Length > maxBytes)
            throw new InvalidDataException(Localization.Translate("A saved file is larger than expected."));
        return File.ReadAllText(path);
    }
}
