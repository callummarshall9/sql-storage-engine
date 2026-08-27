using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using System.Numerics;
using System.Text.Json;
using System.Xml;
using System.Xml.XPath;

namespace sql_storage_engine.Catalog;

/// <summary>Defines whether an indexed column is ordered in ascending or descending order.</summary>
public enum SortDirection : byte
{
    Ascending = 1,
    Descending = 2
}

/// <summary>Defines where SQL NULL values sort relative to non-NULL values.</summary>
public enum NullSortOrder : byte
{
    First = 1,
    Last = 2
}

public enum CatalogIndexStorageKind : byte
{
    Clustered = 1,
    NonClustered = 2,
    Hash = 3
}

public enum CatalogIndexMethod : byte { BTree = 1, Json = 2, Spatial = 3, Vector = 4, Xml = 5, FullText = 6 }
public enum SqlVectorDistanceMetric : byte { Cosine = 1, Euclidean = 2, DotProduct = 3 }
public enum CatalogXmlIndexKind : byte { Primary = 1, Path = 2, Value = 3, Property = 4, Selective = 5 }

/// <summary>A database/schema-qualified ordinary table name.</summary>
public sealed record CatalogTableName
{
    public CatalogTableName(string databaseName, string schemaName, string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databaseName);
        ArgumentException.ThrowIfNullOrWhiteSpace(schemaName);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        DatabaseName = databaseName;
        SchemaName = schemaName;
        Name = name;
    }

    public string DatabaseName { get; }
    public string SchemaName { get; }
    public string Name { get; }
    public string QualifiedName => $"[{DatabaseName}].[{SchemaName}].[{Name}]";

    public override string ToString() => QualifiedName;
}

public sealed record CatalogBTreeIndexOptions
{
    public CatalogBTreeIndexOptions(CatalogIndexStorageKind storageKind = CatalogIndexStorageKind.NonClustered,
        bool isPrimaryKey = false, IEnumerable<ColumnId>? includedColumns = null, bool ignoreDuplicateKey = false)
    { StorageKind = storageKind; IsPrimaryKey = isPrimaryKey; IncludedColumns = includedColumns?.ToArray() ?? []; IgnoreDuplicateKey = ignoreDuplicateKey; }
    public CatalogIndexStorageKind StorageKind { get; }
    public bool IsPrimaryKey { get; }
    public IReadOnlyList<ColumnId> IncludedColumns { get; }
    public bool IgnoreDuplicateKey { get; }
}

public sealed record CatalogSpecializedIndexOptions
{
    private CatalogSpecializedIndexOptions(CatalogIndexMethod method, IReadOnlyList<string>? jsonPaths = null,
        SqlVectorDistanceMetric? vectorMetric = null, CatalogXmlIndexKind? xmlIndexKind = null,
        IReadOnlyDictionary<string, string>? xmlNamespaces = null, int? spatialSrid = null,
        CatalogFullTextIndexOptions? fullTextOptions = null)
    {
        Method = method;
        JsonPaths = jsonPaths ?? Array.Empty<string>();
        VectorMetric = vectorMetric;
        XmlIndexKind = xmlIndexKind;
        XmlNamespaces = xmlNamespaces ?? new Dictionary<string, string>(StringComparer.Ordinal);
        SpatialSrid = spatialSrid;
        FullTextOptions = fullTextOptions;
    }
    public CatalogIndexMethod Method { get; }
    public IReadOnlyList<string> JsonPaths { get; }
    public SqlVectorDistanceMetric? VectorMetric { get; }
    public CatalogXmlIndexKind? XmlIndexKind { get; }
    public IReadOnlyDictionary<string, string> XmlNamespaces { get; }
    /// <summary>
    /// Gets the exact SRID admitted by a spatial index, or <see langword="null"/> when the index admits every SRID.
    /// </summary>
    public int? SpatialSrid { get; }
    /// <summary>Gets the complete linguistic and resource identity for a full-text index.</summary>
    public CatalogFullTextIndexOptions? FullTextOptions { get; }
    public static CatalogSpecializedIndexOptions Json(params string[] paths)
    {
        ArgumentNullException.ThrowIfNull(paths);
        if (paths.Length == 0) paths = ["$"];
        if (paths.Any(path => string.IsNullOrWhiteSpace(path) || !path.StartsWith('$')))
            throw new ArgumentException("A JSON index requires one or more absolute JSON paths.", nameof(paths));
        foreach (var path in paths) ValidateJsonPath(path);
        CatalogTable.ValidateUnique(paths, "JSON paths", nameof(paths), StringComparer.Ordinal);
        for (var left = 0; left < paths.Length; left++) for (var right = left + 1; right < paths.Length; right++)
            if (PathsOverlap(paths[left], paths[right]))
                throw new ArgumentException("JSON index paths cannot overlap.", nameof(paths));
        return new(CatalogIndexMethod.Json, Array.AsReadOnly(paths.ToArray()));
    }
    private static bool PathsOverlap(string left, string right) => IsAncestor(left, right) || IsAncestor(right, left);
    private static bool IsAncestor(string prefix, string value) => value.Equals(prefix, StringComparison.Ordinal) ||
        value.StartsWith(prefix, StringComparison.Ordinal) && value.Length > prefix.Length && value[prefix.Length] is '.' or '[';
    private static void ValidateJsonPath(string path)
    {
        var position = 1; var depth = 0;
        while (position < path.Length)
        {
            if (++depth > 128) throw new ArgumentException($"JSON path '{path}' exceeds 128 levels.", nameof(path));
            if (path[position] == '.')
            {
                position++;
                if (position >= path.Length) throw new ArgumentException($"JSON path '{path}' ends with an empty property.", nameof(path));
                if (path[position] == '*') { position++; continue; }
                if (path[position] == '"')
                {
                    var literalStart = position++;
                    while (position < path.Length && path[position] != '"')
                    {
                        if (path[position++] == '\\')
                        { if (position >= path.Length) throw new ArgumentException($"JSON path '{path}' has an incomplete escape.", nameof(path)); position++; }
                    }
                    if (position >= path.Length)
                        throw new ArgumentException($"JSON path '{path}' has an invalid quoted property.", nameof(path));
                    try { _ = JsonSerializer.Deserialize<string>(path[literalStart..++position]); }
                    catch (JsonException exception)
                    { throw new ArgumentException($"JSON path '{path}' has an invalid quoted property.", nameof(path), exception); }
                    continue;
                }
                var start = position;
                while (position < path.Length && path[position] is not ('.' or '[')) position++;
                if (start == position || path[start..position].Any(char.IsWhiteSpace))
                    throw new ArgumentException($"JSON path '{path}' has an invalid property.", nameof(path));
                continue;
            }
            if (path[position] == '[')
            {
                var end = path.IndexOf(']', position + 1);
                if (end < 0) throw new ArgumentException($"JSON path '{path}' has an unclosed array selector.", nameof(path));
                var selector = path[(position + 1)..end].Trim();
                var valid = selector == "*" || selector.Equals("last", StringComparison.OrdinalIgnoreCase) ||
                    int.TryParse(selector, out var item) && item >= 0;
                if (!valid)
                {
                    var range = selector.Split(" to ", StringSplitOptions.TrimEntries);
                    valid = range.Length == 2 && int.TryParse(range[0], out var start) &&
                        int.TryParse(range[1], out var finish) && start >= 0 && finish >= start;
                }
                if (!valid && selector.Contains(','))
                {
                    var items = selector.Split(',', StringSplitOptions.TrimEntries);
                    valid = items.Length > 1 && items.All(item => item.Equals("last", StringComparison.OrdinalIgnoreCase) ||
                        int.TryParse(item, out var index) && index >= 0) &&
                        items.Distinct(StringComparer.OrdinalIgnoreCase).Count() == items.Length;
                }
                if (!valid) throw new ArgumentException($"JSON path '{path}' has an invalid array selector.", nameof(path));
                position = end + 1;
                continue;
            }
            throw new ArgumentException($"JSON path '{path}' is malformed.", nameof(path));
        }
    }
    public static CatalogSpecializedIndexOptions Spatial() => new(CatalogIndexMethod.Spatial);
    public static CatalogSpecializedIndexOptions Spatial(int srid) =>
        new(CatalogIndexMethod.Spatial, spatialSrid: srid);
    public static CatalogSpecializedIndexOptions Vector(SqlVectorDistanceMetric metric = SqlVectorDistanceMetric.Cosine)
    { if (!Enum.IsDefined(metric)) throw new ArgumentOutOfRangeException(nameof(metric)); return new(CatalogIndexMethod.Vector, vectorMetric: metric); }
    public static CatalogSpecializedIndexOptions FullText(CatalogFullTextIndexOptions options)
    { ArgumentNullException.ThrowIfNull(options); return new(CatalogIndexMethod.FullText, fullTextOptions: options); }
    public static CatalogSpecializedIndexOptions Xml(CatalogXmlIndexKind kind = CatalogXmlIndexKind.Primary,
        params string[] paths) => XmlCore(kind, null, paths);

    public static CatalogSpecializedIndexOptions Xml(CatalogXmlIndexKind kind,
        IReadOnlyDictionary<string, string> namespaces, params string[] paths)
        => XmlCore(kind, namespaces ?? throw new ArgumentNullException(nameof(namespaces)), paths);

    private static CatalogSpecializedIndexOptions XmlCore(CatalogXmlIndexKind kind,
        IReadOnlyDictionary<string, string>? namespaces, string[] paths)
    {
        if (!Enum.IsDefined(kind)) throw new ArgumentOutOfRangeException(nameof(kind)); ArgumentNullException.ThrowIfNull(paths);
        if (kind == CatalogXmlIndexKind.Selective && paths.Length == 0)
            throw new ArgumentException("A selective XML index requires at least one path.", nameof(paths));
        if (kind == CatalogXmlIndexKind.Selective && paths.Length > 1_024)
            throw new ArgumentException("A selective XML index cannot promote more than 1,024 paths.", nameof(paths));
        if (paths.Any(string.IsNullOrWhiteSpace)) throw new ArgumentException("XML index paths cannot be empty.", nameof(paths));
        if (paths.Any(path => !path.StartsWith('/'))) throw new ArgumentException("XML index paths must be absolute.", nameof(paths));
        CatalogTable.ValidateUnique(paths, "XML paths", nameof(paths), StringComparer.Ordinal);
        var bindings = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var binding in namespaces ?? new Dictionary<string, string>())
        {
            if (string.IsNullOrWhiteSpace(binding.Key) || binding.Key.Equals("xmlns", StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("XML namespace prefixes must be non-empty NCNames other than xmlns.", nameof(namespaces));
            try { XmlConvert.VerifyNCName(binding.Key); }
            catch (XmlException exception) { throw new ArgumentException($"XML namespace prefix '{binding.Key}' is invalid.", nameof(namespaces), exception); }
            if (string.IsNullOrEmpty(binding.Value))
                throw new ArgumentException("XML namespace URIs cannot be empty.", nameof(namespaces));
            bindings.Add(binding.Key, binding.Value);
        }
        var nameTable = new NameTable();
        var namespaceManager = new XmlNamespaceManager(nameTable);
        foreach (var binding in bindings) namespaceManager.AddNamespace(binding.Key, binding.Value);
        foreach (var path in paths.Where(path => path != "/"))
            try { var expression = XPathExpression.Compile(path); expression.SetContext(namespaceManager); }
            catch (XPathException exception) { throw new ArgumentException($"XML index path '{path}' is invalid.", nameof(paths), exception); }
        return new(CatalogIndexMethod.Xml, paths.Length == 0 ? ["/"] : Array.AsReadOnly(paths.ToArray()),
            xmlIndexKind: kind, xmlNamespaces: new Dictionary<string, string>(bindings, StringComparer.Ordinal));
    }
}

public enum CatalogAssemblyPermissionSet : byte
{
    Safe = 1,
    ExternalAccess = 2,
    Unsafe = 3
}

public enum CatalogGeneratedAlwaysKind : byte
{ None = 0, RowStart = 1, RowEnd = 2, TransactionIdStart = 3, TransactionIdEnd = 4, SequenceNumberStart = 5, SequenceNumberEnd = 6 }
public enum CatalogEncryptionType : byte { Deterministic = 1, Randomized = 2 }

/// <summary>
/// Identifies the durable history table and the two UTC datetime2 period columns for a system-versioned table.
/// </summary>
public sealed record CatalogSystemVersioning
{
    public CatalogSystemVersioning(TableId historyTableId, ColumnId periodStartColumnId, ColumnId periodEndColumnId)
    {
        if (periodStartColumnId == periodEndColumnId)
            throw new ArgumentException("The temporal period start and end columns must be different.");
        HistoryTableId = historyTableId;
        PeriodStartColumnId = periodStartColumnId;
        PeriodEndColumnId = periodEndColumnId;
    }

    public TableId HistoryTableId { get; }
    public ColumnId PeriodStartColumnId { get; }
    public ColumnId PeriodEndColumnId { get; }
}

public sealed record CatalogColumnEncryption
{
    public CatalogColumnEncryption(string keyName, CatalogEncryptionType encryptionType,
        string algorithm = "AEAD_AES_256_CBC_HMAC_SHA_256")
    {
        KeyName = SqlXmlSchemaCollection.ValidateIdentifier(keyName, nameof(keyName));
        if (!Enum.IsDefined(encryptionType)) throw new ArgumentOutOfRangeException(nameof(encryptionType));
        if (!StringComparer.Ordinal.Equals(algorithm, "AEAD_AES_256_CBC_HMAC_SHA_256"))
            throw new ArgumentException("Unsupported Always Encrypted algorithm.", nameof(algorithm));
        EncryptionType = encryptionType; Algorithm = algorithm;
    }
    public string KeyName { get; }
    public CatalogEncryptionType EncryptionType { get; }
    public string Algorithm { get; }
}

public sealed record CatalogIdentity
{
    public CatalogIdentity(long seed = 1, long increment = 1) : this(new BigInteger(seed), new BigInteger(increment)) { }
    public CatalogIdentity(BigInteger seed, BigInteger increment)
    {
        if (increment.IsZero) throw new ArgumentOutOfRangeException(nameof(increment));
        Seed = seed;
        Increment = increment;
    }
    public BigInteger Seed { get; }
    public BigInteger Increment { get; }
}

public sealed record CatalogCheckConstraint
{
    public CatalogCheckConstraint(string name, string expression)
    {
        Name = SqlXmlSchemaCollection.ValidateIdentifier(name, nameof(name));
        if (string.IsNullOrWhiteSpace(expression)) throw new ArgumentException("Check expression cannot be empty.", nameof(expression));
        Expression = expression;
    }
    public string Name { get; }
    public string Expression { get; }
}

/// <summary>A database-scoped SQL CLR assembly identity and optional deployable image.</summary>
public sealed class CatalogAssembly : IEquatable<CatalogAssembly>
{
    private readonly byte[] _image;
    public CatalogAssembly(string name, ReadOnlySpan<byte> image = default, string? version = null,
        string? culture = null, string? publicKeyToken = null,
        CatalogAssemblyPermissionSet permissionSet = CatalogAssemblyPermissionSet.Safe)
    {
        Name = SqlXmlSchemaCollection.ValidateIdentifier(name, nameof(name));
        if (!Enum.IsDefined(permissionSet)) throw new ArgumentOutOfRangeException(nameof(permissionSet));
        if (version is not null && !System.Version.TryParse(version, out _))
            throw new ArgumentException("Assembly version is invalid.", nameof(version));
        if (publicKeyToken is not null && (publicKeyToken.Length != 16 || !publicKeyToken.All(Uri.IsHexDigit)))
            throw new ArgumentException("Public key token must contain 16 hexadecimal characters.", nameof(publicKeyToken));
        _image = image.ToArray();
        Version = version;
        Culture = culture;
        PublicKeyToken = publicKeyToken?.ToLowerInvariant();
        PermissionSet = permissionSet;
    }
    public string Name { get; }
    public ReadOnlyMemory<byte> Image => _image;
    public string? Version { get; }
    public string? Culture { get; }
    public string? PublicKeyToken { get; }
    public CatalogAssemblyPermissionSet PermissionSet { get; }
    public bool Equals(CatalogAssembly? other) => other is not null && Name == other.Name && Version == other.Version &&
        Culture == other.Culture && PublicKeyToken == other.PublicKeyToken && PermissionSet == other.PermissionSet &&
        _image.AsSpan().SequenceEqual(other._image);
    public override bool Equals(object? obj) => Equals(obj as CatalogAssembly);
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(Name); hash.Add(Version); hash.Add(Culture); hash.Add(PublicKeyToken); hash.Add(PermissionSet);
        hash.AddBytes(System.Security.Cryptography.SHA256.HashData(_image));
        return hash.ToHashCode();
    }
}

/// <summary>A stable catalog record for one table column.</summary>
public sealed record CatalogColumn
{
    public CatalogColumn(ColumnId id, string name, SqlType type, bool isNullable,
        string? defaultExpression = null, CatalogIdentity? identity = null, bool isRowGuidCol = false,
        string? computedExpression = null, bool isComputedPersisted = false, bool isSparse = false,
        bool isColumnSet = false, bool isFileStream = false, string? maskingFunction = null,
        CatalogColumnEncryption? encryption = null,
        CatalogGeneratedAlwaysKind generatedAlways = CatalogGeneratedAlwaysKind.None, bool isHidden = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Column name cannot be empty.", nameof(name));
        ArgumentNullException.ThrowIfNull(type);
        if (!type.CanBeColumn) throw new ArgumentException($"{type.Name} cannot be used as a table column.", nameof(type));
        Id = id;
        Name = name;
        Type = type;
        IsNullable = isNullable;
        if (defaultExpression is not null && string.IsNullOrWhiteSpace(defaultExpression))
            throw new ArgumentException("Default expression cannot be empty.", nameof(defaultExpression));
        if (computedExpression is not null && string.IsNullOrWhiteSpace(computedExpression))
            throw new ArgumentException("Computed expression cannot be empty.", nameof(computedExpression));
        if (computedExpression is not null && (defaultExpression is not null || identity is not null || isRowGuidCol))
            throw new ArgumentException("A computed column cannot also have DEFAULT, IDENTITY, or ROWGUIDCOL metadata.", nameof(computedExpression));
        if (defaultExpression is not null && identity is not null)
            throw new ArgumentException("IDENTITY columns cannot have DEFAULT constraints.", nameof(defaultExpression));
        if (defaultExpression is not null && type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp)
            throw new ArgumentException("rowversion/timestamp columns cannot have DEFAULT constraints.", nameof(defaultExpression));
        if (isComputedPersisted && computedExpression is null)
            throw new ArgumentException("PERSISTED requires a computed expression.", nameof(isComputedPersisted));
        if (identity is not null && type.Name is not (SqlTypeName.TinyInt or SqlTypeName.SmallInt or SqlTypeName.Int or
                SqlTypeName.BigInt or SqlTypeName.Decimal or SqlTypeName.Numeric))
            throw new ArgumentException("IDENTITY requires an exact numeric type.", nameof(identity));
        if (identity is not null && type.Name is (SqlTypeName.Decimal or SqlTypeName.Numeric) && type.Scale != 0)
            throw new ArgumentException("IDENTITY decimal/numeric columns require scale zero.", nameof(identity));
        if (identity is not null)
        {
            try
            {
                _ = SqlConversion.ConvertTo(type, SqlValue.Decimal(new SqlDecimal(identity.Seed, 0)));
                _ = SqlConversion.ConvertTo(type, SqlValue.Decimal(new SqlDecimal(identity.Increment, 0)));
            }
            catch (ArgumentException exception)
            { throw new ArgumentException("IDENTITY seed or increment does not fit the declared type.", nameof(identity), exception); }
        }
        if (isRowGuidCol && (type.Name != SqlTypeName.UniqueIdentifier || type.IsUserDefined))
            throw new ArgumentException("ROWGUIDCOL requires the native uniqueidentifier type.", nameof(isRowGuidCol));
        if (type.Name == SqlTypeName.Vector && defaultExpression is not null)
            throw new ArgumentException("SQL Server vector columns do not support DEFAULT constraints.", nameof(defaultExpression));
        if (isSparse && !isNullable) throw new ArgumentException("SPARSE columns must be nullable.", nameof(isSparse));
        if (isSparse && (defaultExpression is not null || identity is not null || isRowGuidCol ||
                         computedExpression is not null || isFileStream || isColumnSet))
            throw new ArgumentException("SPARSE columns cannot have DEFAULT, IDENTITY, ROWGUIDCOL, computed, FILESTREAM, or COLUMN_SET metadata.", nameof(isSparse));
        if (isSparse && type.Name is SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.RowVersion or
                SqlTypeName.Timestamp or SqlTypeName.Geometry or SqlTypeName.Geography || isSparse && type.IsUserDefined)
            throw new ArgumentException("The SQL type cannot be SPARSE.", nameof(isSparse));
        if (isColumnSet && (type.Name != SqlTypeName.Xml || type.XmlSchemaCollection is not null || !isNullable ||
                            defaultExpression is not null || computedExpression is not null))
            throw new ArgumentException("A column set must be a nullable, untyped XML column without DEFAULT or computed metadata.", nameof(isColumnSet));
        if (isFileStream && (type.Name != SqlTypeName.VarBinary || !type.IsMax || isSparse || computedExpression is not null))
            throw new ArgumentException("FILESTREAM requires a non-sparse varbinary(max) column.", nameof(isFileStream));
        if (maskingFunction is not null && string.IsNullOrWhiteSpace(maskingFunction))
            throw new ArgumentException("Masking function cannot be empty.", nameof(maskingFunction));
        if (maskingFunction is not null && (encryption is not null || isFileStream || isColumnSet || computedExpression is not null))
            throw new ArgumentException("Dynamic data masking is incompatible with encryption, FILESTREAM, COLUMN_SET, and computed columns.", nameof(maskingFunction));
        if (maskingFunction is not null) SqlDataMasking.Validate(type, maskingFunction);
        if (encryption is not null && (computedExpression is not null || isFileStream || type.Name is SqlTypeName.Text or
                SqlTypeName.NText or SqlTypeName.Image or SqlTypeName.Xml or SqlTypeName.Json or SqlTypeName.RowVersion or
                SqlTypeName.Timestamp or SqlTypeName.SqlVariant or SqlTypeName.HierarchyId or SqlTypeName.Geometry or
                SqlTypeName.Geography or SqlTypeName.Vector || type.UserTypeKind == SqlUserTypeKind.Clr))
            throw new ArgumentException("The SQL type or column facet is incompatible with Always Encrypted.", nameof(encryption));
        if (encryption?.EncryptionType == CatalogEncryptionType.Deterministic && type.StorageFamily == SqlStorageFamily.Text &&
            !(type.CollationMetadata?.IsBinary2 ?? false))
            throw new ArgumentException("Deterministically encrypted character columns require a BIN2 collation.", nameof(encryption));
        if (!Enum.IsDefined(generatedAlways)) throw new ArgumentOutOfRangeException(nameof(generatedAlways));
        if (generatedAlways is CatalogGeneratedAlwaysKind.RowStart or CatalogGeneratedAlwaysKind.RowEnd && type.Name != SqlTypeName.DateTime2 ||
            generatedAlways is CatalogGeneratedAlwaysKind.TransactionIdStart or CatalogGeneratedAlwaysKind.TransactionIdEnd or
                CatalogGeneratedAlwaysKind.SequenceNumberStart or CatalogGeneratedAlwaysKind.SequenceNumberEnd && type.Name != SqlTypeName.BigInt)
            throw new ArgumentException("GENERATED ALWAYS has an incompatible SQL type.", nameof(generatedAlways));
        if (isHidden && generatedAlways == CatalogGeneratedAlwaysKind.None)
            throw new ArgumentException("HIDDEN requires a generated-always column.", nameof(isHidden));
        DefaultExpression = defaultExpression;
        Identity = identity;
        IsRowGuidCol = isRowGuidCol;
        ComputedExpression = computedExpression;
        IsComputedPersisted = isComputedPersisted;
        IsSparse = isSparse; IsColumnSet = isColumnSet; IsFileStream = isFileStream;
        MaskingFunction = maskingFunction; Encryption = encryption; GeneratedAlways = generatedAlways; IsHidden = isHidden;
    }

    public ColumnId Id { get; }
    public string Name { get; }
    public SqlType Type { get; }
    public bool IsNullable { get; }
    public string? DefaultExpression { get; }
    public CatalogIdentity? Identity { get; }
    public bool IsRowGuidCol { get; }
    public string? ComputedExpression { get; }
    public bool IsComputedPersisted { get; }
    public bool IsComputed => ComputedExpression is not null;
    public bool IsSparse { get; }
    public bool IsColumnSet { get; }
    public bool IsFileStream { get; }
    public string? MaskingFunction { get; }
    public CatalogColumnEncryption? Encryption { get; }
    public CatalogGeneratedAlwaysKind GeneratedAlways { get; }
    public bool IsHidden { get; }
}

/// <summary>A stable catalog record for a table and its heap entry point.</summary>
public sealed record CatalogTable
{
    private readonly CatalogColumn[] _columns;
    private readonly CatalogCheckConstraint[] _checkConstraints;

    public CatalogTable(TableId id, string name, ulong schemaVersion, PageId firstHeapPageId,
        IEnumerable<CatalogColumn> columns, IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        BigInteger? nextIdentityValue = null, string databaseName = "default", string schemaName = "dbo",
        CatalogSystemVersioning? systemVersioning = null)
    {
        var qualifiedName = new CatalogTableName(databaseName, schemaName, name);
        if (schemaVersion == 0) throw new ArgumentOutOfRangeException(nameof(schemaVersion));
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToArray();
        _checkConstraints = checkConstraints?.ToArray() ?? [];
        if (_columns.Length == 0) throw new ArgumentException("A table must define at least one column.", nameof(columns));
        if (_columns.Length > 30_000)
            throw new ArgumentException("A SQL Server table cannot contain more than 30,000 columns.", nameof(columns));
        if (_columns.Length > 1_024 && (!_columns.Any(column => column.IsColumnSet) ||
                                        _columns.Count(column => !column.IsSparse || column.IsComputed) > 1_024))
            throw new ArgumentException("Tables wider than 1,024 columns require a column set and may have at most 1,024 non-sparse or computed columns.", nameof(columns));
        ValidateUnique(_columns.Select(column => column.Id), "Column IDs", nameof(columns));
        ValidateUnique(_columns.Select(column => column.Name), "Column names", nameof(columns), StringComparer.Ordinal);
        ValidateUnique(_checkConstraints.Select(check => check.Name), "Check-constraint names", nameof(checkConstraints), StringComparer.Ordinal);
        if (_columns.Count(column => column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp) > 1)
            throw new ArgumentException("A table can contain only one rowversion column.", nameof(columns));
        if (_columns.Count(column => column.Identity is not null) > 1)
            throw new ArgumentException("A table can contain only one identity column.", nameof(columns));
        if (_columns.Count(column => column.IsRowGuidCol) > 1)
            throw new ArgumentException("A table can contain only one ROWGUIDCOL column.", nameof(columns));
        if (_columns.Count(column => column.IsColumnSet) > 1)
            throw new ArgumentException("A table can contain only one XML column set.", nameof(columns));
        var columnSet = _columns.SingleOrDefault(column => column.IsColumnSet);
        if (columnSet is not null && _columns.Any(column => column.IsSparse && column.MaskingFunction is not null))
            throw new ArgumentException("A SPARSE column participating in a column set cannot be dynamically masked.", nameof(columns));
        if (columnSet is not null && _columns.Any(column => column.ComputedExpression is { } expression &&
                ExpressionReferencesColumn(expression, columnSet.Name)))
            throw new ArgumentException("Computed columns cannot reference an XML column set.", nameof(columns));
        foreach (var masked in _columns.Where(column => column.MaskingFunction is not null))
            if (_columns.Any(column => column.ComputedExpression is { } expression &&
                                      ExpressionReferencesColumn(expression, masked.Name)))
                throw new ArgumentException("A dynamically masked column cannot have a computed-column dependency.", nameof(columns));
        if (_columns.Any(column => column.IsFileStream) && !_columns.Any(column => column.IsRowGuidCol && !column.IsNullable))
            throw new ArgumentException("FILESTREAM requires a non-null ROWGUIDCOL column.", nameof(columns));
        foreach (var vector in _columns.Where(column => column.Type.Name == SqlTypeName.Vector))
            if (_checkConstraints.Any(check => ExpressionReferencesColumn(check.Expression, vector.Name)))
                throw new ArgumentException("SQL Server vector columns do not support CHECK constraints.", nameof(checkConstraints));
        foreach (var column in _columns.Where(column => column.DefaultExpression is not null))
            if (_columns.Any(candidate => ExpressionReferencesColumn(column.DefaultExpression!, candidate.Name)))
                throw new ArgumentException($"DEFAULT expression for column '{column.Name}' cannot reference a table column.", nameof(columns));
        _ = GetComputedColumnOrder(_columns);
        var identity = _columns.SingleOrDefault(column => column.Identity is not null)?.Identity;
        if (identity is null && nextIdentityValue is not null)
            throw new ArgumentException("A table without IDENTITY cannot have identity allocation state.", nameof(nextIdentityValue));
        if (systemVersioning is not null)
        {
            var start = _columns.SingleOrDefault(column => column.Id == systemVersioning.PeriodStartColumnId)
                ?? throw new ArgumentException("The temporal period start column does not belong to the table.", nameof(systemVersioning));
            var end = _columns.SingleOrDefault(column => column.Id == systemVersioning.PeriodEndColumnId)
                ?? throw new ArgumentException("The temporal period end column does not belong to the table.", nameof(systemVersioning));
            if (start.GeneratedAlways != CatalogGeneratedAlwaysKind.RowStart ||
                end.GeneratedAlways != CatalogGeneratedAlwaysKind.RowEnd ||
                start.Type.Name != SqlTypeName.DateTime2 || end.Type.Name != SqlTypeName.DateTime2 || start.Type != end.Type ||
                start.IsNullable || end.IsNullable)
                throw new ArgumentException(
                    "A temporal period requires non-null datetime2 columns generated always as ROW START and ROW END.",
                    nameof(systemVersioning));
        }
        Id = id;
        Name = qualifiedName.Name;
        DatabaseName = qualifiedName.DatabaseName;
        SchemaName = qualifiedName.SchemaName;
        SchemaVersion = schemaVersion;
        FirstHeapPageId = firstHeapPageId;
        NextIdentityValue = identity is null ? null : nextIdentityValue ?? identity.Seed;
        SystemVersioning = systemVersioning;
    }

    public TableId Id { get; }
    public string Name { get; }
    public string DatabaseName { get; }
    public string SchemaName { get; }
    public CatalogTableName QualifiedName => new(DatabaseName, SchemaName, Name);
    public ulong SchemaVersion { get; }
    public PageId FirstHeapPageId { get; }
    public IReadOnlyList<CatalogColumn> Columns => Array.AsReadOnly(_columns);
    public IReadOnlyList<CatalogCheckConstraint> CheckConstraints => Array.AsReadOnly(_checkConstraints);
    public BigInteger? NextIdentityValue { get; }
    public CatalogSystemVersioning? SystemVersioning { get; }

    internal static void ValidateUnique<T>(IEnumerable<T> values, string description, string parameterName,
        IEqualityComparer<T>? comparer = null)
    {
        var materialized = values.ToArray();
        if (materialized.Distinct(comparer).Count() != materialized.Length)
            throw new ArgumentException($"{description} must be unique within their scope.", parameterName);
    }

    internal static bool ExpressionReferencesColumn(string expression, string columnName)
    {
        ArgumentNullException.ThrowIfNull(expression); ArgumentNullException.ThrowIfNull(columnName);
        foreach (var identifier in ExpressionIdentifiers(expression))
            if (identifier.Equals(columnName, StringComparison.OrdinalIgnoreCase)) return true;
        return false;
    }

    internal static IReadOnlyList<int> GetComputedColumnOrder(IReadOnlyList<CatalogColumn> columns)
    {
        var computed = columns.Select((column, position) => (column, position))
            .Where(item => item.column.IsComputed).ToArray();
        if (computed.Length == 0) return [];
        var computedNames = computed.Select(item => item.column.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dependencies = computed.ToDictionary(item => item.position, item => computedNames
            .Where(name => ExpressionReferencesColumn(item.column.ComputedExpression!, name))
            .ToHashSet(StringComparer.OrdinalIgnoreCase));
        var completed = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var order = new List<int>(computed.Length);
        while (order.Count != computed.Length)
        {
            var progressed = false;
            foreach (var item in computed.Where(item => !order.Contains(item.position)))
            {
                if (!dependencies[item.position].IsSubsetOf(completed)) continue;
                order.Add(item.position); completed.Add(item.column.Name); progressed = true;
            }
            if (!progressed)
                throw new ArgumentException("Computed-column dependencies contain a cycle.", nameof(columns));
        }
        return order;
    }

    internal static CatalogColumn? ResolveDirectColumnReference(string expression,
        IReadOnlyList<CatalogColumn> columns)
    {
        var token = expression.Trim();
        if (token.Length >= 2 && token[0] == '[' && token[^1] == ']')
            token = token[1..^1].Replace("]]", "]", StringComparison.Ordinal);
        return columns.SingleOrDefault(column => column.Name.Equals(token, StringComparison.OrdinalIgnoreCase));
    }

    private static IEnumerable<string> ExpressionIdentifiers(string expression)
    {
        for (var position = 0; position < expression.Length;)
        {
            if (expression[position] == '\'')
            {
                position++;
                while (position < expression.Length)
                {
                    if (expression[position++] != '\'') continue;
                    if (position < expression.Length && expression[position] == '\'') { position++; continue; }
                    break;
                }
                continue;
            }
            if (expression[position] == '[')
            {
                var identifier = new System.Text.StringBuilder(); position++;
                while (position < expression.Length)
                {
                    if (expression[position] != ']') { identifier.Append(expression[position++]); continue; }
                    position++;
                    if (position < expression.Length && expression[position] == ']')
                    { identifier.Append(']'); position++; continue; }
                    break;
                }
                if (identifier.Length != 0) yield return identifier.ToString();
                continue;
            }
            if (!char.IsLetter(expression[position]) && expression[position] is not ('_' or '@'))
            { position++; continue; }
            var start = position++;
            while (position < expression.Length && (char.IsLetterOrDigit(expression[position]) ||
                   expression[position] is '_' or '@' or '$')) position++;
            var token = expression[start..position];
            var next = position; while (next < expression.Length && char.IsWhiteSpace(expression[next])) next++;
            if (next < expression.Length && expression[next] == '(' || token.Equals("CURRENT_TIMESTAMP", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("NULL", StringComparison.OrdinalIgnoreCase) || token.Equals("AND", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("OR", StringComparison.OrdinalIgnoreCase) || token.Equals("NOT", StringComparison.OrdinalIgnoreCase) ||
                token.Equals("IS", StringComparison.OrdinalIgnoreCase)) continue;
            yield return token;
        }
    }
}

/// <summary>Identifies one table column and its complete index ordering configuration.</summary>
public sealed record CatalogIndexedColumn
{
    public CatalogIndexedColumn(ColumnId columnId, SortDirection direction, NullSortOrder nullSortOrder,
        string? collation = null)
    {
        if (!Enum.IsDefined(direction)) throw new ArgumentOutOfRangeException(nameof(direction));
        if (!Enum.IsDefined(nullSortOrder)) throw new ArgumentOutOfRangeException(nameof(nullSortOrder));
        if (collation is not null && string.IsNullOrWhiteSpace(collation))
            throw new ArgumentException("Collation cannot be empty when specified.", nameof(collation));
        ColumnId = columnId;
        Direction = direction;
        NullSortOrder = nullSortOrder;
        Collation = collation;
    }

    public ColumnId ColumnId { get; }
    public SortDirection Direction { get; }
    public NullSortOrder NullSortOrder { get; }
    public string? Collation { get; }
}

/// <summary>A stable catalog record for a composite index.</summary>
public sealed record CatalogIndex
{
    private readonly CatalogIndexedColumn[] _columns;
    private readonly ColumnId[] _includedColumns;

    public CatalogIndex(IndexId id, string name, TableId tableId, PageId rootPageId, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CatalogSpecializedIndexOptions? specializedOptions = null,
        CatalogIndexStorageKind storageKind = CatalogIndexStorageKind.NonClustered, bool isPrimaryKey = false,
        IEnumerable<ColumnId>? includedColumns = null, bool ignoreDuplicateKey = false)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("Index name cannot be empty.", nameof(name));
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToArray();
        if (_columns.Length == 0) throw new ArgumentException("An index must define at least one column.", nameof(columns));
        CatalogTable.ValidateUnique(_columns.Select(column => column.ColumnId), "Indexed column IDs", nameof(columns));
        _includedColumns = includedColumns?.ToArray() ?? [];
        CatalogTable.ValidateUnique(_includedColumns, "Included column IDs", nameof(includedColumns));
        if (_includedColumns.Intersect(_columns.Select(column => column.ColumnId)).Any())
            throw new ArgumentException("An included column cannot also be a key column.", nameof(includedColumns));
        if (!Enum.IsDefined(storageKind) || storageKind == CatalogIndexStorageKind.Hash)
            throw new ArgumentOutOfRangeException(nameof(storageKind), "Disk tables support clustered or nonclustered indexes.");
        if (isPrimaryKey && !isUnique) throw new ArgumentException("A primary key must be unique.", nameof(isUnique));
        if (ignoreDuplicateKey && !isUnique)
            throw new ArgumentException("IGNORE_DUP_KEY applies only to unique indexes.", nameof(ignoreDuplicateKey));
        if (_includedColumns.Length != 0 && storageKind != CatalogIndexStorageKind.NonClustered)
            throw new ArgumentException("Included columns apply only to nonclustered indexes.", nameof(includedColumns));
        if (isPrimaryKey && _includedColumns.Length != 0)
            throw new ArgumentException("A primary-key constraint cannot declare included columns.", nameof(includedColumns));
        Id = id;
        Name = name;
        TableId = tableId;
        RootPageId = rootPageId;
        IsUnique = isUnique;
        SpecializedOptions = specializedOptions;
        StorageKind = storageKind; IsPrimaryKey = isPrimaryKey; IgnoreDuplicateKey = ignoreDuplicateKey;
        if (specializedOptions is not null && _columns.Length != 1)
            throw new ArgumentException("A specialized index requires exactly one source column.", nameof(columns));
        if (specializedOptions is not null && isUnique)
            throw new ArgumentException("Specialized indexes cannot enforce UNIQUE.", nameof(isUnique));
        if (specializedOptions is not null && (storageKind != CatalogIndexStorageKind.NonClustered || isPrimaryKey || _includedColumns.Length != 0))
            throw new ArgumentException("Specialized indexes cannot be clustered, primary, or define included columns.", nameof(specializedOptions));
    }

    public IndexId Id { get; }
    public string Name { get; }
    public TableId TableId { get; }
    public PageId RootPageId { get; }
    public bool IsUnique { get; }
    public IReadOnlyList<CatalogIndexedColumn> Columns => Array.AsReadOnly(_columns);
    public CatalogIndexMethod Method => SpecializedOptions?.Method ?? CatalogIndexMethod.BTree;
    public CatalogSpecializedIndexOptions? SpecializedOptions { get; }
    public CatalogIndexStorageKind StorageKind { get; }
    public bool IsPrimaryKey { get; }
    public IReadOnlyList<ColumnId> IncludedColumns => Array.AsReadOnly(_includedColumns);
    public bool IgnoreDuplicateKey { get; }
}

/// <summary>A database-scoped alias or CLR user-defined scalar type.</summary>
public sealed record CatalogScalarType
{
    public CatalogScalarType(SqlType definition)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (!definition.IsUserDefined)
            throw new ArgumentException("A scalar catalog type must be an alias or CLR user-defined type.", nameof(definition));
        Definition = definition;
    }

    public SqlType Definition { get; }
    public string SchemaName => Definition.UserTypeSchema!;
    public string Name => Definition.UserTypeName!;
    public string QualifiedName => Definition.QualifiedUserTypeName!;
}

/// <summary>A primary, unique, or secondary key declared by a user-defined table type.</summary>
public sealed record CatalogTableTypeIndex
{
    private readonly CatalogIndexedColumn[] _columns;
    private readonly ColumnId[] _includedColumns;

    public CatalogTableTypeIndex(string name, bool isPrimaryKey, bool isUnique,
        IEnumerable<CatalogIndexedColumn> columns, CatalogIndexStorageKind storageKind = CatalogIndexStorageKind.NonClustered,
        IEnumerable<ColumnId>? includedColumns = null, bool ignoreDuplicateKey = false, long? bucketCount = null)
    {
        name = SqlXmlSchemaCollection.ValidateIdentifier(name, nameof(name));
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToArray();
        if (_columns.Length == 0) throw new ArgumentException("A table-type index must define at least one column.", nameof(columns));
        CatalogTable.ValidateUnique(_columns.Select(column => column.ColumnId), "Indexed column IDs", nameof(columns));
        if (isPrimaryKey && !isUnique) throw new ArgumentException("A primary key must be unique.", nameof(isUnique));
        if (!Enum.IsDefined(storageKind)) throw new ArgumentOutOfRangeException(nameof(storageKind));
        _includedColumns = includedColumns?.ToArray() ?? [];
        CatalogTable.ValidateUnique(_includedColumns, "Included column IDs", nameof(includedColumns));
        if (_includedColumns.Intersect(_columns.Select(column => column.ColumnId)).Any())
            throw new ArgumentException("An included column cannot also be a key column.", nameof(includedColumns));
        if (ignoreDuplicateKey && !isUnique)
            throw new ArgumentException("IGNORE_DUP_KEY applies only to unique indexes.", nameof(ignoreDuplicateKey));
        if (_includedColumns.Length != 0 && storageKind != CatalogIndexStorageKind.NonClustered)
            throw new ArgumentException("Included columns apply only to nonclustered indexes.", nameof(includedColumns));
        if (isPrimaryKey && _includedColumns.Length != 0)
            throw new ArgumentException("A primary-key constraint cannot declare included columns.", nameof(includedColumns));
        if (storageKind == CatalogIndexStorageKind.Hash)
        {
            if (bucketCount is null or <= 0 or > 1_073_741_824)
                throw new ArgumentOutOfRangeException(nameof(bucketCount), "A hash index requires BUCKET_COUNT from 1 through 1,073,741,824.");
            if (includedColumns is not null && _includedColumns.Length != 0)
                throw new ArgumentException("Hash indexes cannot define included columns.", nameof(includedColumns));
        }
        else if (bucketCount is not null) throw new ArgumentException("BUCKET_COUNT applies only to hash indexes.", nameof(bucketCount));
        Name = name;
        IsPrimaryKey = isPrimaryKey;
        IsUnique = isUnique;
        StorageKind = storageKind;
        IgnoreDuplicateKey = ignoreDuplicateKey;
        BucketCount = bucketCount;
    }

    public string Name { get; }
    public bool IsPrimaryKey { get; }
    public bool IsUnique { get; }
    public IReadOnlyList<CatalogIndexedColumn> Columns => Array.AsReadOnly(_columns);
    public CatalogIndexStorageKind StorageKind { get; }
    public IReadOnlyList<ColumnId> IncludedColumns => Array.AsReadOnly(_includedColumns);
    public bool IgnoreDuplicateKey { get; }
    public long? BucketCount { get; }
}

/// <summary>A database-scoped user-defined table type used by table-valued parameters and variables.</summary>
public sealed record CatalogTableType
{
    private readonly CatalogColumn[] _columns;
    private readonly CatalogTableTypeIndex[] _indexes;
    private readonly CatalogCheckConstraint[] _checkConstraints;

    public CatalogTableType(string schemaName, string name, IEnumerable<CatalogColumn> columns,
        IEnumerable<CatalogTableTypeIndex>? indexes = null, IEnumerable<CatalogCheckConstraint>? checkConstraints = null,
        bool isMemoryOptimized = false)
    {
        SchemaName = SqlXmlSchemaCollection.ValidateIdentifier(schemaName, nameof(schemaName));
        Name = SqlXmlSchemaCollection.ValidateIdentifier(name, nameof(name));
        ArgumentNullException.ThrowIfNull(columns);
        _columns = columns.ToArray();
        _indexes = indexes?.ToArray() ?? [];
        _checkConstraints = checkConstraints?.ToArray() ?? [];
        if (_columns.Length == 0) throw new ArgumentException("A table type must define at least one column.", nameof(columns));
        if (_columns.Length > 1_024)
            throw new ArgumentException("A user-defined table type cannot contain more than 1,024 columns.", nameof(columns));
        CatalogTable.ValidateUnique(_columns.Select(column => column.Id), "Column IDs", nameof(columns));
        CatalogTable.ValidateUnique(_columns.Select(column => column.Name), "Column names", nameof(columns), StringComparer.Ordinal);
        CatalogTable.ValidateUnique(_indexes.Select(index => index.Name), "Table-type index names", nameof(indexes), StringComparer.Ordinal);
        CatalogTable.ValidateUnique(_checkConstraints.Select(check => check.Name), "Table-type check-constraint names",
            nameof(checkConstraints), StringComparer.Ordinal);
        if (_columns.Count(column => column.Identity is not null) > 1)
            throw new ArgumentException("A table type can contain only one identity column.", nameof(columns));
        if (_columns.Count(column => column.Type.Name is SqlTypeName.RowVersion or SqlTypeName.Timestamp) > 1)
            throw new ArgumentException("A table type can contain only one rowversion column.", nameof(columns));
        if (_columns.Count(column => column.IsRowGuidCol) > 1)
            throw new ArgumentException("A table type can contain only one ROWGUIDCOL column.", nameof(columns));
        if (_columns.Any(column => column.IsSparse || column.IsColumnSet))
            throw new ArgumentException("User-defined table types cannot contain SPARSE columns or a column set.", nameof(columns));
        if (_columns.Any(column => column.IsFileStream || column.MaskingFunction is not null || column.Encryption is not null ||
                                  column.GeneratedAlways != CatalogGeneratedAlwaysKind.None || column.IsHidden))
            throw new ArgumentException("User-defined table types cannot contain FILESTREAM, masking, encryption, or generated-always column facets.", nameof(columns));
        foreach (var column in _columns.Where(column => column.DefaultExpression is not null))
            if (_columns.Any(candidate => CatalogTable.ExpressionReferencesColumn(column.DefaultExpression!, candidate.Name)))
                throw new ArgumentException($"DEFAULT expression for column '{column.Name}' cannot reference a table-type column.", nameof(columns));
        _ = CatalogTable.GetComputedColumnOrder(_columns);
        if (isMemoryOptimized && _columns.Any(column => column.Type.IsUserDefined || column.Type.Name is
                SqlTypeName.DateTimeOffset or SqlTypeName.Geography or SqlTypeName.Geometry or SqlTypeName.HierarchyId or
                SqlTypeName.Json or SqlTypeName.RowVersion or SqlTypeName.Timestamp or SqlTypeName.SqlVariant or
                SqlTypeName.Vector or SqlTypeName.Xml or SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image))
            throw new ArgumentException("A memory-optimized table type contains an unsupported SQL data type.", nameof(columns));
        if (isMemoryOptimized && _columns.Any(column => column.Identity is { } identity &&
                (identity.Seed != BigInteger.One || identity.Increment != BigInteger.One)))
            throw new ArgumentException("Memory-optimized table types support only IDENTITY(1,1).", nameof(columns));
        if (isMemoryOptimized && (_columns.Any(column => column.DefaultExpression is not null || column.IsComputed ||
                                      column.IsRowGuidCol) || _checkConstraints.Length != 0))
            throw new ArgumentException("Memory-optimized table types do not support DEFAULT, computed, ROWGUIDCOL, or CHECK metadata.", nameof(columns));
        if (isMemoryOptimized && _indexes.Length == 0)
            throw new ArgumentException("A memory-optimized table type requires at least one index.", nameof(indexes));
        if (_indexes.Count(index => index.IsPrimaryKey) > 1)
            throw new ArgumentException("A table type can define only one primary key.", nameof(indexes));
        var columnIds = _columns.Select(column => column.Id).ToHashSet();
        foreach (var index in _indexes)
        {
            if (index.Columns.Any(column => !columnIds.Contains(column.ColumnId)))
                throw new ArgumentException($"Table-type index '{index.Name}' references an unknown column.", nameof(indexes));
            foreach (var indexedColumn in index.Columns)
            {
                var column = _columns.Single(candidate => candidate.Id == indexedColumn.ColumnId);
                if (!column.Type.CanBeBTreeKey)
                    throw new ArgumentException($"SQL Server type {column.Type} cannot be a table-type index key.", nameof(indexes));
                if (index.IsPrimaryKey && column.IsNullable)
                    throw new ArgumentException("Primary-key table-type columns cannot be nullable.", nameof(indexes));
            }
            if (index.IncludedColumns.Any(column => !columnIds.Contains(column)))
                throw new ArgumentException($"Table-type index '{index.Name}' includes an unknown column.", nameof(indexes));
            if (index.Columns.Count > 32)
                throw new ArgumentException("A table-type index cannot contain more than 32 key columns.", nameof(indexes));
            if (index.IncludedColumns.Count > 1_023)
                throw new ArgumentException("A table-type index cannot contain more than 1,023 included columns.", nameof(indexes));
            foreach (var includedId in index.IncludedColumns)
            {
                var included = _columns.Single(column => column.Id == includedId);
                if (included.Type.Name is SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image || included.IsColumnSet)
                    throw new ArgumentException($"Column '{included.Name}' cannot be included in an index.", nameof(indexes));
            }
            if (index.StorageKind == CatalogIndexStorageKind.Hash && !isMemoryOptimized)
                throw new ArgumentException("Hash indexes require a memory-optimized table type.", nameof(indexes));
            if (isMemoryOptimized && index.StorageKind == CatalogIndexStorageKind.Clustered)
                throw new ArgumentException("Memory-optimized table types support only nonclustered or hash indexes.", nameof(indexes));
            if (isMemoryOptimized && (index.IncludedColumns.Count != 0 || index.IgnoreDuplicateKey ||
                                      index.IsUnique && !index.IsPrimaryKey))
                throw new ArgumentException("Memory-optimized table-type indexes do not support INCLUDE, IGNORE_DUP_KEY, or standalone UNIQUE metadata.", nameof(indexes));
            if (isMemoryOptimized && index.StorageKind == CatalogIndexStorageKind.NonClustered &&
                index.Columns.Sum(indexed => CatalogDefinition.MaximumIndexKeyBytes(
                    _columns.Single(column => column.Id == indexed.ColumnId).Type)) > 2_500)
                throw new ArgumentException("A memory-optimized nonclustered table-type index cannot exceed 2,500 declared key bytes.", nameof(indexes));
            if (!isMemoryOptimized)
            {
                var maximum = index.StorageKind == CatalogIndexStorageKind.Clustered
                    ? CatalogIndexKey.MaximumClusteredKeyBytes : CatalogIndexKey.MaximumNonClusteredKeyBytes;
                if (index.Columns.Sum(indexed => CatalogDefinition.MinimumIndexKeyBytes(
                        _columns.Single(column => column.Id == indexed.ColumnId).Type)) > maximum)
                    throw new ArgumentException("The fixed-width table-type index key exceeds its SQL Server limit.", nameof(indexes));
            }
        }
        IsMemoryOptimized = isMemoryOptimized;
    }

    public string SchemaName { get; }
    public string Name { get; }
    public string QualifiedName => $"[{SchemaName}].[{Name}]";
    public IReadOnlyList<CatalogColumn> Columns => Array.AsReadOnly(_columns);
    public IReadOnlyList<CatalogTableTypeIndex> Indexes => Array.AsReadOnly(_indexes);
    public IReadOnlyList<CatalogCheckConstraint> CheckConstraints => Array.AsReadOnly(_checkConstraints);
    public bool IsMemoryOptimized { get; }
}

/// <summary>Validates the complete set of authoritative table and index records.</summary>
public sealed class CatalogDefinition
{
    private readonly CatalogTable[] _tables;
    private readonly CatalogIndex[] _indexes;
    private readonly CatalogScalarType[] _scalarTypes;
    private readonly CatalogTableType[] _tableTypes;
    private readonly SqlXmlSchemaCollection[] _xmlSchemaCollections;
    private readonly CatalogAssembly[] _assemblies;

    public CatalogDefinition(IEnumerable<CatalogTable> tables, IEnumerable<CatalogIndex> indexes,
        IEnumerable<CatalogScalarType>? scalarTypes = null, IEnumerable<CatalogTableType>? tableTypes = null,
        IEnumerable<SqlXmlSchemaCollection>? xmlSchemaCollections = null,
        IEnumerable<CatalogAssembly>? assemblies = null)
    {
        ArgumentNullException.ThrowIfNull(tables);
        ArgumentNullException.ThrowIfNull(indexes);
        _tables = tables.ToArray();
        _indexes = indexes.ToArray();
        _scalarTypes = scalarTypes?.ToArray() ?? [];
        _tableTypes = tableTypes?.ToArray() ?? [];
        var referencedXml = _tables.SelectMany(table => table.Columns).Concat(_tableTypes.SelectMany(type => type.Columns))
            .Select(column => column.Type.XmlSchemaCollection).Where(collection => collection is not null).Cast<SqlXmlSchemaCollection>()
            .Concat(_scalarTypes.Select(type => type.Definition.XmlSchemaCollection).Where(collection => collection is not null)
                .Cast<SqlXmlSchemaCollection>()).ToArray();
        _xmlSchemaCollections = xmlSchemaCollections?.ToArray() ?? referencedXml
            .GroupBy(collection => (collection.SchemaName, collection.Name)).Select(group => group.First()).ToArray();
        var referencedAssemblies = _scalarTypes.Where(type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr)
            .Select(type => type.Definition.ClrAssemblyName!).Distinct(StringComparer.Ordinal);
        _assemblies = assemblies?.ToArray() ?? referencedAssemblies.Select(name => new CatalogAssembly(name)).ToArray();
        if (_tables.Any(table => table is null)) throw new ArgumentException("Tables cannot contain null records.", nameof(tables));
        if (_indexes.Any(index => index is null)) throw new ArgumentException("Indexes cannot contain null records.", nameof(indexes));
        if (_scalarTypes.Any(type => type is null)) throw new ArgumentException("Scalar types cannot contain null records.", nameof(scalarTypes));
        if (_tableTypes.Any(type => type is null)) throw new ArgumentException("Table types cannot contain null records.", nameof(tableTypes));
        CatalogTable.ValidateUnique(_xmlSchemaCollections.Select(collection => (collection.SchemaName, collection.Name)),
            "XML schema collection names", nameof(xmlSchemaCollections));
        CatalogTable.ValidateUnique(_assemblies.Select(assembly => assembly.Name), "Assembly names", nameof(assemblies), StringComparer.Ordinal);
        CatalogTable.ValidateUnique(_tables.Select(table => table.Id), "Table IDs", nameof(tables));
        CatalogTable.ValidateUnique(_tables.Select(table => (table.DatabaseName, table.SchemaName, table.Name)),
            "Qualified table names", nameof(tables));
        CatalogTable.ValidateUnique(_indexes.Select(index => index.Id), "Index IDs", nameof(indexes));
        var typeNames = _scalarTypes.Select(type => (type.SchemaName, type.Name))
            .Concat(_tableTypes.Select(type => (type.SchemaName, type.Name))).ToArray();
        CatalogTable.ValidateUnique(typeNames, "User-defined type names", nameof(scalarTypes));
        CatalogTable.ValidateUnique(_scalarTypes.Where(type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr)
            .Select(type => (type.Definition.ClrAssemblyName!, type.Definition.ClrClassName!)),
            "CLR assembly/class bindings", nameof(scalarTypes));
        var xmlByName = _xmlSchemaCollections.ToDictionary(collection => (collection.SchemaName, collection.Name));
        foreach (var collection in referencedXml)
            if (!xmlByName.TryGetValue((collection.SchemaName, collection.Name), out var declared) || !declared.Equals(collection))
                throw new ArgumentException($"Typed XML references undeclared or mismatched collection {collection.QualifiedName}.", nameof(xmlSchemaCollections));
        var assemblyNames = _assemblies.Select(assembly => assembly.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var scalar in _scalarTypes.Where(type => type.Definition.UserTypeKind == SqlUserTypeKind.Clr))
            if (!assemblyNames.Contains(scalar.Definition.ClrAssemblyName!))
                throw new ArgumentException($"CLR type {scalar.QualifiedName} references an undeclared assembly.", nameof(assemblies));

        var scalarTypesByName = _scalarTypes.ToDictionary(type => (type.SchemaName, type.Name));
        foreach (var column in _tables.SelectMany(table => table.Columns)
                     .Concat(_tableTypes.SelectMany(type => type.Columns)))
        {
            if (!column.Type.IsUserDefined) continue;
            var name = (column.Type.UserTypeSchema!, column.Type.UserTypeName!);
            if (!scalarTypesByName.TryGetValue(name, out var declared) || declared.Definition != column.Type)
                throw new ArgumentException($"Column '{column.Name}' references an undeclared or mismatched user-defined type.", nameof(scalarTypes));
        }

        var tablesById = _tables.ToDictionary(table => table.Id);
        foreach (var current in _tables.Where(table => table.SystemVersioning is not null))
        {
            var temporal = current.SystemVersioning!;
            if (!tablesById.TryGetValue(temporal.HistoryTableId, out var history) || history.Id == current.Id)
                throw new ArgumentException($"System-versioned table '{current.Name}' references an invalid history table.", nameof(tables));
            if (history.SystemVersioning is not null)
                throw new ArgumentException("A temporal history table cannot itself be system-versioned.", nameof(tables));
            if (_tables.Count(table => table.SystemVersioning?.HistoryTableId == history.Id) != 1)
                throw new ArgumentException("A temporal history table must belong to exactly one current table.", nameof(tables));
            if (current.Columns.Count != history.Columns.Count || current.Columns.Zip(history.Columns).Any(pair =>
                    pair.First.Id != pair.Second.Id || pair.First.Name != pair.Second.Name ||
                    pair.First.Type != pair.Second.Type || pair.First.IsNullable != pair.Second.IsNullable))
                throw new ArgumentException("A temporal history table must have the current table's ordered column schema.", nameof(tables));
        }
        foreach (var group in _indexes.GroupBy(index => index.TableId))
        {
            CatalogTable.ValidateUnique(group.Select(index => index.Name), "Index names", nameof(indexes), StringComparer.Ordinal);
            if (group.Count(index => index.StorageKind == CatalogIndexStorageKind.Clustered) > 1)
                throw new ArgumentException("A disk table can have only one clustered index.", nameof(indexes));
            if (group.Count(index => index.StorageKind == CatalogIndexStorageKind.NonClustered) > 999)
                throw new ArgumentException("A disk table can have at most 999 nonclustered indexes.", nameof(indexes));
            if (group.Count(index => index.IsPrimaryKey) > 1)
                throw new ArgumentException("A table can have only one primary key.", nameof(indexes));
        }
        foreach (var table in _tables.Where(table => table.Columns.Any(column => column.IsFileStream)))
        {
            var rowGuid = table.Columns.Single(column => column.IsRowGuidCol);
            if (!_indexes.Any(index => index.TableId == table.Id && index.Method == CatalogIndexMethod.BTree &&
                                      index.IsUnique && index.Columns.Count == 1 &&
                                      index.Columns[0].ColumnId == rowGuid.Id))
                throw new ArgumentException("FILESTREAM requires a single-column UNIQUE or PRIMARY KEY index on the ROWGUIDCOL column.", nameof(indexes));
        }
        foreach (var index in _indexes)
        {
            if (!tablesById.TryGetValue(index.TableId, out var table))
                throw new ArgumentException($"Index '{index.Name}' references an unknown table.", nameof(indexes));
            var columnIds = table.Columns.Select(column => column.Id).ToHashSet();
            if (index.Columns.Any(column => !columnIds.Contains(column.ColumnId)))
                throw new ArgumentException($"Index '{index.Name}' references an unknown column.", nameof(indexes));
            if (index.IncludedColumns.Any(column => !columnIds.Contains(column)))
                throw new ArgumentException($"Index '{index.Name}' includes an unknown column.", nameof(indexes));
            if (index.Method == CatalogIndexMethod.BTree && index.Columns.Count > 32)
                throw new ArgumentException("A rowstore index key can contain at most 32 columns.", nameof(indexes));
            if (index.IncludedColumns.Count > 1_023)
                throw new ArgumentException("A nonclustered index can contain at most 1,023 included columns.", nameof(indexes));
            foreach (var includedId in index.IncludedColumns)
            {
                var included = table.Columns.Single(column => column.Id == includedId);
                if (included.Type.Name is SqlTypeName.Text or SqlTypeName.NText or SqlTypeName.Image || included.IsColumnSet)
                    throw new ArgumentException($"Column '{included.Name}' cannot be included in an index.", nameof(indexes));
            }
            foreach (var indexedColumn in index.Columns)
            {
                var column = table.Columns.Single(candidate => candidate.Id == indexedColumn.ColumnId);
                if (column.IsColumnSet)
                    throw new ArgumentException("An XML column set cannot be indexed.", nameof(indexes));
                if (column.IsComputed && index.Method == CatalogIndexMethod.Json)
                    throw new ArgumentException("A JSON index cannot be created on a computed JSON column.", nameof(indexes));
                if (column.IsComputed && index.Method == CatalogIndexMethod.Xml &&
                    index.SpecializedOptions!.XmlIndexKind is CatalogXmlIndexKind.Primary or CatalogXmlIndexKind.Selective)
                    throw new ArgumentException("A primary or selective XML index cannot be created on a computed XML column.", nameof(indexes));
                if (column.IsComputed && index.Method == CatalogIndexMethod.FullText)
                    throw new ArgumentException("A full-text index cannot be created on a computed column.", nameof(indexes));
                var compatible = index.Method switch
                {
                    CatalogIndexMethod.BTree => column.Type.CanBeBTreeKey,
                    CatalogIndexMethod.Json => column.Type.Name == SqlTypeName.Json,
                    CatalogIndexMethod.Spatial => column.Type.Name is SqlTypeName.Geometry or SqlTypeName.Geography,
                    CatalogIndexMethod.Vector => column.Type.Name == SqlTypeName.Vector,
                    CatalogIndexMethod.Xml => column.Type.Name == SqlTypeName.Xml,
                    CatalogIndexMethod.FullText => column.Type.Name is SqlTypeName.Char or SqlTypeName.VarChar or
                        SqlTypeName.Text or SqlTypeName.NChar or SqlTypeName.NVarChar or SqlTypeName.NText,
                    _ => false
                };
                if (!compatible)
                    throw new ArgumentException($"SQL Server type {column.Type} cannot be a B-tree key.", nameof(indexes));
                if (column.Encryption?.EncryptionType == CatalogEncryptionType.Randomized)
                    throw new ArgumentException("Randomized encrypted columns cannot be index keys.", nameof(indexes));
                if (column.IsSparse && (index.StorageKind == CatalogIndexStorageKind.Clustered || index.IsPrimaryKey))
                    throw new ArgumentException("A SPARSE column cannot be part of a clustered index or primary key.", nameof(indexes));
                if (index.Method == CatalogIndexMethod.FullText &&
                    !StringComparer.OrdinalIgnoreCase.Equals(column.Type.CollationMetadata?.Name,
                        index.SpecializedOptions!.FullTextOptions!.Collation))
                    throw new ArgumentException(
                        $"Full-text index '{index.Name}' collation must exactly match its source column collation.",
                        nameof(indexes));
            }
            var maximumKeyBytes = index.StorageKind == CatalogIndexStorageKind.Clustered
                ? CatalogIndexKey.MaximumClusteredKeyBytes : CatalogIndexKey.MaximumNonClusteredKeyBytes;
            if (index.Method == CatalogIndexMethod.BTree && index.Columns.Sum(indexedColumn =>
                    MinimumIndexKeyBytes(table.Columns.Single(column => column.Id == indexedColumn.ColumnId).Type)) >
                maximumKeyBytes)
                throw new ArgumentException("The fixed-width portion of the composite index key exceeds the SQL Server index-kind limit.", nameof(indexes));
            if (index.IsPrimaryKey && index.Columns.Any(indexed => table.Columns.Single(column => column.Id == indexed.ColumnId).IsNullable))
                throw new ArgumentException("Primary-key columns cannot be nullable.", nameof(indexes));
            if (index.Method is CatalogIndexMethod.Json or CatalogIndexMethod.Spatial or CatalogIndexMethod.Vector or
                CatalogIndexMethod.Xml or CatalogIndexMethod.FullText)
            {
                var clusteringKey = _indexes.SingleOrDefault(candidate => candidate.TableId == table.Id &&
                    candidate.IsPrimaryKey && candidate.StorageKind == CatalogIndexStorageKind.Clustered);
                if (clusteringKey is null)
                    throw new ArgumentException("Specialized indexes require a clustered primary key.", nameof(indexes));
                var clusteringKeyBytes = clusteringKey.Columns.Sum(key =>
                    MaximumIndexKeyBytes(table.Columns.Single(column => column.Id == key.ColumnId).Type));
                if (index.Method == CatalogIndexMethod.Json &&
                    (clusteringKey.Columns.Count > 31 || clusteringKeyBytes >= 128))
                    throw new ArgumentException("A JSON index requires at most 31 clustering-key columns totaling less than 128 bytes.", nameof(indexes));
                if (index.Method == CatalogIndexMethod.Spatial &&
                    (clusteringKey.Columns.Count > 15 || clusteringKeyBytes > 895))
                    throw new ArgumentException("A spatial index requires at most 15 primary-key columns totaling at most 895 bytes.", nameof(indexes));
                if (index.Method == CatalogIndexMethod.Xml && clusteringKey.Columns.Count > 15)
                    throw new ArgumentException("An XML index requires at most 15 clustering-key columns.", nameof(indexes));
            }
        }
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Json).GroupBy(index => index.TableId))
            if (group.Count() > 249)
                throw new ArgumentException("A table can have at most 249 JSON indexes.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Xml).GroupBy(index => index.TableId))
            if (group.Count() > 249)
                throw new ArgumentException("A table can have at most 249 XML indexes.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.FullText)
                     .GroupBy(index => (index.TableId, index.Columns.Single().ColumnId)))
            if (group.Count() > 1)
                throw new ArgumentException("A textual column can have only one full-text index.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Spatial)
                     .GroupBy(index => (index.TableId, index.Columns.Single().ColumnId)))
            if (group.Count() > 249)
                throw new ArgumentException("A spatial column can have at most 249 spatial indexes.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Json)
                     .GroupBy(index => (index.TableId, index.Columns.Single().ColumnId)))
            if (group.Count() > 1)
                throw new ArgumentException("A JSON column can have only one JSON index.", nameof(indexes));
        foreach (var group in _indexes.Where(index => index.Method == CatalogIndexMethod.Xml)
                     .GroupBy(index => (index.TableId, index.Columns.Single().ColumnId)))
        {
            if (group.Count(index => index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Primary) > 1)
                throw new ArgumentException("An XML column can have only one primary XML index.", nameof(indexes));
            if (group.Count(index => index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Selective) > 1)
                throw new ArgumentException("An XML column can have only one selective XML index.", nameof(indexes));
            if (group.Any(index => index.SpecializedOptions!.XmlIndexKind is CatalogXmlIndexKind.Path or
                    CatalogXmlIndexKind.Value or CatalogXmlIndexKind.Property) &&
                !group.Any(index => index.SpecializedOptions!.XmlIndexKind == CatalogXmlIndexKind.Primary))
                throw new ArgumentException("Secondary PATH, VALUE, and PROPERTY XML indexes require a primary XML index.", nameof(indexes));
        }
    }

    public IReadOnlyList<CatalogTable> Tables => Array.AsReadOnly(_tables);
    public IReadOnlyList<CatalogIndex> Indexes => Array.AsReadOnly(_indexes);
    public IReadOnlyList<CatalogScalarType> ScalarTypes => Array.AsReadOnly(_scalarTypes);
    public IReadOnlyList<CatalogTableType> TableTypes => Array.AsReadOnly(_tableTypes);
    public IReadOnlyList<SqlXmlSchemaCollection> XmlSchemaCollections => Array.AsReadOnly(_xmlSchemaCollections);
    public IReadOnlyList<CatalogAssembly> Assemblies => Array.AsReadOnly(_assemblies);

    internal static int MaximumIndexKeyBytes(SqlType type) => type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MaximumIndexKeyBytes(type.BaseType!),
        SqlUserTypeKind.Clr when type.ClrMaxByteSize is null => CatalogIndexKey.MaximumNonClusteredKeyBytes,
        SqlUserTypeKind.Clr => type.ClrMaxByteSize is > 0 and <= CatalogIndexKey.MaximumNonClusteredKeyBytes
            ? type.ClrMaxByteSize.Value : CatalogIndexKey.MaximumNonClusteredKeyBytes + 1,
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
            SqlTypeName.Decimal or SqlTypeName.Numeric => type.Precision <= 9 ? 5 :
                type.Precision <= 19 ? 9 : type.Precision <= 28 ? 13 : 17,
            SqlTypeName.UniqueIdentifier => 16,
            SqlTypeName.RowVersion or SqlTypeName.Timestamp => 8,
            SqlTypeName.Char or SqlTypeName.VarChar or SqlTypeName.Binary or SqlTypeName.VarBinary => type.Length!.Value,
            SqlTypeName.NChar or SqlTypeName.NVarChar => checked(type.Length!.Value * 2),
            SqlTypeName.HierarchyId => 892,
            SqlTypeName.SqlVariant => CatalogIndexKey.MaximumSqlVariantKeyBytes,
            _ => CatalogIndexKey.MaximumNonClusteredKeyBytes + 1
        }
    };

    internal static int MinimumIndexKeyBytes(SqlType type) => type.UserTypeKind switch
    {
        SqlUserTypeKind.Alias => MinimumIndexKeyBytes(type.BaseType!),
        SqlUserTypeKind.Clr => type.ClrIsFixedLength && type.ClrMaxByteSize is > 0 ? type.ClrMaxByteSize.Value : 0,
        _ => type.Name switch
        {
            SqlTypeName.VarChar or SqlTypeName.NVarChar or SqlTypeName.VarBinary or SqlTypeName.HierarchyId or
                SqlTypeName.SqlVariant => 0,
            _ => MaximumIndexKeyBytes(type)
        }
    };
}
