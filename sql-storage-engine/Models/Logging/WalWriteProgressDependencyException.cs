// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.IO;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Models.Logging;
internal sealed class WalWriteProgressDependencyException : StorageResourceException
{
    public WalWriteProgressDependencyException() : base(message: "WAL append made no write progress.", innerException: new IOException())
    {
    }
}