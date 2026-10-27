using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace Inspector.App.Diagnostics;

internal static class PipePeer
{
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeClientProcessId(SafePipeHandle pipe, out uint pid);
    [DllImport("kernel32.dll", SetLastError = true)] private static extern bool GetNamedPipeServerProcessId(SafePipeHandle pipe, out uint pid);
    public static int ClientId(NamedPipeServerStream pipe) => GetNamedPipeClientProcessId(pipe.SafePipeHandle,out var id) && id<=int.MaxValue ? (int)id : throw new IOException("Cannot verify diagnostic client.");
    public static int ServerId(NamedPipeClientStream pipe) => GetNamedPipeServerProcessId(pipe.SafePipeHandle,out var id) && id<=int.MaxValue ? (int)id : throw new IOException("Cannot verify diagnostic server.");
}
