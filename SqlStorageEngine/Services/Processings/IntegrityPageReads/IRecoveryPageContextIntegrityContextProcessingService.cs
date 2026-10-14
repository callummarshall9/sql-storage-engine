// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Processings.IntegrityPageReads;
internal interface IRecoveryPageContextIntegrityContextProcessingService
{
    ValueTask<IntegrityContext> ReadIntegrityContextAsync(IntegrityContext integrityContext, CancellationToken cancellationToken);
}