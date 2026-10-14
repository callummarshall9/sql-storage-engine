// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Orchestrations.LeafIndexPages;
internal sealed partial class LeafIndexPageOrchestrationService
{
    private static void ValidateModel(LeafIndexPage model)
    {
        ArgumentNullException.ThrowIfNull(argument: model);
        ArgumentNullException.ThrowIfNull(argument: model.Entries);

        if (model.PageId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(model));
        }

        if (new[]
        {
            model.ParentPageId,
            model.PreviousPageId,
            model.NextPageId
        }.Any(predicate: link => link?.Value == 0 || link == model.PageId))
        {
            throw new ArgumentException("Leaf links cannot reference page zero or self.", nameof(model));
        }

        if (model.PreviousPageId is not null && model.PreviousPageId == model.NextPageId)
        {
            throw new ArgumentException("Previous and next links cannot be equal.", nameof(model));
        }

        for (var index = 1; index < model.Entries.Count; index++)
        {
            if (indexKeys.CompareIndexKeys(indexKeys: [model.Entries[index - 1].Key, model.Entries[index].Key]) > 0)
            {
                throw new ArgumentException("Leaf entries must be ordered.", nameof(model));
            }
        }

        if (model.Entries.Any(predicate: entry => entry.RowId.PageId.Value == 0))
        {
            throw new ArgumentException("Leaf RowId cannot reference page zero.", nameof(model));
        }

        if (model.Entries.Any(predicate: entry => leafEntries.ReadLeafIndexEntryPayload(leafIndexEntry: entry)
            .Length > ushort.MaxValue))
        {
            throw new ArgumentException("Leaf included payload exceeds 65,535 bytes.", nameof(model));
        }
    }

    private static void ValidateLeafIndexPageCapacity(int pageSize, LeafIndexPage leafIndexPage)
    {
        ArgumentNullException.ThrowIfNull(argument: leafIndexPage);
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)), "Unsupported index page size."), (leafIndexPage.CapacityEntries is null && leafIndexPage.Entries is null, "Capacity values are required.")]);
    }

    private static void ValidateLeafIndexPageOnWrite(Span<byte> page, LeafIndexPage leafIndexPage)
    {
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(page.Length)), "Unsupported index page size.")]);
        ValidateModel(model: leafIndexPage);
    }

    private static void ValidateLeafIndexPageSource(ReadOnlySpan<byte> page, ulong expectedPageId) =>
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(page.Length)), "Unsupported index page size."), (expectedPageId == 0, "Index pages cannot use page zero.")]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string Message)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: input.Message);
            }
        }
    }

    private static void ValidateLeafIndexPageTypeSource(ReadOnlySpan<byte> page) =>
        Validate(inputs: [(page.Length < sql_storage_engine.Models.Pages.PageHeaderFormat.EncodedLength, "Page header is truncated.")]);
}