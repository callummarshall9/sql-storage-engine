// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Storage;
public sealed class UnsupportedDatabaseVersionException : StorageFormatException
{
    public UnsupportedDatabaseVersionException(ushort version, Exception? innerException = null) : base($"Unsupported database format version {version}.", innerException)
    {
        Version = version;
    }

    public ushort Version { get; }
}