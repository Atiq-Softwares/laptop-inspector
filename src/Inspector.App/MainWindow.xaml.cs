using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using Inspector.App.Diagnostics;
using Inspector.App.Reports;
using Inspector.Core;
using Microsoft.Win32;

namespace Inspector.App;

public partial class MainWindow : Window
{
    private Inspection? inspection;
    private CancellationTokenSource? scan;
    private string currentPage = "Overview";
    private bool light;
    private readonly List<Button> nav = [];
    private readonly List<Button> scanButtons = [];
    public MainWindow(Inspection? initial = null)
    {
        InitializeComponent(); inspection = initial;
        Height = Math.Min(860, SystemParameters.WorkArea.Height - 12);
        Width = Math.Min(1280, SystemParameters.WorkArea.Width - 12);
        foreach (var (name, icon) in new[] { ("Overview", "◈"), ("Hardware details", "▤"), ("Manual checks", "✓"), ("Export reports", "↗"), ("About", "ⓘ") })
        {
            var button = new Button { Content = icon + "   " + name, Tag = name, Margin = new(0, 0, 0, 7), HorizontalContentAlignment = HorizontalAlignment.Left, Foreground = Brush("#B6C6DA"), BorderThickness = new(0), Padding = new(12, 10, 12, 10) };
            button.Click += (_, _) => ShowPage(name); Navigation.Children.Add(button); nav.Add(button);
        }
        Closing += (_, _) => scan?.Cancel();
        Closed += (_, _) => Application.Current.Shutdown();
        ShowPage("Overview");
    }
    private void ShowPage(string page)
    {
        currentPage = page; PageContent.Children.Clear(); scanButtons.Clear(); PageScroll.ScrollToTop();
        PageTitle.Text = page == "Overview" ? "Inspection overview" : page;
        foreach (var b in nav) { bool active = Equals(b.Tag, page); b.Background = Brush(active ? "#173C43" : "#0A1423"); b.Foreground = Brush(active ? "#75E9D2" : "#B6C6DA"); }
        if (inspection?.IsSample == true) PageContent.Children.Add(Notice("SAMPLE PREVIEW / Synthetic hardware data. Start a full inspection to replace this example with real readings."));
        switch (page) { case "Overview": Overview(); break; case "Hardware details": Hardware(); break; case "Manual checks": Manual(); break; case "Export reports": Reports(); break; default: About(); break; }
    }
    private void Overview()
    {
        var hero = new StackPanel();
        hero.Children.Add(Kicker(inspection == null ? "A CLEARER PICTURE, BEFORE YOU DECIDE" : "YOUR INSPECTION AT A GLANCE"));
        hero.Children.Add(Heading(inspection == null ? "Know your next laptop." : inspection.Manufacturer + " " + inspection.Model, 30));
        hero.Children.Add(Body(inspection == null ? "Understand the hardware. Spot the warnings. Make an informed decision.\nOne local inspection, with every unknown clearly explained." : inspection.Assessment));
        var actions = new WrapPanel { Margin = new(0, 20, 0, 0) };
        actions.Children.Add(ScanButton());
        if (inspection != null) actions.Children.Add(ActionButton("Export report  ↗", () => ShowPage("Export reports"), false));
        hero.Children.Add(actions);
        if (inspection == null) hero.Children.Add(Body("No account  ·  No installation  ·  No automatic stress tests", true, 11, new(0, 15, 0, 0)));
        PageContent.Children.Add(Card(hero, new(0, 0, 0, 22)));
        if (inspection == null)
        {
            var grid = new UniformGrid { Columns = 3, Margin = new(-6, 0, -6, 20) };
            grid.Children.Add(SmallInfo("01 / SCAN", "Inspect the essentials", "CPU, RAM, storage, battery, graphics, security and available sensors."));
            grid.Children.Add(SmallInfo("02 / VERIFY", "Check what matters", "Guided checks for the screen, keyboard, camera, sound, ports and condition."));
            grid.Children.Add(SmallInfo("03 / KEEP", "Take a clear report", "Branded PDF, HTML and JSON exports. No data leaves the device automatically."));
            PageContent.Children.Add(grid);
            PageContent.Children.Add(Heading("Built for a careful decision", 20));
            PageContent.Children.Add(Body("A detected component is not proof of good health. This inspection separates Windows-reported measurements, estimates, unsupported values and manual checks."));
            PageContent.Children.Add(Notice("Some laptops restrict battery, SMART or temperature readings. Missing information will show as NOT VERIFIED. Basic inspection never requests administrator access."));
            return;
        }
        var stats = new UniformGrid { Columns = 4, Margin = new(-5, 0, -5, 20) };
        foreach (var rating in Enum.GetValues<Rating>())
        {
            var panel = new StackPanel(); panel.Children.Add(Text(inspection.Components.Count(c => c.Rating == rating).ToString("00"), 27, true, StatusColor(rating))); panel.Children.Add(Body(Evaluation.Label(rating), true, 10));
            stats.Children.Add(Card(panel, new(5, 0, 5, 0), 16));
        }
        PageContent.Children.Add(stats);
        var line = new DockPanel { Margin = new(0, 0, 0, 12) }; line.Children.Add(Heading("Component overview", 20));
        PageContent.Children.Add(line);
        var cards = new UniformGrid { Columns = 2, Margin = new(-5, 0, -5, 0) };
        foreach (var c in inspection.Components)
        {
            var stack = new StackPanel(); stack.Children.Add(Kicker(c.Category)); stack.Children.Add(Text(c.Name, 16, true)); stack.Children.Add(Badge(c.Rating)); stack.Children.Add(Body(c.Explanation, true, 12));
            cards.Children.Add(Card(stack, new(5, 0, 5, 10), 18));
        }
        PageContent.Children.Add(cards);
        var pending = inspection.ManualChecks.Count(c => c.Outcome == ManualOutcome.NotTested);
        PageContent.Children.Add(Notice($"{pending} manual checks remain. Overall assessment: {Evaluation.Label(inspection.Overall)}. No reliability guarantee is implied."));
        PageContent.Children.Add(ActionButton("Continue to manual checks", () => ShowPage("Manual checks")));
    }
    private void Hardware()
    {
        if (!RequireScan()) return;
        PageContent.Children.Add(Heading("The details behind the assessment", 26));
        PageContent.Children.Add(Body("Measured values are reported by Windows or firmware. Estimates and unavailable values are labelled alongside each result.", true));
        foreach (var c in inspection!.Components)
        {
            var stack = new StackPanel(); stack.Children.Add(Kicker(c.Category)); stack.Children.Add(Heading(c.Name, 20)); stack.Children.Add(Badge(c.Rating)); stack.Children.Add(Body(c.Explanation));
            foreach (var m in c.Measurements)
            {
                var row = new Grid { Margin = new(0, 8, 0, 8) }; row.ColumnDefinitions.Add(new() { Width = new(185) }); row.ColumnDefinitions.Add(new());
                row.Children.Add(Body(m.Name, true, 12)); var value = new StackPanel(); value.Children.Add(Text(m.Value, 13, true)); value.Children.Add(Body(m.Evidence + " / " + m.Source, true, 10)); Grid.SetColumn(value, 1); row.Children.Add(value);
                stack.Children.Add(new Border { BorderBrush = Resource("Line"), BorderThickness = new(0, 0, 0, 1), Child = row });
            }
            foreach (var r in c.Recommendations) stack.Children.Add(Body("Recommendation: " + r, true, 12, new(0, 12, 0, 0)));
            PageContent.Children.Add(Card(stack, new(0, 16, 0, 0)));
        }
        PageContent.Children.Add(Heading("Coverage notes", 20)); foreach (var s in inspection.Limitations.Distinct()) PageContent.Children.Add(Body("• " + s, true, 12));
    }
    private void Manual()
    {
        if (!RequireScan()) return;
        PageContent.Children.Add(Heading("Software cannot verify everything.", 26));
        PageContent.Children.Add(Body("Follow each guide, then record what you observed. These are operator-entered results. Use NOT APPLICABLE only when the laptop does not have that feature.", true));
        PageContent.Children.Add(Notice("Manual notes stay in memory and are excluded from exports by default. Do not enter passwords, personal identifiers or private recordings."));
        foreach (var c in inspection!.ManualChecks)
        {
            var stack = new StackPanel();
            var top = new DockPanel(); var combo = new ComboBox { ItemsSource = Enum.GetValues<ManualOutcome>().Select(v => new ComboBoxItem { Content = ReportWriter.Outcome(v), Tag = v }).ToList(), SelectedIndex = (int)c.Outcome, HorizontalAlignment = HorizontalAlignment.Right, Margin = new(18, 0, 0, 0) };
            DockPanel.SetDock(combo, Dock.Right); top.Children.Add(combo); top.Children.Add(Heading(c.Name, 20)); stack.Children.Add(top);
            combo.SelectionChanged += (_, _) => { if (combo.SelectedItem is ComboBoxItem selected) { c.Outcome = (ManualOutcome)selected.Tag; FooterState.Text = "Manual result recorded in memory"; } };
            stack.Children.Add(Body(c.Instructions, true, 13));
            if (c.Name == "Keyboard") stack.Children.Add(ActionButton("Open keyboard checker", () => new KeyboardWindow { Owner = this }.ShowDialog()));
            if (c.Name == "Display") stack.Children.Add(ActionButton("Open full-screen color test", () => new DisplayWindow { Owner = this }.ShowDialog()));
            if (c.Name == "Webcam") stack.Children.Add(ActionButton("Open Windows Camera", () => OpenUri("microsoft.windows.camera:")));
            if (c.Name is "Microphone" or "Speakers") stack.Children.Add(ActionButton("Open Windows Sound settings", () => OpenUri("ms-settings:sound")));
            var notes = new TextBox { Text = c.Notes, MaxLength = 1000, AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 54, Margin = new(0, 12, 0, 0), ToolTip = "Optional operator notes. Excluded from exports by default." };
            System.Windows.Automation.AutomationProperties.SetName(notes, c.Name + " notes (optional)");
            notes.TextChanged += (_, _) => c.Notes = notes.Text;
            stack.Children.Add(Body("OPTIONAL NOTES", true, 10, new(0, 14, 0, 0))); stack.Children.Add(notes);
            PageContent.Children.Add(Card(stack, new(0, 14, 0, 0)));
        }
    }
    private void Reports()
    {
        if (!RequireScan()) return;
        PageContent.Children.Add(Kicker("A RECORD YOU CAN KEEP")); PageContent.Children.Add(Heading("A professional report. Your choice of format.", 28));
        PageContent.Children.Add(Body("Save an inspection for a buyer, service desk, shop or asset record. Exports include all findings, evidence sources, warnings and manual-check outcomes.", true));
        var stack = new StackPanel(); stack.Children.Add(Badge(inspection!.Overall)); stack.Children.Add(Heading(inspection.Manufacturer + " / " + inspection.Model, 21)); stack.Children.Add(Body(inspection.Assessment));
        stack.Children.Add(Body($"{inspection.Components.Count} component records  ·  {inspection.ManualChecks.Count(c => c.Outcome != ManualOutcome.NotTested)} / {inspection.ManualChecks.Count} manual outcomes recorded", true));
        PageContent.Children.Add(Card(stack, new(0, 20, 0, 20)));
        var include = new CheckBox { Content = "Include my manual notes in the exported report", IsChecked = false, Margin = new(0, 0, 0, 12) }; PageContent.Children.Add(include);
        PageContent.Children.Add(Body(Branding.Privacy, true, 12));
        var formats = new UniformGrid { Columns = 3, Margin = new(-6, 22, -6, 12) };
        foreach (var (ext, title, description) in new[] { ("pdf", "PDF document", "Branded A4 pages, summary and clickable GitHub link. Ready to print or share."), ("html", "HTML report", "A complete, self-contained report. Opens offline in a web browser."), ("json", "JSON data", "Structured findings and evidence for your own tools and records.") })
        {
            var card = new StackPanel(); card.Children.Add(Kicker(ext.ToUpperInvariant())); card.Children.Add(Heading(title, 18)); card.Children.Add(Body(description, true, 12));
            card.Children.Add(ActionButton("Save " + ext.ToUpperInvariant(), () => Export(ext, include.IsChecked == true), ext == "pdf")); formats.Children.Add(Card(card, new(6, 0, 6, 0), 18));
        }
        PageContent.Children.Add(formats); PageContent.Children.Add(Notice(Branding.Disclaimer));
    }
    private void About()
    {
        PageContent.Children.Add(Kicker("ATIQ-SOFTWARES")); PageContent.Children.Add(Heading("Laptop Inspector", 34)); PageContent.Children.Add(Body("Version 1.0.0 / Portable Windows x64 / .NET 10 LTS", true));
        var stack = new StackPanel(); stack.Children.Add(Heading("Hardware clarity, with honest limits.", 22));
        stack.Children.Add(Body("A local, read-only hardware inventory and condition assessment for Windows 10 and 11. Basic use does not require an administrator account or internet connection. No accounts, telemetry, automatic uploads or hidden system changes."));
        stack.Children.Add(Body("Temperature, battery and SMART support depend on firmware, drivers and Windows permissions. The application uses documented Windows providers and does not install kernel drivers. No stress tests are run automatically."));
        stack.Children.Add(Body(Branding.Disclaimer, true)); stack.Children.Add(Body(Branding.Privacy, true));
        PageContent.Children.Add(Card(stack, new(0, 24, 0, 20)));
        PageContent.Children.Add(Text(Branding.Copyright, 14, true)); PageContent.Children.Add(Body(Branding.DevelopedBy));
        PageContent.Children.Add(ActionButton("GitHub: Atiq-Softwares  ↗", () => OpenUri(Branding.GitHub)));
        PageContent.Children.Add(Heading("Open source", 20)); PageContent.Children.Add(Body("Original application code is licensed under MIT. Third-party components retain their own licenses. Full notices and the .NET runtime license are included with the extracted application.", true));
        PageContent.Children.Add(ActionButton("Open third-party notices", () => OpenLocal("THIRD-PARTY-NOTICES.txt")));
    }
    private bool RequireScan()
    {
        if (inspection != null) return true;
        PageContent.Children.Add(Heading("Start with a full inspection.", 28)); PageContent.Children.Add(Body(scan == null ? "Run the local scan to collect real hardware information. Then review the details, complete manual checks and save a report." : "Your inspection is in progress. Results will appear when the scan completes.", true)); PageContent.Children.Add(ScanButton()); return false;
    }
    private Button ScanButton() { var b = ActionButton(scan == null ? "Start Full Inspection  →" : "Inspection in progress…", StartScan, true); b.IsEnabled = scan == null; scanButtons.Add(b); return b; }
    private async void StartScan()
    {
        if (scan != null) return;
        scan = new CancellationTokenSource(); var cancellation = scan;
        inspection = null; ShowPage("Overview"); foreach (var b in scanButtons) b.IsEnabled = false;
        ProgressArea.Visibility = Visibility.Visible; ScanProgress.Value = 0; ProgressText.Text = "Preparing local inspection…"; FooterState.Text = "Read-only inspection in progress";
        try
        {
            var progress = new Progress<(int Percent, string Stage)>(p => { ScanProgress.Value = p.Percent; ProgressText.Text = $"{p.Stage}  /  {p.Percent}%"; });
            inspection = await InspectionRunner.RunAsync(progress, cancellation.Token);
            FooterState.Text = "Inspection completed  ·  " + inspection.CompletedAt?.ToString("HH:mm");
        }
        catch (OperationCanceledException) { FooterState.Text = "Inspection cancelled  ·  No results saved"; }
        catch (Exception ex) { FooterState.Text = "Inspection could not complete"; MessageBox.Show(ex is TimeoutException ? ex.Message : "The local diagnostic worker could not complete. Try again and check that Windows WMI services are available. No system settings were changed.", "Inspection unavailable", MessageBoxButton.OK, MessageBoxImage.Warning); }
        finally { scan = null; cancellation.Dispose(); ProgressArea.Visibility = Visibility.Collapsed; if (IsVisible) ShowPage(currentPage); }
    }
    private void Cancel_Click(object sender, RoutedEventArgs e) => scan?.Cancel();
    private async void Export(string extension, bool includeNotes)
    {
        if (inspection == null) return;
        var dialog = new SaveFileDialog { Title = "Save Atiq-Softwares inspection report", FileName = $"{(inspection.IsSample ? "Sample-" : "")}Laptop-Inspection-{DateTime.Now:yyyy-MM-dd}.{extension}", DefaultExt = extension, Filter = $"{extension.ToUpperInvariant()} report (*.{extension})|*.{extension}", AddExtension = true, OverwritePrompt = true };
        if (dialog.ShowDialog(this) != true) return;
        // Snapshot on the UI thread; export off-thread without racing operator edits.
        var snapshot = ExportPrivacy.Prepare(inspection, includeNotes);
        try { FooterState.Text = "Saving report…"; await Task.Run(() => ReportWriter.Write(snapshot, dialog.FileName, includeNotes)); FooterState.Text = "Report saved  ·  " + Path.GetFileName(dialog.FileName); MessageBox.Show("Your report was saved successfully. Review it before sharing.", "Report saved", MessageBoxButton.OK, MessageBoxImage.Information); }
        catch (Exception) { FooterState.Text = "Report could not be saved"; MessageBox.Show("Cannot save the report. Check the selected folder, available disk space and whether the file is open in another application.", "Export failed", MessageBoxButton.OK, MessageBoxImage.Error); }
    }
    private void Theme_Click(object sender, RoutedEventArgs e)
    {
        light = !light;
        var values = light ? new[] { "#F0F4F8", "#FFFFFF", "#E8EEF5", "#17283E", "#536579", "#D7E1EC", "#087F79" } : new[] { "#101B2C", "#18263A", "#203149", "#F0F5FA", "#A8B9CF", "#304159", "#55D6BE" };
        var keys = new[] { "Canvas", "Surface", "SurfaceAlt", "Ink", "Muted", "Line", "Accent" };
        for (int n = 0; n < keys.Length; n++) Application.Current.Resources[keys[n]] = Brush(values[n]);
        ThemeButton.Content = light ? "Dark theme" : "Light theme"; ShowPage(currentPage);
    }
    private void GitHub_Click(object sender, RoutedEventArgs e) => OpenUri(Branding.GitHub);
    internal void VerifyViews()
    {
        foreach (var page in new[] { "Hardware details", "Manual checks", "Export reports", "About", "Overview" }) { ShowPage(page); UpdateLayout(); if (PageContent.Children.Count == 0) throw new InvalidOperationException("Empty application view."); }
        Theme_Click(this, new RoutedEventArgs());
        if (Application.Current.Resources["Canvas"] is not SolidColorBrush canvas || canvas.Color != Color.FromRgb(240, 244, 248)) throw new InvalidOperationException("Light theme unavailable.");
        Theme_Click(this, new RoutedEventArgs());
    }
    private void OpenLocal(string name) { var path = Path.Combine(AppContext.BaseDirectory, name); if (File.Exists(path)) OpenUri(path); else MessageBox.Show("This file is included in the release ZIP. Extract all files from the downloaded archive."); }
    private void OpenUri(string uri) { try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch (Exception) { MessageBox.Show("Windows could not open this link or application. Open it manually from Start or your browser.", "Open link"); } }
    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    private static Brush Resource(string name) => (Brush)Application.Current.FindResource(name);
    private TextBlock Text(string text, double size = 14, bool bold = false, string? color = null) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = color == null ? Resource("Ink") : Brush(color), TextWrapping = TextWrapping.Wrap };
    private TextBlock Body(string text, bool muted = false, double size = 13, Thickness? margin = null) { var t = Text(text, size); t.Foreground = Resource(muted ? "Muted" : "Ink"); t.LineHeight = size * 1.55; t.Margin = margin ?? new Thickness(0, 5, 0, 6); return t; }
    private TextBlock Heading(string text, double size = 24) { var t = Text(text, size, true); t.Margin = new(0, 3, 0, 10); return t; }
    private TextBlock Kicker(string text) { var t = Text(text.ToUpperInvariant(), 10, true); t.Foreground = Resource("Accent"); t.Margin = new(0, 0, 0, 10); return t; }
    private Border Card(UIElement child, Thickness? margin = null, double padding = 24) => new() { Background = Resource("Surface"), BorderBrush = Resource("Line"), BorderThickness = new(1), CornerRadius = new(12), Padding = new(padding), Child = child, Margin = margin ?? new Thickness(0) };
    private Border Notice(string text) => new() { Background = Resource("SurfaceAlt"), BorderBrush = Resource("Line"), BorderThickness = new(1), CornerRadius = new(8), Padding = new(15), Child = Body(text, true, 12, new(0)), Margin = new(0, 14, 0, 14) };
    private Border SmallInfo(string label, string title, string text) { var p = new StackPanel(); p.Children.Add(Kicker(label)); p.Children.Add(Heading(title, 17)); p.Children.Add(Body(text, true, 12)); return Card(p, new(6, 0, 6, 0), 18); }
    private Button ActionButton(string text, Action action, bool primary = false) { var b = new Button { Content = text, Margin = new(0, 8, 10, 0), HorizontalAlignment = HorizontalAlignment.Left }; if (primary) { b.Background = Brush("#55D6BE"); b.Foreground = Brush("#092D2E"); b.BorderThickness = new(0); } b.Click += (_, _) => action(); return b; }
    private string StatusColor(Rating r) => r switch { Rating.Good => light ? "#16683E" : "#77E5AE", Rating.Caution => light ? "#885500" : "#F6C97B", Rating.Critical => light ? "#AB2828" : "#FDA4A4", _ => light ? "#536579" : "#B4C2D4" };
    private Border Badge(Rating rating) => new() { Child = Text("●  " + Evaluation.Label(rating), 10, true, StatusColor(rating)), Background = Resource("SurfaceAlt"), CornerRadius = new(4), Padding = new(8, 5, 8, 5), HorizontalAlignment = HorizontalAlignment.Left, Margin = new(0, 9, 0, 9) };
}
