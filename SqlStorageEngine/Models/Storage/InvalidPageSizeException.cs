// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Storage;
public sealed class InvalidPageSizeException : StorageFormatException
{
    public InvalidPageSizeException(int pageSize, Exception? innerException = null) : base($"Unsupported database page size {pageSize}.", innerException)
    {
        PageSize = pageSize;
    }

    public int PageSize { get; }
}