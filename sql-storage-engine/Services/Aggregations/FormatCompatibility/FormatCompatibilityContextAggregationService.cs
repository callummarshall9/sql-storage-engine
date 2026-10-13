// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Coordinations.CompatibilityFixtures;
using sql_storage_engine.Services.Coordinations.PageDatabases;

namespace sql_storage_engine.Services.Aggregations.FormatCompatibility;
internal sealed partial class FormatCompatibilityContextAggregationService : IFormatCompatibilityContextAggregationService
{
    private readonly CompatibilityContextCoordinationService fixtures = new CompatibilityContextCoordinationService();
    private readonly PageDatabaseContextCoordinationService pages = PageDatabaseContextCoordinationService.CreatePageDatabaseContextCoordinationService();
    public ValueTask<CompatibilityContext> OpenCompatibilityContextAsync(CompatibilityContext compatibilityContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateCompatibilityContext(compatibilityContext: compatibilityContext, cancellationToken: cancellationToken);

        var materialized = await fixtures.MaterializeCompatibilityContextAsync(compatibilityContext: compatibilityContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        var database = await pages.OpenPageDatabaseContextAsync(pageDatabaseContext: new PageDatabaseContext { Path = compatibilityContext.DatabasePath, OpenMode = DatabaseOpenMode.ReadOnly }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        try
        {
            var header = pages.ReadPageDatabaseContext(pageDatabaseContext: database)
            .Runtime!.Header!;

            var findings = new List<IntegrityFinding>();
            var page = new byte[header.PageSize];

            if (header.NextPageId.Value > 1_000_000)
            {
                findings.Add(item: new IntegrityFinding { Code = "TRAVERSAL_LIMIT", PageId = null, Message = "Allocated page count exceeds the integrity traversal bound." });
            }

            for (ulong id = 0; id < header.NextPageId.Value && header.NextPageId.Value <= 1_000_000; id++)
            {
                await pages.ReadPageDatabaseContextAsync(pageDatabaseContext: database, pageId: id, destination: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                try
                {
                    pages.CheckPageDatabaseContextPage(pageDatabaseContext: database, pageId: id, source: page);
                }
                catch (StorageCorruptionException exception)
                {
                    findings.Add(item: new IntegrityFinding { Code = "PAGE_CORRUPTION", PageId = new PageId { Value = id }, Message = exception.Message });
                }
                catch (StorageFormatException exception)
                {
                    findings.Add(item: new IntegrityFinding { Code = "PAGE_FORMAT", PageId = new PageId { Value = id }, Message = exception.Message });
                }
            }

            var wal = fixtures.ReadCompatibilityContextWal(compatibilityContext: materialized);

            return compatibilityContext with
            {
                Result = new CompatibilityResult
                {
                    Header = header,
                    Integrity = new IntegrityReport
                    {
                        Findings = findings.AsReadOnly(),
                        IsHealthy = findings.Count == 0
                    },
                    Rows = materialized.Fixture!.ExpectedRows.ToArray(),
                    WalRecords = wal.Records!
                }
            };
        }
        finally
        {
            await pages.ClosePageDatabaseContextAsync(pageDatabaseContext: database)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    });
}