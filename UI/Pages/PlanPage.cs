using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>Choice of pricing plan and its contract prices. The choice stays a draft until it is saved.</summary>
internal sealed class PlanPage : PageView
{
    private static readonly (PriceMode Mode, string Label)[] Options =
    {
        (PriceMode.SpotOnly, "Spot electricity\nVariable energy price only"),
        (PriceMode.SpotWithFees, "Spot + charges\nYour electricity plan"),
        (PriceMode.Fixed, "Fixed price\nEnter your energy price")
    };

    private PriceMode draftMode;

    public PlanPage(IPageHost host)
        : base(host)
    {
        draftMode = host.Store.Settings.PriceMode;
    }

    public override string Title => "Your electricity plan";

    public override string Subtitle => "Choose how to price your recorded consumption.";

    public override int ContentHeight => Layout.PlanInfoTop + 160;

    protected override void CreateControls()
    {
        var layout = Layout;
        var settings = Store.Settings;

        for (var index = 0; index < Options.Length; index++)
        {
            var (mode, label) = Options[index];
            AddButton(label, layout.Cell(index, layout.Narrow ? 1 : Options.Length, ResponsiveLayout.Top, ResponsiveLayout.OptionHeight),
                () => SelectMode(mode), selected: draftMode == mode);
        }

        var top = layout.PlanFieldsTop;
        AddNumberField("fixed", "Fixed energy price", settings.FixedPrice, "ct/kWh", layout.FieldCell(0, top), draftMode == PriceMode.Fixed);
        AddNumberField("margin", "Spot contract margin", settings.Margin, "ct/kWh", layout.FieldCell(1, top), draftMode == PriceMode.SpotWithFees);
        AddNumberField("transfer", "Distribution charge", settings.Transfer, "ct/kWh", layout.FieldCell(2, top), draftMode != PriceMode.SpotOnly);
        AddNumberField("tax", "Electricity tax", settings.Tax, "ct/kWh", layout.FieldCell(3, top), draftMode != PriceMode.SpotOnly);
        AddButton("Save and apply", layout.CardRow(layout.PlanSaveTop, ResponsiveLayout.ButtonHeight), Save, selected: true);
    }

    protected override void PaintBody(Graphics g)
    {
        var layout = Layout;
        var width = layout.Width;
        var cardTop = layout.PlanFieldsTop - 36;
        var textLeft = ResponsiveLayout.Margin + ResponsiveLayout.FieldInset;

        g.DrawBox(new Rectangle(ResponsiveLayout.Margin, cardTop, width, layout.PlanSaveTop + 60 - cardTop), Surface);
        g.DrawLabel("CONTRACT PRICES", new Rectangle(textLeft, cardTop + 10, width - 32, 22), TextStyles.Caption);
        g.DrawLabel("All prices include VAT. Spot-only excludes extra charges.\nFixed price does not add the margin again. Monthly fees are not allocated to the PC.",
            new Rectangle(textLeft, layout.PlanNoteTop, width - 32, 90), TextStyles.Note);

        g.DrawBox(new Rectangle(ResponsiveLayout.Margin, layout.PlanInfoTop, width, 144), InfoSurface);
        g.DrawLabel("Compare the same usage periods", new Rectangle(textLeft, layout.PlanInfoTop + 12, width - 32, 44),
            new TextStyle(11, Accent, Bold: true, Wrap: true));
        g.DrawLabel("Saving updates the current rate, €/h, history and CSV. History uses your chosen prices; recorded kWh stay unchanged.",
            new Rectangle(textLeft, layout.PlanInfoTop + 60, width - 32, 78), TextStyles.Note);
    }

    private void SelectMode(PriceMode mode)
    {
        draftMode = mode;
        Rebuild();
    }

    // Only the fields that belong to the chosen plan are read, so a stale value in a disabled field cannot block saving.
    private void Save() => Attempt(() =>
    {
        var plan = Store.Settings with { PriceMode = draftMode };
        if (draftMode == PriceMode.Fixed)
            plan = plan with { FixedPrice = ReadNumber("fixed", SettingLimits.FixedPrice) };
        if (draftMode == PriceMode.SpotWithFees)
            plan = plan with { Margin = ReadNumber("margin", SettingLimits.Margin) };
        if (draftMode != PriceMode.SpotOnly)
            plan = plan with { Transfer = ReadNumber("transfer", SettingLimits.Charge), Tax = ReadNumber("tax", SettingLimits.Charge) };

        Store.SaveSettings(plan);
        Monitor.Reset();
        Host.Notify("Plan saved. History now shows the selected plan's estimated cost.");
    });
}
