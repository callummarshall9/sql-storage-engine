// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Runtime.CompilerServices;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Access;
using sql_storage_engine.Services.Foundations.StorageAccesses;
using sql_storage_engine.Services.Foundations.ScopedStreams;

namespace sql_storage_engine.Services.Orchestrations.ScopedStreams;
internal sealed partial class ScopedStreamOrchestrationService<T> : IScopedStreamOrchestrationService<T>
{
    private readonly StorageAccessService access;
    private readonly ScopedStreamService<T> streams;
    private ScopedStreamOrchestrationService(StorageAccessService access, ScopedStreamService<T> streams)
    {
        this.access = access;
        this.streams = streams;
    }

    internal static ScopedStreamOrchestrationService<T> BindScopedStreamOrchestrationService(EngineAccessBinding engineAccessBinding) =>
        new(access: (StorageAccessService)engineAccessBinding.Service!, streams: new ScopedStreamService<T>());

    public EngineResourceContext TrackEngineResourceContextStream(EngineResourceContext engineResourceContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(engineResourceContext: engineResourceContext);
        var source = streams.WrapScopedStreamContext(scopedStreamContext: new ScopedStreamContext { Source = ReadCoreAsync(source: (IAsyncEnumerable<T>)engineResourceContext.StreamSource!) });

        try
        {
            access.RegisterStorageAccessResource(storageAccess: engineResourceContext.Access!, resource: source.Resource!);
            engineResourceContext.StreamSource = source.Source;
            return engineResourceContext;
        }
        catch
        {
            source.Resource!.DisposeAsync()
            .AsTask()
            .GetAwaiter()
            .GetResult();

            throw;
        }
    });

    private async IAsyncEnumerable<T> ReadCoreAsync(IAsyncEnumerable<T> source, [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        await using var iterator = source.GetAsyncEnumerator(cancellationToken: cancellationToken);

        while (true)
        {
            StorageAccess? lease = null;
            StorageAccess? activation = null;
            T value;

            try
            {
                lease = await access.EnterStorageAccessAsync(storageAccess: null, token: CancellationToken.None, reentrant: true)
            .ConfigureAwait(continueOnCapturedContext: false);

                activation = access.ActivateStorageAccess(storageAccess: lease);

                if (!await iterator.MoveNextAsync()
            .ConfigureAwait(continueOnCapturedContext: false))
                {
                    yield break;
                }

                value = iterator.Current;
            }
            finally
            {
                try
                {
                    if (activation is not null)
                    {
                        access.CloseStorageAccess(storageAccess: activation);
                    }
                }
                finally
                {
                    if (lease is not null)
                    {
                        access.CloseStorageAccess(storageAccess: lease);
                    }
                }
            }

            yield return value;
        }
    }
}