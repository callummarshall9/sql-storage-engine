using AwesomeAssertions;
using System.Buffers.Binary;
using System.Security.Cryptography;
using sql_storage_engine.Models.Transactions;
using sql_storage_engine.Storage;
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
            await service.WriteTransactionMetadataAsync(path, new TransactionMetadata { Identity = identity }, CancellationToken.None);
            (await service.ReadTransactionMetadataAsync(path, false, CancellationToken.None)).Identity.Should().Be(identity);
            var bytes = await File.ReadAllBytesAsync(path);
            bytes.Length.Should().Be(80);
            bytes.AsSpan(0, 8).ToArray().Should().Equal("SQLTXMD1"u8.ToArray());
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(8, 2)).Should().Be(1);
            BinaryPrimitives.ReadUInt16LittleEndian(bytes.AsSpan(10, 2)).Should().Be(1);
            BinaryPrimitives.ReadUInt32LittleEndian(bytes.AsSpan(12, 4)).Should().Be(32);
            Convert.ToHexString(bytes.AsSpan(16, 16)).Should().Be(identity.DatabaseId.Value.ToString("N").ToUpperInvariant());
            Convert.ToHexString(bytes.AsSpan(32, 16)).Should().Be(identity.Value.ToString("N").ToUpperInvariant());
            bytes.AsSpan(48, 32).ToArray().Should().Equal(SHA256.HashData(bytes.AsSpan(0, 48)));
            Directory.GetFiles(directory).Should().Equal(path);
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public async Task InvalidMetadataPreservesExistingPublicationWithoutCreatingTemporaryFiles()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "metadata");
            File.WriteAllText(path, "original");
            Func<Task> write = async () => await CreateDirectoryService().WriteTransactionMetadataAsync(path, new TransactionMetadata { Identity = Identity(), State = StorageTransactionState.Indeterminate }, CancellationToken.None);
            await write.Should().ThrowAsync<StorageModelValidationException>();
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
            Func<Task> write = async () => await CreateDirectoryService().WriteTransactionMetadataAsync(path, new TransactionMetadata { Identity = Identity() }, cancellation.Token);
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
            await io.WriteTransactionMetadataAsync(receiptPath, new TransactionMetadata { Identity = Identity(), State = StorageTransactionState.Committed }, CancellationToken.None);
            Func<Task> resolve = async () => await receipts.ResolveStorageTransactionReceiptAsync(path, proposed, CancellationToken.None);
            var failure = await resolve.Should().ThrowAsync<TransactionReceiptValidationException>();
            failure.Which.InnerException.Should().BeOfType<InvalidDataException>();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestCase(StorageTransactionState.Committed, (ushort)2)]
    [TestCase(StorageTransactionState.Aborted, (ushort)3)]
    public async Task ReceiptDecisionsHaveStableExplicitKinds(StorageTransactionState state, ushort kind)
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "receipt");
            var expected = new TransactionMetadata { Identity = Identity(), State = state };
            var service = CreateDirectoryService();
            await service.WriteTransactionMetadataAsync(path, expected, CancellationToken.None);
            (await service.ReadTransactionMetadataAsync(path, true, CancellationToken.None)).Should().Be(expected);
            BinaryPrimitives.ReadUInt16LittleEndian((await File.ReadAllBytesAsync(path)).AsSpan(10, 2)).Should().Be(kind);
            await ((Func<Task>)(async () => await service.ReadTransactionMetadataAsync(path, false, CancellationToken.None))).Should().ThrowAsync<StorageFormatException>();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestCase("magic")]
    [TestCase("version")]
    [TestCase("kind")]
    [TestCase("length")]
    [TestCase("checksum")]
    [TestCase("database")]
    [TestCase("transaction")]
    public async Task CorruptAndRehashedInvalidEvidenceIsRejected(string corruption)
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "metadata");
            var service = CreateDirectoryService();
            await service.WriteTransactionMetadataAsync(path, new TransactionMetadata { Identity = Identity() }, CancellationToken.None);
            var bytes = await File.ReadAllBytesAsync(path);
            switch (corruption)
            {
                case "magic": bytes[0] ^= 1; break;
                case "version": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(8, 2), 2); break;
                case "kind": BinaryPrimitives.WriteUInt16LittleEndian(bytes.AsSpan(10, 2), 4); break;
                case "length": BinaryPrimitives.WriteUInt32LittleEndian(bytes.AsSpan(12, 4), 31); break;
                case "checksum": bytes[48] ^= 1; break;
                case "database": bytes.AsSpan(16, 16).Clear(); break;
                case "transaction": bytes.AsSpan(32, 16).Clear(); break;
            }
            if (corruption != "checksum") SHA256.HashData(bytes.AsSpan(0, 48)).CopyTo(bytes, 48);
            await File.WriteAllBytesAsync(path, bytes);
            await ((Func<Task>)(async () => await service.ReadTransactionMetadataAsync(path, false, CancellationToken.None))).Should().ThrowAsync<StorageFormatException>();
            (await File.ReadAllBytesAsync(path)).Should().Equal(bytes);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [TestCase(0)]
    [TestCase(79)]
    [TestCase(81)]
    [TestCase(1048576)]
    public async Task BoundedReadsRejectTruncationAndTrailingData(int length)
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "metadata");
            await File.WriteAllBytesAsync(path, new byte[length]);
            await ((Func<Task>)(async () => await CreateDirectoryService().ReadTransactionMetadataAsync(path, false, CancellationToken.None))).Should().ThrowAsync<TransactionDirectoryDependencyException>();
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

    [Test]
    public async Task LegacyTextAndWrongMetadataTypesAreRejected()
    {
        var directory = NewDirectory();
        try
        {
            var path = Path.Combine(directory, "metadata");
            var service = CreateDirectoryService();
            await File.WriteAllTextAsync(path, "{}");
            await ((Func<Task>)(async () => await service.ReadTransactionMetadataAsync(path, false, CancellationToken.None))).Should().ThrowAsync<TransactionDirectoryDependencyException>();
            await service.WriteTransactionMetadataAsync(path, new TransactionMetadata { Identity = Identity() }, CancellationToken.None);
            await ((Func<Task>)(async () => await service.ReadTransactionMetadataAsync(path, true, CancellationToken.None))).Should().ThrowAsync<StorageFormatException>();
        }
        finally { Directory.Delete(directory, recursive: true); }
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