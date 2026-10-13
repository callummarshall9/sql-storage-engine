// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class CatalogIndexKeySizeProcessingService : ICatalogIndexKeySizeProcessingService
{
    public int CalculateMaximumSqlTypeIndexKeyBytes(SqlType sqlType) =>
        TryCatch(operation: () =>
    {
        ValidateSqlType(type: sqlType);
        return MaximumIndexKeyBytes(type: sqlType);
    });

    public int CalculateMinimumSqlTypeIndexKeyBytes(SqlType sqlType) =>
        TryCatch(operation: () =>
    {
        ValidateSqlType(type: sqlType);
        return MinimumIndexKeyBytes(type: sqlType);
    });

    private static int MaximumIndexKeyBytes(SqlType type) =>
        type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MaximumIndexKeyBytes(type: type.BaseType!),
        SqlUserTypeKind.Clr when type.ClrMaxByteSize is null => sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes,
        SqlUserTypeKind.Clr => type.ClrMaxByteSize is> 0 and <= sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes ? type.ClrMaxByteSize.Value : sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes + 1,
        _ => type.Name switch
        {
            SqlTypeName.Bit or SqlTypeName.TinyInt => 1,
            SqlTypeName.SmallInt => 2,
            SqlTypeName.Int or SqlTypeName.Real or SqlTypeName.SmallMoney or SqlTypeName.SmallDateTime => 4,
            SqlTypeName.BigInt or SqlTypeName.Money or SqlTypeName.DateTime => 8,
            SqlTypeName.Float => type.Precision <= 24 ? 4 : 8,
            SqlTypeName.Date => 3,
            SqlTypeName.Time => type.Scale <= 2 ? 3 : type.Scale <= 4 ? 4 : 5,
            SqlTypeName.DateTime2 => type.Scale <= 2 ? 6 : type.Scale <= 4 ? 7 : 8,
            SqlTypeName.DateTimeOffset => type.Scale <= 2 ? 8 : type.Scale <= 4 ? 9 : 10,
            SqlTypeName.Decimal or SqlTypeName.Numeric => type.Precision <= 9 ? 5 : type.Precision <= 19 ? 9 : type.Precision <= 28 ? 13 : 17,
            SqlTypeName.UniqueIdentifier => 16,
            SqlTypeName.RowVersion or SqlTypeName.Timestamp => 8,
            SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Binary or SqlTypeName.VarBinary => type.Length!.Value,
            SqlTypeName.NChar or SqlTypeName.NVarChar => checked(type.Length!.Value * 2),
            SqlTypeName.HierarchyId => 892,
            SqlTypeName.SqlVariant => sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumSqlVariantKeyBytes,
            _ => sql_storage_engine.Exposures.Catalog.CatalogIndexKeyExposure.MaximumNonClusteredKeyBytes + 1
        }};

    private static int MinimumIndexKeyBytes(SqlType type) =>
        type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MinimumIndexKeyBytes(type: type.BaseType!),
        SqlUserTypeKind.Clr => type.ClrIsFixedLength && type.ClrMaxByteSize is> 0 ? type.ClrMaxByteSize.Value : 0,
        _ => type.Name switch
        {
            SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary or SqlTypeName.HierarchyId or SqlTypeName.SqlVariant => 0,
            _ => MaximumIndexKeyBytes(type: type)}};
}