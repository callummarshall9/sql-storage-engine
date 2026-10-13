using AwesomeAssertions;
using System.Text.Json;
using sql_storage_engine.Brokers.Directories;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Services.Foundations.TransactionDirectories;
using sql_storage_engine.Services.Processings.TransactionReceipts;

namespace sql_storage_engine.UnitTests;
public sealed class TransactionMetadataPipelineTests
{
    [Test]
    public async Task AtomicPublicationRoundTripsAndLeavesNoTemporaryFiles()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "metadata");
            var service = CreateDirectoryService();
            var identity = Identity();
            await service.WriteTransactionMetadataAsync(path, identity, CancellationToken.None);
            (await service.GetTransactionMetadataAsync<StorageTransactionIdentity>(path, CancellationToken.None)).Should().Be(identity);
            Directory.GetFiles(directory).Should().Equal(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task FailedSerializationPreservesExistingPublicationAndCleansItsTemporaryFile()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "metadata");
            File.WriteAllText(path, "original");
            var circular = new List<object>();
            circular.Add(circular);
            Func<Task> write = async () => await CreateDirectoryService().WriteTransactionMetadataAsync(path, circular, CancellationToken.None);
            var failure = await write.Should().ThrowAsync<TransactionDirectoryDependencyException>();
            failure.Which.InnerException.Should().BeOfType<IOException>().Which.InnerException.Should().BeOfType<JsonException>();
            File.ReadAllText(path).Should().Be("original");
            Directory.GetFiles(directory).Should().Equal(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task CancellationPreservesTheTokenAndDoesNotReplaceAnExistingPublication()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "metadata");
            File.WriteAllText(path, "original");
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            Func<Task> write = async () => await CreateDirectoryService().WriteTransactionMetadataAsync(path, Identity(), cancellation.Token);
            var failure = await write.Should().ThrowAsync<TransactionMetadataCancellationException>();
            failure.Which.CancellationToken.Should().Be(cancellation.Token);
            File.ReadAllText(path).Should().Be("original");
            Directory.GetFiles(directory).Should().Equal(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task ReceiptLifecycleResolvesAbsentCommittedReleasedAndInvalidEvidence()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "database");
            var identity = Identity();
            var proposed = new StorageTransactionReceipt
            {
                Identity = identity
            };
            var io = CreateDirectoryService();
            var receipts = new TransactionDirectoryReceiptProcessingService(io);
            (await receipts.ResolveStorageTransactionReceiptAsync(path, proposed, CancellationToken.None)).State.Should().Be(StorageTransactionState.Indeterminate);
            var committed = new StorageTransactionReceipt
            {
                Identity = identity,
                State = StorageTransactionState.Committed
            };
            await receipts.WriteStorageTransactionReceiptAsync(path, committed, CancellationToken.None);
            (await receipts.ResolveStorageTransactionReceiptAsync(path, proposed, CancellationToken.None)).Should().Be(committed);
            receipts.ReleaseStorageTransactionReceipt(path, proposed);
            (await receipts.ResolveStorageTransactionReceiptAsync(path, proposed, CancellationToken.None)).State.Should().Be(StorageTransactionState.Indeterminate);
            var receiptPath = path + $".transaction-{identity.Value:N}.receipt";
            await io.WriteTransactionMetadataAsync(receiptPath, committed with { Identity = Identity() }, CancellationToken.None);
            Func<Task> resolve = async () => await receipts.ResolveStorageTransactionReceiptAsync(path, proposed, CancellationToken.None);
            var failure = await resolve.Should().ThrowAsync<TransactionReceiptValidationException>();
            failure.Which.InnerException.Should().BeOfType<InvalidDataException>();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    private static string NewDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "transaction-metadata-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static StorageTransactionIdentity Identity() => new()
    {
        DatabaseId = new sql_storage_engine.Models.Identifiers.DatabaseId
        {
            Value = Guid.NewGuid()
        },
        Value = Guid.NewGuid()
    };
    private static TransactionDirectoryService CreateDirectoryService() => new TransactionDirectoryService(new TransactionDirectoryBroker());
}