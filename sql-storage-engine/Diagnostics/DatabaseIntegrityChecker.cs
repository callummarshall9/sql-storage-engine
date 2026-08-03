using System.Buffers.Binary;
using sql_storage_engine.Heap;
using sql_storage_engine.Identifiers;
using sql_storage_engine.Indexes;
using sql_storage_engine.Overflow;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Diagnostics;

public sealed record IntegrityFinding(string Code, PageId? PageId, string Message);
public sealed record IntegrityReport(IReadOnlyList<IntegrityFinding> Findings)
{
    public bool IsHealthy => Findings.Count == 0;
}
public sealed record IntegrityCrossCheck(IReadOnlySet<RowId> HeapRows, IReadOnlySet<RowId> IndexRows);

/// <summary>Performs bounded, cycle-safe, read-only validation of allocated pages and optional index/heap references.</summary>
public sealed class DatabaseIntegrityChecker(int maximumPages = 1_000_000)
{
    public async Task<IntegrityReport> CheckAsync(IPageStore store, DatabaseHeader header,
        IntegrityCrossCheck? crossCheck = null, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(store);
        ArgumentNullException.ThrowIfNull(header);
        if (header.NextPageId.Value > (ulong)maximumPages)
            return new IntegrityReport([new("TRAVERSAL_LIMIT", null, "Allocated page count exceeds the integrity traversal bound.")]);
        var findings = new List<IntegrityFinding>();
        var page = new byte[store.PageSize];
        var freePages = new HashSet<PageId>();
        for (ulong value = 0; value < header.NextPageId.Value; value++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageId = new PageId(value);
            try
            {
                await store.ReadAsync(pageId, page, cancellationToken).ConfigureAwait(false);
                PageChecksum.ValidateChecksum(page, store.PageSize);
                var common = PageHeaderCodec.Read(page);
                common.Validate(pageId);
                ValidateTypedPage(page, common);
                if (common.PageType == PageType.Free) freePages.Add(pageId);
            }
            catch (StorageCorruptionException exception) { findings.Add(new("PAGE_CORRUPTION", pageId, exception.Message)); }
            catch (StorageFormatException exception) { findings.Add(new("PAGE_FORMAT", pageId, exception.Message)); }
        }
        ValidateFreeList(store, header, freePages, findings, cancellationToken);
        if (crossCheck is not null)
        {
            foreach (var row in crossCheck.HeapRows.Except(crossCheck.IndexRows).OrderBy(row => row.PageId.Value))
                findings.Add(new("INDEX_ENTRY_MISSING", row.PageId, $"No index entry references {row}."));
            foreach (var row in crossCheck.IndexRows.Except(crossCheck.HeapRows).OrderBy(row => row.PageId.Value))
                findings.Add(new("INDEX_ENTRY_STALE", row.PageId, $"Index entry references absent {row}."));
        }
        return new IntegrityReport(findings.AsReadOnly());
    }

    private static void ValidateTypedPage(byte[] page, PageHeader header)
    {
        switch (header.PageType)
        {
            case PageType.Heap: _ = new HeapPage(page, header.PageId); break;
            case PageType.BPlusTreeLeaf: _ = LeafIndexPageCodec.Read(page, header.PageId); break;
            case PageType.BPlusTreeInternal: _ = InternalIndexPageCodec.Read(page, header.PageId); break;
            case PageType.Overflow: _ = OverflowPageCodec.ReadHeader(page, header.PageId); break;
        }
    }

    private static void ValidateFreeList(IPageStore store, DatabaseHeader header, HashSet<PageId> freePages,
        List<IntegrityFinding> findings, CancellationToken token)
    {
        var seen = new HashSet<PageId>();
        var current = header.FreeListHeadPageId;
        var page = new byte[store.PageSize];
        while (current is { } pageId)
        {
            if (!seen.Add(pageId)) { findings.Add(new("FREE_LIST_CYCLE", pageId, "Free-list cycle detected.")); break; }
            if (!freePages.Contains(pageId)) { findings.Add(new("FREE_LIST_OWNERSHIP", pageId, "Free list references a non-free page.")); break; }
            store.ReadAsync(pageId, page, token).AsTask().GetAwaiter().GetResult();
            current = page[PageHeaderCodec.EncodedLength] switch
            {
                0 => null,
                1 => new PageId(BinaryPrimitives.ReadUInt64LittleEndian(page.AsSpan(PageHeaderCodec.EncodedLength + 1))),
                _ => null
            };
        }
        foreach (var orphan in freePages.Except(seen).OrderBy(id => id.Value))
            findings.Add(new("FREE_PAGE_UNREACHABLE", orphan, "Free page is not reachable from the free-list head."));
    }
}
