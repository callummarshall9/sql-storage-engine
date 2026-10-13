// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Foundations.ScopedStreams;
internal sealed partial class ScopedStreamService<T> : IScopedStreamService<T>
{
    private readonly sql_storage_engine.Brokers.ScopedStreams.ScopedStreamBroker<T> broker = new();
    public ScopedStreamContext WrapScopedStreamContext(ScopedStreamContext scopedStreamContext) =>
        TryCatch(operation: () =>
    {
        ValidateSource(scopedStreamContext: scopedStreamContext);
        var opened = broker.Open(source: (IAsyncEnumerable<T>)scopedStreamContext.Source!);

        return scopedStreamContext with
        {
            LeaseId = opened.LeaseId,
            Source = opened.Source,
            Resource = opened.Resource
        };
    });
}