// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Models.LockResources;
using sql_storage_engine.Services.Processings.LockResources;
using sql_storage_engine.Services.Foundations.IndexKeys;

namespace sql_storage_engine.Exposures.LockResources;
public static class LockResourceExposure
{
    private static readonly IIndexKeyLockResourceProcessingService service = new IndexKeyLockResourceProcessingService(keys: new IndexKeyService());
    public static TableLockResource CreateTableLockResource(TableLockResource tableLockResource) =>
        (TableLockResource)service.PrepareLockResource(lockResource: tableLockResource);

    public static RowLockResource CreateRowLockResource(RowLockResource rowLockResource) =>
        (RowLockResource)service.PrepareLockResource(lockResource: rowLockResource);

    public static IndexKeyLockResource CreateIndexKeyLockResource(IndexKeyLockResource indexKeyLockResource) =>
        (IndexKeyLockResource)service.PrepareLockResource(lockResource: indexKeyLockResource);

    public static IndexRangeLockResource CreateIndexRangeLockResource(IndexRangeLockResource indexRangeLockResource) =>
        (IndexRangeLockResource)service.PrepareLockResource(lockResource: indexRangeLockResource);

    public static bool CheckLockResourceConflict(LockResource[] lockResources) =>
        service.CheckLockResourceConflict(lockResources: lockResources);

    public static bool ReadIndexRangeLockResourceEmpty(IndexRangeLockResource indexRangeLockResource) =>
        service.ReadLockResourceEmpty(lockResource: indexRangeLockResource);

    public static IndexRangeLockResource CreateIndexRangeLockResourceFromRange(IndexId indexId, IndexRange indexRange) =>
        (IndexRangeLockResource)service.PrepareLockResource(lockResource: new IndexRangeLockResource { IndexId = indexId, LowerBound = indexRange.LowerBound, UpperBound = indexRange.UpperBound, IncludeLowerBound = indexRange.IncludeLowerBound, IncludeUpperBound = indexRange.IncludeUpperBound });
}