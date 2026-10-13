// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Storage;
using System;
using System.Collections.Generic;

namespace sql_storage_engine.Models.Overflow;
public sealed class OverflowWriteException : StorageException
{
    public OverflowWriteException(string message, IReadOnlyList<PageId> allocatedPages, Exception innerException) : base(message, innerException) => AllocatedPages = allocatedPages;
    public IReadOnlyList<PageId> AllocatedPages { get; }
}