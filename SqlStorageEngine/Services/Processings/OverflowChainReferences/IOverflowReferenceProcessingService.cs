// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Overflow;

namespace sql_storage_engine.Services.Processings.OverflowChainReferences;
internal interface IOverflowReferenceProcessingService
{
    void CheckOverflowReference(OverflowReference overflowReference);
}