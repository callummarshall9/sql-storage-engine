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
{
    None = 0,
    RowStart = 1,
    RowEnd = 2,
    TransactionIdStart = 3,
    TransactionIdEnd = 4,
    SequenceNumberStart = 5,
    SequenceNumberEnd = 6,
    GraphIdentity = 7,
    GraphFromNode = 8,
    GraphToNode = 9
}
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
        if ((generatedAlways is CatalogGeneratedAlwaysKind.RowStart or CatalogGeneratedAlwaysKind.RowEnd &&
             type.Name != SqlTypeName.DateTime2) ||
            (generatedAlways is CatalogGeneratedAlwaysKind.TransactionIdStart or CatalogGeneratedAlwaysKind.TransactionIdEnd or
                 CatalogGeneratedAlwaysKind.SequenceNumberStart or CatalogGeneratedAlwaysKind.SequenceNumberEnd &&
             type.Name != SqlTypeName.BigInt) ||
            (generatedAlways is CatalogGeneratedAlwaysKind.GraphIdentity or CatalogGeneratedAlwaysKind.GraphFromNode or
                 CatalogGeneratedAlwaysKind.GraphToNode && type != SqlType.Binary(GraphNodeId.EncodedLength)))
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
