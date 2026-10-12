namespace JouleDesk;

/// <summary>Power of each part with its source: sensor readings first, then model-based and general estimates.</summary>
internal sealed class ComponentsPage : PageView
{
    public ComponentsPage(IPageHost host)
        : base(host)
    {
    }

    public override string Title => "Where does the power go?";

    public override string Subtitle => "Sensor readings first; estimates are clearly labelled.";

    public override int ContentHeight => ResponsiveLayout.Top + DataTable.HeightFor(Rows().Count) + 84;

    protected override void CreateControls()
    {
        var half = (Layout.Width - 12) / 2;
        AddButton("Data sources", ResponsiveLayout.ActionButton(0, half), OpenSources);
        AddButton("Edit estimates", ResponsiveLayout.ActionButton(half + 12, half), () => Host.SelectPage(Page.Settings));
    }

    protected override void PaintBody(Graphics g)
    {
        var bottom = PaintTable(g, ResponsiveLayout.Top, Rows(), "COMPONENT / SOURCE", "POWER");
        g.DrawLabel("Disk estimates follow activity. RAM and other parts use adjustable assumptions.",
            new Rectangle(ResponsiveLayout.Margin, bottom + 12, Layout.Width, 60), TextStyles.Hint);
    }

    private List<TableRow> Rows()
    {
        const string waiting = "Waiting for sensor data";
        var latest = Monitor.Latest;
        var sensor = EstimateKind.Sensor.Label();
        var rows = new List<TableRow>
        {
            new("Processor", DisplayFormat.Watts(Monitor.CpuWatts), sensor, latest?.CpuStatus ?? waiting, Highlight: true),
            new("Graphics card", DisplayFormat.Watts(Monitor.GpuWatts), sensor, latest?.GpuStatus ?? waiting, Highlight: true)
        };

        foreach (var component in latest?.Components ?? Array.Empty<ComponentEstimate>())
            rows.Add(new TableRow(component.Name, DisplayFormat.Watts(component.Watts, "F2"), component.Kind.Label(), component.Detail));

        if (Monitor.WallWatts is { } wallWatts)
        {
            var efficiency = Store.Settings.Efficiency;
            rows.Add(new TableRow("PSU losses", DisplayFormat.Watts(wallWatts * (1 - efficiency / 100), "F2"), EstimateKind.General.Label(),
                Localization.Format("Assumed efficiency {0}%", $"{efficiency:F1}")));
        }
        return rows;
    }

    private void OpenSources() => Attempt(() =>
    {
        var path = Path.Combine(Store.Folder, DataFiles.ComponentSources);
        File.WriteAllText(path, ComponentSourcesReport.Build(Monitor.Latest?.Components ?? Array.Empty<ComponentEstimate>()));
        ShellLauncher.Open(path);
    });
}
