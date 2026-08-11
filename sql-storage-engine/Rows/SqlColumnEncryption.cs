using System.Collections.Concurrent;
using System.Security.Cryptography;
using sql_storage_engine.Catalog;
using sql_storage_engine.Storage;

namespace sql_storage_engine.Rows;

public interface ISqlColumnEncryptionProvider
{
    byte[] Encrypt(ReadOnlySpan<byte> plaintext, CatalogEncryptionType encryptionType);
    byte[] Decrypt(ReadOnlySpan<byte> ciphertext, CatalogEncryptionType encryptionType);
}

/// <summary>Process-local key bindings for transparent encrypted-column row payloads.</summary>
public static class SqlColumnEncryption
{
    private static readonly ConcurrentDictionary<string, ISqlColumnEncryptionProvider> Providers = new(StringComparer.Ordinal);
    public static void Register(string keyName, ISqlColumnEncryptionProvider provider)
    { ArgumentException.ThrowIfNullOrWhiteSpace(keyName); ArgumentNullException.ThrowIfNull(provider); Providers[keyName] = provider; }
    public static void RegisterKey(string keyName, ReadOnlySpan<byte> key)
    { if (key.Length != 32) throw new ArgumentException("Column encryption keys must contain 256 bits.", nameof(key)); Register(keyName, new AesColumnEncryptionProvider(key)); }
    public static bool Unregister(string keyName) => Providers.TryRemove(keyName, out _);
    internal static byte[] Encrypt(CatalogColumnEncryption metadata, ReadOnlySpan<byte> plaintext) => Require(metadata).Encrypt(plaintext, metadata.EncryptionType);
    internal static byte[] Decrypt(CatalogColumnEncryption metadata, ReadOnlySpan<byte> ciphertext) => Require(metadata).Decrypt(ciphertext, metadata.EncryptionType);
    private static ISqlColumnEncryptionProvider Require(CatalogColumnEncryption metadata) =>
        Providers.TryGetValue(metadata.KeyName, out var provider) ? provider :
            throw new InvalidOperationException($"No column encryption provider is registered for key '{metadata.KeyName}'.");

    private sealed class AesColumnEncryptionProvider : ISqlColumnEncryptionProvider
    {
        private readonly byte[] _encryptionKey; private readonly byte[] _macKey; private readonly byte[] _ivKey;
        public AesColumnEncryptionProvider(ReadOnlySpan<byte> key)
        {
            _encryptionKey = Derive(key, "SQLSTORE-AE-ENC"u8); _macKey = Derive(key, "SQLSTORE-AE-MAC"u8);
            _ivKey = Derive(key, "SQLSTORE-AE-IV"u8);
        }
        public byte[] Encrypt(ReadOnlySpan<byte> plaintext, CatalogEncryptionType encryptionType)
        {
            Span<byte> iv = stackalloc byte[16];
            if (encryptionType == CatalogEncryptionType.Deterministic)
            { using var ivMac = new HMACSHA256(_ivKey); ivMac.ComputeHash(plaintext.ToArray()).AsSpan(0, 16).CopyTo(iv); }
            else RandomNumberGenerator.Fill(iv);
            using var aes = Aes.Create(); aes.Key = _encryptionKey; aes.IV = iv.ToArray(); aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            using var transform = aes.CreateEncryptor(); var encrypted = transform.TransformFinalBlock(plaintext.ToArray(), 0, plaintext.Length);
            var output = new byte[1 + iv.Length + encrypted.Length + 32]; output[0] = 1; iv.CopyTo(output.AsSpan(1)); encrypted.CopyTo(output, 17);
            using var mac = new HMACSHA256(_macKey); mac.ComputeHash(output, 0, output.Length - 32).CopyTo(output, output.Length - 32); return output;
        }
        public byte[] Decrypt(ReadOnlySpan<byte> ciphertext, CatalogEncryptionType encryptionType)
        {
            if (ciphertext.Length < 65 || ciphertext[0] != 1 || (ciphertext.Length - 49) % 16 != 0)
                throw new StorageFormatException("Encrypted column payload is malformed.");
            using var mac = new HMACSHA256(_macKey); var expected = mac.ComputeHash(ciphertext[..^32].ToArray());
            if (!CryptographicOperations.FixedTimeEquals(expected, ciphertext[^32..]))
                throw new StorageFormatException("Encrypted column authentication failed.");
            using var aes = Aes.Create(); aes.Key = _encryptionKey; aes.IV = ciphertext.Slice(1, 16).ToArray(); aes.Mode = CipherMode.CBC; aes.Padding = PaddingMode.PKCS7;
            try { using var transform = aes.CreateDecryptor(); return transform.TransformFinalBlock(ciphertext.Slice(17, ciphertext.Length - 49).ToArray(), 0, ciphertext.Length - 49); }
            catch (CryptographicException exception) { throw new StorageFormatException("Encrypted column decryption failed.", exception); }
        }
        private static byte[] Derive(ReadOnlySpan<byte> key, ReadOnlySpan<byte> label)
        { using var hmac = new HMACSHA256(key.ToArray()); return hmac.ComputeHash(label.ToArray()); }
    }
}
