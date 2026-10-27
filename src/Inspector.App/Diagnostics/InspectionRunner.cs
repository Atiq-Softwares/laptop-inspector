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
        using var registration = timeout.Token.Register(() => { try { if (!process.HasExited) process.Kill(entireProcessTree: true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } });
        void Stop() { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }
        async Task<string> ReadOutput()
        {
            try { return await BoundedText.ReadAsync(process.StandardOutput, 4_000_000, timeout.Token).ConfigureAwait(false); }
            catch { Stop(); throw; }
        }
        async Task ReadProgress()
        {
            try
            {
                await BoundedText.ReadLinesAsync(process.StandardError, 256, 65536, line =>
                {
                    var split = line.Split('|', 2);
                    if (split.Length == 2 && int.TryParse(split[0], out var percent) && split[1].Length <= 128 && !split[1].Any(char.IsControl)) progress.Report((Math.Clamp(percent, 0, 100), split[1]));
                }, timeout.Token).ConfigureAwait(false);
            }
            catch { Stop(); throw; }
        }
        var output = ReadOutput(); var stages = ReadProgress();
        try
        {
            await process.WaitForExitAsync(timeout.Token);
            await stages;
            string json = await output;
            if (process.ExitCode != 0) throw new InvalidOperationException("The diagnostic worker could not complete. Try again or check Windows WMI services.");
            var inspection = JsonSerializer.Deserialize<Inspection>(json, JsonDefaults.Options) ?? throw new InvalidOperationException("No diagnostic data returned.");
            InspectionValidation.Check(inspection); return inspection;
        }
        catch (OperationCanceledException) when (!token.IsCancellationRequested)
        {
            throw new TimeoutException("A Windows diagnostic provider did not respond within three minutes. The inspection was stopped. No results from this attempt were saved.");
        }
        finally { Stop(); }
    }
}
