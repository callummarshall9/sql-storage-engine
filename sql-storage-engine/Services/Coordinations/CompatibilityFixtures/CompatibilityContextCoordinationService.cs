// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Services.Orchestrations.CompatibilityFiles;
using sql_storage_engine.Services.Orchestrations.DatabaseHeaders;
using sql_storage_engine.Services.Orchestrations.CompatibilityWal;

namespace sql_storage_engine.Services.Coordinations.CompatibilityFixtures;
internal sealed partial class CompatibilityContextCoordinationService : ICompatibilityContextCoordinationService
{
    private readonly CompatibilityFileOrchestrationService files = new CompatibilityFileOrchestrationService();
    private readonly DatabaseHeaderOrchestrationService headers = new DatabaseHeaderOrchestrationService();
    private readonly CompatibilityWalOrchestrationService wal = new CompatibilityWalOrchestrationService();
    public ValueTask<CompatibilityContext> MaterializeCompatibilityContextAsync(CompatibilityContext compatibilityContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCompatibilityContext(compatibilityContext: compatibilityContext, cancellationToken: cancellationToken);

        var loaded = await files.ReadCompatibilityContextAsync(compatibilityContext: compatibilityContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var fixture = loaded.Fixture!;

        var header = new DatabaseHeader
        {
            DatabaseId = new sql_storage_engine.Models.Identifiers.DatabaseId
            {
                Value = fixture.DatabaseId
            },
            PageSize = fixture.PageSize,
            FormatVersion = fixture.FormatVersion,
            CatalogRootPageId = null,
            FreeListHeadPageId = null,
            NextTableId = new sql_storage_engine.Models.Identifiers.TableId
            {
                Value = 1
            },
            NextIndexId = new sql_storage_engine.Models.Identifiers.IndexId
            {
                Value = 1
            },
            NextTransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = 1
            },
            NextPageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 1
            },
            IsCleanShutdown = true,
            NextRowVersion = 1,
            DefaultCollation = "SQL_Latin1_General_CP1_CI_AS",
            NextGeneratedSequence = 1
        };

        var page = new byte[fixture.PageSize];
        headers.WriteDatabaseHeader(page: page, databaseHeader: header);

        var result = loaded with
        {
            Bytes = page,
            Header = header
        };

        await files.WriteCompatibilityContextAsync(compatibilityContext: result, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return result;
    });

    public CompatibilityContext ReadCompatibilityContextWal(CompatibilityContext compatibilityContext) =>
        TryCatch(operation: () =>
    {
        ValidateCompatibilityContext(compatibilityContext: compatibilityContext);
        return wal.ReadCompatibilityContext(compatibilityContext: compatibilityContext);
    });
}