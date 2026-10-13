// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Coordinations.CompatibilityFixtures;
internal interface ICompatibilityContextCoordinationService
{
    ValueTask<CompatibilityContext> MaterializeCompatibilityContextAsync(CompatibilityContext compatibilityContext, CancellationToken cancellationToken);

    CompatibilityContext ReadCompatibilityContextWal(CompatibilityContext compatibilityContext);
}