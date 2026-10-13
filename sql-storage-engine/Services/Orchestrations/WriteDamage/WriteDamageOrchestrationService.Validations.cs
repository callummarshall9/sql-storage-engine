// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Orchestrations.WriteDamage;
internal sealed partial class WriteDamageOrchestrationService
{
    private static void ValidatePageSources(ReadOnlySpan<byte> page, ReadOnlySpan<byte> verifiedFullPageImage) =>
        Validate(inputs: [(!System.Enum.IsDefined(value: (sql_storage_engine.Models.Pages.PageSize)(page.Length)), nameof(page)), (!verifiedFullPageImage.IsEmpty && verifiedFullPageImage.Length != page.Length, nameof(verifiedFullPageImage))]);

    private WalDamagePolicy ValidateWalDamagePolicy(ReadOnlySpan<byte> bytes)
    {
        try
        {
            return walRecords.ReadWalReadResult(source: bytes)
            .HasIncompleteTail ? WalDamagePolicy.TruncateIncompleteTail : WalDamagePolicy.Complete;
        }
        catch (StorageCorruptionException)
        {
            return WalDamagePolicy.StopForCorruption;
        }
        catch (StorageFormatException)
        {
            return WalDamagePolicy.StopForCorruption;
        }
    }

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