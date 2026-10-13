// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Services.Processings.PageRecoveries;
internal sealed partial class PageHeaderRecoveryProcessingService
{
    private static void ValidatePageRecovery(ReadOnlySpan<byte> page, ReadOnlySpan<byte> verifiedFullPageImage) =>
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(page.Length)), nameof(page)), (!verifiedFullPageImage.IsEmpty && verifiedFullPageImage.Length != page.Length, nameof(verifiedFullPageImage))]);

    private static void Validate(ReadOnlySpan<(bool Invalid, string ParameterName)> inputs)
    {
        foreach (var input in inputs)
        {
            if (input.Invalid)
            {
                throw new ArgumentException(message: "Recovery requires a supported page and an absent or same-size full-page image.", paramName: input.ParameterName);
            }
        }
    }
}