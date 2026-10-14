// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Brokers.PageFiles;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Services.Foundations.PageFiles;
internal sealed partial class PageFileService : IPageFileService
{
    private readonly PageFileBroker broker;
    private readonly PageFileConfiguration configuration;
    public PageFileService(PageFileConfiguration configuration)
    {
        this.configuration = configuration;
        broker = OpenPageFileBroker(configuration: configuration);
    }

    private static PageFileBroker OpenPageFileBroker(PageFileConfiguration configuration) =>
        TryCatch(operation: () =>
    {
        ValidatePageFileConfiguration(configuration: configuration);
        return new PageFileBroker(configuration: configuration);
    });

    public int ReadPageFileSize() =>
        TryCatch(operation: () =>
    {
        ValidatePageFileConfiguration(configuration: configuration);
        return configuration.PageSize;
    });

    public string ReadPageFilePath() =>
        TryCatch(operation: () =>
    {
        ValidatePageFileConfiguration(configuration: configuration);
        return configuration.Path;
    });

    public ValueTask ReadPageFileAsync(ulong pageId, Memory<byte> destination, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePageFileOnRead(pageId: pageId, destination: destination, cancellationToken: cancellationToken);

        await broker.ReadFileAsync(offset: CalculatePageFileOffset(pageId: pageId, pageSize: configuration.PageSize), destination: destination, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WritePageFileAsync(ulong pageId, ReadOnlyMemory<byte> source, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePageFileOnWrite(pageId: pageId, source: source, cancellationToken: cancellationToken);

        await broker.WriteFileAsync(offset: CalculatePageFileOffset(pageId: pageId, pageSize: configuration.PageSize), source: source, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask FlushPageFileAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidatePageFileOnFlush(cancellationToken: cancellationToken);

        await broker.FlushFileAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void RestorePageFileLength(long length) =>
        TryCatch(operation: () =>
    {
        ValidatePageFileLength(length: length);
        broker.RestoreFileLength(length: length);
    });

    public ValueTask ClosePageFileAsync() =>
        TryCatch(operation: async () =>
    {
        await broker.CloseFileAsync()
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    private static long CalculatePageFileOffset(ulong pageId, int pageSize) =>
        checked((long)checked(pageId * (ulong)pageSize));
}