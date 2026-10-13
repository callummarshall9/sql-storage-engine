// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.RowUpdates;
internal interface IRowUpdateProcessingService
{
    RowUpdate PrepareRowUpdate(RowUpdate rowUpdate);
}