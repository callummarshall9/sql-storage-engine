// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Aggregations.FormatCompatibility;
internal interface IFormatCompatibilityContextAggregationService
{
    ValueTask<CompatibilityContext> OpenCompatibilityContextAsync(CompatibilityContext compatibilityContext, CancellationToken cancellationToken);
}