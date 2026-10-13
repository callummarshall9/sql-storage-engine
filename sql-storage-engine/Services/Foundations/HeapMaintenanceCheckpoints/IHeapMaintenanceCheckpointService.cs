// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.HeapMaintenanceCheckpoints;
internal interface IHeapMaintenanceCheckpointService
{
    ValueTask<HeapMaintenanceProgress?> GetHeapMaintenanceProgressAsync(CancellationToken cancellationToken);

    ValueTask SetHeapMaintenanceProgressAsync(HeapMaintenanceProgress heapMaintenanceProgress, CancellationToken cancellationToken);
}