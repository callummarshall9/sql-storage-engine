// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Exposures.Overflow;
public interface IOverflowChainExposure
{
    OverflowChainBinding ReadOverflowChainBinding();

    ValueTask<OverflowReference> WriteAsync(ReadOnlyMemory<byte> value, CancellationToken cancellationToken = default);

    ValueTask<ReadOnlyMemory<byte>> ReadAsync(OverflowReference overflowReference, CancellationToken cancellationToken = default);

    ValueTask FreeAsync(OverflowReference overflowReference, CancellationToken cancellationToken = default);
}