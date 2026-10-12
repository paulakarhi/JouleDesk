using System.ComponentModel;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Security;
using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// One page of the window. A page owns the controls it adds to the scrolling viewport and paints
/// the rest of its content, such as cards and tables, directly on the viewport surface.
/// </summary>
internal abstract class PageView : IDisposable
{
    private const int TitleTop = 12;
    private const int SubtitleTop = 78;
    private const int StepButtonWidth = 44;
    private const float StepGlyphSize = 18;

    private readonly List<Control> controls = new();
    private readonly Dictionary<string, LabeledTextField> fields = new();
    private ResponsiveLayout? layout;
    private bool building;

    protected PageView(IPageHost host) => Host = host;

    /// <summary>Page heading as a translation key.</summary>
    public abstract string Title { get; }

    /// <summary>The line under the heading, as a translation key or translated text.</summary>
    public abstract string Subtitle { get; }

    /// <summary>Height of the page content; the viewport scrolls when it exceeds the window.</summary>
    public abstract int ContentHeight { get; }

    protected IPageHost Host { get; }

    protected UsageStore Store => Host.Store;

    protected PowerMonitor Monitor => Host.Monitor;

    protected ResponsiveLayout Layout
    {
        get
        {
            var width = Host.Viewport.Surface.Width;
            if (layout is null || layout.SurfaceWidth != width)
                layout = new ResponsiveLayout(width);
            return layout;
        }
    }

    /// <summary>Creates the page controls for the first time, with values taken from the settings.</summary>
    public void Build() => Build(preserveDrafts: false);

    /// <summary>Creates the controls again, for example after a resize, keeping what the user has typed and the focused field.</summary>
    public void Rebuild() => Build(preserveDrafts: true);

    /// <summary>Called when the underlying data has changed, before the page is painted again.</summary>
    public virtual void Refresh()
    {
    }

    public void Paint(Graphics g)
    {
        g.TranslateTransform(0, -Host.Viewport.Offset);
        g.SmoothingMode = SmoothingMode.AntiAlias;

        var width = Layout.Width;
        var titleSize = Layout.Narrow ? 20 : 25;
        g.DrawLabel(Title, new Rectangle(ResponsiveLayout.Margin, TitleTop, width, 62), new TextStyle(titleSize, Foreground, Bold: true, Wrap: true));
        g.DrawLabel(Subtitle, new Rectangle(ResponsiveLayout.Margin, SubtitleTop, width, 40), TextStyles.Note);
        PaintBody(g);
    }

    public void Dispose() => DisposeControls();

    protected abstract void CreateControls();

    protected abstract void PaintBody(Graphics g);

    protected ThemedButton AddButton(string text, Rectangle bounds, Action onClick, bool selected = false)
    {
        var button = new ThemedButton(text, onClick) { Bounds = bounds, Selected = selected };
        AddControl(button);
        return button;
    }

    /// <summary>Adds a button for an operation that is awaited; failures are shown as a notice instead of crashing the app.</summary>
    protected ThemedButton AddAsyncButton(string text, Rectangle bounds, Func<Task> onClick, bool selected = false) =>
        AddButton(text, bounds, async () =>
        {
            try
            {
                await onClick();
            }
            catch (Exception exception)
            {
                Host.Notify(exception.Message, isError: true);
            }
        }, selected);

    /// <summary>
    /// Adds the ‹ and › buttons at the right end of the action row, for stepping through days or months.
    /// The names are translation keys that tell assistive technology what the buttons do.
    /// </summary>
    protected (ThemedButton Previous, ThemedButton Next) AddStepButtons(string previousName, string nextName, Action previous, Action next)
    {
        var nextOffset = Layout.Width - StepButtonWidth;
        var previousButton = AddButton("‹", ResponsiveLayout.ActionButton(nextOffset - StepButtonWidth - 8, StepButtonWidth), previous);
        var nextButton = AddButton("›", ResponsiveLayout.ActionButton(nextOffset, StepButtonWidth), next);
        previousButton.AccessibleName = Localization.Translate(previousName);
        nextButton.AccessibleName = Localization.Translate(nextName);
        previousButton.GlyphSize = StepGlyphSize;
        nextButton.GlyphSize = StepGlyphSize;
        return (previousButton, nextButton);
    }

    /// <summary>
    /// Runs something the user asked for. The failures that files, the registry and the shell can cause become a
    /// notice instead of an exception; <paramref name="failureFormat"/> puts the message into a translated sentence.
    /// </summary>
    /// <returns>Whether the action completed.</returns>
    protected bool Attempt(Action action, string? failureFormat = null)
    {
        try
        {
            action();
            return true;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException or InvalidDataException
                                              or Win32Exception or SecurityException or InvalidOperationException)
        {
            Host.Notify(failureFormat is null ? exception.Message : Localization.Format(failureFormat, exception.Message), isError: true);
            return false;
        }
    }

    protected LabeledTextField AddNumberField(string key, string label, double value, string unit, Rectangle bounds, bool enabled = true) =>
        AddTextField(key, label, value.ToString("0.###", CultureInfo.CurrentCulture), unit, bounds, enabled);

    protected LabeledTextField AddTextField(string key, string label, string value, string unit, Rectangle bounds, bool enabled = true)
    {
        var field = new LabeledTextField(label, value, unit) { Bounds = bounds, Enabled = enabled, Name = key };
        AddControl(field);
        fields[key] = field;
        return field;
    }

    protected void AddControl(Control control)
    {
        Host.Viewport.AddContent(control);
        controls.Add(control);
        control.Enter += (_, _) => Host.Viewport.Reveal(control);
    }

    /// <summary>Paints a table card with its top edge at <paramref name="top"/> and returns where its bottom edge is.</summary>
    protected int PaintTable(Graphics g, int top, IReadOnlyList<TableRow> rows, string heading, string valueHeading) =>
        DataTable.Paint(g, Layout, top, rows, heading, valueHeading, Host.Viewport.Offset, Host.Viewport.Height);

    protected string ReadText(string key) => fields[key].TextValue.Trim();

    /// <summary>Reads a decimal number from a field, accepting a comma or a point, and checks it against the allowed range.</summary>
    protected double ReadNumber(string key, ValueRange range)
    {
        var field = fields[key];
        var text = field.TextValue.Trim().Replace(',', '.');
        if (!double.TryParse(text, NumberStyles.AllowDecimalPoint | NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out var number) ||
            !range.Contains(number))
            throw new InvalidDataException(Localization.Format("{0}: enter a number between {1} and {2}.", Localization.Translate(field.Label), range.Min, range.Max));
        return number;
    }

    private void Build(bool preserveDrafts)
    {
        if (building)
            return;
        building = true;

        var surface = Host.Viewport.Surface;
        surface.SuspendLayout();
        try
        {
            var focusedKey = preserveDrafts ? fields.FirstOrDefault(pair => pair.Value.ContainsFocus).Key : null;
            var drafts = preserveDrafts ? fields.ToDictionary(pair => pair.Key, pair => pair.Value.TextValue) : new Dictionary<string, string>();

            DisposeControls();
            CreateControls();
            Refresh();

            // The new controls must sit at their scrolled positions before focus is given back, or revealing the focused field misjudges where it is.
            Host.Viewport.SetExtent(ContentHeight);

            foreach (var (key, text) in drafts)
            {
                if (fields.TryGetValue(key, out var field))
                    field.TextValue = text;
            }
            if (focusedKey is not null && fields.TryGetValue(focusedKey, out var focused))
                focused.FocusEditor();
        }
        finally
        {
            surface.ResumeLayout();
            building = false;
        }
    }

    private void DisposeControls()
    {
        foreach (var control in controls)
            control.Dispose();
        controls.Clear();
        fields.Clear();
    }
}
