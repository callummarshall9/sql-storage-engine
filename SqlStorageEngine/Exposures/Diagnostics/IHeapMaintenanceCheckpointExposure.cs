// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Exposures.Diagnostics;
public interface IHeapMaintenanceCheckpointExposure
{
    ValueTask<HeapMaintenanceProgress?> ReadAsync(CancellationToken cancellationToken = default);

    ValueTask WriteAsync(HeapMaintenanceProgress heapMaintenanceProgress, CancellationToken cancellationToken = default);
}