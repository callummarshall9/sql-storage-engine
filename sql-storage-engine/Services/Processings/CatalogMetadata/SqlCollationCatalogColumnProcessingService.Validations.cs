// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Rows;
using System.Numerics;
using System;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Models.Rows;

namespace sql_storage_engine.Services.Processings.CatalogMetadata;
internal sealed partial class SqlCollationCatalogColumnProcessingService
{
    private void ValidateCatalogColumn(CatalogColumn catalogColumn)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogColumn);
        var id = catalogColumn.IdSource;
        var name = catalogColumn.NameSource;
        var type = catalogColumn.TypeSource;
        var isNullable = catalogColumn.IsNullableSource;
        var defaultExpression = catalogColumn.DefaultExpressionSource;
        var identity = catalogColumn.IdentitySource;
        var isRowGuidCol = catalogColumn.IsRowGuidColSource;
        var computedExpression = catalogColumn.ComputedExpressionSource;
        var isComputedPersisted = catalogColumn.IsComputedPersistedSource;
        var isSparse = catalogColumn.IsSparseSource;
        var isColumnSet = catalogColumn.IsColumnSetSource;
        var isFileStream = catalogColumn.IsFileStreamSource;
        var maskingFunction = catalogColumn.MaskingFunctionSource;
        var encryption = catalogColumn.EncryptionSource;
        var generatedAlways = catalogColumn.GeneratedAlwaysSource;
        var isHidden = catalogColumn.IsHiddenSource;

        if (identity is not null)
        {
            identity = PrepareCatalogIdentity(catalogIdentity: identity);
        }

        if (encryption is not null)
        {
            encryption = PrepareCatalogColumnEncryption(catalogColumnEncryption: encryption);
        }

        if (string.IsNullOrWhiteSpace(value: name))
        {
            throw new ArgumentException(message: "Column name cannot be empty.", paramName: nameof(name));
        }

        ArgumentNullException.ThrowIfNull(argument: type);

        if (!CanBeColumn(type: type))
        {
            throw new ArgumentException(message: $"{type.Name} cannot be used as a table column.", paramName: nameof(type));
        }

        catalogColumn.Id = id;
        catalogColumn.Name = name;
        catalogColumn.Type = type;
        catalogColumn.IsNullable = isNullable;

        if (defaultExpression is not null && string.IsNullOrWhiteSpace(value: defaultExpression))
        {
            throw new ArgumentException(message: "Default expression cannot be empty.", paramName: nameof(defaultExpression));
        }

        if (computedExpression is not null && string.IsNullOrWhiteSpace(value: computedExpression))
        {
            throw new ArgumentException(message: "Computed expression cannot be empty.", paramName: nameof(computedExpression));
        }

        if (computedExpression is not null && (defaultExpression is not null || identity is not null || isRowGuidCol))
        {
            throw new ArgumentException(message: "A computed column cannot also have DEFAULT, IDENTITY, or ROWGUIDCOL metadata.", paramName: nameof(computedExpression));
        }

        if (defaultExpression is not null && identity is not null)
        {
            throw new ArgumentException(message: "IDENTITY columns cannot have DEFAULT constraints.", paramName: nameof(defaultExpression));
        }

        if (defaultExpression is not null && type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
        {
            throw new ArgumentException(message: "rowversion/timestamp columns cannot have DEFAULT constraints.", paramName: nameof(defaultExpression));
        }

        if (isComputedPersisted && computedExpression is null)
        {
            throw new ArgumentException(message: "PERSISTED requires a computed expression.", paramName: nameof(isComputedPersisted));
        }

        if (identity is not null && type.Name is not (SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or SqlTypeName.BigInt or SqlTypeName.Decimal or SqlTypeName.Numeric))
        {
            throw new ArgumentException(message: "IDENTITY requires an exact numeric type.", paramName: nameof(identity));
        }

        if (identity is not null && type.Name is (SqlTypeName.Decimal or SqlTypeName.Numeric) && type.Scale != 0)
        {
            throw new ArgumentException(message: "IDENTITY decimal/numeric columns require scale zero.", paramName: nameof(identity));
        }

        if (identity is not null)
        {
            try
            {
                ValidateIdentityRange(type: type, value: identity.Seed);
                ValidateIdentityRange(type: type, value: identity.Increment);
            }
            catch (ArgumentException exception)
            {
                throw new ArgumentException(message: "IDENTITY seed or increment does not fit the declared type.", paramName: nameof(identity), innerException: exception);
            }
        }

        if (isRowGuidCol && (type.Name != SqlTypeName.UniqueIdentifier || IsUserDefined(type: type)))
        {
            throw new ArgumentException(message: "ROWGUIDCOL requires the native uniqueidentifier type.", paramName: nameof(isRowGuidCol));
        }

        if (type.Name == SqlTypeName.Vector && defaultExpression is not null)
        {
            throw new ArgumentException(message: "SQL Server vector columns do not support DEFAULT constraints.", paramName: nameof(defaultExpression));
        }

        if (isSparse && !isNullable)
        {
            throw new ArgumentException(message: "SPARSE columns must be nullable.", paramName: nameof(isSparse));
        }

        if (isSparse && (defaultExpression is not null || identity is not null || isRowGuidCol || computedExpression is not null || isFileStream || isColumnSet))
        {
            throw new ArgumentException(message: "SPARSE columns cannot have DEFAULT, IDENTITY, ROWGUIDCOL, computed, FILESTREAM, or COLUMN_SET metadata.", paramName: nameof(isSparse));
        }

        if (isSparse && type.Name is SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.RowVersion or SqlTypeName.Timestamp or SqlTypeName.Geometry or SqlTypeName.Geography || isSparse && IsUserDefined(type: type))
        {
            throw new ArgumentException(message: "The SQL type cannot be SPARSE.", paramName: nameof(isSparse));
        }

        if (isColumnSet && (type.Name != SqlTypeName.Xml || type.XmlSchemaCollection is not null || !isNullable || defaultExpression is not null || computedExpression is not null))
        {
            throw new ArgumentException(message: "A column set must be a nullable, untyped XML column without DEFAULT or computed metadata.", paramName: nameof(isColumnSet));
        }

        if (isFileStream && (type.Name != SqlTypeName.VarBinary || !type.IsMax || isSparse || computedExpression is not null))
        {
            throw new ArgumentException(message: "FILESTREAM requires a non-sparse varbinary(max) column.", paramName: nameof(isFileStream));
        }

        if (maskingFunction is not null && string.IsNullOrWhiteSpace(value: maskingFunction))
        {
            throw new ArgumentException(message: "Masking function cannot be empty.", paramName: nameof(maskingFunction));
        }

        if (maskingFunction is not null && (encryption is not null || isFileStream || isColumnSet || computedExpression is not null))
        {
            throw new ArgumentException(message: "Dynamic data masking is incompatible with encryption, FILESTREAM, COLUMN_SET, and computed columns.", paramName: nameof(maskingFunction));
        }

        if (encryption is not null && (computedExpression is not null || isFileStream || type.Name is SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.Xml or SqlTypeName.Json or SqlTypeName.RowVersion or SqlTypeName.Timestamp or SqlTypeName.SqlVariant or SqlTypeName.HierarchyId or SqlTypeName.Geometry or SqlTypeName.Geography or SqlTypeName.Vector || type.UserTypeKind == SqlUserTypeKind.Clr))
        {
            throw new ArgumentException(message: "The SQL type or column facet is incompatible with Always Encrypted.", paramName: nameof(encryption));
        }

        if (encryption?.EncryptionType == CatalogEncryptionType.Deterministic && type.Name is (SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Text or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText or SqlTypeName.Xml or SqlTypeName.Json) && (type.Collation is null || !collations.CheckSqlCollationIsBinary2(sqlCollation: collations.ParseSqlCollation(name: type.Collation))))
        {
            throw new ArgumentException(message: "Deterministically encrypted character columns require a BIN2 collation.", paramName: nameof(encryption));
        }

        if (!Enum.IsDefined(value: generatedAlways))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(generatedAlways));
        }

        if ((generatedAlways is CatalogGeneratedAlwaysKind.RowStart or CatalogGeneratedAlwaysKind.RowEnd && type.Name != SqlTypeName.DateTime2) || (generatedAlways is CatalogGeneratedAlwaysKind.TransactionIdStart or CatalogGeneratedAlwaysKind.TransactionIdEnd or CatalogGeneratedAlwaysKind.SequenceNumberStart or CatalogGeneratedAlwaysKind.SequenceNumberEnd && type.Name != SqlTypeName.BigInt) || (generatedAlways is CatalogGeneratedAlwaysKind.GraphIdentity or CatalogGeneratedAlwaysKind.GraphFromNode or CatalogGeneratedAlwaysKind.GraphToNode && type != new SqlType
        {
            Name = SqlTypeName.Binary,
            Length = 50
        }

        ))
        {
            throw new ArgumentException(message: "GENERATED ALWAYS has an incompatible SQL type.", paramName: nameof(generatedAlways));
        }

        if (isHidden && generatedAlways == CatalogGeneratedAlwaysKind.None)
        {
            throw new ArgumentException(message: "HIDDEN requires a generated-always column.", paramName: nameof(isHidden));
        }

        catalogColumn.DefaultExpression = defaultExpression;
        catalogColumn.Identity = identity;
        catalogColumn.IsRowGuidCol = isRowGuidCol;
        catalogColumn.ComputedExpression = computedExpression;
        catalogColumn.IsComputedPersisted = isComputedPersisted;
        catalogColumn.IsSparse = isSparse;
        catalogColumn.IsColumnSet = isColumnSet;
        catalogColumn.IsFileStream = isFileStream;
        catalogColumn.MaskingFunction = maskingFunction;
        catalogColumn.Encryption = encryption;
        catalogColumn.GeneratedAlways = generatedAlways;
        catalogColumn.IsHidden = isHidden;
        catalogColumn.IsComputed = catalogColumn.ComputedExpression is not null;
    }

    private static void ValidateCatalogIdentity(CatalogIdentity catalogIdentity)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIdentity);
        var seed = catalogIdentity.Seed;
        var increment = catalogIdentity.Increment;
        var isNotForReplication = catalogIdentity.IsNotForReplication;

        if (increment.IsZero)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(increment));
        }
    }

    private static CatalogIdentity PrepareCatalogIdentity(CatalogIdentity catalogIdentity)
    {
        ValidateCatalogIdentity(catalogIdentity: catalogIdentity);
        return catalogIdentity;
    }

    private static void ValidateCatalogColumnEncryption(CatalogColumnEncryption catalogColumnEncryption)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogColumnEncryption);
        var keyName = catalogColumnEncryption.KeyName;
        var encryptionType = catalogColumnEncryption.EncryptionType;
        var algorithm = catalogColumnEncryption.Algorithm;
        ValidateIdentifier(value: keyName, parameterName: nameof(keyName));

        if (!Enum.IsDefined(value: encryptionType))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(encryptionType));
        }

        if (!StringComparer.Ordinal.Equals(x: algorithm, y: "AEAD_AES_256_CBC_HMAC_SHA_256"))
        {
            throw new ArgumentException(message: "Unsupported Always Encrypted algorithm.", paramName: nameof(algorithm));
        }
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value: value))
        {
            throw new ArgumentException(message: "SQL identifier cannot be empty.", paramName: parameterName);
        }

        if (value.Length > 128)
        {
            throw new ArgumentOutOfRangeException(paramName: parameterName, message: "SQL identifiers cannot exceed 128 characters.");
        }
    }

    private static CatalogColumnEncryption PrepareCatalogColumnEncryption(CatalogColumnEncryption catalogColumnEncryption)
    {
        ValidateCatalogColumnEncryption(catalogColumnEncryption: catalogColumnEncryption);
        return catalogColumnEncryption;
    }

    private static bool CanBeColumn(SqlType type) =>
        type.Name is not (SqlTypeName.Cursor or SqlTypeName.Table);

    private static bool IsUserDefined(SqlType type) =>
        type.UserTypeKind != SqlUserTypeKind.None;

    private static void ValidateIdentityRange(SqlType type, BigInteger value)
    {
        var maximum = type.Name switch
        {
            SqlTypeName.TinyInt => new BigInteger(value: byte.MaxValue),
            SqlTypeName.SmallInt => new BigInteger(value: short.MaxValue),
            SqlTypeName.Int => new BigInteger(value: int.MaxValue),
            SqlTypeName.BigInt => new BigInteger(value: long.MaxValue),
            _ => BigInteger.Pow(value: new BigInteger(value: 10), exponent: type.Precision!.Value) - BigInteger.One
        };

        var minimum = type.Name switch
        {
            SqlTypeName.TinyInt => BigInteger.Zero,
            SqlTypeName.SmallInt => new BigInteger(value: short.MinValue),
            SqlTypeName.Int => new BigInteger(value: int.MinValue),
            SqlTypeName.BigInt => new BigInteger(value: long.MinValue),
            _ => -maximum
        };

        if (value < minimum || value > maximum)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(value));
        }
    }
}