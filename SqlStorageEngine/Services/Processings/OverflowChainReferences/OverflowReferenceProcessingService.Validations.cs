// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Overflow;

using sql_storage_engine.Storage;

namespace sql_storage_engine.Services.Processings.OverflowChainReferences;
internal sealed partial class OverflowReferenceProcessingService
{
    private static void ValidateOverflowReference(OverflowReference overflowReference)
    {
        if (overflowReference.FirstPageId.Value == 0 || overflowReference.TotalLength <= 0 || overflowReference.TotalLength > OverflowReferenceFormat.MaximumValueLength)
        {
            throw new sql_storage_engine.Storage.StorageFormatException(message: "Overflow reference is outside the supported bounds.");
        }
    }
}