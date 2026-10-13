using System.Buffers.Binary;
using System.Security.Cryptography;
using AwesomeAssertions;
using sql_storage_engine.Exposures.Transactions;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class StatementJournalArchitectureTests
{
    [Test]
    public async Task DurableSnapshotHeaderAndLifecycleRemainStable()
    {
        var path = Path.Combine(Path.GetTempPath(), $"journal-boundary-{Guid.NewGuid():N}");
        var original = Enumerable.Range(0, 37).Select(i => (byte)i).ToArray();
        await File.WriteAllBytesAsync(path, original);
        var journalPath = StatementJournalExposure.GetPath(path);
        try
        {
            var journal = await StatementJournalExposure.CreateStatementJournalExposureAsync(path, CancellationToken.None);
            await journal.DisposeAsync();
            File.Exists(journalPath).Should().BeTrue();
            var durable = await File.ReadAllBytesAsync(journalPath);
            durable.AsSpan(0, 8).ToArray().Should().Equal("SQLSTMT1"u8.ToArray());
            durable[8].Should().Be(1);
            BinaryPrimitives.ReadInt64LittleEndian(durable.AsSpan(16, 8)).Should().Be(original.Length);
            durable.AsSpan(24, 32).ToArray().Should().Equal(SHA256.HashData(original));
            durable.AsSpan(64).ToArray().Should().Equal(original);
            await File.WriteAllBytesAsync(path, [99]);
            await StatementJournalExposure.RecoverIfNeededAsync(path, CancellationToken.None, retain: true);
            (await File.ReadAllBytesAsync(path)).Should().Equal(original);
            await journal.MarkCommittedAsync(CancellationToken.None, retain: true);
            await File.WriteAllBytesAsync(path, [77]);
            await StatementJournalExposure.RecoverIfNeededAsync(path, CancellationToken.None);
            (await File.ReadAllBytesAsync(path)).Should().Equal(77);
            File.Exists(journalPath).Should().BeFalse();
        }
        finally { File.Delete(path); File.Delete(journalPath); }
    }

    [Test]
    public async Task CorruptSnapshotsCannotReplaceDatabaseBytes()
    {
        var path = Path.Combine(Path.GetTempPath(), $"journal-corruption-{Guid.NewGuid():N}");
        var journalPath = StatementJournalExposure.GetPath(path);
        await File.WriteAllBytesAsync(path, [1, 2, 3]);
        try
        {
            await using var journal = await StatementJournalExposure.CreateStatementJournalExposureAsync(path, CancellationToken.None);
            var snapshot = await File.ReadAllBytesAsync(journalPath);
            snapshot[64] ^= 1;
            await File.WriteAllBytesAsync(journalPath, snapshot);
            await File.WriteAllBytesAsync(path, [9, 8]);
            await ((Func<Task>)(async () => await StatementJournalExposure.RecoverIfNeededAsync(path, CancellationToken.None))).Should().ThrowAsync<StorageCorruptionException>();
            (await File.ReadAllBytesAsync(path)).Should().Equal(9, 8);
            File.Exists(journalPath).Should().BeTrue();
            snapshot[0] = 0;
            await File.WriteAllBytesAsync(journalPath, snapshot);
            await ((Func<Task>)(async () => await StatementJournalExposure.RecoverIfNeededAsync(path, CancellationToken.None))).Should().ThrowAsync<StorageFormatException>();
        }
        finally { File.Delete(path); File.Delete(journalPath); }
    }

    [Test]
    public async Task RestoreValidatesAlignmentBeforeWritingAndPreservesCancellation()
    {
        var path = Path.Combine(Path.GetTempPath(), $"journal-restore-{Guid.NewGuid():N}");
        var journalPath = StatementJournalExposure.GetPath(path);
        await File.WriteAllBytesAsync(path, new byte[4097]);
        var writes = 0;
        try
        {
            await using var journal = await StatementJournalExposure.CreateStatementJournalExposureAsync(path, CancellationToken.None);
            await ((Func<Task>)(async () => await journal.RestoreAsync(4096, (_, _, _) => { writes++; return ValueTask.CompletedTask; }, _ => { }, _ => ValueTask.CompletedTask, _ => ValueTask.CompletedTask, CancellationToken.None))).Should().ThrowAsync<StorageFormatException>();
            writes.Should().Be(0);
            File.Exists(journalPath).Should().BeTrue();
            using var cancellation = new CancellationTokenSource();
            cancellation.Cancel();
            var failure = await ((Func<Task>)(async () => await StatementJournalExposure.RecoverIfNeededAsync(path, cancellation.Token))).Should().ThrowAsync<OperationCanceledException>();
            failure.Which.CancellationToken.Should().Be(cancellation.Token);
            journal.Delete();
            await journal.DisposeAsync();
            File.Exists(journalPath).Should().BeFalse();
        }
        finally { File.Delete(path); File.Delete(journalPath); }
    }
}