// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;

namespace sql_storage_engine.Services.Orchestrations.SavepointSessions;
internal interface IStorageSavepointSessionOrchestrationService
{
    StorageSavepointSession PrepareStorageSavepointSession(StorageSavepointSession storageSavepointSession);

    ValueTask<StorageSavepointSession> CreateStorageSavepointSessionAsync(StorageSavepointSession storageSavepointSession, string name, CancellationToken token);

    StorageSavepointSession FindStorageSavepointSession(StorageSavepointSession storageSavepointSession, string name);

    ValueTask RollbackStorageSavepointSessionAsync(StorageSavepointSession storageSavepointSession, CancellationToken token);
}