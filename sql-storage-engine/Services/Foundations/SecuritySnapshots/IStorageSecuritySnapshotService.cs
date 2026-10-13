// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Security;

namespace sql_storage_engine.Services.Foundations.SecuritySnapshots;
internal interface IStorageSecuritySnapshotService
{
    StorageSecuritySnapshot PrepareStorageSecuritySnapshot(StorageSecuritySnapshot storageSecuritySnapshot);
}