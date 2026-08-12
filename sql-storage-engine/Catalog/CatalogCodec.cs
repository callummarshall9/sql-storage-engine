using System.Buffers;
using System.Buffers.Binary;
using System.Numerics;
using System.Text;
using System.Xml;
using System.Xml.Schema;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Catalog;

/// <summary>Encodes the self-describing bootstrap catalog without relying on user schemas.</summary>
public static class CatalogCodec
{
    public const ushort FormatVersion = 7;
    public const uint Magic = 0x37544143; // "CAT7" in persisted little-endian byte order.
    private const uint Version6Magic = 0x36544143;
    public const int HeaderLength = 32;
    public const int MaximumRecordCount = 65_535;
    public const int MaximumStringBytes = int.MaxValue;
    private static readonly UTF8Encoding Utf8 = new(false, true);

    public static byte[] Encode(CatalogDefinition catalog)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ValidateCount(catalog.Tables.Count, nameof(catalog));
        ValidateCount(catalog.Indexes.Count, nameof(catalog));
        ValidateCount(catalog.ScalarTypes.Count, nameof(catalog));
        ValidateCount(catalog.TableTypes.Count, nameof(catalog));
        ValidateCount(catalog.XmlSchemaCollections.Count, nameof(catalog));
        ValidateCount(catalog.Assemblies.Count, nameof(catalog));
        var output = new ArrayBufferWriter<byte>();
        WriteUInt32(output, Magic);
        WriteUInt16(output, FormatVersion);
        WriteUInt16(output, 0);
        WriteUInt32(output, checked((uint)catalog.Tables.Count));
        WriteUInt32(output, checked((uint)catalog.Indexes.Count));
        WriteUInt32(output, checked((uint)catalog.ScalarTypes.Count));
        WriteUInt32(output, checked((uint)catalog.TableTypes.Count));
        WriteUInt32(output, checked((uint)catalog.XmlSchemaCollections.Count));
        WriteUInt32(output, checked((uint)catalog.Assemblies.Count));
        foreach (var collection in catalog.XmlSchemaCollections)
        {
            WriteString(output, collection.SchemaName);
            WriteString(output, collection.Name);
            WriteUInt32(output, checked((uint)collection.Definitions.Count));
            foreach (var definition in collection.Definitions) WriteString(output, definition);
        }
        foreach (var assembly in catalog.Assemblies)
        {
            WriteString(output, assembly.Name);
            WriteString(output, assembly.Version ?? string.Empty);
            WriteString(output, assembly.Culture ?? string.Empty);
            WriteString(output, assembly.PublicKeyToken ?? string.Empty);
            WriteByte(output, (byte)assembly.PermissionSet);
            WriteBytes(output, assembly.Image.Span);
        }
        foreach (var table in catalog.Tables)
        {
            WriteUInt64(output, table.Id.Value);
            WriteString(output, table.DatabaseName);
            WriteString(output, table.SchemaName);
            WriteString(output, table.Name);
            WriteUInt64(output, table.SchemaVersion);
            WriteUInt64(output, table.FirstHeapPageId.Value);
            WriteUInt32(output, checked((uint)table.Columns.Count));
            foreach (var column in table.Columns)
            {
                WriteColumn(output, column);
            }
            WriteUInt32(output, checked((uint)table.CheckConstraints.Count));
            foreach (var check in table.CheckConstraints) { WriteString(output, check.Name); WriteString(output, check.Expression); }
            WriteByte(output, table.NextIdentityValue is null ? (byte)0 : (byte)1);
            if (table.NextIdentityValue is { } nextIdentity) WriteBigInteger(output, nextIdentity);
        }
        foreach (var index in catalog.Indexes)
        {
            WriteUInt64(output, index.Id.Value);
            WriteString(output, index.Name);
            WriteUInt64(output, index.TableId.Value);
            WriteUInt64(output, index.RootPageId.Value);
            WriteByte(output, index.IsUnique ? (byte)1 : (byte)0);
            WriteByte(output, (byte)index.Method);
            WriteUInt16(output, checked((ushort)index.Columns.Count));
            foreach (var column in index.Columns)
            {
                WriteUInt64(output, column.ColumnId.Value);
                WriteByte(output, (byte)column.Direction);
                WriteByte(output, (byte)column.NullSortOrder);
                WriteString(output, column.Collation ?? string.Empty);
            }
            WriteUInt16(output, checked((ushort)(index.SpecializedOptions?.JsonPaths.Count ?? 0)));
            foreach (var path in index.SpecializedOptions?.JsonPaths ?? []) WriteString(output, path);
            WriteByte(output, index.SpecializedOptions?.VectorMetric is null ? (byte)0 : (byte)index.SpecializedOptions.VectorMetric.Value);
            WriteByte(output, index.SpecializedOptions?.XmlIndexKind is null ? (byte)0 : (byte)index.SpecializedOptions.XmlIndexKind.Value);
            WriteUInt16(output, checked((ushort)(index.SpecializedOptions?.XmlNamespaces.Count ?? 0)));
            foreach (var binding in (index.SpecializedOptions?.XmlNamespaces ??
                         new Dictionary<string, string>()).OrderBy(item => item.Key, StringComparer.Ordinal))
            {
                WriteString(output, binding.Key);
                WriteString(output, binding.Value);
            }
            WriteByte(output, (byte)index.StorageKind); WriteByte(output, index.IsPrimaryKey ? (byte)1 : (byte)0);
            WriteByte(output, index.IgnoreDuplicateKey ? (byte)1 : (byte)0); WriteByte(output, 0);
            WriteUInt16(output, checked((ushort)index.IncludedColumns.Count));
            foreach (var included in index.IncludedColumns) WriteUInt64(output, included.Value);
        }
        foreach (var scalarType in catalog.ScalarTypes) WriteType(output, scalarType.Definition);
        foreach (var tableType in catalog.TableTypes)
        {
            WriteString(output, tableType.SchemaName);
            WriteString(output, tableType.Name);
            WriteByte(output, tableType.IsMemoryOptimized ? (byte)1 : (byte)0);
            WriteByte(output, 0); WriteUInt16(output, 0);
            WriteUInt32(output, checked((uint)tableType.Columns.Count));
            foreach (var column in tableType.Columns) WriteColumn(output, column);
            WriteUInt32(output, checked((uint)tableType.Indexes.Count));
            foreach (var index in tableType.Indexes)
            {
                WriteString(output, index.Name);
                WriteByte(output, index.IsPrimaryKey ? (byte)1 : (byte)0);
                WriteByte(output, index.IsUnique ? (byte)1 : (byte)0);
                WriteByte(output, (byte)index.StorageKind);
                WriteByte(output, index.IgnoreDuplicateKey ? (byte)1 : (byte)0);
                WriteUInt16(output, checked((ushort)index.Columns.Count));
                foreach (var column in index.Columns) WriteIndexedColumn(output, column);
                WriteUInt16(output, checked((ushort)index.IncludedColumns.Count));
                foreach (var columnId in index.IncludedColumns) WriteUInt64(output, columnId.Value);
                WriteUInt64(output, checked((ulong)(index.BucketCount ?? 0)));
            }
            WriteUInt32(output, checked((uint)tableType.CheckConstraints.Count));
            foreach (var check in tableType.CheckConstraints) { WriteString(output, check.Name); WriteString(output, check.Expression); }
        }
        return output.WrittenSpan.ToArray();
    }

    public static CatalogDefinition Decode(ReadOnlySpan<byte> source)
    {
        var reader = new Reader(source);
        var magic = reader.UInt32();
        if (magic is not (Magic or Version6Magic)) throw new StorageFormatException("Invalid bootstrap catalog magic number.");
        var version = reader.UInt16();
        if (version is not (6 or FormatVersion) || version == 6 && magic != Version6Magic ||
            version == FormatVersion && magic != Magic)
            throw new StorageFormatException($"Unsupported catalog format version {version}.");
        if (reader.UInt16() != 0) throw new StorageFormatException("Reserved catalog header bytes must be zero.");
        var tableCount = reader.Count();
        var indexCount = reader.Count();
        var scalarTypeCount = reader.Count();
        var tableTypeCount = reader.Count();
        var xmlSchemaCount = reader.Count();
        var assemblyCount = reader.Count();
        List<SqlXmlSchemaCollection> xmlSchemas = new(xmlSchemaCount);
        for (var schemaNumber = 0; schemaNumber < xmlSchemaCount; schemaNumber++)
        {
            var schemaName = reader.String(); var name = reader.String(); var definitionCount = reader.Count(nonZero: true);
            var definitions = new string[definitionCount];
            for (var index = 0; index < definitions.Length; index++) definitions[index] = reader.String();
            try { xmlSchemas.Add(new SqlXmlSchemaCollection(schemaName, name, definitions)); }
            catch (Exception exception) when (exception is ArgumentException or XmlException or XmlSchemaException)
            { throw new StorageFormatException("Invalid XML schema collection record.", exception); }
        }
        reader.SetXmlSchemas(xmlSchemas);
        List<CatalogAssembly> assemblies = new(assemblyCount);
        for (var assemblyNumber = 0; assemblyNumber < assemblyCount; assemblyNumber++)
        {
            var name = reader.String(); var versionText = reader.String(); var culture = reader.String(); var token = reader.String();
            var permission = (CatalogAssemblyPermissionSet)reader.Byte(); var image = reader.Bytes();
            try { assemblies.Add(new CatalogAssembly(name, image, EmptyToNull(versionText), EmptyToNull(culture),
                EmptyToNull(token), permission)); }
            catch (ArgumentException exception) { throw new StorageFormatException("Invalid CLR assembly record.", exception); }
        }
        List<CatalogTable> tables = new(tableCount);
        for (var tableNumber = 0; tableNumber < tableCount; tableNumber++)
        {
            var id = new TableId(reader.UInt64());
            var databaseName = version >= 7 ? reader.String() : "default";
            var schemaName = version >= 7 ? reader.String() : "dbo";
            var name = reader.String();
            var schemaVersion = reader.UInt64();
            var heapRoot = new PageId(reader.UInt64());
            var columnCount = reader.Count(nonZero: true);
            List<CatalogColumn> columns = new(columnCount);
            for (var columnNumber = 0; columnNumber < columnCount; columnNumber++)
            {
                columns.Add(reader.Column());
            }
            var checkCount = reader.Count(); var checks = new CatalogCheckConstraint[checkCount];
            for (var checkNumber = 0; checkNumber < checkCount; checkNumber++)
                checks[checkNumber] = new CatalogCheckConstraint(reader.String(), reader.String());
            var nextIdentity = reader.Boolean() ? reader.BigInteger() : (BigInteger?)null;
            try { tables.Add(new CatalogTable(id, name, schemaVersion, heapRoot, columns, checks, nextIdentity,
                databaseName, schemaName)); }
            catch (ArgumentException exception) { throw new StorageFormatException("Invalid catalog table record.", exception); }
        }
        List<CatalogIndex> indexes = new(indexCount);
        for (var indexNumber = 0; indexNumber < indexCount; indexNumber++)
        {
            var id = new IndexId(reader.UInt64());
            var name = reader.String();
            var tableId = new TableId(reader.UInt64());
            var root = new PageId(reader.UInt64());
            var unique = reader.Boolean();
            var method = (CatalogIndexMethod)reader.Byte();
            if (!Enum.IsDefined(method)) throw new StorageFormatException("Unknown catalog index method.");
            var columnCount = reader.UInt16();
            if (columnCount == 0) throw new StorageFormatException("Catalog index has no columns.");
            List<CatalogIndexedColumn> columns = new(columnCount);
            for (var columnNumber = 0; columnNumber < columnCount; columnNumber++)
            {
                var columnId = new ColumnId(reader.UInt64());
                var direction = (SortDirection)reader.Byte();
                var nullOrder = (NullSortOrder)reader.Byte();
                var collation = reader.String();
                try { columns.Add(new CatalogIndexedColumn(columnId, direction, nullOrder,
                    collation.Length == 0 ? null : collation)); }
                catch (ArgumentException exception) { throw new StorageFormatException("Invalid indexed-column record.", exception); }
            }
            var jsonPathCount = reader.UInt16();
            var jsonPaths = new string[jsonPathCount];
            for (var pathNumber = 0; pathNumber < jsonPaths.Length; pathNumber++) jsonPaths[pathNumber] = reader.String();
            var rawMetric = reader.Byte();
            var rawXmlKind = reader.Byte();
            var xmlNamespaceCount = reader.UInt16();
            var xmlNamespaces = new Dictionary<string, string>(xmlNamespaceCount, StringComparer.Ordinal);
            for (var namespaceNumber = 0; namespaceNumber < xmlNamespaceCount; namespaceNumber++)
                if (!xmlNamespaces.TryAdd(reader.String(), reader.String()))
                    throw new StorageFormatException("Duplicate XML namespace prefix in specialized-index options.");
            var storageKind = (CatalogIndexStorageKind)reader.Byte(); var primary = reader.Boolean(); var ignoreDuplicate = reader.Boolean();
            if (reader.Byte() != 0) throw new StorageFormatException("Reserved index-option byte must be zero.");
            var includedCount = reader.UInt16(); var included = new ColumnId[includedCount];
            for (var includedNumber = 0; includedNumber < included.Length; includedNumber++) included[includedNumber] = new ColumnId(reader.UInt64());
            CatalogSpecializedIndexOptions? options = method switch
            {
                CatalogIndexMethod.BTree when jsonPaths.Length == 0 && rawMetric == 0 && rawXmlKind == 0 && xmlNamespaces.Count == 0 => null,
                CatalogIndexMethod.Json when rawMetric == 0 && rawXmlKind == 0 && xmlNamespaces.Count == 0 => CatalogSpecializedIndexOptions.Json(jsonPaths),
                CatalogIndexMethod.Spatial when jsonPaths.Length == 0 && rawMetric == 0 && rawXmlKind == 0 && xmlNamespaces.Count == 0 => CatalogSpecializedIndexOptions.Spatial(),
                CatalogIndexMethod.Vector when jsonPaths.Length == 0 && rawMetric != 0 && rawXmlKind == 0 && xmlNamespaces.Count == 0 =>
                    CatalogSpecializedIndexOptions.Vector((SqlVectorDistanceMetric)rawMetric),
                CatalogIndexMethod.Xml when rawMetric == 0 && rawXmlKind != 0 =>
                    CatalogSpecializedIndexOptions.Xml((CatalogXmlIndexKind)rawXmlKind, xmlNamespaces, jsonPaths),
                _ => throw new StorageFormatException("Invalid specialized-index options.")
            };
            try { indexes.Add(new CatalogIndex(id, name, tableId, root, unique, columns, options,
                storageKind, primary, included, ignoreDuplicate)); }
            catch (ArgumentException exception) { throw new StorageFormatException("Invalid catalog index record.", exception); }
        }
        List<CatalogScalarType> scalarTypes = new(scalarTypeCount);
        for (var typeNumber = 0; typeNumber < scalarTypeCount; typeNumber++)
        {
            try { scalarTypes.Add(new CatalogScalarType(reader.Type())); }
            catch (ArgumentException exception) { throw new StorageFormatException("Invalid scalar user-defined type record.", exception); }
        }
        List<CatalogTableType> tableTypes = new(tableTypeCount);
        for (var typeNumber = 0; typeNumber < tableTypeCount; typeNumber++)
        {
            var schemaName = reader.String();
            var name = reader.String();
            var memoryOptimized = reader.Boolean();
            if (reader.Byte() != 0 || reader.UInt16() != 0) throw new StorageFormatException("Reserved table-type bytes must be zero.");
            var columnCount = reader.Count(nonZero: true);
            List<CatalogColumn> columns = new(columnCount);
            for (var columnNumber = 0; columnNumber < columnCount; columnNumber++) columns.Add(reader.Column());
            var typeIndexCount = reader.Count();
            List<CatalogTableTypeIndex> typeIndexes = new(typeIndexCount);
            for (var indexNumber = 0; indexNumber < typeIndexCount; indexNumber++)
            {
                var indexName = reader.String();
                var primary = reader.Boolean();
                var unique = reader.Boolean();
                var storageKind = (CatalogIndexStorageKind)reader.Byte();
                var ignoreDuplicateKey = reader.Boolean();
                var indexedColumnCount = reader.UInt16();
                if (indexedColumnCount == 0) throw new StorageFormatException("Table-type index has no columns.");
                List<CatalogIndexedColumn> indexedColumns = new(indexedColumnCount);
                for (var columnNumber = 0; columnNumber < indexedColumnCount; columnNumber++)
                    indexedColumns.Add(reader.IndexedColumn());
                var includedCount = reader.UInt16();
                var included = new ColumnId[includedCount];
                for (var includeNumber = 0; includeNumber < included.Length; includeNumber++) included[includeNumber] = new ColumnId(reader.UInt64());
                var rawBucketCount = reader.UInt64();
                long? bucketCount = rawBucketCount == 0 ? null : checked((long)rawBucketCount);
                try { typeIndexes.Add(new CatalogTableTypeIndex(indexName, primary, unique, indexedColumns,
                    storageKind, included, ignoreDuplicateKey, bucketCount)); }
                catch (ArgumentException exception) { throw new StorageFormatException("Invalid table-type index record.", exception); }
            }
            var checkCount = reader.Count();
            List<CatalogCheckConstraint> checks = new(checkCount);
            for (var checkNumber = 0; checkNumber < checkCount; checkNumber++)
                try { checks.Add(new CatalogCheckConstraint(reader.String(), reader.String())); }
                catch (ArgumentException exception) { throw new StorageFormatException("Invalid table-type check constraint.", exception); }
            try { tableTypes.Add(new CatalogTableType(schemaName, name, columns, typeIndexes, checks, memoryOptimized)); }
            catch (ArgumentException exception) { throw new StorageFormatException("Invalid table-type record.", exception); }
        }
        if (!reader.End) throw new StorageFormatException("Bootstrap catalog contains trailing bytes.");
        try { return new CatalogDefinition(tables, indexes, scalarTypes, tableTypes, xmlSchemas, assemblies); }
        catch (ArgumentException exception) { throw new StorageCorruptionException("Bootstrap catalog cross-references are invalid.", exception); }
    }

    private static void ValidateCount(int count, string parameterName)
    {
        if (count > MaximumRecordCount) throw new ArgumentException("Catalog record count exceeds the format limit.", parameterName);
    }

    private static void WriteByte(IBufferWriter<byte> output, byte value) { var span = output.GetSpan(1); span[0] = value; output.Advance(1); }
    private static void WriteInt32(IBufferWriter<byte> output, int value) { var span = output.GetSpan(4); BinaryPrimitives.WriteInt32LittleEndian(span, value); output.Advance(4); }
    private static void WriteUInt16(IBufferWriter<byte> output, ushort value) { var span = output.GetSpan(2); BinaryPrimitives.WriteUInt16LittleEndian(span, value); output.Advance(2); }
    private static void WriteUInt32(IBufferWriter<byte> output, uint value) { var span = output.GetSpan(4); BinaryPrimitives.WriteUInt32LittleEndian(span, value); output.Advance(4); }
    private static void WriteUInt64(IBufferWriter<byte> output, ulong value) { var span = output.GetSpan(8); BinaryPrimitives.WriteUInt64LittleEndian(span, value); output.Advance(8); }
    private static void WriteString(IBufferWriter<byte> output, string value)
    {
        var length = Utf8.GetByteCount(value);
        if (length > MaximumStringBytes) throw new ArgumentException("Catalog string exceeds the format limit.", nameof(value));
        WriteUInt32(output, checked((uint)length));
        var span = output.GetSpan(length);
        Utf8.GetBytes(value, span);
        output.Advance(length);
    }
    private static void WriteBytes(IBufferWriter<byte> output, ReadOnlySpan<byte> value)
    {
        WriteUInt32(output, checked((uint)value.Length)); value.CopyTo(output.GetSpan(value.Length)); output.Advance(value.Length);
    }
    private static void WriteBigInteger(IBufferWriter<byte> output, BigInteger value) =>
        WriteBytes(output, value.ToByteArray(isUnsigned: false, isBigEndian: false));
    private static string? EmptyToNull(string value) => value.Length == 0 ? null : value;

    private static void WriteColumn(IBufferWriter<byte> output, CatalogColumn column)
    {
        WriteUInt64(output, column.Id.Value);
        WriteString(output, column.Name);
        WriteType(output, column.Type);
        WriteByte(output, column.IsNullable ? (byte)1 : (byte)0);
        WriteByte(output, column.IsRowGuidCol ? (byte)1 : (byte)0);
        WriteByte(output, column.IsComputedPersisted ? (byte)1 : (byte)0);
        WriteByte(output, column.Identity is null ? (byte)0 : (byte)1);
        WriteString(output, column.DefaultExpression ?? string.Empty);
        WriteString(output, column.ComputedExpression ?? string.Empty);
        if (column.Identity is { } identity) { WriteBigInteger(output, identity.Seed); WriteBigInteger(output, identity.Increment); }
        WriteByte(output, column.IsSparse ? (byte)1 : (byte)0); WriteByte(output, column.IsColumnSet ? (byte)1 : (byte)0);
        WriteByte(output, column.IsFileStream ? (byte)1 : (byte)0); WriteByte(output, column.IsHidden ? (byte)1 : (byte)0);
        WriteByte(output, (byte)column.GeneratedAlways);
        WriteByte(output, column.Encryption is null ? (byte)0 : (byte)column.Encryption.EncryptionType);
        WriteUInt16(output, 0);
        WriteString(output, column.MaskingFunction ?? string.Empty);
        WriteString(output, column.Encryption?.KeyName ?? string.Empty);
        WriteString(output, column.Encryption?.Algorithm ?? string.Empty);
    }

    private static void WriteIndexedColumn(IBufferWriter<byte> output, CatalogIndexedColumn column)
    {
        WriteUInt64(output, column.ColumnId.Value);
        WriteByte(output, (byte)column.Direction);
        WriteByte(output, (byte)column.NullSortOrder);
        WriteString(output, column.Collation ?? string.Empty);
    }

    private static void WriteType(IBufferWriter<byte> output, SqlType type)
    {
        WriteByte(output, (byte)type.Name);
        WriteByte(output, type.Precision ?? byte.MaxValue);
        WriteByte(output, type.Scale ?? byte.MaxValue);
        WriteByte(output, type.IsMax ? (byte)1 : (byte)0);
        WriteInt32(output, type.Length ?? -1);
        WriteUInt16(output, type.VectorDimensions ?? 0);
        WriteByte(output, type.VectorBaseType is null ? byte.MaxValue : (byte)type.VectorBaseType.Value);
        WriteByte(output, type.XmlContentKind is null ? (byte)0 : (byte)type.XmlContentKind.Value);
        WriteString(output, type.Collation ?? string.Empty);
        WriteByte(output, type.XmlSchemaCollection is null ? (byte)0 : (byte)1);
        if (type.XmlSchemaCollection is { } xmlSchema)
        {
            WriteString(output, xmlSchema.SchemaName);
            WriteString(output, xmlSchema.Name);
        }
        WriteByte(output, (byte)type.UserTypeKind);
        switch (type.UserTypeKind)
        {
            case SqlUserTypeKind.None:
                break;
            case SqlUserTypeKind.Alias:
                WriteString(output, type.UserTypeSchema!);
                WriteString(output, type.UserTypeName!);
                WriteByte(output, type.UserTypeNullable!.Value ? (byte)1 : (byte)0);
                WriteType(output, type.BaseType!);
                break;
            case SqlUserTypeKind.Clr:
                WriteString(output, type.UserTypeSchema!);
                WriteString(output, type.UserTypeName!);
                WriteString(output, type.ClrAssemblyName!);
                WriteString(output, type.ClrClassName!);
                WriteByte(output, (byte)type.ClrSerializationFormat!.Value);
                WriteInt32(output, type.ClrMaxByteSize ?? int.MinValue);
                WriteByte(output, type.ClrIsByteOrdered ? (byte)1 : (byte)0);
                WriteByte(output, type.ClrIsFixedLength ? (byte)1 : (byte)0);
                WriteString(output, type.ClrValidationMethodName ?? string.Empty);
                break;
            default:
                throw new ArgumentException("Unknown SQL user-defined type kind.", nameof(type));
        }
    }

    private ref struct Reader
    {
        private readonly ReadOnlySpan<byte> _source;
        private int _offset;
        private Dictionary<(string Schema, string Name), SqlXmlSchemaCollection>? _xmlSchemas;
        public Reader(ReadOnlySpan<byte> source) => _source = source;
        public void SetXmlSchemas(IEnumerable<SqlXmlSchemaCollection> schemas) => _xmlSchemas = schemas.ToDictionary(
            schema => (schema.SchemaName, schema.Name));
        public bool End => _offset == _source.Length;
        public byte Byte() { Require(1); return _source[_offset++]; }
        public bool Boolean() => Byte() switch { 0 => false, 1 => true, _ => throw new StorageFormatException("Invalid catalog Boolean value.") };
        public ushort UInt16() { Require(2); var value = BinaryPrimitives.ReadUInt16LittleEndian(_source[_offset..]); _offset += 2; return value; }
        public uint UInt32() { Require(4); var value = BinaryPrimitives.ReadUInt32LittleEndian(_source[_offset..]); _offset += 4; return value; }
        public int Int32() { Require(4); var value = BinaryPrimitives.ReadInt32LittleEndian(_source[_offset..]); _offset += 4; return value; }
        public ulong UInt64() { Require(8); var value = BinaryPrimitives.ReadUInt64LittleEndian(_source[_offset..]); _offset += 8; return value; }
        public int Count(bool nonZero = false) { var value = UInt32(); if (value > MaximumRecordCount || nonZero && value == 0) throw new StorageFormatException("Invalid catalog record count."); return checked((int)value); }
        public string String()
        {
            var rawLength = UInt32();
            if (rawLength > MaximumStringBytes) throw new StorageFormatException("Catalog string exceeds the format limit.");
            var length = checked((int)rawLength);
            Require(length);
            try { var value = Utf8.GetString(_source.Slice(_offset, length)); _offset += length; return value; }
            catch (DecoderFallbackException exception) { throw new StorageFormatException("Catalog string is not valid UTF-8.", exception); }
        }
        public byte[] Bytes() { var rawLength = UInt32(); if (rawLength > int.MaxValue) throw new StorageFormatException("Catalog byte array is too large."); var length = (int)rawLength; Require(length); var result = _source.Slice(_offset, length).ToArray(); _offset += length; return result; }
        public BigInteger BigInteger()
        {
            var bytes = Bytes();
            if (bytes.Length == 0) throw new StorageFormatException("Catalog integer is empty.");
            var value = new System.Numerics.BigInteger(bytes, isUnsigned: false, isBigEndian: false);
            if (!bytes.AsSpan().SequenceEqual(value.ToByteArray(isUnsigned: false, isBigEndian: false)))
                throw new StorageFormatException("Catalog integer is not canonically encoded.");
            return value;
        }
        public CatalogColumn Column()
        {
            var id = new ColumnId(UInt64());
            var name = String();
            var type = Type();
            var nullable = Boolean();
            var rowGuid = Boolean();
            var computedPersisted = Boolean();
            var hasIdentity = Boolean();
            var defaultExpression = String();
            var computedExpression = String();
            CatalogIdentity? identity = null;
            if (hasIdentity) identity = new CatalogIdentity(BigInteger(), BigInteger());
            var sparse = Boolean(); var columnSet = Boolean(); var fileStream = Boolean(); var hidden = Boolean();
            var generated = (CatalogGeneratedAlwaysKind)Byte(); var rawEncryption = Byte();
            if (UInt16() != 0) throw new StorageFormatException("Reserved column-facet bytes must be zero.");
            var masking = EmptyToNull(String()); var encryptionKey = EmptyToNull(String()); var encryptionAlgorithm = EmptyToNull(String());
            CatalogColumnEncryption? encryption = rawEncryption == 0 ? null : new CatalogColumnEncryption(
                encryptionKey ?? throw new StorageFormatException("Encrypted column has no key."),
                (CatalogEncryptionType)rawEncryption,
                encryptionAlgorithm ?? throw new StorageFormatException("Encrypted column has no algorithm."));
            try { return new CatalogColumn(id, name, type, nullable, EmptyToNull(defaultExpression), identity,
                rowGuid, EmptyToNull(computedExpression), computedPersisted, sparse, columnSet, fileStream,
                masking, encryption, generated, hidden); }
            catch (ArgumentException exception) { throw new StorageFormatException("Invalid catalog column record.", exception); }
        }
        public CatalogIndexedColumn IndexedColumn()
        {
            var columnId = new ColumnId(UInt64());
            var direction = (SortDirection)Byte();
            var nullOrder = (NullSortOrder)Byte();
            var collation = String();
            try { return new CatalogIndexedColumn(columnId, direction, nullOrder, collation.Length == 0 ? null : collation); }
            catch (ArgumentException exception) { throw new StorageFormatException("Invalid indexed-column record.", exception); }
        }
        public SqlType Type()
        {
            var name = (SqlTypeName)Byte();
            if (!Enum.IsDefined(name)) throw new StorageFormatException("Unknown SQL type name.");
            var rawPrecision = Byte();
            var rawScale = Byte();
            var isMax = Boolean();
            var rawLength = Int32();
            if (rawLength < -1) throw new StorageFormatException("Invalid SQL type length.");
            var rawDimensions = UInt16();
            var rawVectorType = Byte();
            var rawXmlContentKind = Byte();
            var collation = String();
            SqlXmlSchemaCollection? xmlSchema = null;
            if (Boolean())
            {
                var schemaName = String();
                var collectionName = String();
                if (_xmlSchemas is null || !_xmlSchemas.TryGetValue((schemaName, collectionName), out xmlSchema))
                    throw new StorageFormatException($"Typed XML references unknown collection [{schemaName}].[{collectionName}].");
            }
            var userTypeKind = (SqlUserTypeKind)Byte();
            if (!Enum.IsDefined(userTypeKind)) throw new StorageFormatException("Unknown SQL user-defined type kind.");
            string? userSchema = null, userName = null, assemblyName = null, className = null;
            SqlType? baseType = null;
            bool? userNullable = null;
            SqlClrSerializationFormat? serializationFormat = null;
            int? maxByteSize = null;
            var byteOrdered = false;
            var fixedLength = false;
            string? validationMethodName = null;
            if (userTypeKind == SqlUserTypeKind.Alias)
            {
                userSchema = String();
                userName = String();
                userNullable = Boolean();
                baseType = Type();
            }
            else if (userTypeKind == SqlUserTypeKind.Clr)
            {
                userSchema = String();
                userName = String();
                assemblyName = String();
                className = String();
                serializationFormat = (SqlClrSerializationFormat)Byte();
                if (!Enum.IsDefined(serializationFormat.Value))
                    throw new StorageFormatException("Unknown CLR serialization format.");
                var rawMaxByteSize = Int32();
                maxByteSize = rawMaxByteSize == int.MinValue ? null : rawMaxByteSize;
                byteOrdered = Boolean();
                fixedLength = Boolean();
                var rawValidationMethod = String();
                validationMethodName = rawValidationMethod.Length == 0 ? null : rawValidationMethod;
            }
            try
            {
                return SqlType.Restore(name,
                    rawPrecision == byte.MaxValue ? null : rawPrecision,
                    rawScale == byte.MaxValue ? null : rawScale,
                    rawLength < 0 ? null : rawLength,
                    isMax,
                    collation.Length == 0 ? null : collation,
                    rawDimensions == 0 ? null : rawDimensions,
                    rawVectorType == byte.MaxValue ? null : (SqlVectorBaseType)rawVectorType,
                    rawXmlContentKind == 0 ? null : (SqlXmlContentKind)rawXmlContentKind,
                    xmlSchema, userTypeKind, userSchema, userName, baseType, userNullable,
                    assemblyName, className, serializationFormat, maxByteSize, byteOrdered, fixedLength,
                    validationMethodName);
            }
            catch (ArgumentException exception)
            {
                throw new StorageFormatException("Invalid SQL type declaration.", exception);
            }
        }
        private void Require(int length) { if (length < 0 || _offset > _source.Length - length) throw new StorageFormatException("Bootstrap catalog record is truncated."); }
    }
}
