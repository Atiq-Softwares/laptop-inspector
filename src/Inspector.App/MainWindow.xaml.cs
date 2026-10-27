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
    private bool light = true;
    private string hardwareCategory = "All";
    private readonly List<Button> nav = [];
    private readonly List<Button> scanButtons = [];
    public MainWindow(Inspection? initial = null)
    {
        InitializeComponent(); inspection = initial;
        Height = Math.Min(860, SystemParameters.WorkArea.Height - 12);
        Width = Math.Min(1280, SystemParameters.WorkArea.Width - 12);
        foreach (var (name, icon) in new[] { ("Overview", "01"), ("Hardware details", "02"), ("Manual checks", "03"), ("Export reports", "04"), ("About", "05") })
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
        PageTitle.Text = page == "Overview" ? "Device summary" : page;
        foreach (var b in nav) { bool active = Equals(b.Tag, page); b.Background = Brush(active ? "#35434B" : "#18232B"); b.Foreground = Brush(active ? "#F0CD86" : "#C5CFD4"); }
        if (inspection?.IsSample == true) PageContent.Children.Add(Notice("SAMPLE PREVIEW / Synthetic hardware data. Start a full inspection to replace this example with real readings."));
        switch (page) { case "Overview": Overview(); break; case "Hardware details": Hardware(); break; case "Manual checks": Manual(); break; case "Export reports": Reports(); break; default: About(); break; }
    }
    private static string V(Component? c, params string[] names) => names.Select(n => c?.Measurements.FirstOrDefault(m => m.Name == n && m.Evidence != Evidence.Unavailable)?.Value).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? "Not available";
    private void Overview()
    {
        if (inspection == null)
        {
            PageContent.Children.Add(Kicker("INSPECTION FILE / NEW"));
            PageContent.Children.Add(Heading("New inspection", 36));
            PageContent.Children.Add(Body("Read the hardware in this Windows PC. Review the figures, complete the operator checks and issue a report.",true,14));
            var actions = new WrapPanel { Margin = new(0,14,0,26) }; actions.Children.Add(ScanButton()); PageContent.Children.Add(actions);
            var panel = new StackPanel(); panel.Children.Add(Kicker("AUTOMATIC READINGS"));
            foreach(var (name,description) in new[]{("Processor","Model, cores, threads, clocks, cache and virtualization"),("Memory","Individual slots, capacities, part numbers, rates and voltage"),("Storage","Models, firmware, SMART, power-on hours, wear and temperature"),("Battery","Design capacity, current full-charge capacity, charge and condition"),("Platform","Graphics, BIOS, Windows, security and network hardware")}) panel.Children.Add(Readout(name,description));
            PageContent.Children.Add(Card(panel,new(0,0,0,20)));
            PageContent.Children.Add(Notice("Standard inspection runs without administrator access. If Windows restricts drive SMART access, an optional read-only retry can request permission. No stress test is started."));
            PageContent.Children.Add(Body("NEXT / 8 guided operator checks cover the keyboard, display, camera, sound, touchpad, ports and physical condition.",true,12));
            return;
        }
        var i = inspection;
        var header = new DockPanel(); var stamp = Badge(i.Overall); DockPanel.SetDock(stamp,Dock.Right); header.Children.Add(stamp);
        var identity = new StackPanel(); identity.Children.Add(Kicker("INSPECTION FILE / " + i.StartedAt.ToString("yyyyMMdd-HHmm"))); identity.Children.Add(Heading(i.Manufacturer + " " + i.Model,28)); identity.Children.Add(Body(i.CompletedAt?.ToString("dd MMM yyyy  /  HH:mm zzz") ?? "In progress",true,11)); header.Children.Add(identity); PageContent.Children.Add(header);
        var actionsRow = new WrapPanel {Margin = new(0,4,0,20)}; actionsRow.Children.Add(ScanButton()); actionsRow.Children.Add(ActionButton("Export inspection",()=>ShowPage("Export reports"))); actionsRow.Children.Add(ActionButton("Advanced drive access",AdvancedStorage)); PageContent.Children.Add(actionsRow);
        var metrics = new UniformGrid {Columns=3,Margin=new(-5,0,-5,20)};
        var cpu=i.Components.FirstOrDefault(c=>c.Category=="Processor"); var mem=i.Components.FirstOrDefault(c=>c.Category=="Memory");
        metrics.Children.Add(Metric("PROCESSOR",cpu?.Name ?? "Not available",V(cpu,"Physical cores") + " cores / " + V(cpu,"Logical processors") + " threads"));
        metrics.Children.Add(Metric("INSTALLED MEMORY",V(mem,"Installed capacity","Total installed capacity"),i.Components.Count(c=>c.Category=="Memory slot") + " firmware slot records"));
        var unavailable=i.Components.SelectMany(c=>c.Measurements).Count(m=>m.Evidence==Evidence.Unavailable); var measured=i.Components.Sum(c=>c.Measurements.Count)-unavailable;
        metrics.Children.Add(Metric("EVIDENCE COVERAGE",measured + " readings",unavailable + " unavailable / " + i.ManualChecks.Count(c=>c.Outcome==ManualOutcome.NotTested) + " operator checks pending")); PageContent.Children.Add(metrics);
        var detail = new Grid {Margin=new(0,0,0,18)}; detail.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)}); detail.ColumnDefinitions.Add(new(){Width=new(1,GridUnitType.Star)});
        var batteryPanel=new StackPanel(); batteryPanel.Children.Add(Kicker("01 / BATTERY"));
        foreach(var battery in i.Components.Where(c=>c.Category=="Battery"))
        {
            var value=V(battery,"Capacity retention"); batteryPanel.Children.Add(Heading(value == "Not available" ? "Capacity not verified" : value + " capacity retained",24));
            batteryPanel.Children.Add(Body(V(battery,"Condition"),true,12));
            if(double.TryParse(System.Text.RegularExpressions.Regex.Match(value,@"[0-9]+(?:[.,][0-9]+)?").Value.Replace(',','.'),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var health)) batteryPanel.Children.Add(new ProgressBar{Value=Math.Clamp(health,0,100),Maximum=100,Height=7,Margin=new(0,12,0,18)});
            foreach(var (key,label) in new[]{("Design capacity","Design capacity"),("Full-charge capacity","Full charge now"),("Current remaining capacity","Energy remaining"),("Current charge","Charge now"),("Cycle count","Cycle count"),("Power state","Power state")}) batteryPanel.Children.Add(Readout(label,V(battery,key)));
        }
        var batteryCard=Card(batteryPanel,new(0,0,8,0),20); detail.Children.Add(batteryCard);
        var memoryPanel=new StackPanel(); memoryPanel.Children.Add(Kicker("02 / MEMORY SLOTS"));
        var modules=i.Components.Where(c=>c.Category=="Memory slot").ToList();
        if(modules.Count==0) memoryPanel.Children.Add(Body("Slot locations were not exposed. Open the full memory inventory for available module details.",true));
        foreach(var slot in modules.Take(4))
        {
            var block=new StackPanel{Margin=new(0,8,0,8)}; block.Children.Add(Text(slot.Name.ToUpperInvariant(),11,true));
            block.Children.Add(Body(V(slot,"Slot state")=="Empty (firmware reported)" ? "EMPTY / firmware reported" : V(slot,"Module capacity") + "  /  " + V(slot,"Memory type") + "  /  " + V(slot,"Configured data rate"),false,13));
            if(V(slot,"Slot state")!="Empty (firmware reported)") {block.Children.Add(Body(V(slot,"Manufacturer")+"  "+V(slot,"Part number"),true,10));block.Children.Add(Body("Rated "+V(slot,"Rated data rate")+" / DDR clock ~"+V(slot,"Equivalent DDR clock"),true,10));}
            memoryPanel.Children.Add(new Border{BorderBrush=Resource("Line"),BorderThickness=new(3,0,0,0),Padding=new(12,0,0,0),Margin=new(0,4,0,8),Child=block});
        }
        memoryPanel.Children.Add(ActionButton("Open all memory readings",()=>{hardwareCategory="Memory";ShowPage("Hardware details");}));
        var memoryCard=Card(memoryPanel,new(8,0,0,0),20);Grid.SetColumn(memoryCard,1);detail.Children.Add(memoryCard); PageContent.Children.Add(detail);
        PageContent.Children.Add(Kicker("03 / STORAGE DEVICES"));
        foreach(var disk in i.Components.Where(c=>c.Category=="Storage" && !c.Name.Contains("unmapped")))
        {
            var panel=new StackPanel(); var top=new DockPanel(); var badge=Badge(disk.Rating);DockPanel.SetDock(badge,Dock.Right);top.Children.Add(badge);top.Children.Add(Heading(disk.Name,20));panel.Children.Add(top);
            var values=new UniformGrid{Columns=3};
            foreach(var(key,label) in new[]{("Capacity","CAPACITY"),("Power-on hours","POWER-ON HOURS"),("Wear used","ENDURANCE USED"),("Power cycles","POWER CYCLES"),("Drive temperature","TEMPERATURE"),("SMART overall result","SMART RESULT")}){var v=new StackPanel{Margin=new(0,8,12,8)};v.Children.Add(Body(label,true,9));v.Children.Add(Text(V(disk,key),13,true));values.Children.Add(v);} panel.Children.Add(values);
            PageContent.Children.Add(Card(panel,new(0,0,0,14),20));
        }
        PageContent.Children.Add(Heading("Inspection notes",20));
        foreach(var c in i.Components.Where(c=>c.Rating is Rating.Caution or Rating.Critical)) PageContent.Children.Add(Notice(c.Category+" / "+c.Name+" — "+c.Explanation));
        PageContent.Children.Add(Body(i.Assessment,true,12));
        PageContent.Children.Add(ActionButton("Review every measurement",()=>{hardwareCategory="All";ShowPage("Hardware details");}));
    }
    private Border Metric(string label,string value,string note)
    {
        var p=new StackPanel();p.Children.Add(Kicker(label));p.Children.Add(Text(value,18,true));p.Children.Add(Body(note,true,11));return Card(p,new(5,0,5,0),18);
    }
    private Border Readout(string label,string value)
    {
        var grid=new Grid{Margin=new(0,8,0,8)};grid.ColumnDefinitions.Add(new(){Width=new(145)});grid.ColumnDefinitions.Add(new());
        grid.Children.Add(Body(label,true,12,new(0)));var text=Text(value,12,true);Grid.SetColumn(text,1);grid.Children.Add(text);
        return new Border{BorderBrush=Resource("Line"),BorderThickness=new(0,0,0,1),Child=grid};
    }
    private void Hardware()
    {
        if(!RequireScan())return;
        PageContent.Children.Add(Kicker("COMPONENT REGISTER / FULL READOUT"));PageContent.Children.Add(Heading("Hardware details",28));
        var filters=new DockPanel{Margin=new(0,8,0,18)};
        var category=new ComboBox{ItemsSource=new[]{"All","Processor","Memory","Storage","Volumes","Battery","Display & graphics","Motherboard & BIOS","Windows & security","Network & Bluetooth","Temperature & reliability"},SelectedItem=hardwareCategory,Margin=new(0,0,12,0),MinWidth=200};
        DockPanel.SetDock(category,Dock.Left);filters.Children.Add(category);
        var search=new TextBox{ToolTip="Filter by hardware name, field or value",MinWidth=260};System.Windows.Automation.AutomationProperties.SetName(search,"Search hardware readings");filters.Children.Add(search);PageContent.Children.Add(filters);
        category.SelectionChanged+=(_,_)=>{hardwareCategory=category.SelectedItem?.ToString()??"All";ShowPage("Hardware details");};
        var records=new List<Border>();
        foreach(var c in inspection!.Components.Where(c=>hardwareCategory=="All" || c.Category==hardwareCategory || hardwareCategory=="Memory" && c.Category=="Memory slot"))
        {
            var stack=new StackPanel();stack.Children.Add(Kicker(c.Category));stack.Children.Add(Heading(c.Name,22));stack.Children.Add(Badge(c.Rating));stack.Children.Add(Body(c.Explanation,true,12));
            foreach(var m in c.Measurements)
            {
                var row=new Grid{Margin=new(0,9,0,9)};row.ColumnDefinitions.Add(new(){Width=new(205)});row.ColumnDefinitions.Add(new());row.Children.Add(Body(m.Name,true,12,new(0)));
                var v=new StackPanel();v.Children.Add(Text(m.Value,13,true));v.Children.Add(Body(m.Evidence+" / "+m.Source,true,10,new(0,4,0,0)));Grid.SetColumn(v,1);row.Children.Add(v);stack.Children.Add(new Border{BorderBrush=Resource("Line"),BorderThickness=new(0,0,0,1),Child=row});
            }
            foreach(var recommendation in c.Recommendations)stack.Children.Add(Body("Recommendation / "+recommendation,true,12));
            var card=Card(stack,new(0,0,0,16),22);card.Tag=c.Name+" "+c.Category+" "+string.Join(" ",c.Measurements.Select(m=>m.Name+" "+m.Value));records.Add(card);PageContent.Children.Add(card);
        }
        search.TextChanged+=(_,_)=>{foreach(var card in records)card.Visibility=card.Tag.ToString()!.Contains(search.Text,StringComparison.OrdinalIgnoreCase)?Visibility.Visible:Visibility.Collapsed;};
        PageContent.Children.Add(Heading("Provider coverage",19));foreach(var limit in inspection.Limitations.Distinct())PageContent.Children.Add(Body(limit,true,11));
    }
    private async void AdvancedStorage()
    {
        if(inspection==null || scan!=null)return;
        if(inspection.IsSample){MessageBox.Show("Run a real inspection before requesting advanced drive access.","Sample preview");return;}
        if(MessageBox.Show("This optional operation asks Windows for administrator permission to read restricted SMART logs. It reads drive information only; it does not run tests, change settings or install a service. Cancel the Windows prompt if you do not want to grant access. Continue?","Advanced drive access",MessageBoxButton.YesNo,MessageBoxImage.Information)!=MessageBoxResult.Yes)return;
        scan=new CancellationTokenSource();var cancellation=scan;ProgressArea.Visibility=Visibility.Visible;ProgressText.Text="Reading restricted drive information…";ScanProgress.IsIndeterminate=true;
        try
        {
            var data=await SmartEngine.ElevatedAsync(cancellation.Token);int changed=0;
            foreach(var disk in inspection.Components.Where(c=>c.Category=="Storage")){var matches=data.Where(d=>SmartEngine.Matches(disk,d)).ToList();if(matches.Count==1){DriveSmart.Apply(disk,matches[0].Facts);changed++;}}
            FooterState.Text=$"Advanced drive access complete / {changed} drive records updated";
            if(changed==0)MessageBox.Show("No additional matched drive data was available. Some controllers do not expose SMART even with elevated access.","Drive information");
        }
        catch(System.ComponentModel.Win32Exception){FooterState.Text="Advanced access cancelled or unavailable";}
        catch(OperationCanceledException){FooterState.Text="Advanced read stopped";}
        catch(Exception){FooterState.Text="Advanced access unavailable";MessageBox.Show("No advanced data was received. The helper must run as the same Windows user, and the bundled drive reader must be intact. Your existing inspection is retained.","Drive information");}
        finally{scan=null;cancellation.Dispose();ScanProgress.IsIndeterminate=false;ProgressArea.Visibility=Visibility.Collapsed;if(IsVisible)ShowPage(currentPage);}
    }
    private void Manual()
    {
        if (!RequireScan()) return;
        PageContent.Children.Add(Heading("Operator checks", 26));
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
        PageContent.Children.Add(Kicker("REPORT DESK / ISSUE A RECORD")); PageContent.Children.Add(Heading("Export inspection report", 28));
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
        PageContent.Children.Add(Kicker("ATIQ-SOFTWARES")); PageContent.Children.Add(Heading("Laptop Inspector", 34)); PageContent.Children.Add(Body("Version 1.1.0 / Portable Windows x64 / .NET 10 LTS", true));
        var stack = new StackPanel(); stack.Children.Add(Heading("Local inspection engine", 22));
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
        var values = light ? new[] { "#F3F1EB", "#FFFFFF", "#E9E6DF", "#18232B", "#647078", "#D4D4CD", "#996A22" } : new[] { "#141D23", "#1D2A32", "#2B3942", "#F2F1EB", "#ADBAC1", "#3B4B54", "#E6BB69" };
        var keys = new[] { "Canvas", "Surface", "SurfaceAlt", "Ink", "Muted", "Line", "Accent" };
        for (int n = 0; n < keys.Length; n++) Application.Current.Resources[keys[n]] = Brush(values[n]);
        ThemeButton.Content = light ? "Dark theme" : "Light theme"; ShowPage(currentPage);
    }
    private void GitHub_Click(object sender, RoutedEventArgs e) => OpenUri(Branding.GitHub);
    internal void VerifyViews()
    {
        foreach (var page in new[] { "Hardware details", "Manual checks", "Export reports", "About", "Overview" }) { ShowPage(page); UpdateLayout(); if (PageContent.Children.Count == 0) throw new InvalidOperationException("Empty application view."); }
        if(light) Theme_Click(this, new RoutedEventArgs());
        Theme_Click(this, new RoutedEventArgs());
        if (Application.Current.Resources["Canvas"] is not SolidColorBrush canvas || canvas.Color != Color.FromRgb(243, 241, 235)) throw new InvalidOperationException("Light theme unavailable.");
    }
    private void OpenLocal(string name) { var path = Path.Combine(AppContext.BaseDirectory, name); if (File.Exists(path)) OpenUri(path); else MessageBox.Show("This file is included in the release ZIP. Extract all files from the downloaded archive."); }
    private void OpenUri(string uri) { try { Process.Start(new ProcessStartInfo(uri) { UseShellExecute = true }); } catch (Exception) { MessageBox.Show("Windows could not open this link or application. Open it manually from Start or your browser.", "Open link"); } }
    private static SolidColorBrush Brush(string hex) => (SolidColorBrush)new BrushConverter().ConvertFromString(hex)!;
    private static Brush Resource(string name) => (Brush)Application.Current.FindResource(name);
    private TextBlock Text(string text, double size = 14, bool bold = false, string? color = null) => new() { Text = text, FontSize = size, FontWeight = bold ? FontWeights.SemiBold : FontWeights.Normal, Foreground = color == null ? Resource("Ink") : Brush(color), TextWrapping = TextWrapping.Wrap };
    private TextBlock Body(string text, bool muted = false, double size = 13, Thickness? margin = null) { var t = Text(text, size); t.Foreground = Resource(muted ? "Muted" : "Ink"); t.LineHeight = size * 1.55; t.Margin = margin ?? new Thickness(0, 5, 0, 6); return t; }
    private TextBlock Heading(string text, double size = 24) { var t = Text(text, size, true); t.Margin = new(0, 3, 0, 10); return t; }
    private TextBlock Kicker(string text) { var t = Text(text.ToUpperInvariant(), 10, true); t.Foreground = Resource("Accent"); t.Margin = new(0, 0, 0, 10); return t; }
    private Border Card(UIElement child, Thickness? margin = null, double padding = 24) => new() { Background = Resource("Surface"), BorderBrush = Resource("Line"), BorderThickness = new(1), CornerRadius = new(3), Padding = new(padding), Child = child, Margin = margin ?? new Thickness(0) };
    private Border Notice(string text) => new() { Background = Resource("SurfaceAlt"), BorderBrush = Resource("Line"), BorderThickness = new(1), CornerRadius = new(3), Padding = new(15), Child = Body(text, true, 12, new(0)), Margin = new(0, 14, 0, 14) };
    private Border SmallInfo(string label, string title, string text) { var p = new StackPanel(); p.Children.Add(Kicker(label)); p.Children.Add(Heading(title, 17)); p.Children.Add(Body(text, true, 12)); return Card(p, new(6, 0, 6, 0), 18); }
    private Button ActionButton(string text, Action action, bool primary = false) { var b = new Button { Content = text, Margin = new(0, 8, 10, 0), HorizontalAlignment = HorizontalAlignment.Left }; if (primary) { b.Background = Brush("#D9AC5E"); b.Foreground = Brush("#18232B"); b.BorderThickness = new(0); } b.Click += (_, _) => action(); return b; }
    private string StatusColor(Rating r) => r switch { Rating.Good => light ? "#16683E" : "#77E5AE", Rating.Caution => light ? "#885500" : "#F6C97B", Rating.Critical => light ? "#AB2828" : "#FDA4A4", _ => light ? "#536579" : "#B4C2D4" };
    private Border Badge(Rating rating) => new() { Child = Text("●  " + Evaluation.Label(rating), 10, true, StatusColor(rating)), Background = Resource("SurfaceAlt"), CornerRadius = new(3), Padding = new(8, 5, 8, 5), HorizontalAlignment = HorizontalAlignment.Left, VerticalAlignment = VerticalAlignment.Top, Margin = new(0, 9, 0, 9) };
}
