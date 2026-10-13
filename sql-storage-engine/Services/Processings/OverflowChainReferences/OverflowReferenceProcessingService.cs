// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Overflow;
using sql_storage_engine.Services.Foundations.OverflowReferences;

namespace sql_storage_engine.Services.Processings.OverflowChainReferences;
internal sealed partial class OverflowReferenceProcessingService(OverflowReferenceService references) : IOverflowReferenceProcessingService
{
    internal static OverflowReferenceProcessingService CreateOverflowReferenceProcessingService() =>
        new OverflowReferenceProcessingService(references: new OverflowReferenceService());

    public void CheckOverflowReference(OverflowReference overflowReference) =>
        TryCatch(operation: () =>
    {
        ValidateOverflowReference(overflowReference: overflowReference);
        references.CheckOverflowReference(overflowReference: overflowReference);
    });
}