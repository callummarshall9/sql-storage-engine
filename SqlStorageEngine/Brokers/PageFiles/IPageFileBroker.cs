// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Brokers.PageFiles;
internal interface IPageFileBroker
{
    ValueTask ReadFileAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WriteFileAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushFileAsync(CancellationToken cancellationToken);

    void RestoreFileLength(long length);

    ValueTask CloseFileAsync();
}