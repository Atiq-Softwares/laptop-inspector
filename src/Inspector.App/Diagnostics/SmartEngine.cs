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
        try
        {
            foreach (var (file, hash) in new[] { ("smartctl.exe", "B5DB94E5082C042BE44994B7A4FA8F7B5C8E713B2AB1C9A560D8F7A7995EA27D"), ("drivedb.h", "DD39C6A520D38895DA61923FE26FE7C9C5EB3F42325F2E4477A06ED7A61966D0") })
                using (var input = File.OpenRead(Path.Combine(Folder, file))) if (!Convert.ToHexString(SHA256.HashData(input)).Equals(hash, StringComparison.OrdinalIgnoreCase)) return false;
            return true;
        }
        catch (IOException) { return false; } catch (UnauthorizedAccessException) { return false; }
    }
    public static DriveFacts? Read(int index)
    {
        if (index is < 0 or > 255 || !Verify()) return null;
        var start = new ProcessStartInfo(Path.Combine(Folder, "smartctl.exe")) { WorkingDirectory = Folder, UseShellExecute = false, CreateNoWindow = true, RedirectStandardOutput = true, RedirectStandardError = true };
        foreach (var arg in new[] { "-j", "-x", $"/dev/pd{index}" }) start.ArgumentList.Add(arg);
        try
        {
            using var process = Process.Start(start)!;
            var output = process.StandardOutput.ReadToEndAsync(); var errors = process.StandardError.ReadToEndAsync();
            if (!process.WaitForExit(12000)) { process.Kill(true); return null; }
            var json = output.GetAwaiter().GetResult(); errors.GetAwaiter().GetResult();
            if (json.Length > 2_000_000) return null;
            var facts = DriveSmart.Parse(json);
            // smartctl exit codes are a bitmask; nonzero health bits must not discard failing-drive data.
            return facts.Model == null ? null : facts;
        }
        catch (Exception e) when (e is IOException or JsonException or System.ComponentModel.Win32Exception or InvalidOperationException) { return null; }
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
        if (component.DiskIndex != probe.Index) return false;
        static string Normalize(string value) => string.Concat(value.Where(char.IsLetterOrDigit)).ToUpperInvariant();
        if (probe.Facts.Model == null || Normalize(component.Name) != Normalize(probe.Facts.Model)) return false;
        return component.CapacityBytes == null || probe.Facts.Capacity == null || Math.Abs(component.CapacityBytes.Value - probe.Facts.Capacity.Value) < 1048576;
    }
    public static async Task<List<DriveProbe>> ElevatedAsync(CancellationToken token, bool elevate = true)
    {
        var name = "AtiqInspector-" + Guid.NewGuid().ToString("N");
        using var pipe = new NamedPipeServerStream(name, PipeDirection.In, 1, PipeTransmissionMode.Byte, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        var exe = Environment.ProcessPath ?? throw new IOException("Executable path unavailable.");
        using var process = Process.Start(new ProcessStartInfo(exe) { UseShellExecute = elevate, Verb = elevate ? "runas" : "", CreateNoWindow = !elevate, Arguments = "--advanced-collect " + name });
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token); timeout.CancelAfter(TimeSpan.FromMinutes(3));
        await pipe.WaitForConnectionAsync(timeout.Token);
        using var reader = new StreamReader(pipe);
        var json = await reader.ReadToEndAsync(timeout.Token);
        if (json.Length > 4_000_000) throw new IOException("Unexpected diagnostic output size.");
        return JsonSerializer.Deserialize<List<DriveProbe>>(json, JsonDefaults.Options) ?? [];
    }
    public static async Task SendElevatedAsync(string name)
    {
        if (!System.Text.RegularExpressions.Regex.IsMatch(name, "^AtiqInspector-[a-f0-9]{32}$")) throw new ArgumentException("Invalid diagnostic channel.");
        using var pipe = new NamedPipeClientStream(".", name, PipeDirection.Out, PipeOptions.Asynchronous | PipeOptions.CurrentUserOnly);
        await pipe.ConnectAsync(15000);
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
