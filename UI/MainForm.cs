using static JouleDesk.Theme;

namespace JouleDesk;

/// <summary>
/// The application window: the custom title bar, the navigation rail (a row of buttons when narrow)
/// and the page shown in the scrolling viewport. Closing the window hides it; monitoring continues until the app is quit.
/// </summary>
internal sealed class MainForm : FramelessForm, IPageHost
{
    private const int RailWidth = 208;
    private const int WideNavigationFrom = 1000;
    private const int CaptionHeight = 52;
    private const int CaptionButtonsWidth = 150;
    private const int FooterHeight = 45;

    private static readonly (Page Page, string Label)[] NavigationEntries =
    {
        (Page.Overview, "Overview"),
        (Page.Components, "Components"),
        (Page.History, "History"),
        (Page.Prices, "Electricity prices"),
        (Page.Plan, "Electricity plan"),
        (Page.Settings, "Settings")
    };

    private readonly Icon appIcon = AppIcon.Load();
    private readonly bool startInBackground;
    private readonly bool runsApplication;
    private readonly NotifyIcon tray;
    private readonly System.Windows.Forms.Timer timer = new() { Interval = (int)PowerMonitor.SampleInterval.TotalMilliseconds };
    private readonly List<ThemedButton> navigation = new();
    private readonly ThemedButton minimizeButton;
    private readonly ThemedButton maximizeButton;
    private readonly ThemedButton closeButton;

    private PageView pageView;
    private Page currentPage = Page.Overview;
    private string notice = "";
    private bool noticeIsError;
    private bool quitting;
    private bool updatingCatalog;

    /// <summary>
    /// Creates the window around the given services and takes ownership of the monitor. A window that runs the
    /// application samples on a timer, shows the notification icon and hides instead of closing; without that, the
    /// caller drives the monitor. A window started in the background stays hidden until it is restored.
    /// </summary>
    public MainForm(UsageStore store, ComponentCatalog catalog, PowerMonitor monitor, bool startInBackground = false, bool runsApplication = true)
    {
        Store = store;
        Monitor = monitor;
        Catalog = catalog;
        this.startInBackground = startInBackground;
        this.runsApplication = runsApplication;
        if (startInBackground)
        {
            Opacity = 0;
            ShowInTaskbar = false;
        }
        pageView = CreatePage(currentPage);

        Text = "JouleDesk";
        Icon = appIcon;
        Font = GetFont();
        BackColor = Background;
        ForeColor = Foreground;
        ClientSize = new Size(1200, 880);
        MinimumSize = new Size(380, 360);
        StartPosition = FormStartPosition.CenterScreen;
        DoubleBuffered = true;
        SetStyle(ControlStyles.ResizeRedraw, true);

        Controls.Add(Viewport);
        Viewport.Surface.Paint += (_, e) => pageView.Paint(e.Graphics);
        foreach (var (page, label) in NavigationEntries)
        {
            var target = page;
            var button = new ThemedButton(label, () => SelectPage(target)) { IsNavigation = true, Marker = ((int)page + 1).ToString("00") };
            navigation.Add(button);
            Controls.Add(button);
        }

        minimizeButton = new ThemedButton("—", () => WindowState = FormWindowState.Minimized) { Quiet = true };
        maximizeButton = new ThemedButton("□", ToggleMaximized) { Quiet = true, CaptionHitTarget = true };
        closeButton = new ThemedButton("×", HideToTray) { Quiet = true, Danger = true };
        Controls.AddRange(new Control[] { minimizeButton, maximizeButton, closeButton });

        tray = new NotifyIcon { Icon = appIcon, Text = "JouleDesk", Visible = runsApplication };
        tray.MouseClick += (_, e) =>
        {
            if (e.Button is MouseButtons.Left or MouseButtons.Right)
                Restore();
        };

        timer.Tick += async (_, _) => await Monitor.SampleAsync();
        Shown += async (_, _) => await OnShownAsync();
        FormClosing += OnFormClosing;
        FormClosed += (_, _) => Shutdown();
        Resize += (_, _) => OnResized();
        Monitor.Changed += OnMonitorChanged;

        if (Store.Warning is { } warning)
        {
            notice = warning;
            noticeIsError = true;
        }

        LayoutChrome();
        ShowCurrentPage();
    }

    public UsageStore Store { get; }

    public PowerMonitor Monitor { get; }

    public ComponentCatalog Catalog { get; }

    public ScrollViewport Viewport { get; } = new();

    private bool WideNavigation => ClientSize.Width >= WideNavigationFrom;

    public void SelectPage(Page page)
    {
        pageView.Dispose();
        currentPage = page;
        pageView = CreatePage(page);
        Viewport.ScrollTo(0);
        notice = "";
        noticeIsError = false;
        ShowCurrentPage();
    }

    public void Notify(string message, bool isError = false)
    {
        if (IsDisposed || quitting)
            return;
        notice = message;
        noticeIsError = isError;
        RefreshView();
    }

    public async Task UpdateCatalogAsync()
    {
        if (updatingCatalog || quitting)
            return;
        var url = Store.Settings.CatalogUrl;
        if (string.IsNullOrWhiteSpace(url))
        {
            Notify("No URL configured. Bundled manufacturer data is in use.");
            return;
        }

        updatingCatalog = true;
        try
        {
            await Monitor.UpdateCatalogAsync(url);
            Notify("Component power data updated.");
        }
        catch (OperationCanceledException) when (Monitor.IsStopped)
        {
        }
        catch (Exception exception)
        {
            Notify(Localization.Format("Update failed. Previous data retained: {0}", exception.Message), isError: true);
        }
        finally
        {
            updatingCatalog = false;
        }
    }

    public void HideToTray() => Hide();

    public void Restore()
    {
        Show();
        if (WindowState == FormWindowState.Minimized)
            WindowState = FormWindowState.Normal;
        Activate();
    }

    /// <summary>Shows the window from another thread, for the signal a second launch sends.</summary>
    public void RequestRestore()
    {
        try
        {
            if (!IsDisposed && IsHandleCreated)
                BeginInvoke(Restore);
        }
        catch (InvalidOperationException)
        {
            // The window is already being torn down.
        }
    }

    public void Quit()
    {
        quitting = true;
        Monitor.Stop();
        Close();
    }

    protected override HitTarget HitTestContent(Point point)
    {
        if (maximizeButton.Bounds.Contains(point))
            return HitTarget.MaximizeButton;
        if (point.Y < CaptionHeight && point.X < ClientSize.Width - CaptionButtonsWidth)
            return HitTarget.Caption;
        return HitTarget.Client;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        if (WideNavigation)
        {
            using var rail = new SolidBrush(Sidebar);
            g.FillRectangle(rail, 0, 0, RailWidth, Height);
            g.DrawLabel("PC ENERGY MONITOR", new Rectangle(26, 82, 170, 22), TextStyles.SmallCaption);
            g.DrawLabel("Your PC. Your energy.", new Rectangle(26, 108, 172, 35), TextStyles.Note);
        }
        g.DrawIcon(appIcon, new Rectangle(18, 12, 28, 28));
        g.DrawLabel("JOULEDESK", new Rectangle(56, 18, 140, 27), new TextStyle(11, Foreground, Bold: true));

        using var pen = new Pen(Border);
        g.DrawRectangle(pen, 0, 0, Width - 1, Height - 1);
        var footerLeft = WideNavigation ? RailWidth + 22 : 20;
        g.DrawLine(pen, footerLeft, Height - FooterHeight, Width - 20, Height - FooterHeight);

        var showNotice = notice.Length > 0;
        g.DrawLabel(showNotice ? notice : Monitor.Status, new Rectangle(footerLeft, Height - 35, Width - footerLeft - 20, 30),
            new TextStyle(9, showNotice && noticeIsError ? Warning : Accent));
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            quitting = true;
            Monitor.Dispose();
            timer.Dispose();
            tray.Dispose();
            pageView.Dispose();
        }
        base.Dispose(disposing);
        if (disposing)
            appIcon.Dispose();
    }

    private PageView CreatePage(Page page) => page switch
    {
        Page.Overview => new OverviewPage(this),
        Page.Components => new ComponentsPage(this),
        Page.History => new HistoryPage(this),
        Page.Prices => new PricesPage(this),
        Page.Plan => new PlanPage(this),
        _ => new SettingsPage(this)
    };

    private void ShowCurrentPage()
    {
        for (var index = 0; index < navigation.Count; index++)
            navigation[index].Selected = NavigationEntries[index].Page == currentPage;
        pageView.Build();
        RefreshView();
    }

    // Brings the page and the footer up to date after any change in the data they show.
    private void RefreshView()
    {
        if (IsDisposed || quitting)
            return;
        pageView.Refresh();
        Viewport.SetExtent(pageView.ContentHeight);
        Viewport.Surface.Invalidate();
        Invalidate();
    }

    private void OnMonitorChanged()
    {
        if (Monitor.WallWatts is { } watts)
            tray.Text = $"JouleDesk · {watts:F0} W";
        RefreshView();
    }

    private async Task OnShownAsync()
    {
        if (startInBackground)
        {
            Hide();
            Opacity = 1;
            ShowInTaskbar = true;
        }
        if (!runsApplication)
            return;

        timer.Start();
        if (!string.IsNullOrWhiteSpace(Store.Settings.CatalogUrl))
            _ = UpdateCatalogAsync();
        await Monitor.SampleAsync();
    }

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (!quitting && runsApplication && e.CloseReason == CloseReason.UserClosing)
        {
            e.Cancel = true;
            HideToTray();
        }
    }

    private void Shutdown()
    {
        quitting = true;
        Monitor.Stop();
        timer.Stop();
    }

    private void OnResized()
    {
        if (WindowState == FormWindowState.Minimized)
            return;
        LayoutChrome();
        pageView.Rebuild();
        RefreshView();
    }

    private void LayoutChrome()
    {
        var width = ClientSize.Width;
        var wide = WideNavigation;
        var columns = width < 650 ? 2 : 3;
        var rows = (navigation.Count + columns - 1) / columns;
        var navigationHeight = wide ? 0 : rows * 46 + 12;

        for (var index = 0; index < navigation.Count; index++)
        {
            var button = navigation[index];
            button.IsNavigation = wide;
            var columnWidth = (width - 24) / columns;
            button.Bounds = wide
                ? new Rectangle(18, 174 + 54 * index, 174, 44)
                : new Rectangle(12 + index % columns * columnWidth, 56 + index / columns * 46, columnWidth - 8, 40);
        }

        Viewport.Bounds = new Rectangle(wide ? RailWidth + 2 : 2, 54 + navigationHeight,
            width - (wide ? RailWidth + 4 : 4), Math.Max(1, ClientSize.Height - 100 - navigationHeight));
        Viewport.SetExtent(Viewport.Height);

        minimizeButton.Bounds = new Rectangle(width - 138, 8, 38, 32);
        maximizeButton.Bounds = new Rectangle(width - 94, 8, 38, 32);
        closeButton.Bounds = new Rectangle(width - 50, 8, 38, 32);
    }
}
