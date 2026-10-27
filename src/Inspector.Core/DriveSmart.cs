using System.Text.Json;

namespace Inspector.Core;

public sealed record DriveFacts(string? Model, string? Firmware, long? Capacity, string? Protocol, double? Hours, double? Cycles, double? Temperature, double? WearUsed, double? Spare, double? ReadBytes, double? WrittenBytes, double? MediaErrors, double? UnsafeShutdowns, bool? Passed, List<Measurement> Attributes);
public static class DriveSmart
{
    public static void Validate(DriveFacts? f)
    {
        if(f==null || f.Capacity<0 || f.Attributes==null || f.Attributes.Count>64)throw new JsonException("Invalid drive response.");
        static void Text(string? value,int maximum){if(value!=null && (value.Length>maximum || value.Any(char.IsControl)))throw new JsonException("Invalid drive text.");}
        Text(f.Model,512);Text(f.Firmware,512);Text(f.Protocol,64);
        foreach(var n in new[]{f.Hours,f.Cycles,f.Temperature,f.WearUsed,f.Spare,f.ReadBytes,f.WrittenBytes,f.MediaErrors,f.UnsafeShutdowns})
            if(n is double value && (!double.IsFinite(value) || value<0))throw new JsonException("Invalid drive counter.");
        foreach(var a in f.Attributes){if(a==null || a.Evidence!=Evidence.Measured)throw new JsonException("Invalid drive attribute.");Text(a.Name,512);Text(a.Value,2048);Text(a.Source,1024);}
    }
    public static DriveFacts Parse(string json)
    {
        if(json.Length>2_000_000)throw new JsonException("Drive response is too large.");
        using var doc = JsonDocument.Parse(json,new JsonDocumentOptions{MaxDepth=16}); var root = doc.RootElement;
        if(root.ValueKind!=JsonValueKind.Object)throw new JsonException("Drive response must be an object.");
        JsonElement At(params string[] path) { var node = root; foreach (var key in path) { if (node.ValueKind != JsonValueKind.Object || !node.TryGetProperty(key, out node)) return default; } return node; }
        double? N(params string[] path) { var node = At(path); return node.ValueKind == JsonValueKind.Number && node.TryGetDouble(out var n) && double.IsFinite(n) && n >= 0 ? n : null; }
        string? S(params string[] path) { var node = At(path); var value=node.ValueKind==JsonValueKind.String?node.GetString():null; return string.IsNullOrWhiteSpace(value)?null:value; }
        bool? passed = At("smart_status", "passed").ValueKind switch { JsonValueKind.True => true, JsonValueKind.False => false, _ => null };
        var attrs = new List<Measurement>();
        var table = At("ata_smart_attributes", "table");
        if(table.ValueKind==JsonValueKind.Array && table.GetArrayLength()>64)throw new JsonException("Too many drive attributes.");
        if (table.ValueKind == JsonValueKind.Array)
            foreach (var a in table.EnumerateArray())
            {
                if(a.ValueKind!=JsonValueKind.Object)throw new JsonException("Invalid drive attribute.");
                string name = a.TryGetProperty("name", out var nm) ? nm.GetString() ?? "ATA attribute" : "ATA attribute";
                string id = a.TryGetProperty("id", out var ident) ? ident.ToString() : "?";
                string raw = a.TryGetProperty("raw", out var r) && r.TryGetProperty("string", out var str) ? str.GetString() ?? "Not available" : "Not available";
                string normalized = a.TryGetProperty("value", out var v) ? v.ToString() : "unknown";
                string threshold = a.TryGetProperty("thresh", out var t) ? t.ToString() : "unknown";
                attrs.Add(new($"ATA {id} / {name}", $"{raw} | normalized {normalized}; threshold {threshold}", Evidence.Measured, "smartctl / drive database interpretation; raw semantics remain vendor-dependent"));
            }
        var capacityNode=At("user_capacity","bytes");long? capacity=capacityNode.ValueKind==JsonValueKind.Number && capacityNode.TryGetInt64(out var bytes) && bytes>=0 ? bytes:null;
        var facts = new DriveFacts(S("model_name") ?? S("scsi_model_name"), S("firmware_version"), capacity, S("device", "protocol"), N("power_on_time", "hours") ?? N("nvme_smart_health_information_log", "power_on_hours"), N("power_cycle_count") ?? N("nvme_smart_health_information_log", "power_cycles"), N("temperature", "current"), N("endurance_used", "current_percent") ?? N("nvme_smart_health_information_log", "percentage_used"), N("spare_available", "current_percent") ?? N("nvme_smart_health_information_log", "available_spare"), N("nvme_smart_health_information_log", "data_units_read") * 512000, N("nvme_smart_health_information_log", "data_units_written") * 512000, N("nvme_smart_health_information_log", "media_errors"), N("nvme_smart_health_information_log", "unsafe_shutdowns"), passed, attrs);
        Validate(facts);return facts;
    }
    public static void Apply(Component c, DriveFacts f)
    {
        Validate(f);
        const string source = "smartctl 7.5 / read-only drive SMART log";
        void Set(string name, object? value, Evidence evidence = Evidence.Measured)
        {
            if (value == null) return;
            c.Measurements.RemoveAll(m => m.Name == name); c.Add(name, value, source, evidence);
        }
        if (!string.IsNullOrWhiteSpace(f.Model)) c.Name = f.Model;
        Set("Firmware version", f.Firmware); Set("SMART protocol", f.Protocol);
        Set("Power-on hours", f.Hours is double hours ? $"{hours:N0} hours" : null);
        Set("Power cycles", f.Cycles); Set("Drive temperature", f.Temperature is double temp ? $"{temp:0.#} °C" : null);
        Set("Wear used", f.WearUsed is double wear ? $"{wear:0.#}% of rated endurance used" : null);
        Set("Available spare", f.Spare is double spare ? $"{spare:0.#}%" : null);
        Set("Host data read", f.ReadBytes is double read ? $"{read / 1e12:0.###} TB ({read:N0} bytes)" : null);
        Set("Host data written", f.WrittenBytes is double written ? $"{written / 1e12:0.###} TB ({written:N0} bytes)" : null);
        Set("Media / data integrity errors", f.MediaErrors); Set("Unsafe shutdowns", f.UnsafeShutdowns);
        Set("SMART overall result", f.Passed is bool passed ? passed ? "Passed (drive-reported)" : "Failed (drive-reported)" : null);
        c.Measurements.AddRange(f.Attributes);
        if (f.Passed == false || f.WearUsed >= 100) c.Rating = Rating.Critical;
        else if ((f.WearUsed >= 80 || f.MediaErrors > 0) && c.Rating != Rating.Critical) c.Rating = Rating.Caution;
        else if (f.Passed == true && c.Rating == Rating.NotVerified) c.Rating = Rating.Good;
        c.Explanation = "Drive-reported SMART readings supplement Windows inventory. Hours describe powered-on time, not the date of first use. A passed SMART result does not guarantee reliability. Raw ATA fields retain their vendor-specific meaning.";
    }
}
