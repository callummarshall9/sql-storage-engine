// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Pages;

namespace sql_storage_engine.UnitTests;
public sealed class PageDatabaseTests
{
    [Test]
    public async Task CreateWriteCloseAndOpen_PreservesCompletePages()
    {
        var(directory, path) = NewPath();
        try
        {
            PageId id;
            byte[] expected;
            await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                id = await database.AllocateAsync(PageType.Heap);
                expected = CreatePage(id,PageType.Heap,database.GetPageSize(),77);
                await database.WriteAsync(id,expected);
                await database.FlushAsync();
            }

            await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure reopened = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path);
            var actual = new byte[reopened.GetPageSize()];
            await reopened.ReadAsync(id,actual);
            actual.Should().Equal(expected);
            var beyond = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = reopened.ReadHeader().NextPageId.Value + 10
            };
            await ((Func<Task>)(async () => await reopened.ReadAsync(beyond,actual))).Should().ThrowAsync<StorageFormatException>();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task Create_RefusesExistingFileWithoutChangingIt()
    {
        var(directory, path) = NewPath();
        try
        {
            await File.WriteAllTextAsync(path, "existing");
            await ((Func<Task>)(async () => await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))).Should().ThrowAsync<IOException>();
            (await File.ReadAllTextAsync(path)).Should().Be("existing");
            Directory.GetFiles(directory).Should().ContainSingle();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task Open_InvalidDatabase_DoesNotModifyIt()
    {
        var(directory, path) = NewPath();
        try
        {
            var invalid = Enumerable.Repeat((byte)0x5a, sql_storage_engine.Models.Pages.PageConstants.DefaultSize).ToArray();
            await File.WriteAllBytesAsync(path, invalid);
            await ((Func<Task>)(async () => await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path))).Should().ThrowAsync<InvalidDatabaseMagicException>();
            (await File.ReadAllBytesAsync(path)).Should().Equal(invalid);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task FreeAndReopen_ReusesPagesBeforeExtendingFile()
    {
        var(directory, path) = NewPath();
        try
        {
            PageId first;
            PageId second;
            await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                first = await database.AllocateAsync(PageType.Heap);
                second = await database.AllocateAsync(PageType.Catalog);
                await database.FreeAsync(first);
            }

            var lengthBefore = new FileInfo(path).Length;
            await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure reopened = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path);
            (await reopened.AllocateAsync(PageType.Overflow)).Should().Be(first);
            new FileInfo(path).Length.Should().Be(lengthBefore);
            await ((Func<Task>)(async () => await reopened.FreeAsync(new sql_storage_engine.Models.Identifiers.PageId { Value = reopened.ReadHeader().NextPageId.Value + 1 }))).Should().ThrowAsync<StorageResourceException>();
            second.Should().NotBe(first);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task Free_TwiceFailsAndCyclicFreeListIsDetectedOnReopen()
    {
        var(directory, path) = NewPath();
        try
        {
            PageId first;
            PageId second;
            await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                first = await database.AllocateAsync(PageType.Heap);
                second = await database.AllocateAsync(PageType.Heap);
                await database.FreeAsync(first);
                await ((Func<Task>)(async () => await database.FreeAsync(first))).Should().ThrowAsync<StorageResourceException>();
                await database.FreeAsync(second);
                var firstPage = new byte[database.GetPageSize()];
                await database.ReadAsync(first,firstPage);
                firstPage[sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength] = 1;
                BinaryPrimitives.WriteUInt64LittleEndian(firstPage.AsSpan(sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength + 1), second.Value);
                PageChecksumExposure.WriteChecksum(firstPage,database.GetPageSize());
                await database.WriteAsync(first,firstPage);
                await database.FlushAsync();
            }

            await ((Func<Task>)(async () => await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path))).Should().ThrowAsync<StorageCorruptionException>();
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task Free_RejectsCorruptAllocatedPageWithoutPublishingItToTheFreeList()
    {
        var(directory, path) = NewPath();
        try
        {
            PageId id;
            await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                id = await database.AllocateAsync(PageType.Heap);
                var original = new byte[database.GetPageSize()];
                await database.ReadAsync(id,original);
                var corrupt = original.ToArray();
                corrupt[sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength + 10] ^= 0xff;
                await database.WriteAsync(id, corrupt);
                await ((Func<Task>)(async () => await database.FreeAsync(id))).Should().ThrowAsync<StorageCorruptionException>();
                database.ReadHeader().FreeListHeadPageId.Should().BeNull();
                await database.WriteAsync(id,original);
                await database.FreeAsync(id);
            }

            await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure reopened = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path);
            (await reopened.AllocateAsync(PageType.Heap)).Should().Be(id);
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public async Task RandomizedAllocation_AgreesWithOwnershipModelAcrossReopen()
    {
        const int seed = 1701;
        var random = new Random(seed);
        var(directory, path) = NewPath();
        var live = new HashSet<PageId>();
        try
        {
            await using (sql_storage_engine.Exposures.Pages.PageDatabaseExposure database = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.CreatePageDatabaseExposureAsync(path))
            {
                for (var operation = 0; operation < 200; operation++)
                {
                    if (live.Count > 0 && random.Next(3) == 0)
                    {
                        var id = live.ElementAt(random.Next(live.Count));
                        await database.FreeAsync(id);
                        live.Remove(id);
                    }
                    else
                    {
                        var id = await database.AllocateAsync(PageType.Heap);
                        live.Add(id).Should().BeTrue($"seed {seed}, operation {operation}");
                    }
                }
            }

            await using sql_storage_engine.Exposures.Pages.PageDatabaseExposure reopened = await sql_storage_engine.Exposures.Pages.PageDatabaseExposure.OpenAsync(path);
            var newlyAllocated = await reopened.AllocateAsync(PageType.Heap);
            live.Contains(newlyAllocated).Should().BeFalse($"seed {seed}");
        }
        finally
        {
            Directory.Delete(directory, true);
        }
    }

    [Test]
    public void PageOffset_RejectsArithmeticOverflow()
    {
        ((Action)(() => sql_storage_engine.Exposures.Pages.PageAddressExposure.CalculatePageAddress(pageId: new sql_storage_engine.Models.Identifiers.PageId { Value = ulong.MaxValue },pageSize: sql_storage_engine.Models.Pages.PageConstants.DefaultSize))).Should().Throw<OverflowException>();
    }

    private static byte[] CreatePage(PageId id, PageType type, int size, byte payload)
    {
        var page = new byte[size];
        sql_storage_engine.Exposures.Pages.PageHeaderExposure.WritePageHeader(page,new PageHeader { PageId = id, PageType = type, FormatVersion = new sql_storage_engine.Models.Pages.PageFormatVersion { Value = sql_storage_engine.Models.Pages.PageHeaderFormat.CurrentVersion }, PageLogSequenceNumber = new sql_storage_engine.Models.Identifiers.LogSequenceNumber { Value = 0 }, ChecksumAlgorithm = PageChecksumAlgorithm.Crc32, Checksum = 0 });
        page[^1] = payload;
        PageChecksumExposure.WriteChecksum(page,size);
        return page;
    }

    private static (string Directory, string Path) NewPath()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(),$"sql-storage-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(directory);
        return (directory, System.IO.Path.Combine(directory,"database.sse"));
    }
}