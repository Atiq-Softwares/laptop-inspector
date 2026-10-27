using System.Runtime.InteropServices;
using Inspector.Core;

namespace Inspector.App.Diagnostics;

internal static partial class FirmwareReader
{
    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetSystemFirmwareTable(uint provider, uint id, byte[]? buffer, uint size);
    internal static IReadOnlyList<MemoryDevice> Memory()
    {
        var size = GetSystemFirmwareTable(0x52534D42, 0, null, 0);
        if (size is < 8 or > 16 * 1024 * 1024) return [];
        var bytes = new byte[size];
        try
        {
            if (GetSystemFirmwareTable(0x52534D42, 0, bytes, size) != size) return [];
            int length = BitConverter.ToInt32(bytes, 4);
            return length is > 0 && length <= bytes.Length - 8 ? MemoryInventory.Parse(bytes.AsSpan(8, length)) : [];
        }
        finally { Array.Clear(bytes); }
    }
}
