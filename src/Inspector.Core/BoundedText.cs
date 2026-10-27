using System.Text;

namespace Inspector.Core;

public static class BoundedText
{
    public static async Task ReadLinesAsync(TextReader reader, int maximumLine, int maximumTotal, Action<string> onLine, CancellationToken token)
    {
        var buffer = new char[256]; var line = new StringBuilder(); int total = 0;
        while (true)
        {
            int read = await reader.ReadAsync(buffer.AsMemory(), token).ConfigureAwait(false);
            if (read == 0) { if (line.Length > 0) onLine(line.ToString()); return; }
            if (read > maximumTotal - total) throw new InvalidDataException("Diagnostic progress exceeded its size limit."); total += read;
            for (int n = 0; n < read; n++)
                if (buffer[n] == '\n') { onLine(line.ToString().TrimEnd('\r')); line.Clear(); }
                else { if (line.Length >= maximumLine) throw new InvalidDataException("Diagnostic progress line is too long."); line.Append(buffer[n]); }
        }
    }
    public static async Task<string> ReadAsync(TextReader reader, int maximumCharacters, CancellationToken token = default)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(maximumCharacters);
        var output = new StringBuilder(Math.Min(maximumCharacters, 8192));
        var buffer = new char[4096];
        while (true)
        {
            // Read at most one character past the boundary; never buffer an unlimited response.
            int count = Math.Min(buffer.Length, maximumCharacters - output.Length + 1);
            int read = await reader.ReadAsync(buffer.AsMemory(0, count), token).ConfigureAwait(false);
            if (read == 0) return output.ToString();
            if (read > maximumCharacters - output.Length) throw new InvalidDataException("Diagnostic output exceeded its allowed size.");
            output.Append(buffer, 0, read);
        }
    }
}
