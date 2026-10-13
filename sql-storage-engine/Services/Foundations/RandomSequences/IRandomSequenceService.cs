// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Models.Diagnostics;

namespace sql_storage_engine.Services.Foundations.RandomSequences;
internal interface IRandomSequenceService
{
    RandomSequence GenerateRandomSequence(RandomSequence randomSequence);
}