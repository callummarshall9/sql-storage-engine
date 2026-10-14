// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Configurations;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Services.Foundations.PageFiles;

namespace sql_storage_engine.Exposures.Pages;
public sealed class FilePageStoreExposure : sql_storage_engine.Exposures.Pages.IPageStoreExposure
{
    private readonly PageFileService service;
    private FilePageStoreExposure(PageFileConfiguration configuration)
    {
        service = new PageFileService(configuration: configuration);
    }

    public static FilePageStoreExposure OpenExistingFilePageStore(string path, int pageSize, bool readOnly = false) =>
        new FilePageStoreExposure(configuration: new PageFileConfiguration { Path = path, PageSize = pageSize, ReadOnly = readOnly });

    public static FilePageStoreExposure OpenNewFilePageStore(string path, int pageSize) =>
        new FilePageStoreExposure(configuration: new PageFileConfiguration { Path = path, PageSize = pageSize, CreateNew = true });

    public int GetPageSize() =>
        service.ReadPageFileSize();

    internal string ReadFilePath() =>
        service.ReadPageFilePath();

    public ValueTask ReadAsync(PageId pageId, Memory<byte> destination, CancellationToken cancellationToken = default) =>
        service.ReadPageFileAsync(pageId: pageId.Value, destination: destination, cancellationToken: cancellationToken);

    public ValueTask WriteAsync(PageId pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken = default) =>
        service.WritePageFileAsync(pageId: pageId.Value, source: source, cancellationToken: cancellationToken);

    public ValueTask FlushAsync(CancellationToken cancellationToken = default) =>
        service.FlushPageFileAsync(cancellationToken: cancellationToken);

    internal void RestoreFileLength(long length) =>
        service.RestorePageFileLength(length: length);

    public ValueTask DisposeAsync() =>
        service.ClosePageFileAsync();
}