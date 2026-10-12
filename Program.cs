namespace JouleDesk;

internal static class Program
{
    /// <summary>
    /// Held for the life of the process so a second launch finds the first. It is per Windows session,
    /// and the installer names it too.
    /// </summary>
    private const string SingleInstanceMutexName = @"Local\JouleDesk";

    [STAThread]
    private static void Main(string[] args)
    {
        Localization.Initialize();
        AppIdentity.Register();
        ApplicationConfiguration.Initialize();

        using var singleInstance = new Mutex(initiallyOwned: true, SingleInstanceMutexName, out var isFirstInstance);
        if (!isFirstInstance)
        {
            InstanceChannel.Signal();
            return;
        }

        using var shutdown = new CancellationTokenSource();
        try
        {
            var store = new UsageStore();
            var catalog = new ComponentCatalog();
            catalog.LoadCache(store.Folder);
            var monitor = new PowerMonitor(store, catalog, SensorReader.ReadAsync, SpotPriceClient.FetchAsync);
            using var form = new MainForm(store, catalog, monitor, startInBackground: args.Contains(Startup.BackgroundArgument));

            // The handle must exist before another launch can post a request to this window.
            _ = form.Handle;
            _ = InstanceChannel.ListenAsync(form.RequestRestore, shutdown.Token);
            Application.Run(form);
        }
        catch (Exception exception)
        {
            MessageBox.Show(exception.Message, "JouleDesk");
        }
        finally
        {
            shutdown.Cancel();
        }
    }
}
