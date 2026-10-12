using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>Estimation assumptions, the optional drive profile catalog, startup behaviour and quitting.</summary>
internal sealed class SettingsPage : PageView
{
    private bool draftComponents;

    public SettingsPage(IPageHost host)
        : base(host)
    {
        draftComponents = host.Store.Settings.ComponentEstimates;
    }

    public override string Title => "Your PC settings";

    public override string Subtitle => "Refine consumption estimates and their inputs.";

    public override int ContentHeight => Layout.SettingsBottom;

    protected override void CreateControls()
    {
        var layout = Layout;
        var settings = Store.Settings;

        const int modeTop = ResponsiveLayout.Top;
        const int modeHeight = ResponsiveLayout.ModeButtonHeight;
        AddButton("Component estimates", layout.ButtonCell(0, modeTop, modeHeight), () => SelectComponentEstimates(true), draftComponents);
        AddButton("One combined estimate", layout.ButtonCell(1, modeTop, modeHeight), () => SelectComponentEstimates(false), !draftComponents);

        var top = layout.SettingsFieldsTop;
        AddNumberField("board", "Motherboard, cooling and other parts", settings.BoardAndCoolingWatts, "W", layout.FieldCell(0, top), draftComponents);
        AddNumberField("ram", "Memory · per module", settings.RamWattsPerModule, "W", layout.FieldCell(1, top), draftComponents);
        AddNumberField("other", "Other parts · combined / fallback", settings.OtherWatts, "W", layout.FieldCell(2, top));
        AddNumberField("efficiency", "Estimated PSU efficiency", settings.Efficiency, "%", layout.FieldCell(3, top));

        AddTextField("catalog", "Component database HTTPS URL (optional)", settings.CatalogUrl, "", layout.CardRow(layout.CatalogTop, ResponsiveLayout.FieldHeight));
        AddAsyncButton("Get update", layout.CardRow(layout.CatalogTop + 96, 42), GetCatalogUpdateAsync);

        AddButton("Save settings", layout.ButtonCell(0, layout.SettingsSaveTop), () => Save(announce: true), selected: true);
        AddButton("Open data folder", layout.ButtonCell(1, layout.SettingsSaveTop), OpenDataFolder);

        var startup = Startup.Enabled;
        AddButton(startup ? "Start at sign-in: on" : "Start at sign-in: off", layout.ButtonCell(0, layout.StartupTop), ToggleStartup, startup);
        AddButton("Quit app and monitoring", layout.ButtonCell(1, layout.StartupTop), Host.Quit);
    }

    protected override void PaintBody(Graphics g)
    {
        var layout = Layout;
        var width = layout.Width;
        var cardTop = layout.SettingsFieldsTop - 36;
        var textLeft = ResponsiveLayout.Margin + ResponsiveLayout.FieldInset;

        g.DrawBox(new Rectangle(ResponsiveLayout.Margin, cardTop, width, layout.FieldRowsHeight + 44), Surface);
        g.DrawLabel("ESTIMATION ASSUMPTIONS", new Rectangle(textLeft, cardTop + 10, width - 32, 22), TextStyles.Caption);

        g.DrawBox(new Rectangle(ResponsiveLayout.Margin, layout.CatalogTop - 12, width, 182), Surface);
        g.DrawLabel(Host.Catalog.Status, new Rectangle(textLeft, layout.CatalogTop + 144, width - 32, 24), new TextStyle(9, SecondaryText));
        g.DrawLabel("Drives without a profile use a general estimate. Online updates require a database URL.",
            new Rectangle(ResponsiveLayout.Margin, layout.StartupTop - 62, width, 52), TextStyles.Hint);
        g.DrawLabel("X hides the window. Quit also stops background monitoring.",
            new Rectangle(ResponsiveLayout.Margin, layout.SettingsBottom - 66, width, 60), TextStyles.Hint);
    }

    private void SelectComponentEstimates(bool enabled)
    {
        draftComponents = enabled;
        Rebuild();
    }

    private async Task GetCatalogUpdateAsync()
    {
        if (Save(announce: false))
            await Host.UpdateCatalogAsync();
    }

    private bool Save(bool announce) => Attempt(() =>
    {
        var settings = Store.Settings with
        {
            OtherWatts = ReadNumber("other", SettingLimits.OtherWatts),
            Efficiency = ReadNumber("efficiency", SettingLimits.Efficiency),
            ComponentEstimates = draftComponents,
            CatalogUrl = ReadText("catalog")
        };
        if (draftComponents)
        {
            settings = settings with
            {
                BoardAndCoolingWatts = ReadNumber("board", SettingLimits.BoardAndCoolingWatts),
                RamWattsPerModule = ReadNumber("ram", SettingLimits.RamWattsPerModule)
            };
        }
        if (settings.CatalogUrl.Length > 0 && !SettingsValidator.IsHttpsUrl(settings.CatalogUrl))
            throw new InvalidDataException(Localization.Translate("Database URL must start with https://."));

        Store.SaveSettings(settings);
        Monitor.Reset();
        if (announce)
            Host.Notify("Settings saved. New estimates apply to future samples.");
    });

    private void OpenDataFolder() => Attempt(() => ShellLauncher.Open(Store.Folder));

    private void ToggleStartup() => Attempt(() =>
    {
        Startup.Set(!Startup.Enabled);
        Rebuild();
        Host.Notify("Startup setting saved.");
    });
}
