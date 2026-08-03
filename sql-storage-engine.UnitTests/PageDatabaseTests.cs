using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class PageDatabaseTests
{
    [Test]
    public async Task CreateWriteCloseAndOpen_PreservesCompletePages()
    {
        var (directory, path) = NewPath();
        try
        {
            PageId id;
            byte[] expected;
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                id = await database.AllocateAsync(PageType.Heap);
                expected = CreatePage(id, PageType.Heap, database.PageSize, 77);
                await database.WriteAsync(id, expected);
                await database.FlushAsync();
            }
            await using var reopened = await PageDatabase.OpenAsync(path);
            var actual = new byte[reopened.PageSize];
            await reopened.ReadAsync(id, actual);
            actual.Should().Equal(expected);
            var beyond = new PageId(reopened.Header.NextPageId.Value + 10);
            await ((Func<Task>)(async () => await reopened.ReadAsync(beyond, actual))).Should().ThrowAsync<StorageFormatException>();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task Create_RefusesExistingFileWithoutChangingIt()
    {
        var (directory, path) = NewPath();
        try
        {
            await File.WriteAllTextAsync(path, "existing");
            await ((Func<Task>)(async () => await PageDatabase.CreateAsync(path))).Should().ThrowAsync<IOException>();
            (await File.ReadAllTextAsync(path)).Should().Be("existing");
            Directory.GetFiles(directory).Should().ContainSingle();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task Open_InvalidDatabase_DoesNotModifyIt()
    {
        var (directory, path) = NewPath();
        try
        {
            var invalid = Enumerable.Repeat((byte)0x5a, PageConstants.DefaultSize).ToArray();
            await File.WriteAllBytesAsync(path, invalid);
            await ((Func<Task>)(async () => await PageDatabase.OpenAsync(path))).Should().ThrowAsync<InvalidDatabaseMagicException>();
            (await File.ReadAllBytesAsync(path)).Should().Equal(invalid);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task FreeAndReopen_ReusesPagesBeforeExtendingFile()
    {
        var (directory, path) = NewPath();
        try
        {
            PageId first;
            PageId second;
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                first = await database.AllocateAsync(PageType.Heap);
                second = await database.AllocateAsync(PageType.Catalog);
                await database.FreeAsync(first);
            }
            var lengthBefore = new FileInfo(path).Length;
            await using var reopened = await PageDatabase.OpenAsync(path);
            (await reopened.AllocateAsync(PageType.Overflow)).Should().Be(first);
            new FileInfo(path).Length.Should().Be(lengthBefore);
            await ((Func<Task>)(async () => await reopened.FreeAsync(new PageId(reopened.Header.NextPageId.Value + 1))))
                .Should().ThrowAsync<StorageResourceException>();
            second.Should().NotBe(first);
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task Free_TwiceFailsAndCyclicFreeListIsDetectedOnReopen()
    {
        var (directory, path) = NewPath();
        try
        {
            PageId first;
            PageId second;
            await using (var database = await PageDatabase.CreateAsync(path))
            {
                first = await database.AllocateAsync(PageType.Heap);
                second = await database.AllocateAsync(PageType.Heap);
                await database.FreeAsync(first);
                await ((Func<Task>)(async () => await database.FreeAsync(first))).Should().ThrowAsync<StorageResourceException>();
                await database.FreeAsync(second);
                var firstPage = new byte[database.PageSize];
                await database.ReadAsync(first, firstPage);
                firstPage[PageHeaderCodec.EncodedLength] = 1;
                BinaryPrimitives.WriteUInt64LittleEndian(firstPage.AsSpan(PageHeaderCodec.EncodedLength + 1), second.Value);
                PageChecksum.WriteChecksum(firstPage, database.PageSize);
                await database.WriteAsync(first, firstPage);
                await database.FlushAsync();
            }
            await ((Func<Task>)(async () => await PageDatabase.OpenAsync(path))).Should().ThrowAsync<StorageCorruptionException>();
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public async Task RandomizedAllocation_AgreesWithOwnershipModelAcrossReopen()
    {
        const int seed = 1701;
        var random = new Random(seed);
        var (directory, path) = NewPath();
        var live = new HashSet<PageId>();
        try
        {
            await using (var database = await PageDatabase.CreateAsync(path))
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
            await using var reopened = await PageDatabase.OpenAsync(path);
            var newlyAllocated = await reopened.AllocateAsync(PageType.Heap);
            live.Contains(newlyAllocated).Should().BeFalse($"seed {seed}");
        }
        finally { Directory.Delete(directory, true); }
    }

    [Test]
    public void PageOffset_RejectsArithmeticOverflow()
    {
        ((Action)(() => PageConstants.GetPageOffset(new PageId(ulong.MaxValue), PageConstants.DefaultSize)))
            .Should().Throw<OverflowException>();
    }

    private static byte[] CreatePage(PageId id, PageType type, int size, byte payload)
    {
        var page = new byte[size];
        PageHeaderCodec.Write(page, new PageHeader(id, type, PageFormatVersion.Current,
            new LogSequenceNumber(0), PageChecksumAlgorithm.Crc32, 0));
        page[^1] = payload;
        PageChecksum.WriteChecksum(page, size);
        return page;
    }

    private static (string Directory, string Path) NewPath()
    {
        var directory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"sql-storage-{Guid.NewGuid():N}");
        System.IO.Directory.CreateDirectory(directory);
        return (directory, System.IO.Path.Combine(directory, "database.sse"));
    }
}
