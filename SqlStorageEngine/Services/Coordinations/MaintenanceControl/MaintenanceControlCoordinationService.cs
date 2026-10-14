// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Transactions;
using sql_storage_engine.Services.Orchestrations.LockManagers;
using sql_storage_engine.Services.Orchestrations.MaintenanceProgress;

namespace sql_storage_engine.Services.Coordinations.MaintenanceControl;
internal sealed partial class MaintenanceControlCoordinationService(LockManagerContextOrchestrationService locks, MaintenanceProgressOrchestrationService progress) : IMaintenanceControlCoordinationService
{
    internal static MaintenanceControlCoordinationService CreateMaintenanceControlCoordinationService(LockManagerBinding lockManagerBinding) =>
        new MaintenanceControlCoordinationService(locks: (LockManagerContextOrchestrationService)lockManagerBinding.Service!, progress: new MaintenanceProgressOrchestrationService());

    public ValueTask<MaintenanceContext> ReadMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        return await progress.ReadMaintenanceContextAsync(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask WriteMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        await progress.WriteMaintenanceContextAsync(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask YieldMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        await progress.YieldMaintenanceContextAsync(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public MaintenanceContext CalculateMaintenanceContextEnd(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);
        return progress.CalculateMaintenanceContextEnd(maintenanceContext: maintenanceContext);
    });

    public MaintenanceContext ApplyMaintenanceContextSuccess(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);
        return progress.ApplyMaintenanceContextSuccess(maintenanceContext: maintenanceContext);
    });

    public MaintenanceContext ApplyMaintenanceContextFailure(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);
        return progress.ApplyMaintenanceContextFailure(maintenanceContext: maintenanceContext);
    });

    public ValueTask AcquireMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        await locks.AcquireLockManagerContextAsync(lockManagerContext: new LockManagerContext { TransactionId = new TransactionId { Value = ulong.MaxValue - 1 }, LockResource = new TableLockResource { TableId = maintenanceContext.TableId }, Mode = LockMode.Update }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public void ReleaseMaintenanceContext(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);
        locks.ReleaseLockManagerContext(lockManagerContext: new LockManagerContext { TransactionId = new TransactionId { Value = ulong.MaxValue - 1 }, LockResource = new TableLockResource { TableId = maintenanceContext.TableId } });
    });

    public void CloseMaintenanceContext(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContextOnClose(maintenanceContext: maintenanceContext);
        progress.CloseMaintenanceContext(maintenanceContext: maintenanceContext);
    });
}