using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Inspector.Core;

namespace Inspector.App.Diagnostics;

public static class InspectionRunner
{
    public static async Task<Inspection> RunAsync(IProgress<(int Percent, string Stage)> progress, CancellationToken token)
    {
        var executable = Environment.ProcessPath ?? throw new InvalidOperationException("Executable path unavailable.");
        var start = new ProcessStartInfo(executable) { UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        if (Path.GetFileNameWithoutExtension(executable).Equals("dotnet", StringComparison.OrdinalIgnoreCase)) start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory, "Atiq-Laptop-Inspector.dll"));
        start.ArgumentList.Add("--collect");
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Cannot start the local diagnostic worker.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var registration = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } });
        var output = process.StandardOutput.ReadToEndAsync(timeout.Token);
        var stages = Task.Run(async () =>
        {
            while (await process.StandardError.ReadLineAsync(timeout.Token) is string line)
            {
                var split = line.Split('|', 2);
                if (split.Length == 2 && int.TryParse(split[0], out var percent)) progress.Report((Math.Clamp(percent, 0, 100), split[1]));
            }
        }, timeout.Token);
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await stages;
            string json = await output;
            if (process.ExitCode != 0) throw new InvalidOperationException("The diagnostic worker could not complete. Try again or check Windows WMI services.");
            return JsonSerializer.Deserialize<Inspection>(json, JsonDefaults.Options) ?? throw new InvalidOperationException("No diagnostic data returned.");
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("A Windows diagnostic provider did not respond within three minutes. The inspection was stopped. No results from this attempt were saved.");
        }
    }
}
