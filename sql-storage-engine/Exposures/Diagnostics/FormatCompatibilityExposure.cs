// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Aggregations.FormatCompatibility;

namespace sql_storage_engine.Exposures.Diagnostics;
public sealed class FormatCompatibilityExposure : ICompositionExposure
{
    public const int MaximumFixtureBytes = 1024 * 1024;
    private static readonly IFormatCompatibilityContextAggregationService service = new FormatCompatibilityContextAggregationService();
    private FormatCompatibilityExposure()
    {
    }

    public static async Task<CompatibilityResult> OpenAsync(string fixturePath, string materializedDatabasePath, CancellationToken cancellationToken = default) =>
        (await service.OpenCompatibilityContextAsync(compatibilityContext: new CompatibilityContext { FixturePath = fixturePath, DatabasePath = materializedDatabasePath }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Result!;
}