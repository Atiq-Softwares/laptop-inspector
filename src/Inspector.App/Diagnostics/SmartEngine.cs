using System.Diagnostics;
using System.IO;
using System.IO.Pipes;
using System.Security.Cryptography;
using System.Text.Json;
using Inspector.Core;

namespace Inspector.App.Diagnostics;

public sealed record DriveProbe(int Index, DriveFacts Facts);
public static class SmartEngine
{
    private static string Folder => Path.Combine(AppContext.BaseDirectory, "third-party", "smartmontools");
    public static bool Verify()
    {
        try { using var files = new VerifiedEngineFiles(Folder); return true; }
        catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
    }
    public static DriveFacts? Read(int index)
    {
        if (index is < 0 or > 255) return null;
        try
        {
            using var files = new VerifiedEngineFiles(Folder);
            var start = new ProcessStartInfo(files.Executable) { WorkingDirectory = Folder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
            foreach (var arg in new[] { "-j", "-x", "-B", files.Database, $"/dev/pd{index}" }) start.ArgumentList.Add(arg);
            using var process = Process.Start(start) ?? throw new IOException("Drive reader did not start.");
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(12));
            void Stop() { try { if (!process.HasExited) process.Kill(true); } catch (InvalidOperationException) { } catch (System.ComponentModel.Win32Exception) { } }
            using var cancellation = timeout.Token.Register(Stop);
            async Task<string> ReadOutput(StreamReader reader, int limit)
            {
                try { return await BoundedText.ReadAsync(reader,limit,timeout.Token).ConfigureAwait(false); }
                catch { Stop(); throw; }
            }
            var output = ReadOutput(process.StandardOutput,2_000_000); var errors = ReadOutput(process.StandardError,65536);
            try
            {
                process.WaitForExitAsync(timeout.Token).GetAwaiter().GetResult();
                var json = output.GetAwaiter().GetResult(); errors.GetAwaiter().GetResult();
                var facts = DriveSmart.Parse(json);
                return facts.Model == null ? null : facts;
            }
            finally { Stop(); }
        }
        catch (Exception e) when (e is IOException or JsonException or System.ComponentModel.Win32Exception or InvalidOperationException or UnauthorizedAccessException or OperationCanceledException) { return null; }
    }
    public static List<DriveProbe> Collect()
    {
        var rows = WmiReader.Query(@"root\cimv2", "Win32_DiskDrive", "Index").Rows;
        var result = new List<DriveProbe>();
        foreach (var row in rows.Take(16))
            if (row.I("Index") is int index && Read(index) is DriveFacts facts) result.Add(new(index, facts));
        return result;
    }
    public static bool Matches(Component component, DriveProbe probe)
    {
        if (component.DiskIndex != probe.Index || component.CapacityBytes < 0 || probe.Facts.Capacity < 0) return false;
        static string Normalize(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
        if (probe.Facts.Model == null || Normalize(component.Name) != Normalize(probe.Facts.Model)) return false;
        return component.CapacityBytes == null || probe.Facts.Capacity == null || Math.Abs((decimal)component.CapacityBytes.Value - probe.Facts.Capacity.Value) < 1048576;
    }
    public static async Task<List<DriveProbe>> ElevatedAsync(CancellationToken token, bool elevate = true)
    {
        var name = "AtiqInspector-" + Guid.NewGuid().ToString("N");
        using var pipe = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var exe = Environment.ProcessPath ?? throw new IOException("Executable path unavailable.");
        var start = new ProcessStartInfo(exe) { UseShellExecute = elevate, Verb = elevate ? "runas" : "", CreateNoWindow = !elevate };
        if(Path.GetFileNameWithoutExtension(exe).Equals("dotnet",StringComparison.OrdinalIgnoreCase))
        {
            if(elevate) throw new IOException("Use the portable executable for optional elevated access.");
            start.ArgumentList.Add(Path.Combine(AppContext.BaseDirectory,"Atiq-Laptop-Inspector.dll"));
        }
        start.ArgumentList.Add("--advanced-collect");start.ArgumentList.Add(name);start.ArgumentList.Add(Environment.ProcessId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        using var process = Process.Start(start) ?? throw new IOException("Diagnostic helper did not start.");
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(3));
        for(int attempts=0;;attempts++)
        {
            await pipe.WaitForConnectionAsync(timeout.Token);
            if(PipePeer.ClientId(pipe)==process.Id)break;
            pipe.Disconnect();if(attempts>=7)throw new IOException("Unexpected diagnostic peer.");
        }
        using var reader = new StreamReader(pipe);
        var json = await BoundedText.ReadAsync(reader,4_000_000,timeout.Token);
        var results=JsonSerializer.Deserialize<List<DriveProbe>>(json,JsonDefaults.Options) ?? throw new IOException("No diagnostic response.");
        if(results.Count>16 || results.Any(r=>r==null || r.Index is <0 or >255) || results.Select(r=>r.Index).Distinct().Count()!=results.Count)throw new IOException("Invalid drive response.");
        foreach(var result in results)DriveSmart.Validate(result.Facts);
        return results;
    }
    public static async Task SendElevatedAsync(string name, int expectedParent)
    {
        if (expectedParent<=0 || !System.Text.RegularExpressions.Regex.IsMatch(name, "^AtiqInspector-[a-f0-9]{32}$",System.Text.RegularExpressions.RegexOptions.CultureInvariant,TimeSpan.FromMilliseconds(100))) throw new ArgumentException("Invalid diagnostic channel.");
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly, System.Security.Principal.TokenImpersonationLevel.Identification);
        await pipe.ConnectAsync(15000);
        if(PipePeer.ServerId(pipe)!=expectedParent)throw new IOException("Unexpected diagnostic parent.");
        using var writer = new StreamWriter(pipe); await writer.WriteAsync("["); await writer.FlushAsync();
        bool first = true;
        foreach(var row in WmiReader.Query(@"root\cimv2","Win32_DiskDrive","Index").Rows.Take(16))
        {
            // A pipe flush before each drive also detects a parent that has cancelled/exited.
            await writer.WriteAsync(" "); await writer.FlushAsync();
            if(row.I("Index") is not int index || Read(index) is not DriveFacts facts) continue;
            if(!first) await writer.WriteAsync(",");first=false;
            await writer.WriteAsync(JsonSerializer.Serialize(new DriveProbe(index,facts),JsonDefaults.Options));await writer.FlushAsync();
        }
        await writer.WriteAsync("]");await writer.FlushAsync();
    }
}
