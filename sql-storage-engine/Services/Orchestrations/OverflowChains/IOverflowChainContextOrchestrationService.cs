// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Services.Orchestrations.OverflowChains;
internal interface IOverflowChainContextOrchestrationService
{
    ValueTask<OverflowChainContext> WriteOverflowChainContextAsync(OverflowChainContext overflowChainContext, CancellationToken cancellationToken);

    ValueTask<OverflowChainContext> ReadOverflowChainContextAsync(OverflowChainContext overflowChainContext, CancellationToken cancellationToken);

    ValueTask FreeOverflowChainContextAsync(OverflowChainContext overflowChainContext, CancellationToken cancellationToken);
}