namespace Inspector.Core;

public static class InspectionValidation
{
    private static void Text(string? value, int limit)
    {
        if (value == null || value.Length > limit || value.Any(c => char.IsControl(c) && c is not '\r' and not '\n' and not '\t'))
            throw new InvalidDataException("Invalid or oversized diagnostic text.");
    }
    public static void Check(Inspection i)
    {
        ArgumentNullException.ThrowIfNull(i);
        Text(i.SchemaVersion, 16); if (i.SchemaVersion != "1.0") throw new InvalidDataException("Unsupported inspection schema.");
        Text(i.ApplicationVersion, 32); Text(i.Manufacturer, 512); Text(i.Model, 512);
        if (i.Components == null || i.Components.Count > 256 || i.ManualChecks == null || i.ManualChecks.Count > 32 || i.Limitations == null || i.Limitations.Count > 256)
            throw new InvalidDataException("Invalid inspection collection size.");
        int measurements = 0;
        foreach (var c in i.Components)
        {
            if (c == null || !Enum.IsDefined(c.Rating) || c.Measurements == null || c.Measurements.Count > 512 || c.Recommendations == null || c.Recommendations.Count > 64)
                throw new InvalidDataException("Invalid component data.");
            Text(c.Category, 128); Text(c.Name, 512); Text(c.Explanation, 8192);
            if (c.DiskIndex is < 0 or > 255 || c.CapacityBytes < 0) throw new InvalidDataException("Invalid internal drive mapping.");
            measurements += c.Measurements.Count;
            if (measurements > 8192) throw new InvalidDataException("Too many measurements.");
            foreach (var m in c.Measurements)
            {
                if (m == null || !Enum.IsDefined(m.Evidence)) throw new InvalidDataException("Invalid evidence type.");
                Text(m.Name, 512); Text(m.Value, 8192); Text(m.Source, 1024);
            }
            foreach (var r in c.Recommendations) Text(r, 8192);
        }
        foreach (var c in i.ManualChecks)
        {
            if (c == null || !Enum.IsDefined(c.Outcome)) throw new InvalidDataException("Invalid manual outcome.");
            Text(c.Name, 128); Text(c.Instructions, 8192); Text(c.Notes, 1000);
        }
        foreach (var limitation in i.Limitations) Text(limitation, 8192);
    }
}
