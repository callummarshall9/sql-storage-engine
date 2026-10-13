// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Models.Tables;
using sql_storage_engine.Exposures.Heap;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Services.Aggregations.StorageSessions;

namespace sql_storage_engine.Exposures.Tables;
public sealed class TableStorageExposure : ICompositionExposure, ITableStorageExposure
{
    private readonly StorageSessionAggregationService service;
    private readonly TableStorageContext context;
    public TableStorageExposure(CatalogTable catalogTable, ITableHeapExposure heap, IRowCodecExposure rowCodec, IEnumerable<ITableIndexExposure> indexes)
    {
        var binding = StorageSessionAggregationService.CreateStandaloneStorageSessionContext(catalogTable: catalogTable, tableHeapBinding: heap.ReadTableHeapBinding(), rowCodecBinding: rowCodec.ReadRowCodecBinding(), tableIndexBindings: indexes.Select(selector: index => index.ReadTableIndexBinding())
            .ToArray());
        service = (StorageSessionAggregationService)binding.Binding!.Service!;
        context = binding.Table!;
    }

    public TableStorageExposure(TableStorageBinding tableStorageBinding)
    {
        service = (StorageSessionAggregationService)tableStorageBinding.Service!;
        context = tableStorageBinding.Context!;
    }

    public TableStorageBinding ReadTableStorageBinding() =>
        new TableStorageBinding
    {
        Service = service,
        Context = context
    };

    public CatalogTable ReadDefinition() =>
        service.ReadStorageSessionContextTableDefinition(storageSessionContext: new StorageSessionContext { Table = context })
            .Table!.Definition!;

    public IAsyncEnumerable<StoredRow> ScanAsync(CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextTableScan(storageSessionContext: new StorageSessionContext { Table = context }, cancellationToken: cancellationToken)
            .Table!.Rows!;

    public IAsyncEnumerable<StoredRow> SampleAsync(decimal pagePercentage, long? requestedRows = null, long? repeatableSeed = null, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextTableSample(storageSessionContext: new StorageSessionContext { Table = context with { Binding = context, PagePercentage = pagePercentage, RequestedRows = requestedRows, RepeatableSeed = repeatableSeed } }, cancellationToken: cancellationToken)
            .Table!.Rows!;

    public async ValueTask<RowId> InsertAsync(Row newRow, CancellationToken cancellationToken = default) =>
        (await service.AddStorageSessionContextTableAsync(newStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, Row = newRow } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.InsertResult!.RowId!.Value;

    public async ValueTask<TableInsertResult> TryInsertAsync(Row row, CancellationToken cancellationToken = default) =>
        (await service.AddStorageSessionContextTableTryAsync(newStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, Row = row } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.InsertResult!;

    internal async ValueTask<TableInsertResult> InsertGraphAsync(Row newRow, CancellationToken cancellationToken = default) =>
        (await service.AddStorageSessionContextTableGraphAsync(newStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, Row = newRow } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.InsertResult!;

    internal async ValueTask<TableInsertResult> InsertPreparedAsync(Row newRow, CancellationToken cancellationToken = default) =>
        (await service.AddStorageSessionContextTablePreparedAsync(newStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, Row = newRow } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.InsertResult!;

    public async ValueTask<(bool Found, Row? Row)> TryGetAsync(RowId rowId, CancellationToken cancellationToken = default) =>
        ReadRowResult(storageSessionContext: await service.ReadStorageSessionContextTableAsync(storageSessionContext: new StorageSessionContext { Table = context with { Binding = context, RowId = rowId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false));

    private static (bool Found, Row? Row) ReadRowResult(StorageSessionContext storageSessionContext) =>
        (storageSessionContext.Table!.Found, storageSessionContext.Table.Row);

    public async ValueTask<TableUpdateResult> UpdateAsync(RowId updatedRowId, RowUpdate updatedRowUpdate, CancellationToken cancellationToken = default) =>
        (await service.UpdateStorageSessionContextTableAsync(updatedStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, RowId = updatedRowId, Update = updatedRowUpdate } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.UpdateResult!;

    internal async ValueTask<TableUpdateResult> UpdateGraphAsync(RowId updatedRowId, RowUpdate updatedRowUpdate, CancellationToken cancellationToken = default) =>
        (await service.UpdateStorageSessionContextTableGraphAsync(updatedStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, RowId = updatedRowId, Update = updatedRowUpdate } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.UpdateResult!;

    internal async ValueTask<TableUpdateResult> UpdateSystemVersionedAsync(RowId updatedRowId, RowUpdate updatedRowUpdate, SqlValue updatedSqlValue, CancellationToken cancellationToken = default) =>
        (await service.UpdateStorageSessionContextTableSystemVersionedAsync(updatedStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, RowId = updatedRowId, Update = updatedRowUpdate, PeriodStart = updatedSqlValue } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.UpdateResult!;

    public async ValueTask<TableDeleteResult> DeleteAsync(RowId deletedRowId, CancellationToken cancellationToken = default) =>
        (await service.DeleteStorageSessionContextTableAsync(deletedStorageSessionContext: new StorageSessionContext { Table = context with { Binding = context, RowId = deletedRowId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).Table!.DeleteResult!;
}