// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.LockResources;

namespace sql_storage_engine.Services.Processings.LockResources;
internal interface IIndexKeyLockResourceProcessingService
{
    LockResource PrepareLockResource(LockResource lockResource);

    bool CheckLockResourceConflict(LockResource[] lockResources);

    bool ReadLockResourceEmpty(LockResource lockResource);
}