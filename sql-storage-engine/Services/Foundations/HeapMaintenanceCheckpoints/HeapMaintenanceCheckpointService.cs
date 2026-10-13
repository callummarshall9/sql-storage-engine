// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.HeapMaintenanceCheckpoints;
/// <summary>Validates and publishes immutable in-memory checkpoint snapshots.</summary>
internal sealed partial class HeapMaintenanceCheckpointService : IHeapMaintenanceCheckpointService
{
    private HeapMaintenanceProgress? progress;
    public ValueTask<HeapMaintenanceProgress?> GetHeapMaintenanceProgressAsync(CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateHeapMaintenanceProgressOnGet(cancellationToken: cancellationToken);
        return ValueTask.FromResult(result: Volatile.Read(location: ref progress));
    });

    public ValueTask SetHeapMaintenanceProgressAsync(HeapMaintenanceProgress heapMaintenanceProgress, CancellationToken cancellationToken) =>
        TryCatch(operation: () =>
    {
        ValidateHeapMaintenanceProgressOnSet(heapMaintenanceProgress: heapMaintenanceProgress, cancellationToken: cancellationToken);
        Volatile.Write(location: ref progress, value: heapMaintenanceProgress);
        return ValueTask.CompletedTask;
    });
}