// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Linq;
using sql_storage_engine.Models.Indexes;

namespace sql_storage_engine.Services.Orchestrations.InternalIndexPages;
internal sealed partial class InternalIndexPageOrchestrationService
{
    private static void ValidateModel(InternalIndexPage model)
    {
        ArgumentNullException.ThrowIfNull(argument: model);
        ArgumentNullException.ThrowIfNull(argument: model.Separators);

        if (model.PageId.Value == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(model));
        }

        if (model.ParentPageId?.Value == 0 || model.ParentPageId == model.PageId)
        {
            throw new ArgumentException("Invalid parent page ID.", nameof(model));
        }

        if (model.Separators.Count == 0 || model.Children.Count != model.Separators.Count + 1)
        {
            throw new ArgumentException("Internal child count must equal nonzero separator count plus one.", nameof(model));
        }

        for (var index = 1; index < model.Separators.Count; index++)
        {
            if (indexKeys.CompareIndexKeys(indexKeys: [model.Separators[index - 1], model.Separators[index]]) > 0)
            {
                throw new ArgumentException("Separators must be ordered.", nameof(model));
            }
        }

        if (model.Children.Any(predicate: child => child.Value == 0 || child == model.PageId) || model.Children.Distinct()
            .Count() != model.Children.Count)
        {
            throw new ArgumentException("Child page IDs must be unique, nonzero, and not self-referential.", nameof(model));
        }
    }

    private static void ValidateInternalIndexPageCapacity(int pageSize, InternalIndexPage internalIndexPage)
    {
        ArgumentNullException.ThrowIfNull(argument: internalIndexPage);
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(pageSize)), "Unsupported index page size."), (internalIndexPage.CapacitySeparators is null && internalIndexPage.Separators is null, "Capacity values are required.")]);
    }

    private static void ValidateInternalIndexPageOnWrite(Span<byte> page, InternalIndexPage internalIndexPage)
    {
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(page.Length)), "Unsupported index page size.")]);
        ValidateModel(model: internalIndexPage);
    }

    private static void ValidateInternalIndexPageSource(ReadOnlySpan<byte> page, ulong expectedPageId) =>
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
}