// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Services.Orchestrations.SavepointSessions;
using sql_storage_engine.Services.Processings.StatementJournals;
using sql_storage_engine.Services.Processings.SavepointFiles;
using sql_storage_engine.Services.Foundations.TransactionDirectories;
using sql_storage_engine.Brokers.Directories;

namespace sql_storage_engine.Exposures.Transactions;
internal sealed class StorageSavepointStoreExposure
{
    private static readonly IStorageSavepointSessionOrchestrationService service = new StorageSavepointSessionOrchestrationService(journals: new TransactionDirectoryStatementJournalProcessingService(transactionDirectoryService: new TransactionDirectoryService(transactionDirectoryBroker: new TransactionDirectoryBroker())), savepointFiles: new TransactionDirectorySavepointProcessingService(new TransactionDirectoryService(new TransactionDirectoryBroker())));
    private readonly StorageSavepointSession session;
    public StorageSavepointStoreExposure(StorageSavepointSession storageSavepointSession)
    {
        session = service.PrepareStorageSavepointSession(storageSavepointSession: storageSavepointSession);
    }

    public async ValueTask<StorageSavepoint> CreateStorageSavepointAsync(string name, CancellationToken token)
    {
        var result = await service.CreateStorageSavepointSessionAsync(storageSavepointSession: session, name: name, token: token)
            .ConfigureAwait(continueOnCapturedContext: false);

        return result.Result!;
    }

    public StorageSavepoint Find(string name) =>
        service.FindStorageSavepointSession(storageSavepointSession: session, name: name)
            .Result!;

    public ValueTask RollbackAsync(StorageSavepoint storageSavepoint, CancellationToken token) =>
        service.RollbackStorageSavepointSessionAsync(storageSavepointSession: session with { RequestedPoint = storageSavepoint }, token: token);
}