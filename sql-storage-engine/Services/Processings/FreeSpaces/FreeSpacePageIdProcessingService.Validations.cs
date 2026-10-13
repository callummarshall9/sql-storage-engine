// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Configurations;

namespace sql_storage_engine.Services.Processings.FreeSpaces;
internal sealed partial class FreeSpacePageIdProcessingService
{
    private static void ValidateFreeSpaceConfiguration(FreeSpaceConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(argument: configuration);
        Validate(inputs: [(!Enum.IsDefined(value: (PageSize)configuration.PageSize), "Unsupported free-space page size.")]);
    }

    private static void ValidateFreeSpaceRequirement(int requiredBytes) =>
        Validate(inputs: [(requiredBytes < 0, "Required bytes cannot be negative.")]);

    private void ValidateFreeSpacePageIdOnUpdate(ulong pageId, int freeBytes) =>
        Validate(inputs: [(freeBytes < 0 || freeBytes > state.PageSize, "Free bytes must fit within the page.")]);

    private void ValidateFreeSpaceMapState(ulong pageId) =>
        Validate(inputs: [(!Enum.IsDefined(value: (PageSize)state.PageSize), "Invalid free-space map state.")]);

    private void ValidateFreeSpaceCategoryInput(int freeBytes) =>
        Validate(inputs: [(!Enum.IsDefined(value: (PageSize)state.PageSize), "Invalid free-space map state.")]);

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