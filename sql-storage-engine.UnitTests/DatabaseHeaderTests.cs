// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class DatabaseHeaderTests
{
    [Test]
    public void Codec_RoundTripsDatabaseHeaderWithoutMutatingInputOnRead()
    {
        var expected = Header();
        var page = new byte[expected.PageSize];
        sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.WriteDatabaseHeader(page,expected);
        var snapshot = page.ToArray();
        sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.ReadDatabaseHeader(page).Should().Be(expected);
        page.Should().Equal(snapshot);
        Convert.ToHexString(page.AsSpan(32, 24)).Should().Be("53514C53544F524500112233445566778899AABBCCDDEEFF");
    }

    [Test]
    public void Codec_ReportsMagicChecksumPageSizeAndVersionSeparately()
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.WriteDatabaseHeader(page,Header());
        page[32] ^= 1;
        ((Action)(() => sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.ReadDatabaseHeader(page))).Should().Throw<InvalidDatabaseMagicException>();
        sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.WriteDatabaseHeader(page,Header());
        page[^1] = 1;
        ((Action)(() => sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.ReadDatabaseHeader(page))).Should().Throw<StorageCorruptionException>();
        sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.WriteDatabaseHeader(page,Header());
        page[60] = 1;
        ((Action)(() => sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.ReadDatabaseHeader(page))).Should().Throw<InvalidPageSizeException>();
        sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.WriteDatabaseHeader(page,Header());
        page[56] = 99;
        ((Action)(() => sql_storage_engine.Exposures.Pages.DatabaseHeaderExposure.ReadDatabaseHeader(page))).Should().Throw<UnsupportedDatabaseVersionException>();
    }

    private static DatabaseHeader Header() => new sql_storage_engine.Models.Pages.DatabaseHeader
    {
        DatabaseId = new sql_storage_engine.Models.Identifiers.DatabaseId
        {
            Value = Guid.Parse("00112233-4455-6677-8899-aabbccddeeff")
        },
        PageSize = sql_storage_engine.Models.Pages.PageConstants.DefaultSize,
        FormatVersion = sql_storage_engine.Models.Pages.DatabaseHeaderFormat.CurrentVersion,
        CatalogRootPageId = new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = 1
        },
        FreeListHeadPageId = null,
        NextTableId = new sql_storage_engine.Models.Identifiers.TableId
        {
            Value = 2
        },
        NextIndexId = new sql_storage_engine.Models.Identifiers.IndexId
        {
            Value = 3
        },
        NextTransactionId = new sql_storage_engine.Models.Identifiers.TransactionId
        {
            Value = 4
        },
        NextPageId = new sql_storage_engine.Models.Identifiers.PageId
        {
            Value = 5
        },
        IsCleanShutdown = true,
        NextRowVersion = 1,
        DefaultCollation = "SQL_Latin1_General_CP1_CI_AS",
        NextGeneratedSequence = 1
    };
}