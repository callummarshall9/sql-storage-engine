// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;
using System.Collections.Generic;
using System.Linq;
using sql_storage_engine.Logging;
using sql_storage_engine.Services.Processings.RecoveryPages;
using sql_storage_engine.Services.Processings.RecoveryPageHeaders;
using sql_storage_engine.Services.Processings.PhysicalPageChanges;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.RecoveryPages;
internal sealed partial class RecoveryPageContextOrchestrationService : IRecoveryPageContextOrchestrationService
{
    private readonly RecoveryPageContextProcessingService pages;
    private readonly PageHeaderRecoveryPageContextProcessingService headers;
    private readonly PhysicalPageChangeCodecProcessingService changes;
    private RecoveryPageContextOrchestrationService(RecoveryPageContextProcessingService pages, PageHeaderRecoveryPageContextProcessingService headers, PhysicalPageChangeCodecProcessingService changes)
    {
        this.pages = pages;
        this.headers = headers;
        this.changes = changes;
    }

    internal static RecoveryPageContextOrchestrationService CreateRecoveryPageContextOrchestrationService() =>
        new RecoveryPageContextOrchestrationService(pages: RecoveryPageContextProcessingService.CreateRecoveryPageContextProcessingService(), headers: PageHeaderRecoveryPageContextProcessingService.CreatePageHeaderRecoveryPageContextProcessingService(), changes: new PhysicalPageChangeCodecProcessingService());

    public RecoveryPageContext ReadRecoveryPageContextAnalysis(RecoveryPageContext recoveryPageContext) =>
        TryCatch(operation: () =>
    {
        ValidateAnalysis(recoveryPageContext: recoveryPageContext);
        return recoveryPageContext;
    });

    public RecoveryPageContext ReadRecoveryPageContextChange(RecoveryPageContext recoveryPageContext) =>
        TryCatch(operation: () =>
    {
        ValidatePayload(recoveryPageContext: recoveryPageContext);

        return recoveryPageContext with
        {
            Change = changes.ReadPhysicalPageChange(source: recoveryPageContext.Payload!)
        };
    });

    public ValueTask UndoRecoveryPageContextImageAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateUndo(recoveryPageContext: recoveryPageContext, cancellationToken: cancellationToken);
        var change = changes.ReadPhysicalPageChange(source: recoveryPageContext.Payload!);

        var image = recoveryPageContext with
        {
            Change = change,
            PageId = change.PageId.Value,
            Image = change.BeforeImage.ToArray()
        };

        _ = headers.CheckRecoveryPageContext(recoveryPageContext: image);

        await pages.WriteRecoveryPageContextAsync(recoveryPageContext: image, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask RedoRecoveryPageContextAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRedo(recoveryPageContext: recoveryPageContext, cancellationToken: cancellationToken);
        var analysis = recoveryPageContext.Analysis!;

        foreach (var record in analysis.Records.Where(predicate: record => record.Type == WalRecordType.PageChange && analysis.Transactions.GetValueOrDefault(key: record.TransactionId) == sql_storage_engine.Transactions.TransactionState.Committed))
        {
            var change = changes.ReadPhysicalPageChange(source: record.Payload.Span);

            var after = recoveryPageContext with
            {
                Change = change,
                PageId = change.PageId.Value,
                Image = change.AfterImage.ToArray(),
                CheckExpectedLsn = true,
                ExpectedLsn = record.Lsn.Value
            };

            _ = headers.CheckRecoveryPageContext(recoveryPageContext: after);

            var current = await pages.ReadRecoveryPageContextAsync(recoveryPageContext: recoveryPageContext with { Change = change, PageId = change.PageId.Value }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

            try
            {
                current = headers.CheckRecoveryPageContext(recoveryPageContext: current);

                if (current.StoredLsn >= record.Lsn.Value)
                {
                    continue;
                }
            }
            catch (StorageCorruptionException)
            {
            }

            await pages.WriteRecoveryPageContextAsync(recoveryPageContext: after, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    });
}