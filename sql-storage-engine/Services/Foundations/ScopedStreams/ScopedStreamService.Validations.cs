// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Generic;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Foundations.ScopedStreams;
internal sealed partial class ScopedStreamService<T>
{
    private static void ValidateSource(ScopedStreamContext scopedStreamContext)
    {
        ArgumentNullException.ThrowIfNull(argument: scopedStreamContext);
        ArgumentNullException.ThrowIfNull(argument: scopedStreamContext.Source);

        if (scopedStreamContext.Source is not IAsyncEnumerable<T>)
        {
            throw new ArgumentException(message: "The source is not compatible with the stream element type.", paramName: nameof(scopedStreamContext));
        }
    }
}