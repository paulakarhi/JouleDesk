using System.Runtime.InteropServices;

namespace JouleDesk;

internal static class AppIdentity
{
    /// <summary>Must match the AppUserModelID of the installer's shortcuts so the taskbar groups them with the running window.</summary>
    private const string AppUserModelId = "JouleDesk.Desktop";

    /// <summary>A failure only changes how the taskbar groups the window, so the result is not acted on.</summary>
    public static void Register() => _ = SetCurrentProcessExplicitAppUserModelID(AppUserModelId);

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern int SetCurrentProcessExplicitAppUserModelID(string appId);
}
