// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Foundations.SqlCollations;
using sql_storage_engine.Services.Foundations.VariantFacets;

namespace sql_storage_engine.Services.Orchestrations.SqlValueComparisons;
internal sealed partial class SqlTextVariantOrchestrationService : ISqlTextVariantOrchestrationService
{
    private readonly SqlCollationService collations;
    private readonly SqlVariantFacetService facets;
    public SqlTextVariantOrchestrationService(SqlCollationService collations, SqlVariantFacetService facets)
    {
        this.collations = collations;
        this.facets = facets;
    }

    internal static SqlTextVariantOrchestrationService CreateSqlTextVariantOrchestrationService() =>
        new(collations: new SqlCollationService(), facets: new SqlVariantFacetService());

    public SqlValueComparisonContext PrepareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateValues(sqlValueComparisonContext: sqlValueComparisonContext);
        return facets.PrepareSqlValueComparisonContext(sqlValueComparisonContext: sqlValueComparisonContext);
    });

    public SqlValueComparisonContext CompareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateValues(sqlValueComparisonContext: sqlValueComparisonContext);

        var order = (sqlValueComparisonContext.Left, sqlValueComparisonContext.Right) switch
        {
            (TextSqlValue a, TextSqlValue b) => string.CompareOrdinal(strA: a.Value, strB: b.Value),
            (BinarySqlValue a, BinarySqlValue b) => a.Content.Select(selector: item => checked((byte)item))
            .ToArray()
            .AsSpan()
            .SequenceCompareTo(other: b.Content.Select(selector: item => checked((byte)item))
            .ToArray()),
            (VariantSqlValue a, VariantSqlValue b) => CompareVariantText(left: a, right: b),
            _ => throw new InvalidOperationException(message: "Unknown text value representation.")};

        return sqlValueComparisonContext with
        {
            Order = order
        };
    });

    private int CompareVariantText(VariantSqlValue left, VariantSqlValue right)
    {
        sql_storage_engine.Models.Rows.SqlCollation leftCollation = ReadCollation(sqlType: left.DeclaredType) ?? collations.ReadDefaultSqlCollation();
        sql_storage_engine.Models.Rows.SqlCollation rightCollation = ReadCollation(sqlType: right.DeclaredType) ?? collations.ReadDefaultSqlCollation();
        var collationComparison = collations.CompareSqlCollationMetadata(sqlCollations: [leftCollation, rightCollation]);

        if (collationComparison != 0)
        {
            return collationComparison;
        }

        var leftKey = collations.CalculateSqlCollationSortKey(sqlCollation: leftCollation, value: ((TextSqlValue)left.Value).Value, unicode: left.DeclaredType.Name is SqlTypeName.NChar or SqlTypeName.NVarChar);
        var rightKey = collations.CalculateSqlCollationSortKey(sqlCollation: rightCollation, value: ((TextSqlValue)right.Value).Value, unicode: right.DeclaredType.Name is SqlTypeName.NChar or SqlTypeName.NVarChar);

        return leftKey.AsSpan()
            .SequenceCompareTo(other: rightKey);
    }

    private SqlCollation? ReadCollation(SqlType sqlType) =>
        sqlType.Collation is null ? null : collations.ParseSqlCollation(name: sqlType.Collation);
}