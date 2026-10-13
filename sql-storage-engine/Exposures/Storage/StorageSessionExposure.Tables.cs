// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Tables;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Exposures.Storage;
public sealed partial class StorageSessionExposure
{
    CatalogTable IStorageTable.ReadDefinition() =>
        tableContext.Storage!.Definition!;

    public async ValueTask<RowId> InsertAsync(Row newRow, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextPublicTableAsync(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Insert", Row = newRow } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PublicTable!.InsertResult!.RowId!.Value;

    public async ValueTask<TableInsertResult> TryInsertAsync(Row row, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextPublicTableAsync(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "TryInsert", Row = row } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PublicTable!.InsertResult!;

    public async ValueTask<StoredRow?> GetAsync(RowId rowId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextPublicTableAsync(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Get", RowId = rowId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PublicTable!.StoredRow;

    public async ValueTask<StoredRow?> GetAsync(RowId rowId, StorageReadOptions storageReadOptions, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextPublicTableAsync(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Get", RowId = rowId, ReadOptions = storageReadOptions } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PublicTable!.StoredRow;

    public IAsyncEnumerable<StoredRow> ScanAsync(CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextPublicTableScan(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Scan" } }, cancellationToken: cancellationToken)
            .PublicTable!.Rows!;

    public IAsyncEnumerable<StoredRow> ScanAsync(StorageReadOptions storageReadOptions, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextPublicTableScan(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Scan", ReadOptions = storageReadOptions } }, cancellationToken: cancellationToken)
            .PublicTable!.Rows!;

    public IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample storageTableSample, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextPublicTableScan(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Sample", Sample = storageTableSample } }, cancellationToken: cancellationToken)
            .PublicTable!.Rows!;

    public IAsyncEnumerable<StoredRow> SampleAsync(StorageTableSample storageTableSample, StorageReadOptions storageReadOptions, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextPublicTableScan(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Sample", Sample = storageTableSample, ReadOptions = storageReadOptions } }, cancellationToken: cancellationToken)
            .PublicTable!.Rows!;

    public IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery storageTemporalQuery, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextPublicTableScan(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Temporal", TemporalQuery = storageTemporalQuery } }, cancellationToken: cancellationToken)
            .PublicTable!.TemporalRows!;

    public IAsyncEnumerable<StorageTemporalRow> TemporalScanAsync(StorageTemporalQuery storageTemporalQuery, StorageReadOptions storageReadOptions, CancellationToken cancellationToken = default) =>
        service.ReadStorageSessionContextPublicTableScan(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Temporal", TemporalQuery = storageTemporalQuery, ReadOptions = storageReadOptions } }, cancellationToken: cancellationToken)
            .PublicTable!.TemporalRows!;

    public async ValueTask<TableUpdateResult> UpdateAsync(RowId updatedRowId, RowUpdate updatedRowUpdate, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextPublicTableAsync(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Update", RowId = updatedRowId, Update = updatedRowUpdate } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PublicTable!.UpdateResult!;

    public async ValueTask<TableDeleteResult> DeleteAsync(RowId deletedRowId, CancellationToken cancellationToken = default) =>
        (await service.ExecuteStorageSessionContextPublicTableAsync(storageSessionContext: new StorageSessionContext { PublicTable = tableContext with { Operation = "Delete", RowId = deletedRowId } }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false)).PublicTable!.DeleteResult!;
}