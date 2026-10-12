using Microsoft.Win32;

namespace JouleDesk;

/// <summary>Starting JouleDesk in the background when the user signs in to Windows.</summary>
internal static class Startup
{
    /// <summary>Command-line switch that starts the app hidden in the notification area.</summary>
    public const string BackgroundArgument = "--background";

    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string ValueName = "JouleDesk";

    public static bool Enabled
    {
        get
        {
            using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath);
            return key?.GetValue(ValueName) is string;
        }
    }

    public static void Set(bool enable)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (enable)
            key.SetValue(ValueName, $"\"{Environment.ProcessPath}\" {BackgroundArgument}");
        else
            key.DeleteValue(ValueName, throwOnMissingValue: false);
    }
}
