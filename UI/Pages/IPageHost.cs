namespace JouleDesk;

/// <summary>What a page can use from the window that shows it.</summary>
internal interface IPageHost
{
    UsageStore Store { get; }

    PowerMonitor Monitor { get; }

    ComponentCatalog Catalog { get; }

    ScrollViewport Viewport { get; }

    /// <summary>Shows a message in the footer until the user moves to another page.</summary>
    void Notify(string message, bool isError = false);

    void SelectPage(Page page);

    void HideToTray();

    void Quit();

    Task UpdateCatalogAsync();
}
