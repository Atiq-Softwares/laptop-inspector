namespace Inspector.Core;

public static class AtomicReportFile
{
    public static void Write(string destination, Action<Stream> write)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(destination); ArgumentNullException.ThrowIfNull(write);
        var path = Path.GetFullPath(destination);
        if (Path.GetExtension(path).ToLowerInvariant() is not (".pdf" or ".html" or ".json")) throw new ArgumentException("Unsupported report format.");
        var folder = Path.GetDirectoryName(path) ?? throw new IOException("A report folder is required.");
        if (!Directory.Exists(folder)) throw new DirectoryNotFoundException("Choose an existing report folder.");
        for (var current = new DirectoryInfo(folder); current != null; current = current.Parent)
            if (current.LinkTarget != null) throw new IOException("Choose a report folder without directory links or junctions.");
        if (File.Exists(path) && (File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Cannot replace a linked report file.");
        var temporary = Path.Combine(folder, ".atiq-report-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            using (var output = new FileStream(temporary, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            {
                write(output); output.Flush(flushToDisk: true);
            }
            // Replace the directory entry, without following or overwriting another hard-link target.
            File.Move(temporary, path, overwrite: true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
