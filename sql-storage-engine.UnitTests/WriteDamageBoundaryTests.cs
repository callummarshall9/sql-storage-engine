using System.Buffers.Binary;
using AwesomeAssertions;
using sql_storage_engine.Diagnostics;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Pages;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;
public sealed class WriteDamageBoundaryTests
{
    [TestCase(int.MinValue)]
    [TestCase(-1)]
    [TestCase(0)]
    [TestCase(1729)]
    [TestCase(int.MaxValue)]
    public void RandomTearsPreserveTheSeededFrameworkSequenceAndSourceBuffers(int seed)
    {
        var original = Enumerable.Repeat((byte)0x11, 1024).ToArray();
        var replacement = Enumerable.Range(0, 1024).Select(index => (byte)index).ToArray();
        var expected = original.ToArray();
        var random = new Random(seed);
        for (var index = 0; index < replacement.Length / 4; index++)
        {
            var offset = random.Next(replacement.Length);
            expected[offset] = replacement[offset];
        }

        PartialWriteExposure.Tear(original, replacement, PartialWritePattern.Random, seed).Should().Equal(expected);
        original.Should().OnlyContain(value => value == 0x11);
        replacement.Should().Equal(Enumerable.Range(0, 1024).Select(index => (byte)index));
    }

    [Test]
    public void AValidPrimaryPageNeedsNoRecoveryImageAndIsReturnedAsAnIndependentCopy()
    {
        var page = Page(1);
        var result = WriteDamageExposure.RecoverPage(page,[]);
        result.Should().Equal(page);
        result[0] ^= 1;
        PageChecksumExposure.ValidateChecksum(page,page.Length);
    }

    [Test]
    public void MissingRecoveryImagePreservesThePrimaryChecksumFailure()
    {
        var page = Page(1);
        page[^1] ^= 1;
        ((Func<byte[]>)(() => WriteDamageExposure.RecoverPage(page,[]))).Should().Throw<UnrecoverablePageCorruptionException>().Which.GetBaseException().Should().BeOfType<StorageCorruptionException>();
    }

    [Test]
    public void TwoFailedImagesPreserveBothChecksumCauses()
    {
        var page = Page(1);
        var image = Page(2);
        page[^1] ^= 1;
        image[^2] ^= 1;
        var failure = ((Func<byte[]>)(() => WriteDamageExposure.RecoverPage(page,image))).Should().Throw<UnrecoverablePageCorruptionException>().Which;
        Exception? current = failure;
        while (current is not null && current is not AggregateException)
            current = current.InnerException;
        var causes = current.Should().BeOfType<AggregateException>().Which.InnerExceptions;
        causes.Should().HaveCount(2).And.AllBeOfType<StorageCorruptionException>();
        causes[0].Should().NotBeSameAs(causes[1]);
    }

    [Test]
    public void InvalidImageShapesAreRejectedBeforeRecoveryOrMutation()
    {
        var page = Page(1);
        var original = page.ToArray();
        ((Func<byte[]>)(() => WriteDamageExposure.RecoverPage(page,new byte[4096]))).Should().Throw<StorageModelValidationException>();
        ((Func<byte[]>)(() => PartialWriteExposure.Tear(page,new byte[4096],PartialWritePattern.Prefix))).Should().Throw<StorageModelValidationException>();
        ((Func<byte[]>)(() => PartialWriteExposure.Tear(page,page,PartialWritePattern.Sector,sectorSize: 0))).Should().Throw<StorageModelValidationException>();
        page.Should().Equal(original);
    }

    [Test]
    public void WalAssessmentHandlesEmptyPartialAndUnsignedCorruptInput()
    {
        WriteDamageExposure.ClassifyWal([]).Should().Be(WalDamagePolicy.Complete);
        WriteDamageExposure.ClassifyWal(new byte[] { 1, 2, 3 }).Should().Be(WalDamagePolicy.TruncateIncompleteTail);
        var prefix = new byte[sizeof(uint)];
        BinaryPrimitives.WriteUInt32LittleEndian(prefix,uint.MaxValue);
        WriteDamageExposure.ClassifyWal(prefix).Should().Be(WalDamagePolicy.StopForCorruption);
    }

    private static byte[] Page(byte fill)
    {
        var page = new byte[sql_storage_engine.Models.Pages.PageConstants.DefaultSize];
        page.AsSpan().Fill(fill);
        PageChecksumExposure.WriteChecksum(page,page.Length);
        return page;
    }
}