using System.Runtime.InteropServices;

namespace JouleDesk;

internal static class AppIdentity
{
    /// <summary>Must match the AppUserModelID of the installer's shortcuts so the taskbar groups them with the running window.</summary>
    private const string AppUserModelId = "JouleDesk.Desktop";

    public static void Register() => SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
