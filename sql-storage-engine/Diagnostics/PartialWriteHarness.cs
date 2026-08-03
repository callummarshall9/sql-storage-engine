using sql_storage_engine.Pages;
using sql_storage_engine.Storage;
using sql_storage_engine.Logging;

namespace sql_storage_engine.Diagnostics;

public enum PartialWritePattern { Prefix = 1, Suffix = 2, Sector = 3, Random = 4 }
public enum WalDamagePolicy { Complete = 1, TruncateIncompleteTail = 2, StopForCorruption = 3 }
public sealed class UnrecoverablePageCorruptionException(string message) : StorageException(message);

/// <summary>Creates deterministic torn-page images without modifying either source buffer.</summary>
public static class PartialWriteHarness
{
    public static byte[] Tear(ReadOnlySpan<byte> original, ReadOnlySpan<byte> replacement,
        PartialWritePattern pattern, int seed = 1729, int sectorSize = 512)
    {
        if (original.Length != replacement.Length || original.IsEmpty) throw new ArgumentException("Write images must have one non-empty length.");
        if (!Enum.IsDefined(pattern)) throw new ArgumentOutOfRangeException(nameof(pattern));
        if (sectorSize <= 0 || sectorSize > original.Length) throw new ArgumentOutOfRangeException(nameof(sectorSize));
        var result = original.ToArray();
        switch (pattern)
        {
            case PartialWritePattern.Prefix: replacement[..(replacement.Length / 2)].CopyTo(result); break;
            case PartialWritePattern.Suffix: replacement[(replacement.Length / 2)..].CopyTo(result.AsSpan(replacement.Length / 2)); break;
            case PartialWritePattern.Sector:
                replacement.Slice((replacement.Length / sectorSize / 2) * sectorSize, sectorSize)
                    .CopyTo(result.AsSpan((replacement.Length / sectorSize / 2) * sectorSize)); break;
            case PartialWritePattern.Random:
                var random = new Random(seed);
                for (var index = 0; index < replacement.Length / 4; index++)
                { var offset = random.Next(replacement.Length); result[offset] = replacement[offset]; }
                break;
        }
        return result;
    }

    public static byte[] RecoverPage(ReadOnlySpan<byte> page, ReadOnlySpan<byte> verifiedFullPageImage)
    {
        try { PageChecksum.ValidateChecksum(page, page.Length); return page.ToArray(); }
        catch (StorageCorruptionException) { }
        try { PageChecksum.ValidateChecksum(verifiedFullPageImage, verifiedFullPageImage.Length); return verifiedFullPageImage.ToArray(); }
        catch (StorageCorruptionException exception)
        { throw new UnrecoverablePageCorruptionException("Torn page has no verified full-page recovery image: " + exception.Message); }
    }

    public static WalDamagePolicy ClassifyWal(ReadOnlySpan<byte> bytes)
    {
        try { return WalFormat.ReadRecords(bytes).HasIncompleteTail ? WalDamagePolicy.TruncateIncompleteTail : WalDamagePolicy.Complete; }
        catch (StorageCorruptionException) { return WalDamagePolicy.StopForCorruption; }
        catch (StorageFormatException) { return WalDamagePolicy.StopForCorruption; }
    }
}
