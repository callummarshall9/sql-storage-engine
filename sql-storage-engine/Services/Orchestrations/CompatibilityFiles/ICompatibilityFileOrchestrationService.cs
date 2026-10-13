// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Orchestrations.CompatibilityFiles;
internal interface ICompatibilityFileOrchestrationService
{
    ValueTask<CompatibilityContext> ReadCompatibilityContextAsync(CompatibilityContext compatibilityContext, CancellationToken cancellationToken);

    ValueTask WriteCompatibilityContextAsync(CompatibilityContext compatibilityContext, CancellationToken cancellationToken);
}