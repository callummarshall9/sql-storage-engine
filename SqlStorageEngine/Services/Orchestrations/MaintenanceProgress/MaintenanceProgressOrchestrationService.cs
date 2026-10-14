// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Foundations.MaintenanceCheckpoints;
using sql_storage_engine.Services.Foundations.MaintenancePolicies;

namespace sql_storage_engine.Services.Orchestrations.MaintenanceProgress;
internal sealed partial class MaintenanceProgressOrchestrationService : IMaintenanceProgressOrchestrationService
{
    private readonly MaintenanceCheckpointContextService checkpoint = new MaintenanceCheckpointContextService();
    private readonly MaintenancePolicyService policy = new MaintenancePolicyService();
    public ValueTask<MaintenanceContext> ReadMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        var read = await checkpoint.ReadMaintenanceContextAsync(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        return policy.PrepareMaintenanceContext(maintenanceContext: read);
    });

    public ValueTask WriteMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        await checkpoint.WriteMaintenanceContextAsync(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public ValueTask YieldMaintenanceContextAsync(MaintenanceContext maintenanceContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken);

        await checkpoint.YieldMaintenanceContextAsync(maintenanceContext: maintenanceContext, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);
    });

    public MaintenanceContext CalculateMaintenanceContextEnd(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);
        return policy.CalculateMaintenanceContextEnd(maintenanceContext: maintenanceContext);
    });

    public MaintenanceContext ApplyMaintenanceContextSuccess(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);
        return policy.ApplyMaintenanceContextSuccess(maintenanceContext: maintenanceContext);
    });

    public MaintenanceContext ApplyMaintenanceContextFailure(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContext(maintenanceContext: maintenanceContext);
        return policy.ApplyMaintenanceContextFailure(maintenanceContext: maintenanceContext);
    });

    public void CloseMaintenanceContext(MaintenanceContext maintenanceContext) =>
        TryCatch(operation: () =>
    {
        ValidateMaintenanceContextOnClose(maintenanceContext: maintenanceContext);
        checkpoint.CloseMaintenanceContext(maintenanceContext: maintenanceContext);
    });
}