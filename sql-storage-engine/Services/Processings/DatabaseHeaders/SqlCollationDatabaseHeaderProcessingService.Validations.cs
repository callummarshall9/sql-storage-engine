// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.DatabaseHeaders;
internal sealed partial class SqlCollationDatabaseHeaderProcessingService
{
    private static void ValidateDatabaseHeaderOnWrite(Span<byte> page, DatabaseHeader databaseHeader)
    {
        ArgumentNullException.ThrowIfNull(argument: databaseHeader);

        if (page.Length != databaseHeader.PageSize)
        {
            throw new ArgumentException("Buffer length must equal the database page size.", nameof(page));
        }

        if (!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(databaseHeader.PageSize)))
        {
            throw new InvalidPageSizeException(databaseHeader.PageSize);
        }

        if (databaseHeader.FormatVersion != sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion)
        {
            throw new UnsupportedDatabaseVersionException(databaseHeader.FormatVersion);
        }

        if (databaseHeader.NextPageId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(databaseHeader), "Next page ID must follow page zero.");
        }

        if (databaseHeader.NextRowVersion == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(databaseHeader), "Next rowversion must be nonzero.");
        }

        if (databaseHeader.NextGeneratedSequence == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(databaseHeader), "Next generated sequence must be nonzero.");
        }

        sqlCollations.CheckSqlCollationName(name: databaseHeader.DefaultCollation);

        if (sqlCollations.CountSqlCollationUtf8(value: databaseHeader.DefaultCollation) > DatabaseHeaderFormat.MaximumCollationNameBytes)
        {
            throw new ArgumentOutOfRangeException(nameof(databaseHeader), "Default collation name is too long.");
        }
    }

    private static void ValidateDatabaseHeaderSource(ReadOnlySpan<byte> page) =>
        Validate(inputs: [(page.Length < sql_storage_engine.Models.Pages.PageConstants.MinimumSize, "Database header page is truncated.")]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new StorageFormatException(message: input.Message);
            }
        }
    }
}