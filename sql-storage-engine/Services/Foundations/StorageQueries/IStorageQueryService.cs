// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Storage;

namespace sql_storage_engine.Services.Foundations.StorageQueries;
internal interface IStorageQueryService
{
    StorageQueryContext CreateStorageQueryContext(StorageQueryContext storageQueryContext);
}