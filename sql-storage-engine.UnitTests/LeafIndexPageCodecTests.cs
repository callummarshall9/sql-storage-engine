// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Models.Indexes;
using sql_storage_engine.Exposures.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class LeafIndexPageCodecTests
{
    [Test]
    public void LeafEntriesLinksAndDuplicates_RoundTrip()
    {
        var model = new LeafIndexPage
        {
            PageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 5
            },
            ParentPageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 2
            },
            PreviousPageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 3
            },
            NextPageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 8
            },
            Entries = new[]
            {
                sql_storage_engine.Exposures.Indexes.LeafIndexEntryExposure.CreateLeafIndexEntry(indexKey: sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new byte[] { 1 }),rowId: new sql_storage_engine.Models.Identifiers.RowId { PageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 10 }, SlotId = new sql_storage_engine.Models.Identifiers.SlotId { Value = 1 }, Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration { Value = 2 } },includedPayload: new byte[] { 4, 5, 6 }),
                Entry(1,11,2,3),
                Entry(9,12,3,4)
            }
        };
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(page,model);
        var actual = sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(page,(model.PageId).Value);
        actual.ParentPageId.Should().Be(model.ParentPageId);
        actual.PreviousPageId.Should().Be(model.PreviousPageId);
        actual.NextPageId.Should().Be(model.NextPageId);
        actual.Entries.Should().Equal(model.Entries);
    }

    [Test]
    public void EmptyLeaf_RoundTripsAndMalformedOffsetsAndRowIdsAreRejected()
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        var empty = new LeafIndexPage
        {
            PageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 1
            },
            ParentPageId = null,
            PreviousPageId = null,
            NextPageId = null,
            Entries = Array.Empty<sql_storage_engine.Models.Indexes.LeafIndexEntry>()
        };
        sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(page,empty);
        sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(page,(empty.PageId).Value).Entries.Should().BeEmpty();
        var model = new LeafIndexPage
        {
            PageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 5
            },
            ParentPageId = null,
            PreviousPageId = null,
            NextPageId = null,
            Entries = new[]
            {
                Entry(1,2,0,0)
            }
        };
        sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(page,model);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Indexes.LeafIndexPageFormat.HeaderLength), uint.MaxValue);
        PageChecksumExposure.WriteChecksum(page,page.Length);
        ((Action)(() => sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(page,(model.PageId).Value))).Should().Throw<StorageCorruptionException>();
        sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(page,model);
        BinaryPrimitives.WriteUInt64LittleEndian(page.AsSpan(sql_storage_engine.Models.Indexes.LeafIndexPageFormat.HeaderLength + 8), 0);
        PageChecksumExposure.WriteChecksum(page,page.Length);
        ((Action)(() => sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.ReadLeafIndexPage(page,(model.PageId).Value))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void UnorderedEntriesAreRejectedBeforeEncoding()
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        var model = new LeafIndexPage
        {
            PageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 5
            },
            ParentPageId = null,
            PreviousPageId = null,
            NextPageId = null,
            Entries = new[]
            {
                Entry(2,2,0,0),
                Entry(1,3,0,0)
            }
        };
        ((Action)(() => sql_storage_engine.Exposures.Indexes.LeafIndexPageExposure.WriteLeafIndexPage(page,model))).Should().Throw<ArgumentException>();
    }

    private static sql_storage_engine.Models.Indexes.LeafIndexEntry Entry(byte key, ulong page, ushort slot, uint generation) => sql_storage_engine.Exposures.Indexes.LeafIndexEntryExposure.CreateLeafIndexEntry(indexKey: sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new[] { key }),rowId: new sql_storage_engine.Models.Identifiers.RowId { PageId = new sql_storage_engine.Models.Identifiers.PageId { Value = page }, SlotId = new sql_storage_engine.Models.Identifiers.SlotId { Value = slot }, Generation = new sql_storage_engine.Models.Identifiers.SlotGeneration { Value = generation } });
}