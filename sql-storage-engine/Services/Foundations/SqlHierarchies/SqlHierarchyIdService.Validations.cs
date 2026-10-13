// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Foundations.SqlHierarchies;
internal sealed partial class SqlHierarchyIdService
{
    private static void ValidateRoot()
    {
    }

    private static void ValidateText(string value)
    {
        ArgumentNullException.ThrowIfNull(argument: value);
    }

    private static void ValidateSqlHierarchyId(SqlHierarchyId sqlHierarchyId)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyId);
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyId.Path);
        _ = ParseLabels(value: sqlHierarchyId.Path);
    }

    private static void ValidateAncestor(SqlHierarchyId sqlHierarchyId, int levels)
    {
        ValidateSqlHierarchyId(sqlHierarchyId: sqlHierarchyId);
        ArgumentOutOfRangeException.ThrowIfNegative(value: levels);
    }

    private static void ValidatePair(SqlHierarchyId[] sqlHierarchyIds)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyIds);

        if (sqlHierarchyIds.Length != 2)
        {
            throw new ArgumentException(message: "Two hierarchy values are required.");
        }

        foreach (var value in sqlHierarchyIds)
        {
            ValidateSqlHierarchyId(sqlHierarchyId: value);
        }
    }

    private static void ValidateTriple(SqlHierarchyId[] sqlHierarchyIds)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyIds);

        if (sqlHierarchyIds.Length != 3)
        {
            throw new ArgumentException(message: "Three hierarchy values are required.");
        }

        foreach (var value in sqlHierarchyIds)
        {
            ValidateSqlHierarchyId(sqlHierarchyId: value);
        }
    }

    private static void ValidateChildren(SqlHierarchyId? [] sqlHierarchyIds)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyIds);

        if (sqlHierarchyIds.Length != 3)
        {
            throw new ArgumentException(message: "A parent and two child bounds are required.");
        }

        ValidateSqlHierarchyId(sqlHierarchyId: sqlHierarchyIds[0]!);

        foreach (var value in sqlHierarchyIds.Skip(count: 1))
        {
            if (value != null)
            {
                ValidateSqlHierarchyId(sqlHierarchyId: value);
            }
        }
    }

    private static void ValidateNullablePair(SqlHierarchyId? [] sqlHierarchyIds)
    {
        ArgumentNullException.ThrowIfNull(argument: sqlHierarchyIds);

        if (sqlHierarchyIds.Length != 2)
        {
            throw new ArgumentException(message: "Two hierarchy values are required.");
        }

        ValidateSqlHierarchyId(sqlHierarchyId: sqlHierarchyIds[0]!);

        if (sqlHierarchyIds[1] != null)
        {
            ValidateSqlHierarchyId(sqlHierarchyId: sqlHierarchyIds[1]!);
        }
    }

    private static void ValidateBinary(byte[] value)
    {
        ArgumentNullException.ThrowIfNull(argument: value);

        if (value.Length > 892 || value.Length == 0 || value[0] != 2)
        {
            throw new StorageFormatException(message: "Unsupported hierarchyid binary version or length.");
        }
    }
}