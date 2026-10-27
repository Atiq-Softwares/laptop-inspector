using System.Globalization;
using System.Text.RegularExpressions;

namespace Inspector.Core;

public static class Evaluation
{
    // These are practical inspection policies, not manufacturer failure predictions.
    public static double? BatteryHealth(double? design, double? full)
        => design is > 0 && full is > 0 && double.IsFinite(design.Value) && double.IsFinite(full.Value) && full / design <= 1.2 ? Math.Round(full.Value / design.Value * 100, 1) : null;
    public static Rating BatteryRating(double? percent) => percent switch
    {
        null => Rating.NotVerified,
        < 50 => Rating.Critical,
        < 80 => Rating.Caution,
        _ => Rating.Good
    };
    public static Rating MemoryRating(double? gib) => gib switch { null or <= 0 => Rating.NotVerified, < 8 => Rating.Caution, _ => Rating.Good };
    public static Rating StorageRating(int? health, bool? predictFailure, int? wear)
    {
        if (predictFailure == true || health == 2 || wear >= 100) return Rating.Critical;
        if (health == 1 || wear >= 80) return Rating.Caution;
        if (health == 0 || predictFailure == false) return Rating.Good;
        return Rating.NotVerified;
    }
    public static Rating Overall(Inspection i)
    {
        if (i.Components.Any(c => c.Rating == Rating.Critical) || i.ManualChecks.Any(c => c.Outcome == ManualOutcome.Fail)) return Rating.Critical;
        if (i.Components.Any(c => c.Rating == Rating.Caution)) return Rating.Caution;
        if (!i.IsComplete || i.Components.Count == 0 || i.Components.Any(c => c.Rating == Rating.NotVerified) || i.ManualChecks.Any(c => c.Outcome == ManualOutcome.NotTested)) return Rating.NotVerified;
        return Rating.Good;
    }
    public static string Assessment(Inspection i) => Overall(i) switch
    {
        Rating.Critical => "A critical finding or failed manual check requires investigation before purchase or continued use. Review the affected component; this label is not a failure prediction.",
        Rating.Caution => "Review the flagged findings and budget for any recommended service or upgrades. Complete all remaining manual checks before a buying decision.",
        Rating.NotVerified => "Inspection coverage is incomplete. Available readings alone cannot confirm overall condition. Complete manual checks and verify unavailable measurements before purchase.",
        _ => "Available checks meet this app's inspection policies. Confirm the laptop suits your workload and verify ownership, warranty and long-term reliability separately."
    };
    public static string Label(Rating rating) => rating == Rating.NotVerified ? "NOT VERIFIED" : rating.ToString().ToUpperInvariant();
    public static string? IntelGeneration(string name)
    {
        // Only identify the well-defined legacy Core i3/i5/i7/i9 naming pattern.
        var m = Regex.Match(name, @"\bi[3579]-(\d{4,5})([A-Za-z][A-Za-z0-9]*)?\b", RegexOptions.IgnoreCase);
        if (!m.Success) return null;
        string digits = m.Groups[1].Value;
        bool modernMobile = digits.Length == 4 && int.Parse(digits[..2], CultureInfo.InvariantCulture) is >= 10 and <= 14;
        int generation = int.Parse(digits.Length == 5 || modernMobile ? digits[..2] : digits[..1], CultureInfo.InvariantCulture);
        return generation is >= 2 and <= 14 ? $"Intel Core generation {generation} (inferred from model name)" : null;
    }
    public static string Bytes(double? bytes) => bytes is >= 0 ? $"{bytes.Value / 1073741824:0.##} GiB" : "Not available";
    public static double? Number(object? o)
    {
        if (o == null) return null;
        return double.TryParse(Convert.ToString(o, CultureInfo.InvariantCulture), NumberStyles.Float, CultureInfo.InvariantCulture, out var n) && double.IsFinite(n) ? n : null;
    }
    public static string? MemoryType(int? type) => type switch { 20 => "DDR", 21 => "DDR2", 24 => "DDR3", 26 => "DDR4", 30 => "LPDDR4", 34 => "DDR5", 35 => "LPDDR5", _ => null };
    public static string? BusType(int? bus) => bus switch { 1 => "SCSI", 2 => "ATAPI", 3 => "ATA", 7 => "USB", 8 => "RAID", 10 => "SAS", 11 => "SATA", 15 => "File-backed virtual", 16 => "Storage Spaces", 17 => "NVMe", _ => null };
}

public sealed record SmartAttribute(byte Id, byte Current, byte Worst, ulong Raw);
public static class SmartParser
{
    // Legacy ATA vendor-specific 512-byte table. Raw meanings are vendor-dependent.
    public static IReadOnlyList<SmartAttribute> Parse(byte[] data)
    {
        if (data.Length < 362) return [];
        var result = new List<SmartAttribute>();
        for (var offset = 2; offset + 11 < Math.Min(data.Length, 362); offset += 12)
        {
            if (data[offset] == 0) continue;
            ulong raw = 0;
            for (int j = 0; j < 6; j++) raw |= (ulong)data[offset + 5 + j] << (8 * j);
            result.Add(new(data[offset], data[offset + 3], data[offset + 4], raw));
        }
        return result;
    }
}
