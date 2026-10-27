using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Inspector.App.Diagnostics;
using Inspector.App.Reports;
using Inspector.Core;

namespace Inspector.App;

public partial class App : Application
{
    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        DispatcherUnhandledException += (_, args) => { MessageBox.Show("The operation could not complete. No system settings were changed. Restart the application and try again.", "Atiq Laptop Inspector", MessageBoxButton.OK, MessageBoxImage.Error); args.Handled = true; };
        try
        {
            if (e.Args.Contains("--engine-check")) { Shutdown(SmartEngine.Verify() ? 0 : 1); return; }
            if (e.Args.Contains("--pipe-self-test")) { await SmartEngine.ElevatedAsync(CancellationToken.None, false); Shutdown(0); return; }
            if (e.Args.Length == 2 && e.Args[0] == "--advanced-collect") { await SmartEngine.SendElevatedAsync(e.Args[1]); Shutdown(0); return; }
            if (e.Args.Contains("--collect"))
            {
                var report = new HardwareCollector().Collect((p, stage) => { Console.Error.WriteLine($"{p}|{stage}"); Console.Error.Flush(); });
                Console.Out.Write(JsonSerializer.Serialize(report, JsonDefaults.Options)); Console.Out.Flush(); Shutdown(0); return;
            }
            if (e.Args.Length == 2 && (e.Args[0] == "--sample-report" || e.Args[0] == "--scan-report"))
            {
                Directory.CreateDirectory(e.Args[1]);
                var report = e.Args[0] == "--sample-report" ? Sample.Create() : await InspectionRunner.RunAsync(new Progress<(int, string)>(), CancellationToken.None);
                foreach (var ext in new[] { "pdf", "html", "json" }) ReportWriter.Write(report, Path.Combine(e.Args[1], (report.IsSample ? "Sample-Inspection-Report" : "Inspection-Report") + "." + ext));
                Shutdown(0); return;
            }
            var window = new MainWindow(e.Args.Contains("--demo") ? Sample.Create() : null);
            if (e.Args.Contains("--screenshot")) { window.Width = 1280; window.Height = 980; }
            MainWindow = window;
            window.Show();
            if (e.Args.Contains("--smoke-test"))
            {
                await Dispatcher.InvokeAsync(() => { }, DispatcherPriority.ApplicationIdle);
                window.UpdateLayout();
                window.VerifyViews();
                await Dispatcher.InvokeAsync(() => window.UpdateLayout(), DispatcherPriority.ApplicationIdle);
                if (!window.IsLoaded || window.ActualWidth < 900 || window.ActualHeight < 600) throw new InvalidOperationException("Startup layout failed.");
                var visual = (FrameworkElement)window.Content;
                var bitmap = new RenderTargetBitmap((int)visual.ActualWidth, (int)visual.ActualHeight, 96, 96, PixelFormats.Pbgra32);
                bitmap.Render(visual);
                int index = Array.IndexOf(e.Args, "--screenshot");
                if (index >= 0 && index + 1 < e.Args.Length)
                {
                    var encoder = new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bitmap));
                    using var file = File.Create(e.Args[index + 1]); encoder.Save(file);
                }
                window.Close(); Shutdown(0);
            }
        }
        catch (Exception)
        {
            if (e.Args.Length == 0) MessageBox.Show("Atiq Laptop Inspector could not start. Extract the entire ZIP to a writable folder and try again.", "Startup failed", MessageBoxButton.OK, MessageBoxImage.Error);
            Shutdown(1);
        }
    }
}
