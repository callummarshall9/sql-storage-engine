using System.ComponentModel;
using System.Runtime.InteropServices;

namespace sql_storage_engine.Transactions;

internal static class TransactionDirectory
{
    internal static void Flush(string path)
    {
        // Explicit transactions are admitted only on Linux. Preserve the legacy statement API elsewhere.
        if (!OperatingSystem.IsLinux()) return;
        var descriptor = Open(Path.GetDirectoryName(Path.GetFullPath(path))!, 0x10000 | 0x80000);
        if (descriptor < 0) throw new IOException("Cannot open transaction directory.", new Win32Exception(Marshal.GetLastPInvokeError()));
        try
        {
            if (Fsync(descriptor) != 0) throw new IOException("Cannot synchronize transaction directory.", new Win32Exception(Marshal.GetLastPInvokeError()));
        }
        finally { Close(descriptor); }
    }

    internal static void Delete(string path)
    {
        File.Delete(path);
        Flush(path);
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags);
    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int Fsync(int descriptor);
    [DllImport("libc", EntryPoint = "close")]
    private static extern int Close(int descriptor);
}
