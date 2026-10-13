// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
namespace sql_storage_engine.Services.Foundations.TransactionSequences;
internal interface ITransactionSequenceService
{
    ulong NextTransactionSequence();
}