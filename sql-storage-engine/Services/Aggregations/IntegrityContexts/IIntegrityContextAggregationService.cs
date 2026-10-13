// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Aggregations.IntegrityContexts;
internal interface IIntegrityContextAggregationService
{
    ValueTask<IntegrityContext> CheckIntegrityContextAsync(IntegrityContext integrityContext, CancellationToken cancellationToken);
}