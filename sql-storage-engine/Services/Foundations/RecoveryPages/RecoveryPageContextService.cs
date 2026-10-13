// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Logging;
using sql_storage_engine.Brokers.RecoveryPages;

namespace sql_storage_engine.Services.Foundations.RecoveryPages;
internal sealed partial class RecoveryPageContextService : IRecoveryPageContextService
{
    private readonly RecoveryPageBroker broker = new();
    public ValueTask<RecoveryPageContext> ReadRecoveryPageContextAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateRead(recoveryPageContext: recoveryPageContext, cancellationToken: cancellationToken);
        var page = new byte[broker.ReadPageSize(pageSize: recoveryPageContext.PageSize!)];

        await broker.ReadAsync(read: recoveryPageContext.Read!, pageId: recoveryPageContext.PageId, destination: page, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return recoveryPageContext with
        {
            Image = page
        };
    });

    public ValueTask WriteRecoveryPageContextAsync(RecoveryPageContext recoveryPageContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateWrite(recoveryPageContext: recoveryPageContext, cancellationToken: cancellationToken);

        await broker.WriteAsync(write: recoveryPageContext.Write!, pageId: recoveryPageContext.PageId, source: recoveryPageContext.Image!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });
}