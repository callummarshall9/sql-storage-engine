// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;

namespace sql_storage_engine.Services.Foundations.VariantFacets;
internal sealed partial class SqlVariantFacetService : ISqlVariantFacetService
{
    public SqlValueComparisonContext PrepareSqlValueComparisonContext(SqlValueComparisonContext sqlValueComparisonContext) =>
        TryCatch(operation: () =>
    {
        ValidateValues(sqlValueComparisonContext: sqlValueComparisonContext);
        var left = (VariantSqlValue)sqlValueComparisonContext.Left!;
        var right = (VariantSqlValue)sqlValueComparisonContext.Right!;
        var family = GetVariantFamilyOrder(type: left.DeclaredType.Name);
        var order = family.CompareTo(value: GetVariantFamilyOrder(type: right.DeclaredType.Name));

        if (order != 0)
        {
            return sqlValueComparisonContext with
            {
                Order = order,
                VariantFamily = family
            };
        }

        if (family == 6)
        {
            return sqlValueComparisonContext with
            {
                Order = CompareVariantTemporal(left: left, right: right),
                VariantFamily = family
            };
        }

        return sqlValueComparisonContext with
        {
            VariantFamily = family,
            Order = 0,
            Left = family == 4 ? new DecimalSqlValue
            {
                StorageFamily = SqlStorageFamily.Decimal,
                Value = ToVariantDecimal(value: left.Value)
            }

            : left.Value,
            Right = family == 4 ? new DecimalSqlValue
            {
                StorageFamily = SqlStorageFamily.Decimal,
                Value = ToVariantDecimal(value: right.Value)
            }

            : right.Value
        };
    });

    private static int GetVariantFamilyOrder(SqlTypeName type) =>
        type switch
    {
        SqlTypeName.UniqueIdentifier => 1,
        SqlTypeName.VarBinary or SqlTypeName.Binary => 2,
        SqlTypeName.NVarChar or SqlTypeName.NChar or SqlTypeName.VarChar or SqlTypeName.Char => 3,
        SqlTypeName.Decimal or SqlTypeName.Numeric or SqlTypeName.Money or SqlTypeName.SmallMoney or SqlTypeName.BigInt or SqlTypeName.Int or SqlTypeName.SmallInt or SqlTypeName.TinyInt or SqlTypeName.Bit => 4,
        SqlTypeName.Float or SqlTypeName.Real => 5,
        SqlTypeName.DateTimeOffset or SqlTypeName.DateTime2 or SqlTypeName.DateTime or SqlTypeName.SmallDateTime or SqlTypeName.Date or SqlTypeName.Time => 6,
        _ => throw new InvalidOperationException(message: $"{type} is not valid in sql_variant.")};

    private static SqlDecimal ToVariantDecimal(SqlValue value) =>
        value switch
    {
        BooleanSqlValue boolean => new SqlDecimal
        {
            Coefficient = boolean.Value ? 1 : 0,
            Scale = 0
        },
        IntegerSqlValue integer => new SqlDecimal
        {
            Coefficient = integer.Value,
            Scale = 0
        },
        DecimalSqlValue exact => exact.Value,
        _ => throw new InvalidOperationException(message: "Invalid exact-numeric sql_variant value.")};

    private static int CompareVariantTemporal(VariantSqlValue left, VariantSqlValue right)
    {
        var target = TemporalRank(type: left.DeclaredType.Name) <= TemporalRank(type: right.DeclaredType.Name) ? left.DeclaredType.Name : right.DeclaredType.Name;

        return ToVariantTemporalTicks(value: left.Value, target: target)
            .CompareTo(value: ToVariantTemporalTicks(value: right.Value, target: target));
    }

    private static int TemporalRank(SqlTypeName type) =>
        type switch
    {
        SqlTypeName.DateTime2 => 0,
        SqlTypeName.DateTimeOffset => 1,
        SqlTypeName.DateTime => 2,
        SqlTypeName.SmallDateTime => 3,
        SqlTypeName.Date => 4,
        SqlTypeName.Time => 5,
        _ => throw new InvalidOperationException(message: "Invalid temporal sql_variant type.")};

    private static long ToVariantTemporalTicks(SqlValue value, SqlTypeName target) =>
        value switch
    {
        DateSqlValue date => date.Value.ToDateTime(time: TimeOnly.MinValue)
            .Ticks,
        TimeSqlValue time => new DateTime(year: 1900, month: 1, day: 1).Ticks + time.Value.Ticks,
        DateTimeSqlValue dateTime => dateTime.Value.Ticks,
        DateTimeOffsetSqlValue offset when target == SqlTypeName.DateTime2 => offset.Value.DateTime.Ticks,
        DateTimeOffsetSqlValue offset => offset.Value.UtcTicks,
        _ => throw new InvalidOperationException(message: "Invalid temporal sql_variant value.")};
}