// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using sql_storage_engine.Transactions;

namespace sql_storage_engine.Services.Processings.LockRules;
internal interface ILockRulesProcessingService
{
    bool AreCompatible(LockMode first, LockMode second);

    bool CanConvert(LockMode current, LockMode requested);

    void EnsureValidConversion(LockMode current, LockMode requested);
}