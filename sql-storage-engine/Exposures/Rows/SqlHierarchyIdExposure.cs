// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Services.Foundations.SqlHierarchies;

namespace sql_storage_engine.Exposures.Rows;
public static class SqlHierarchyIdExposure
{
    private static readonly ISqlHierarchyIdService service = new SqlHierarchyIdService();
    public static SqlHierarchyId ReadRootSqlHierarchyId() =>
        service.ReadRootSqlHierarchyId();

    public static SqlHierarchyId ParseSqlHierarchyId(string value) =>
        service.ParseSqlHierarchyId(value: value);

    public static int ReadSqlHierarchyIdLevel(SqlHierarchyId sqlHierarchyId) =>
        service.ReadSqlHierarchyIdLevel(sqlHierarchyId: sqlHierarchyId);

    public static SqlHierarchyId? ReadSqlHierarchyIdAncestor(SqlHierarchyId sqlHierarchyId, int levels) =>
        service.ReadSqlHierarchyIdAncestor(sqlHierarchyId: sqlHierarchyId, levels: levels);

    public static bool CheckSqlHierarchyIdDescendant(SqlHierarchyId[] sqlHierarchyIds) =>
        service.CheckSqlHierarchyIdDescendant(sqlHierarchyIds: sqlHierarchyIds);

    public static SqlHierarchyId ReparentSqlHierarchyId(SqlHierarchyId[] sqlHierarchyIds) =>
        service.ReparentSqlHierarchyId(sqlHierarchyIds: sqlHierarchyIds);

    public static SqlHierarchyId CreateSqlHierarchyIdDescendant(SqlHierarchyId? [] sqlHierarchyIds) =>
        service.CreateSqlHierarchyIdDescendant(sqlHierarchyIds: sqlHierarchyIds);

    public static int CompareSqlHierarchyIds(SqlHierarchyId? [] sqlHierarchyIds) =>
        service.CompareSqlHierarchyIds(sqlHierarchyIds: sqlHierarchyIds);

    public static byte[] WriteSqlHierarchyId(SqlHierarchyId sqlHierarchyId) =>
        service.WriteSqlHierarchyId(sqlHierarchyId: sqlHierarchyId);

    public static byte[] ReadSqlHierarchyIdSortKey(SqlHierarchyId sqlHierarchyId) =>
        service.ReadSqlHierarchyIdSortKey(sqlHierarchyId: sqlHierarchyId);

    public static SqlHierarchyId ReadSqlHierarchyId(ReadOnlySpan<byte> value) =>
        service.ReadSqlHierarchyId(value: value.ToArray());

    public static string FormatSqlHierarchyId(SqlHierarchyId sqlHierarchyId) =>
        service.FormatSqlHierarchyId(sqlHierarchyId: sqlHierarchyId);
}