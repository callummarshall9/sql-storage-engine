// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.IntegrityPageSources;
internal interface IIntegrityPageSourceOrchestrationService
{
    ValueTask<IntegrityContext> ReadIntegrityContextAsync(IntegrityContext integrityContext, CancellationToken cancellationToken);

    ValueTask<IntegrityContext> ReadRawIntegrityContextAsync(IntegrityContext integrityContext, CancellationToken cancellationToken);
}