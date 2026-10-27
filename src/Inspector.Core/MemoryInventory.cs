using System.Buffers.Binary;
using System.Text;

namespace Inspector.Core;

public sealed record MemoryDevice(string Locator, string? Bank, ulong? CapacityBytes, bool Empty, string? Manufacturer, string? PartNumber, int? Type, int? FormFactor, uint? RatedRate, uint? ConfiguredRate, int? Ranks, int? VoltageMv, int? DataWidth, int? TotalWidth);

public static class MemoryInventory
{
    // DMTF DSP0134 Type 17. Input is the structure table, excluding the Windows 8-byte wrapper.
    // Only selected non-identifying fields are decoded. Serial and asset strings are never returned.
    public static IReadOnlyList<MemoryDevice> Parse(ReadOnlySpan<byte> table)
    {
        var devices = new List<MemoryDevice>();
        int pos = 0;
        while (pos + 4 <= table.Length)
        {
            int type = table[pos], length = table[pos + 1];
            if (length < 4 || pos + length > table.Length) break;
            int end = pos + length;
            while (end + 1 < table.Length && !(table[end] == 0 && table[end + 1] == 0)) end++;
            if (end + 1 >= table.Length) break;
            if (type == 17 && length >= 0x15)
            {
                var data = table.Slice(pos, length).ToArray();
                var strings = table.Slice(pos + length, end - pos - length + 1).ToArray();
                string? S(int offset)
                {
                    if (offset >= data.Length || data[offset] == 0) return null;
                    int start = 0;
                    for (int n = 1; n < data[offset]; n++) { while (start < strings.Length && strings[start] != 0) start++; start++; }
                    if (start >= strings.Length) return null;
                    int stop = start; while (stop < strings.Length && strings[stop] != 0) stop++;
                    var value = Encoding.ASCII.GetString(strings, start, stop - start).Trim();
                    return string.IsNullOrWhiteSpace(value) || value.Equals("Unknown", StringComparison.OrdinalIgnoreCase) || value.Equals("Not Specified", StringComparison.OrdinalIgnoreCase) ? null : value;
                }
                ushort? U16(int offset) => offset + 2 <= data.Length ? BinaryPrimitives.ReadUInt16LittleEndian(data.AsSpan(offset)) : null;
                uint? U32(int offset) => offset + 4 <= data.Length ? BinaryPrimitives.ReadUInt32LittleEndian(data.AsSpan(offset)) : null;
                int? Valid(ushort? value) => value is > 0 and < ushort.MaxValue ? value : null;
                uint? Rate(int normal, int extended)
                {
                    var value = U16(normal);
                    if (value == ushort.MaxValue) { var ext = U32(extended); return ext is > 0 and < uint.MaxValue ? ext : null; }
                    return value is > 0 ? value : null;
                }
                ushort size = U16(0x0C) ?? ushort.MaxValue;
                ulong? capacity = size switch { 0 => 0, ushort.MaxValue => null, 0x7FFF => U32(0x1C) is uint extended ? (extended & 0x7FFFFFFFUL) * 1048576UL : null, _ => (ulong)(size & 0x7FFF) * ((size & 0x8000) != 0 ? 1024UL : 1048576UL) };
                int? ranks = data.Length > 0x1B && (data[0x1B] & 15) > 0 ? data[0x1B] & 15 : null;
                devices.Add(new(S(0x10) ?? $"Firmware memory device {devices.Count + 1}", S(0x11), capacity, size == 0, S(0x17), S(0x1A), data[0x12], data[0x0E], Rate(0x15, 0x54), Rate(0x20, 0x58), ranks, Valid(U16(0x26)), Valid(U16(0x0A)), Valid(U16(0x08))));
                Array.Clear(data); Array.Clear(strings);
            }
            pos = end + 2;
            if (type == 127) break;
        }
        return devices;
    }
    public static double? DdrClock(uint? rate, int? type) => rate is > 0 && type is 20 or 21 or 22 or 24 or 26 or 27 or 28 or 29 or 30 or 34 or 35 ? rate / 2d : null;
}
