// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;

namespace sql_storage_engine.Services.Foundations.PageFiles;
internal interface IPageFileService
{
    int ReadPageFileSize();

    string ReadPageFilePath();

    ValueTask ReadPageFileAsync(ulong pageId, Memory<byte> destination, CancellationToken cancellationToken);

    ValueTask WritePageFileAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken);

    ValueTask FlushPageFileAsync(CancellationToken cancellationToken);

    void RestorePageFileLength(long length);

    ValueTask ClosePageFileAsync();
}