// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Text;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Foundations.SqlCollations;

namespace sql_storage_engine.Services.Processings.DatabaseHeaders;
internal sealed partial class SqlCollationDatabaseHeaderProcessingService : ISqlCollationDatabaseHeaderProcessingService
{
    public void WriteDatabaseHeaderMetadata(Span<byte> page, DatabaseHeader databaseHeader) =>
        TryCatch(operation: static context =>
    {
        ValidateDatabaseHeaderOnWrite(page: context.page, databaseHeader: context.databaseHeader);
        WriteCore(page: context.page, header: context.databaseHeader);
    }, context: new WritableDatabaseHeaderContext { page = page, databaseHeader = databaseHeader });

    public DatabaseHeader ReadDatabaseHeaderMetadata(ReadOnlySpan<byte> page) =>
        TryCatch(operation: static context =>
    {
        ValidateDatabaseHeaderSource(page: context.page);
        return ReadCore(page: context.page);
    }, context: new ReadableDatabaseHeaderContext { page = page });
    private static readonly byte[] Magic = "SQLSTORE"u8.ToArray();
    private static readonly ISqlCollationService sqlCollations = new SqlCollationService();
    private static void WriteCore(Span<byte> page, DatabaseHeader header)
    {
        page.Clear();
        var data = page[DatabaseHeaderFormat.PayloadOffset..];
        Magic.CopyTo(destination: data);
        WriteCanonicalGuid(destination: data[8..24], value: header.DatabaseId.Value);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: data[24..], value: header.FormatVersion);
        data[26] = header.IsCleanShutdown ? (byte)1 : (byte)0;
        BinaryPrimitives.WriteInt32LittleEndian(destination: data[28..], value: header.PageSize);
        WriteOptionalPage(destination: data[32..], value: header.CatalogRootPageId);
        WriteOptionalPage(destination: data[41..], value: header.FreeListHeadPageId);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: data[50..], value: header.NextTableId.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: data[58..], value: header.NextIndexId.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: data[66..], value: header.NextTransactionId.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: data[74..], value: header.NextPageId.Value);
        BinaryPrimitives.WriteUInt64LittleEndian(destination: data[82..], value: header.NextRowVersion);
        var collationLength = sqlCollations.CountSqlCollationUtf8(value: header.DefaultCollation);
        BinaryPrimitives.WriteUInt16LittleEndian(destination: data[90..], value: checked((ushort)collationLength));
        sqlCollations.WriteSqlCollationUtf8(value: header.DefaultCollation, destination: data.Slice(start: 92, length: collationLength));
        BinaryPrimitives.WriteUInt64LittleEndian(destination: data[220..], value: header.NextGeneratedSequence);
    }

    private static DatabaseHeader ReadCore(ReadOnlySpan<byte> page)
    {
        if (page.Length < sql_storage_engine.Models.Pages.PageConstants.MinimumSize)
        {
            throw new StorageFormatException("Database header page is truncated.");
        }

        var data = page[DatabaseHeaderFormat.PayloadOffset..];

        if (!data[..8].SequenceEqual(other: Magic))
        {
            throw new InvalidDatabaseMagicException();
        }

        var pageSize = BinaryPrimitives.ReadInt32LittleEndian(source: data[28..]);

        if (!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)) || page.Length != pageSize)
        {
            throw new InvalidPageSizeException(pageSize);
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(source: data[24..]);

        if (version != sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion)
        {
            throw new UnsupportedDatabaseVersionException(version);
        }

        if (data[26] > 1 || data[27] != 0)
        {
            throw new StorageFormatException("Invalid shutdown marker or reserved database-header byte.");
        }

        return new DatabaseHeader
        {
            DatabaseId = new sql_storage_engine.Models.Identifiers.DatabaseId
            {
                Value = ReadCanonicalGuid(source: data[8..24])
            },
            PageSize = pageSize,
            FormatVersion = version,
            CatalogRootPageId = ReadOptionalPage(source: data[32..]),
            FreeListHeadPageId = ReadOptionalPage(source: data[41..]),
            NextTableId = new sql_storage_engine.Models.Identifiers.TableId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: data[50..])
            },
            NextIndexId = new sql_storage_engine.Models.Identifiers.IndexId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: data[58..])
            },
            NextTransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: data[66..])
            },
            NextPageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = BinaryPrimitives.ReadUInt64LittleEndian(source: data[74..])
            },
            IsCleanShutdown = data[26] == 1,
            NextRowVersion = BinaryPrimitives.ReadUInt64LittleEndian(source: data[82..]),
            DefaultCollation = ReadCollation(data: data),
            NextGeneratedSequence = BinaryPrimitives.ReadUInt64LittleEndian(source: data[220..])
        };
    }

    private static string ReadCollation(ReadOnlySpan<byte> data)
    {
        var length = BinaryPrimitives.ReadUInt16LittleEndian(source: data[90..]);

        if (length is 0 or > DatabaseHeaderFormat.MaximumCollationNameBytes)
        {
            throw new StorageFormatException("Invalid default collation length.");
        }

        if (data.Slice(start: 92 + length, length: DatabaseHeaderFormat.MaximumCollationNameBytes - length)
            .ContainsAnyExcept(value: (byte)0))
        {
            throw new StorageFormatException("Reserved default-collation bytes must be zero.");
        }

        try
        {
            var name = sqlCollations.ReadSqlCollationUtf8(value: data.Slice(start: 92, length: length));
            sqlCollations.CheckSqlCollationName(name: name);
            return name;
        }
        catch (Exception exception)when (exception is DecoderFallbackException or ArgumentException)
        {
            throw new StorageFormatException("Invalid database default collation.", exception);
        }
    }

    private static void WriteOptionalPage(Span<byte> destination, PageId? value)
    {
        destination[0] = value.HasValue ? (byte)1 : (byte)0;

        BinaryPrimitives.WriteUInt64LittleEndian(destination: destination[1..], value: value.GetValueOrDefault()
            .Value);
    }

    private static PageId? ReadOptionalPage(ReadOnlySpan<byte> source) =>
        source[0] switch
    {
        0 when BinaryPrimitives.ReadUInt64LittleEndian(source: source[1..]) == 0 => null,
        1 => new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = BinaryPrimitives.ReadUInt64LittleEndian(source: source[1..])
        },
        _ => throw new StorageFormatException("Invalid nullable page identifier encoding.")};
    /// <summary>Writes a GUID using RFC 4122 byte order.</summary>
    private static void WriteCanonicalGuid(Span<byte> destination, Guid value)
    {
        Span<byte> mixed = stackalloc byte[16];
        value.TryWriteBytes(destination: mixed);
        destination[0] = mixed[3];
        destination[1] = mixed[2];
        destination[2] = mixed[1];
        destination[3] = mixed[0];
        destination[4] = mixed[5];
        destination[5] = mixed[4];
        destination[6] = mixed[7];
        destination[7] = mixed[6];
        mixed[8..].CopyTo(destination: destination[8..]);
    }

    private static Guid ReadCanonicalGuid(ReadOnlySpan<byte> source)
    {
        Span<byte> mixed = stackalloc byte[16];
        mixed[0] = source[3];
        mixed[1] = source[2];
        mixed[2] = source[1];
        mixed[3] = source[0];
        mixed[4] = source[5];
        mixed[5] = source[4];
        mixed[6] = source[7];
        mixed[7] = source[6];
        source[8..16].CopyTo(destination: mixed[8..]);
        return new Guid(mixed);
    }
}