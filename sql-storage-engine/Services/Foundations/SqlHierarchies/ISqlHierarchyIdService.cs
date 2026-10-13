// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Foundations.SqlHierarchies;
internal interface ISqlHierarchyIdService
{
    SqlHierarchyId ReadRootSqlHierarchyId();

    SqlHierarchyId ParseSqlHierarchyId(string value);

    int ReadSqlHierarchyIdLevel(SqlHierarchyId sqlHierarchyId);

    SqlHierarchyId? ReadSqlHierarchyIdAncestor(SqlHierarchyId sqlHierarchyId, int levels);

    bool CheckSqlHierarchyIdDescendant(SqlHierarchyId[] sqlHierarchyIds);

    SqlHierarchyId ReparentSqlHierarchyId(SqlHierarchyId[] sqlHierarchyIds);

    SqlHierarchyId CreateSqlHierarchyIdDescendant(SqlHierarchyId? [] sqlHierarchyIds);

    int CompareSqlHierarchyIds(SqlHierarchyId? [] sqlHierarchyIds);

    byte[] WriteSqlHierarchyId(SqlHierarchyId sqlHierarchyId);

    byte[] ReadSqlHierarchyIdSortKey(SqlHierarchyId sqlHierarchyId);

    SqlHierarchyId ReadSqlHierarchyId(byte[] value);

    string FormatSqlHierarchyId(SqlHierarchyId sqlHierarchyId);
}