// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Models.Tables;

namespace sql_storage_engine.Exposures.Tables;
public interface ITableStorageExposure
{
    TableStorageBinding ReadTableStorageBinding();

    CatalogTable ReadDefinition();

    IAsyncEnumerable<StoredRow> ScanAsync(CancellationToken cancellationToken = default);

    IAsyncEnumerable<StoredRow> SampleAsync(decimal pagePercentage, long? requestedRows = null, long? repeatableSeed = null, CancellationToken cancellationToken = default);

    ValueTask<RowId> InsertAsync(Row newRow, CancellationToken cancellationToken = default);

    ValueTask<TableInsertResult> TryInsertAsync(Row row, CancellationToken cancellationToken = default);

    ValueTask<(bool Found, Row? Row)> TryGetAsync(RowId rowId, CancellationToken cancellationToken = default);

    ValueTask<TableUpdateResult> UpdateAsync(RowId updatedRowId, RowUpdate updatedRowUpdate, CancellationToken cancellationToken = default);

    ValueTask<TableDeleteResult> DeleteAsync(RowId deletedRowId, CancellationToken cancellationToken = default);
}