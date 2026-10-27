using System.Management;

namespace Inspector.App.Diagnostics;

internal sealed record QueryResult(List<Dictionary<string, object?>> Rows, string? Failure);
internal static class WmiReader
{
    public static QueryResult Query(string scope, string className, string properties, string? where = null)
    {
        try
        {
            var options = new EnumerationOptions { Timeout = TimeSpan.FromSeconds(4), ReturnImmediately = false };
            using var searcher = new ManagementObjectSearcher(scope, $"SELECT {properties} FROM {className}" + (where == null ? "" : $" WHERE {where}"), options);
            using var values = searcher.Get();
            var rows = new List<Dictionary<string, object?>>();
            foreach (ManagementObject value in values)
            {
                using (value)
                {
                    var row = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);
                    foreach (var name in properties.Split(',').Select(p => p.Trim())) row[name] = value[name];
                    rows.Add(row);
                }
                if (rows.Count >= 128) break;
            }
            return new(rows, null);
        }
        catch (Exception e) when (e is ManagementException or UnauthorizedAccessException or System.Runtime.InteropServices.COMException or System.Security.SecurityException)
        {
            // Do not export exception messages: these may contain a machine name or device path.
            return new([], e is UnauthorizedAccessException ? "Access denied" : "Provider unavailable or unsupported");
        }
    }
    public static object? V(this Dictionary<string, object?> row, string key) => row.GetValueOrDefault(key);
    public static string? S(this Dictionary<string, object?> row, string key) => row.V(key)?.ToString()?.Trim();
    public static double? N(this Dictionary<string, object?> row, string key) => Core.Evaluation.Number(row.V(key));
    public static int? I(this Dictionary<string, object?> row, string key) => row.N(key) is double n && n is >= int.MinValue and <= int.MaxValue ? (int)n : null;
    public static bool? B(this Dictionary<string, object?> row, string key) => row.V(key) is bool b ? b : null;
}
