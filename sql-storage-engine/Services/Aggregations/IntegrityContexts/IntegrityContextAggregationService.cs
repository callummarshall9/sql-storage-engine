// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Diagnostics;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Services.Orchestrations.IntegrityPageSources;
using sql_storage_engine.Services.Orchestrations.HeapPageLayouts;
using sql_storage_engine.Services.Orchestrations.LeafIndexPages;
using sql_storage_engine.Services.Orchestrations.InternalIndexPages;

namespace sql_storage_engine.Services.Aggregations.IntegrityContexts;
internal sealed partial class IntegrityContextAggregationService(IntegrityPageSourceOrchestrationService source, HeapPageLayoutOrchestrationService heap, LeafIndexPageOrchestrationService leaf, InternalIndexPageOrchestrationService branches) : IIntegrityContextAggregationService
{
    internal static IntegrityContextAggregationService CreateIntegrityContextAggregationService() =>
        new IntegrityContextAggregationService(source: IntegrityPageSourceOrchestrationService.CreateIntegrityPageSourceOrchestrationService(), heap: new HeapPageLayoutOrchestrationService(), leaf: new LeafIndexPageOrchestrationService(), branches: new InternalIndexPageOrchestrationService());

    public ValueTask<IntegrityContext> CheckIntegrityContextAsync(IntegrityContext integrityContext, CancellationToken cancellationToken) =>
        TryCatch(operation: async () =>
    {
        ValidateIntegrityContext(integrityContext: integrityContext, cancellationToken: cancellationToken);
        var header = integrityContext.DatabaseHeader!;
        var maximumPages = integrityContext.MaximumPages;
        var crossCheck = integrityContext.CrossCheck;
        ArgumentNullException.ThrowIfNull(argument: header);

        if (header.NextPageId.Value > (ulong)maximumPages)
        {
            return integrityContext with
            {
                Report = new IntegrityReport
                {
                    Findings = [new IntegrityFinding
                    {
                        Code = "TRAVERSAL_LIMIT",
                        PageId = null,
                        Message = "Allocated page count exceeds the integrity traversal bound."
                    }

                    ],
                    IsHealthy = false
                }
            };
        }

        var findings = new List<IntegrityFinding>();
        var freePages = new HashSet<PageId>();

        for (ulong value = 0; value < header.NextPageId.Value; value++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            var pageId = new sql_storage_engine.Models.Identifiers.PageId
            {
                Value = value
            };

            try
            {
                var result = await source.ReadIntegrityContextAsync(integrityContext: integrityContext with { PageId = pageId.Value }, cancellationToken: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

                var page = result.Page!;
                var common = result.PageHeader!;
                ValidateTypedPage(page: page, header: common);

                if (common.PageType == PageType.Free)
                {
                    freePages.Add(item: pageId);
                }
            }
            catch (StorageCorruptionException exception)
            {
                findings.Add(item: new sql_storage_engine.Models.Diagnostics.IntegrityFinding { Code = "PAGE_CORRUPTION", PageId = pageId, Message = exception.Message });
            }
            catch (StorageFormatException exception)
            {
                findings.Add(item: new sql_storage_engine.Models.Diagnostics.IntegrityFinding { Code = "PAGE_FORMAT", PageId = pageId, Message = exception.Message });
            }
        }

        await ValidateFreeListAsync(integrityContext: integrityContext, header: header, freePages: freePages, findings: findings, token: cancellationToken)
            .ConfigureAwait(continueOnCapturedContext: false);

        if (crossCheck is not null)
        {
            foreach (var row in crossCheck.HeapRows.Except(second: crossCheck.IndexRows)
            .OrderBy(keySelector: row => row.PageId.Value))
            {
                findings.Add(item: new sql_storage_engine.Models.Diagnostics.IntegrityFinding { Code = "INDEX_ENTRY_MISSING", PageId = row.PageId, Message = $"No index entry references {row}." });
            }

            foreach (var row in crossCheck.IndexRows.Except(second: crossCheck.HeapRows)
            .OrderBy(keySelector: row => row.PageId.Value))
            {
                findings.Add(item: new sql_storage_engine.Models.Diagnostics.IntegrityFinding { Code = "INDEX_ENTRY_STALE", PageId = row.PageId, Message = $"Index entry references absent {row}." });
            }
        }

        return integrityContext with
        {
            Report = new IntegrityReport
            {
                Findings = findings.AsReadOnly(),
                IsHealthy = findings.Count == 0
            }
        };
    });

    private void ValidateTypedPage(byte[] page, PageHeader header)
    {
        switch (header.PageType)
        {
            case PageType.Heap:
                _ = heap.BindHeapPageLayout(page: page, expectedPageId: header.PageId.Value);
                break;
            case PageType.BPlusTreeLeaf:
                _ = leaf.ReadLeafIndexPage(page: page, expectedPageId: (header.PageId).Value);
                break;
            case PageType.BPlusTreeInternal:
                _ = branches.ReadInternalIndexPage(page: page, expectedPageId: (header.PageId).Value);
                break;
        }
    }

    private async ValueTask ValidateFreeListAsync(IntegrityContext integrityContext, DatabaseHeader header, HashSet<PageId> freePages, List<IntegrityFinding> findings, CancellationToken token)
    {
        var seen = new HashSet<PageId>();
        var current = header.FreeListHeadPageId;

        while (current is { } pageId)
        {
            if (!seen.Add(item: pageId))
            {
                findings.Add(item: new sql_storage_engine.Models.Diagnostics.IntegrityFinding { Code = "FREE_LIST_CYCLE", PageId = pageId, Message = "Free-list cycle detected." });
                break;
            }

            if (!freePages.Contains(item: pageId))
            {
                findings.Add(item: new sql_storage_engine.Models.Diagnostics.IntegrityFinding { Code = "FREE_LIST_OWNERSHIP", PageId = pageId, Message = "Free list references a non-free page." });
                break;
            }

            var result = await source.ReadRawIntegrityContextAsync(integrityContext: integrityContext with { PageId = pageId.Value }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);

            var page = result.Page!;

            current = page[sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength] switch
            {
                0 => null,
                1 => new sql_storage_engine.Models.Identifiers.PageId
                {
                    Value = BinaryPrimitives.ReadUInt64LittleEndian(source: page.AsSpan(start: sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength + 1))
                },
                _ => null
            };
        }

        foreach (var orphan in freePages.Except(second: seen)
            .OrderBy(keySelector: id => id.Value))
        {
            findings.Add(item: new sql_storage_engine.Models.Diagnostics.IntegrityFinding { Code = "FREE_PAGE_UNREACHABLE", PageId = orphan, Message = "Free page is not reachable from the free-list head." });
        }
    }
}