using System.Text;
using AwesomeAssertions;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Rows;
using sql_storage_engine.Exposures.Rows;
using sql_storage_engine.Storage;

namespace sql_storage_engine.UnitTests;

public sealed class ColumnEncryptionArchitectureTests
{
    [Test]
    public void DurableCiphertextMatchesIndependentVectorAndRejectsTampering()
    {
        var keyName = Guid.NewGuid().ToString();
        var key = Enumerable.Range(0, 32).Select(i => (byte)i).ToArray();
        var metadata = new CatalogColumnEncryption { KeyName = keyName, EncryptionType = CatalogEncryptionType.Deterministic, Algorithm = "AEAD_AES_256_CBC_HMAC_SHA_256" };
        var plaintext = Encoding.UTF8.GetBytes("durable encrypted column");
        SqlColumnEncryptionExposure.RegisterKey(keyName, key);
        Array.Clear(key);
        try
        {
            var encrypted = SqlColumnEncryptionExposure.Encrypt(metadata,plaintext);
            Convert.ToHexString(encrypted).Should().Be("01D6CAE9FE953200BBB508F322D9007D0D6889C82A45DA36B815F60B8862B450E4F939313AC3D81BFF77E1888D91EFFC082B0870678888F44FFE1DC52AAE76ADB35EED8EA533989B0F0C2B1BFB231B597F");
            SqlColumnEncryptionExposure.Decrypt(metadata,encrypted).Should().Equal(plaintext);
            Parallel.For(0, 64, _ => SqlColumnEncryptionExposure.Encrypt(metadata,plaintext).Should().Equal(encrypted));
            encrypted[17] ^= 1;
            ((Action)(() => SqlColumnEncryptionExposure.Decrypt(metadata,encrypted))).Should().Throw<StorageFormatException>();
            ((Action)(() => SqlColumnEncryptionExposure.Decrypt(metadata,new byte[64]))).Should().Throw<StorageFormatException>();
        }
        finally { SqlColumnEncryptionExposure.Unregister(keyName); }
    }

    [Test]
    public void RandomizedEncryptionAndCustomProvidersRemainUsable()
    {
        var keyName = Guid.NewGuid().ToString();
        var metadata = new CatalogColumnEncryption { KeyName = keyName, EncryptionType = CatalogEncryptionType.Randomized, Algorithm = "AEAD_AES_256_CBC_HMAC_SHA_256" };
        var plaintext = new byte[] { 1, 2, 3, 4 };
        SqlColumnEncryptionExposure.RegisterKey(keyName, new byte[32]);
        try
        {
            var first = SqlColumnEncryptionExposure.Encrypt(metadata,plaintext);
            var second = SqlColumnEncryptionExposure.Encrypt(metadata,plaintext);
            first.Should().NotEqual(second);
            SqlColumnEncryptionExposure.Decrypt(metadata,first).Should().Equal(plaintext);
            SqlColumnEncryptionExposure.Decrypt(metadata,second).Should().Equal(plaintext);
            SqlColumnEncryptionExposure.Register(keyName, new ReverseProvider());
            var custom = SqlColumnEncryptionExposure.Encrypt(metadata,plaintext);
            custom.Should().Equal(4, 3, 2, 1);
            SqlColumnEncryptionExposure.Decrypt(metadata,custom).Should().Equal(plaintext);
            ((Action)(() => SqlColumnEncryptionExposure.Register(keyName, null!))).Should().Throw<ArgumentException>();
            SqlColumnEncryptionExposure.Encrypt(metadata,plaintext).Should().Equal(custom);
        }
        finally { SqlColumnEncryptionExposure.Unregister(keyName); }
        SqlColumnEncryptionExposure.Unregister(keyName).Should().BeFalse();
    }

    private sealed class ReverseProvider : ISqlColumnEncryptionProvider
    {
        public byte[] Encrypt(ReadOnlySpan<byte> plaintext, CatalogEncryptionType encryptionType) => plaintext.ToArray().Reverse().ToArray();
        public byte[] Decrypt(ReadOnlySpan<byte> ciphertext, CatalogEncryptionType encryptionType) => ciphertext.ToArray().Reverse().ToArray();
    }
}