using AwesomeAssertions;
using System.ComponentModel;
using sql_storage_engine.Brokers.Directories;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Services.Foundations.TransactionDirectories;

namespace sql_storage_engine.UnitTests;
public sealed class TransactionDirectoryPipelineTests
{
    [Test]
    public void DeletePublishesTheRemovalAndFlushCanBeRepeated()
    {
        var directory = Path.Combine(Path.GetTempPath(), "transaction-directory-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "journal");
            File.WriteAllText(path, "pending");
            var service = CreateService();
            service.DeleteTransactionFile(path);
            File.Exists(path).Should().BeFalse();
            service.FlushTransactionDirectory(path);
            service.DeleteTransactionFile(path);
            File.Exists(path).Should().BeFalse();
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Test]
    public void MissingDirectoryPreservesTheNativeFailureCause()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("Native directory durability is a Linux contract.");
        var path = Path.Combine(Path.GetTempPath(), "missing-transaction-directory-" + Guid.NewGuid().ToString("N"), "journal");
        Action flush = () => CreateService().FlushTransactionDirectory(path);
        var failure = flush.Should().Throw<TransactionDirectoryDependencyException>();
        var io = failure.Which.InnerException.Should().BeOfType<IOException>().Subject;
        io.InnerException.Should().BeOfType<Win32Exception>().Which.NativeErrorCode.Should().Be(2);
    }

    [TestCase(null)]
    [TestCase(" ")]
    public void InvalidPathsFailBeforeDeletionAndPreserveTheParameterName(string? path)
    {
        Action delete = () => CreateService().DeleteTransactionFile(path!);
        var failure = delete.Should().Throw<StorageModelValidationException>();
        failure.Which.ParamName.Should().Be("path");
        failure.Which.InnerException.Should().BeAssignableTo<ArgumentException>();
    }

    [Test]
    public void FailedNativeSyncClosesItsDirectoryHandle()
    {
        if (!OperatingSystem.IsLinux())
            Assert.Ignore("Native directory durability is a Linux contract.");
        var directory = $"/proc/{Environment.ProcessId}";
        var before = CountHandles(directory);
        for (var attempt = 0; attempt < 16; attempt++)
        {
            Action flush = () => CreateService().FlushTransactionDirectory(Path.Combine(directory, "journal"));
            flush.Should().Throw<TransactionDirectoryDependencyException>();
        }

        CountHandles(directory).Should().Be(before);
    }

    private static int CountHandles(string directory) => Directory.GetFiles("/proc/self/fd").Count(path => new FileInfo(path).LinkTarget == directory);
    private static ITransactionDirectoryService CreateService() => new TransactionDirectoryService(new TransactionDirectoryBroker());
}