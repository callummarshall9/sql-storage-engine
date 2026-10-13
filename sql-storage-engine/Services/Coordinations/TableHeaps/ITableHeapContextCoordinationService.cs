// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Heap;

namespace sql_storage_engine.Services.Coordinations.TableHeaps;
internal interface ITableHeapContextCoordinationService
{
    ValueTask<TableHeapContext> CreateTableHeapContextAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken);

    ValueTask<TableHeapContext> OpenTableHeapContextAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken);

    TableHeapContext ReadTableHeapContext(TableHeapContext tableHeapContext);

    ValueTask<TableHeapContext> AddTableHeapContextAsync(TableHeapContext newTableHeapContext, CancellationToken cancellationToken);

    ValueTask<TableHeapContext> DeleteTableHeapContextAsync(TableHeapContext deletedTableHeapContext, CancellationToken cancellationToken);

    ValueTask<TableHeapContext> UpdateTableHeapContextAsync(TableHeapContext updatedTableHeapContext, CancellationToken cancellationToken);

    ValueTask<TableHeapContext> CompactTableHeapContextAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken);

    ValueTask<TableHeapContext> ReadTableHeapContextPageIdsAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken);

    ValueTask RebuildTableHeapContextFreeSpacesAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken);

    ValueTask<TableHeapContext> ReadTableHeapContextRowAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken);

    TableHeapContext ReadTableHeapContextScan(TableHeapContext tableHeapContext, CancellationToken cancellationToken);

    TableHeapContext ReadTableHeapContextSample(TableHeapContext tableHeapContext, CancellationToken cancellationToken);

    ValueTask<TableHeapContext> ReadTableHeapContextLiveCountAsync(TableHeapContext tableHeapContext, CancellationToken cancellationToken);

    TableHeapContext FindTableHeapContextFreeSpacePage(TableHeapContext tableHeapContext);

    void SetTableHeapContextFreeSpace(TableHeapContext tableHeapContext, int freeBytes);

    void RemoveTableHeapContextFreeSpace(TableHeapContext tableHeapContext);

    void ClearTableHeapContextFreeSpaces(TableHeapContext tableHeapContext);
}