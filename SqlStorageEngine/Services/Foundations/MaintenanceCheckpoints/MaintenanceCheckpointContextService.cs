// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Brokers.MaintenanceCheckpoints;

namespace sql_storage_engine.Services.Foundations.MaintenanceCheckpoints;
internal sealed partial class MaintenanceCheckpointContextService : IMaintenanceCheckpointContextService
{
    private readonly MaintenanceCheckpointBroker checkpoint = new MaintenanceCheckpointBroker();
    public ValueTask<MaintenanceContext> ReadMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        return maintenanceContext with
        {
            Progress = await checkpoint.ReadAsync(read: maintenanceContext.Read!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)
        };
    });

    public ValueTask WriteMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        await checkpoint.WriteAsync(write: maintenanceContext.Write!, value: maintenanceContext.Progress!, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask YieldMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        if (maintenanceContext.Throttle is not null)
        {
            await checkpoint.YieldAsync(throttle: maintenanceContext.Throttle, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
        }
    });

    public void CloseMaintenanceContext(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContextOnClose(maintenanceContext: maintenanceContext);
        checkpoint.Close();
    });
}