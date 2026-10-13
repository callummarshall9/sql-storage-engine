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
public sealed class InternalIndexPageCodecTests
{
    [Test]
    public void MinimumInternalPage_RoundTripsAndHasGoldenHeader()
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        var expected = new InternalIndexPage
        {
            PageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 5
            },
            ParentPageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 2
            },
            Separators = new[]
            {
                Key(10)
            },
            Children = new[]
            {
                new sql_storage_engine.Models.Identifiers.PageId
                {
                    Value = 3
                },
                new sql_storage_engine.Models.Identifiers.PageId
                {
                    Value = 4
                }
            }
        };
        sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.WriteInternalIndexPage(page,expected);
        var actual = sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.ReadInternalIndexPage(page,(expected.PageId).Value);
        actual.PageId.Should().Be(expected.PageId);
        actual.ParentPageId.Should().Be(expected.ParentPageId);
        actual.Separators.Should().Equal(expected.Separators);
        actual.Children.Should().Equal(expected.Children);
        Convert.ToHexString(page.AsSpan(32, 32)).Should().Be("0102000000000000000100020050000000FF1F00000000000300000000000000");
    }

    [Test]
    public void EmptyAndMalformedInternalPagesAreRejected()
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        ((Action)(() => sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.WriteInternalIndexPage(page,new InternalIndexPage { PageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, ParentPageId = null, Separators = Array.Empty<sql_storage_engine.Models.Indexes.IndexKey>(), Children = new[] { new sql_storage_engine.Models.Identifiers.PageId { Value = 2 } } }))).Should().Throw<ArgumentException>();
        var model = new InternalIndexPage
        {
            PageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = 5
            },
            ParentPageId = null,
            Separators = new[]
            {
                Key(1)
            },
            Children = new[]
            {
                new sql_storage_engine.Models.Identifiers.PageId
                {
                    Value = 2
                },
                new sql_storage_engine.Models.Identifiers.PageId
                {
                    Value = 3
                }
            }
        };
        sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.WriteInternalIndexPage(page,model);
        BinaryPrimitives.WriteUInt16LittleEndian(page.AsSpan(sql_storage_engine.Models.Indexes.InternalIndexPageFormat.ChildCountOffset), 1);
        PageChecksumExposure.WriteChecksum(page,page.Length);
        ((Action)(() => sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.ReadInternalIndexPage(page,(model.PageId).Value))).Should().Throw<StorageCorruptionException>();
        sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.WriteInternalIndexPage(page,model);
        BinaryPrimitives.WriteUInt32LittleEndian(page.AsSpan(sql_storage_engine.Models.Indexes.InternalIndexPageFormat.HeaderLength), uint.MaxValue);
        PageChecksumExposure.WriteChecksum(page,page.Length);
        ((Action)(() => sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.ReadInternalIndexPage(page,(model.PageId).Value))).Should().Throw<StorageCorruptionException>();
    }

    [Test]
    public void MaximumOccupancy_RoundTripsAndOneMoreEntryDoesNotFit()
    {
        var separators = Enumerable.Range(0, 451).Select(index => sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new[] { (byte)(index / 256), (byte)index })).ToArray();
        var children = Enumerable.Range(1, separators.Length + 1).Select(index => new sql_storage_engine.Models.Identifiers.PageId { Value = (ulong)index + 10 }).ToArray();
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.WriteInternalIndexPage(page,new InternalIndexPage { PageId = new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }, ParentPageId = null, Separators = separators, Children = children });
        sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.ReadInternalIndexPage(page,(new sql_storage_engine.Models.Identifiers.PageId { Value = 1 }).Value).Separators.Should().HaveCount(separators.Length);
        sql_storage_engine.Exposures.Indexes.InternalIndexPageExposure.CheckInternalIndexPageCapacity(page.Length, separators.Append(sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new byte[] { 0xff, 0xff, 0xff }))).Should().BeFalse();
    }

    private static sql_storage_engine.Models.Indexes.IndexKey Key(byte value) => sql_storage_engine.Exposures.Indexes.IndexKeyExposure.CreateIndexKey(bytes: new[] { value });
}