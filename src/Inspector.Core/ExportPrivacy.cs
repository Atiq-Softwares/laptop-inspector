using System.Text.Json;
using System.Text.RegularExpressions;

namespace Inspector.Core;

public static class ExportPrivacy
{
    // Defense in depth in addition to the collector's explicit WMI property allowlist.
    private static readonly Regex SensitiveLabel = new(@"serial|uuid|product.?key|user.?name|computer.?name|host.?name|\b(?:ip|mac)\s*(?:address)?\b|ssid|device.?id|instance.?name|file.?path|pnpdevice", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);
    public static Inspection Prepare(Inspection source, bool includeManualNotes = false)
    {
        var copy = JsonSerializer.Deserialize<Inspection>(JsonSerializer.Serialize(source, JsonDefaults.Options), JsonDefaults.Options)!;
        foreach (var c in copy.Components) c.Measurements.RemoveAll(m => SensitiveLabel.IsMatch(m.Name));
        if (!includeManualNotes) foreach (var check in copy.ManualChecks) check.Notes = "";
        copy.Publisher = Branding.Publisher; copy.Copyright = Branding.Copyright; copy.GitHub = Branding.GitHub;
        return copy;
    }
}
