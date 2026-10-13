// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System.Threading;
using System.Threading.Tasks;
using cCoder.CodeAnalysis.Exposures;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Storage;
using sql_storage_engine.Services.Coordinations.EngineDurabilities;

namespace sql_storage_engine.Exposures.Transactions;
internal sealed class StorageTransactionFilesExposure : ICompositionExposure
{
    private StorageTransactionFilesExposure()
    {
    }

    private static readonly EngineDurabilityResourceCoordinationService service = EngineDurabilityResourceCoordinationService.CreateEngineDurabilityResourceCoordinationService();
    public static string JournalPath(string path) =>
        service.ReadTransactionJournalPathEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = path })
            .TextResult!;

    public static string ActivePath(string path) =>
        service.ReadTransactionActivePathEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = path })
            .TextResult!;

    public static async ValueTask WriteAsync<T>(string path, T value, CancellationToken token)
    {
        _ = await service.WriteMetadataEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = path, Metadata = value }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public static async ValueTask<StorageTransactionReceipt> ResolveAsync(string path, StorageTransactionIdentity storageTransactionIdentity, CancellationToken token) =>
        (await service.ResolveReceiptEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = path, Receipt = new StorageTransactionReceipt { Identity = storageTransactionIdentity } }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false)).Receipt!;

    public static async ValueTask WriteReceiptAsync(string path, StorageTransactionReceipt storageTransactionReceipt, CancellationToken token)
    {
        _ = await service.WriteReceiptEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = path, Receipt = storageTransactionReceipt }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);
    }

    public static void ReleaseReceipt(string path, StorageTransactionIdentity storageTransactionIdentity) =>
        service.ReleaseReceiptEngineResourceContext(engineResourceContext: new EngineResourceContext { Path = path, Receipt = new StorageTransactionReceipt { Identity = storageTransactionIdentity } });

    public static async ValueTask RecoverAsync(string path, CancellationToken token)
    {
        _ = await service.RecoverEngineResourceContextAsync(engineResourceContext: new EngineResourceContext { Path = path }, cancellationToken: token)
            .ConfigureAwait(continueOnCapturedContext: false);
    }
}