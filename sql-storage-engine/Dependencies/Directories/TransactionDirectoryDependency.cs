// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.ComponentModel;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace sql_storage_engine.Dependencies.Directories;
/// <summary>Owns the native directory handle used to publish transaction-file changes durably.</summary>
internal sealed partial class TransactionDirectoryDependency : IDisposable
{
    private SafeFileHandle? directory;
    private FileStream? file;
    public void DeleteFile(string path)
    {
        File.Delete(path: path);
        FlushDirectory(path: path);
    }

    public void FlushDirectory(string path)
    {
        if (!OperatingSystem.IsLinux())
        {
            return;
        }

        var parent = Path.GetDirectoryName(path: Path.GetFullPath(path: path))!;
        var descriptor = Open(path: parent, flags: 0x10000 | 0x80000);

        if (descriptor < 0)
        {
            throw new IOException(message: "Cannot open transaction directory.", innerException: new Win32Exception(Marshal.GetLastPInvokeError()));
        }

        directory?.Dispose();
        directory = new SafeFileHandle(preexistingHandle: (IntPtr)descriptor, ownsHandle: true);

        if (Fsync(descriptor: descriptor) != 0)
        {
            throw new IOException(message: "Cannot synchronize transaction directory.", innerException: new Win32Exception(Marshal.GetLastPInvokeError()));
        }
    }

    public string GetFileName(string path) =>
        Path.GetFileName(path: Path.GetFullPath(path: path));

    public IReadOnlyList<(string Path, string Name)> GetSiblingFiles(string path)
    {
        var parent = Path.GetDirectoryName(path: Path.GetFullPath(path: path))!;

        return Directory.GetFiles(path: parent)
            .Select(selector: entry => (entry, Path.GetFileName(path: entry)))
            .ToArray();
    }

    public bool FileExists(string path) =>
        File.Exists(path: path);

    public async ValueTask WriteMetadataBytesAsync(string path, ReadOnlyMemory<byte> bytes, CancellationToken cancellationToken)
    {
        var temporary = path + $".{Guid.NewGuid():N}.tmp";

        try
        {
            file = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None, 4096, FileOptions.Asynchronous);

            await file.WriteAsync(buffer: bytes, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            file.Flush(flushToDisk: true);

            await file.DisposeAsync()
            .ConfigureAwait(continueOnCapturedContext: false);

            file = null;
            File.Move(sourceFileName: temporary, destFileName: path, overwrite: true);
            FlushDirectory(path: path);
        }
        finally
        {
            if (file is not null)
            {
                await file.DisposeAsync()
            .ConfigureAwait(continueOnCapturedContext: false);

                file = null;
            }

            if (File.Exists(path: temporary))
            {
                DeleteFile(path: temporary);
            }
        }
    }

    public async ValueTask<byte[]> ReadMetadataBytesAsync(string path, int length, CancellationToken cancellationToken)
    {
        file = File.OpenRead(path: path);

        try
        {
            if (file.Length != length)
            {
                throw new IOException(message: "Transaction metadata length is invalid.");
            }

            var bytes = new byte[length];
            await file.ReadExactlyAsync(buffer: bytes, cancellationToken: cancellationToken)
                .ConfigureAwait(continueOnCapturedContext: false);

            if (file.ReadByte() != -1)
            {
                throw new IOException(message: "Transaction metadata contains trailing bytes.");
            }

            return bytes;
        }
        finally
        {
            await file.DisposeAsync()
                .ConfigureAwait(continueOnCapturedContext: false);

            file = null;
        }
    }

    public void Dispose()
    {
        file?.Dispose();
        directory?.Dispose();
    }

    [DllImport("libc", EntryPoint = "open", SetLastError = true)]
    private static extern int Open(string path, int flags);

    [DllImport("libc", EntryPoint = "fsync", SetLastError = true)]
    private static extern int Fsync(int descriptor);

    public Guid GenerateIdentity() =>
        Guid.NewGuid();
}