using System.IO;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using Microsoft.Win32.SafeHandles;

namespace Inspector.App.Diagnostics;

internal sealed class VerifiedEngineFiles : IDisposable
{
    private readonly List<IDisposable> locks = [];
    public string Executable { get; }
    public string Database { get; }
    public static readonly (string Name, string Hash)[] Expected = [
        ("smartctl.exe", "B5DB94E5082C042BE44994B7A4FA8F7B5C8E713B2AB1C9A560D8F7A7995EA27D"),
        ("drivedb.h", "DD39C6A520D38895DA61923FE26FE7C9C5EB3F42325F2E4477A06ED7A61966D0")
    ];
    [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern SafeFileHandle CreateFileW(string path, uint access, uint share, IntPtr security, uint creation, uint flags, IntPtr template);
    public VerifiedEngineFiles(string folder)
    {
        folder = Path.GetFullPath(folder); Executable = Path.Combine(folder, "smartctl.exe"); Database = Path.Combine(folder, "drivedb.h");
        try
        {
            // Lock every ancestor against rename/reparse changes while resolving/executing the verified path.
            var directories = new Stack<DirectoryInfo>();
            for (var directory = new DirectoryInfo(folder); directory != null; directory = directory.Parent) directories.Push(directory);
            foreach (var directory in directories)
            {
                var handle = CreateFileW(directory.FullName, 0, 1 /* FILE_SHARE_READ */, IntPtr.Zero, 3 /* OPEN_EXISTING */, 0x02000000 /* BACKUP_SEMANTICS */, IntPtr.Zero);
                if (handle.IsInvalid) { handle.Dispose(); throw new IOException("Cannot protect the drive-reader directory."); }
                locks.Add(handle);
                if (new DirectoryInfo(directory.FullName).LinkTarget != null) throw new IOException("Redirected drive-reader directories are not accepted.");
            }
            foreach (var (name, hash) in Expected)
            {
                var path = Path.Combine(folder, name);
                if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0) throw new IOException("Redirected drive-reader files are not accepted.");
                var file = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read); locks.Add(file);
                if (!Convert.ToHexString(SHA256.HashData(file)).Equals(hash, StringComparison.OrdinalIgnoreCase)) throw new IOException("Drive-reader integrity check failed.");
            }
        }
        catch { Dispose(); throw; }
    }
    public void Dispose() { for (int n = locks.Count - 1; n >= 0; n--) locks[n].Dispose(); locks.Clear(); }
}
