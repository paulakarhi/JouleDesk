using System.Diagnostics;

namespace JouleDesk;

/// <summary>Hands files and folders to Windows Explorer and the default applications.</summary>
internal static class ShellLauncher
{
    /// <summary>Opens a file with its default application, or a folder in Explorer.</summary>
    public static void Open(string path) =>
        Process.Start(new ProcessStartInfo(path) { UseShellExecute = true })?.Dispose();

    /// <summary>Shows the containing folder in Explorer with the file selected.</summary>
    public static void RevealFile(string path) =>
        Process.Start(new ProcessStartInfo("explorer.exe", $"/select,\"{path}\"") { UseShellExecute = true })?.Dispose();
}
