// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Services.Foundations.HeapMaintenanceCheckpoints;

namespace sql_storage_engine.Exposures.Diagnostics;
public sealed class MemoryHeapMaintenanceCheckpointExposure : IHeapMaintenanceCheckpointExposure
{
    private readonly IHeapMaintenanceCheckpointService service = new HeapMaintenanceCheckpointService();
    public ValueTask<HeapMaintenanceProgress?> ReadAsync(CancellationToken cancellationToken = default) =>
        service.GetHeapMaintenanceProgressAsync(cancellationToken: cancellationToken);

    public ValueTask WriteAsync(HeapMaintenanceProgress heapMaintenanceProgress, CancellationToken cancellationToken = default) =>
        service.SetHeapMaintenanceProgressAsync(heapMaintenanceProgress: heapMaintenanceProgress, cancellationToken: cancellationToken);
}