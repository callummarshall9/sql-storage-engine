using System.Text;
using AwesomeAssertions;
using NUnit.Framework;
using sql_storage_engine.Exposures.Diagnostics;
using sql_storage_engine.Exposures.Pages;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Models.Exceptions;
using sql_storage_engine.Models.Identifiers;
using sql_storage_engine.Models.Pages;
using sql_storage_engine.Models.Rows;
using sql_storage_engine.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class PageAndCollationArchitectureTests
{
    [Test]
    public void Utf8EncodingUsesStrictFrameworkContractForArrayAndSpanCalls()
    {
        var collation = SqlCollationExposure.ParseSqlCollation("Latin1_General_100_CI_AS_SC_UTF8");
        var encoding = SqlCollationExposure.ReadSqlCollationEncoding(collation);
        const string value = "Résumé 🐘";
        var expected = new UTF8Encoding(false, true).GetBytes(value);
        encoding.CodePage.Should().Be(65001);
        encoding.GetBytes(value).Should().Equal(expected);
        var destination = new byte[encoding.GetMaxByteCount(value.Length)];
        var written = encoding.GetBytes(value.AsSpan(), destination.AsSpan());
        destination.AsSpan(0, written).ToArray().Should().Equal(expected);
        encoding.GetString(destination.AsSpan(0, written)).Should().Be(value);
        ((Action)(() => encoding.GetBytes("\uD800"))).Should().Throw<EncoderFallbackException>();
        ((Action)(() => encoding.GetString(new byte[] { 0xFF }))).Should().Throw<DecoderFallbackException>();
    }

    [Test]
    public void Utf8DestinationIsValidatedBeforeAnyMutation()
    {
        var destination = Enumerable.Repeat((byte)0xCC, 3).ToArray();
        ((Action)(() => SqlCollationExposure.WriteSqlCollationUtf8(destination, "🐘")))
            .Should().Throw<StorageModelValidationException>();
        destination.Should().OnlyContain(value => value == 0xCC);
    }

    [Test]
    public void RegisteredMetadataAndHostWeightsRemainAvailableThroughTheNewBoundary()
    {
        var name = "ArchitectureCustom_" + Guid.NewGuid().ToString("N");
        var collation = SqlCollationExposure.CreateSqlCollation(name, 0x409, 2,
            SqlCollationComparisonFlags.IgnoreCase, 0, 1252, "en-US");
        SqlCollationExposure.RegisterSqlCollation(collation);
        SqlCollationExposure.ParseSqlCollation(name.ToUpperInvariant()).Should().Be(collation);
        string? suppliedValue = null;
        bool? suppliedUnicode = null;
        SqlCollationExposure.RegisterSqlCollationRuntime(name, (value, unicode) =>
        {
            suppliedValue = value;
            suppliedUnicode = unicode;
            return [0xAB, 0xCD];
        });
        SqlCollationExposure.CalculateSqlCollationSortKey(collation,"Example   ",true)
            .Should().Equal(0xAB, 0xCD);
        suppliedValue.Should().Be("Example");
        suppliedUnicode.Should().BeTrue();
    }

    [Test]
    public void DirectDataInitializationCannotBypassMetadataValidation()
    {
        var invalid = new SqlCollation
        {
            Name = "Invalid_Architecture_Collation",
            Lcid = 0x409,
            CodePage = 0,
            CultureName = "en-US"
        };
        ((Action)(() => SqlCollationExposure.RegisterSqlCollation(invalid)))
            .Should().Throw<StorageModelValidationException>();
        ((Action)(() => SqlCollationExposure.EncodeSqlCollation(invalid,"text")))
            .Should().Throw<StorageModelValidationException>();
    }

    [Test]
    public void VersionTokensPreserveThePersistedRegexBoundaryRules()
    {
        SqlCollationExposure.ParseSqlCollation("100_Latin1_General_CI_AS").Version.Should().Be(0);
        SqlCollationExposure.ParseSqlCollation("Latin1_General_100_CI_AS").Version.Should().Be(2);
        SqlCollationExposure.ParseSqlCollation("Latin1_General_1000_CI_AS").Version.Should().Be(0);
    }

    [Test]
    public void HeaderIdentityValidationRunsBeforeDestinationMutation()
    {
        var destination = Enumerable.Repeat((byte)0xCC, 32).ToArray();
        var invalid = new PageHeader
        {
            PageId = new PageId { Value = 1 },
            PageType = Pages.PageType.Heap,
            FormatVersion = new PageFormatVersion { Value = 2 },
            ChecksumAlgorithm = Pages.PageChecksumAlgorithm.Crc32
        };
        ((Action)(() => PageHeaderExposure.WritePageHeader(destination, invalid)))
            .Should().Throw<StorageFormatException>();
        destination.Should().OnlyContain(value => value == 0xCC);
    }

    [Test]
    public void MetricsRejectInvalidDurationsAndOperationsAfterClosing()
    {
        using var metrics = new StorageMetricsExposure();
        ((Action)(() => metrics.RecordPageFlush(TimeSpan.FromTicks(-1), true)))
            .Should().Throw<StorageModelValidationException>();
        metrics.Dispose();
        metrics.Dispose();
        ((Action)(() => metrics.RecordPageRead(true)))
            .Should().Throw<StorageModelValidationException>();
    }
}