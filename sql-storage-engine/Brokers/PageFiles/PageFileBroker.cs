// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Dependencies.PageFiles;

namespace sql_storage_engine.Brokers.PageFiles;
internal sealed class PageFileBroker : IPageFileBroker
{
    private readonly PageFileDependency dependency;
    public PageFileBroker(PageFileConfiguration configuration)
    {
        dependency = new PageFileDependency(path: configuration.Path, readOnly: configuration.ReadOnly, createNew: configuration.CreateNew);
    }

    public ValueTask ReadFileAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken) =>
        dependency.ReadFileAsync(offset: offset, destination: destination, cancellationToken: cancellationToken);

    public ValueTask WriteFileAsync(long offset, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        dependency.WriteFileAsync(offset: offset, source: source, cancellationToken: cancellationToken);

    public ValueTask FlushFileAsync(CancellationToken cancellationToken) =>
        dependency.FlushFileAsync(cancellationToken: cancellationToken);

    public void RestoreFileLength(long length) =>
        dependency.RestoreFileLength(length: length);

    public ValueTask CloseFileAsync() =>
        dependency.DisposeAsync();
}