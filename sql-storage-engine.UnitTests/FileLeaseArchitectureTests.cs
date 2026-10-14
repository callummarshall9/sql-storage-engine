using AwesomeAssertions;
using sql_storage_engine.Exposures.Files;

namespace sql_storage_engine.UnitTests;

public sealed class FileLeaseArchitectureTests
{
    [Test]
    public void FileLeaseKeepsItsPersistentInodeAndReleasesUnderConcurrentDisposal()
    {
        var directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        var path = Path.Combine(directory, "database");
        try
        {
            var lease = StorageFileLeaseExposure.Acquire(path, creating: true);
            File.Exists(path + ".storage-lock").Should().BeTrue();
            ((Action)(() => StorageFileLeaseExposure.Acquire(path, creating: false))).Should().Throw<IOException>().Which.InnerException.Should().BeAssignableTo<IOException>();
            Parallel.For(0, 64, _ => lease.Dispose());
            File.Exists(path + ".storage-lock").Should().BeTrue();
            using var next = StorageFileLeaseExposure.Acquire(path, creating: false);
        }
        finally { Directory.Delete(directory, recursive: true); }
    }

}