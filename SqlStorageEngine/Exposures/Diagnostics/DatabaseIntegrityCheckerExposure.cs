// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Services.Aggregations.IntegrityContexts;
using cCoder.CodeAnalysis.Exposures;

namespace sql_storage_engine.Exposures.Diagnostics;
public sealed class DatabaseIntegrityCheckerExposure : ICompositionExposure
{
    private readonly IntegrityContextAggregationService service = IntegrityContextAggregationService.CreateIntegrityContextAggregationService();
    private readonly IntegrityContext state;
    public DatabaseIntegrityCheckerExposure(IPageStoreExposure store, int maximumPages = 1_000_000)
    {
        state = new IntegrityContext
        {
            MaximumPages = maximumPages,
            Source = new RecoveryPageContext
            {
                ProviderSource = store,
                PageSize = store.GetPageSize,
                Read = (id, bytes, token) => store.ReadAsync(pageId: new PageId { Value = id }, destination: bytes, cancellationToken: token)
            }
        };
    }

    public async Task<IntegrityReport> CheckAsync(DatabaseHeader databaseHeader, IntegrityCrossCheck? integrityCrossCheck = null, CancellationToken cancellationToken = default) =>
        (await service.CheckIntegrityContextAsync(integrityContext: state with { DatabaseHeader = databaseHeader, CrossCheck = integrityCrossCheck }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Report!;
}