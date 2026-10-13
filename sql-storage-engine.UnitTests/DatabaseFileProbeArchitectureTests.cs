using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Brokers.Directories;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Services.Foundations.TransactionDirectories;
using sql_storage_engine.Services.Processings.DatabaseFileProbes;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class DatabaseFileProbeArchitectureTests
{
    [Test]
    public async Task PrefixReadsAreBoundedAndRecognizeTruncationMagicAndPageSize()
    {
        var path = Path.Combine(Path.GetTempPath(), $"probe-{Guid.NewGuid():N}");
        var directory = new TransactionDirectoryService(new TransactionDirectoryBroker());
        var probe = new TransactionDirectoryDatabaseFileProcessingService(directory);
        try
        {
            var bytes = new byte[4096];
            "SQLSTORE"u8.CopyTo(bytes.AsSpan(32));
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(60), 8192);
            await File.WriteAllBytesAsync(path, bytes);
            (await directory.ReadTransactionFilePrefixAsync(path, 64, default)).Should().HaveCount(64);
            (await probe.ReadTransactionDirectoryDatabasePageSizeAsync(path, default)).Should().Be(8192);
            await File.WriteAllBytesAsync(path, bytes[..63]);
            await ((Func<Task>)(async () => await probe.ReadTransactionDirectoryDatabasePageSizeAsync(path, default))).Should().ThrowAsync<StorageFormatException>();
            bytes[32] = 0;
            await File.WriteAllBytesAsync(path, bytes);
            await ((Func<Task>)(async () => await probe.ReadTransactionDirectoryDatabasePageSizeAsync(path, default))).Should().ThrowAsync<InvalidDatabaseMagicException>();
            bytes[32] = (byte)'S';
            BinaryPrimitives.WriteInt32LittleEndian(bytes.AsSpan(60), 3);
            await File.WriteAllBytesAsync(path, bytes);
            await ((Func<Task>)(async () => await probe.ReadTransactionDirectoryDatabasePageSizeAsync(path, default))).Should().ThrowAsync<InvalidPageSizeException>();
            File.Delete(path);
            var missing = await ((Func<Task>)(async () => await probe.ReadTransactionDirectoryDatabasePageSizeAsync(path, default))).Should().ThrowAsync<DatabaseProbeDependencyException>();
            missing.Which.Message.Should().Contain(path);
            missing.Which.InnerException.Should().BeOfType<TransactionDirectoryDependencyException>();
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Test]
    public async Task PublicationPreservesAnExistingDestinationAndCancellationToken()
    {
        var root = Path.Combine(Path.GetTempPath(), $"publication-{Guid.NewGuid():N}");
        var directory = new TransactionDirectoryService(new TransactionDirectoryBroker());
        var publication = directory.PrepareTransactionDatabasePublication(Path.Combine(root, "db"));
        try
        {
            await File.WriteAllBytesAsync(publication.FullPath, [1]);
            await File.WriteAllBytesAsync(publication.TemporaryPath, [2]);
            ((Action)(() => directory.PublishTransactionDatabaseFile(publication.TemporaryPath,publication.FullPath))).Should().Throw<IOException>();
            (await File.ReadAllBytesAsync(publication.FullPath)).Should().Equal(1);
            (await File.ReadAllBytesAsync(publication.TemporaryPath)).Should().Equal(2);
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var canceled = await ((Func<Task>)(async () => await directory.ReadTransactionFilePrefixAsync(publication.FullPath, 64, cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();
            canceled.Which.CancellationToken.Should().Be(cancellation.Token);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}