// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using System.Collections.Concurrent;
using System.Security.Cryptography;

namespace sql_storage_engine.Dependencies.ColumnEncryption;
internal sealed class ColumnEncryptionRegistryDependency : HMACSHA256
{
    private readonly ConcurrentDictionary<string, (Func<byte[], byte, byte[]> Encrypt, Func<byte[], byte, byte[]> Decrypt)> providers = new(StringComparer.Ordinal);
    private readonly object derivationLock = new();
    public void Register(string keyName, Func<byte[], byte, byte[]> encrypt, Func<byte[], byte, byte[]> decrypt)
    {
        providers[keyName] = (encrypt, decrypt);
    }

    public bool Unregister(string keyName) =>
        providers.TryRemove(key: keyName, value: out _);

    public byte[] Encrypt(string keyName, byte[] payload, byte encryptionType) =>
        Require(keyName: keyName)
            .Encrypt(payload, encryptionType);

    public byte[] Decrypt(string keyName, byte[] payload, byte encryptionType) =>
        Require(keyName: keyName)
            .Decrypt(payload, encryptionType);

    private (Func<byte[], byte, byte[]> Encrypt, Func<byte[], byte, byte[]> Decrypt) Require(string keyName) =>
        providers.TryGetValue(key: keyName, value: out var provider) ? provider : throw new InvalidOperationException($"No column encryption provider is registered for key '{keyName}'.");

    public void RegisterKey(string keyName, byte[] key)
    {
        byte[] encryptionKey;
        byte[] macKey;
        byte[] ivKey;

        lock (derivationLock)
        {
            Key = key;
            encryptionKey = ComputeHash(buffer: "SQLSTORE-AE-ENC"u8.ToArray());
            macKey = ComputeHash(buffer: "SQLSTORE-AE-MAC"u8.ToArray());
            ivKey = ComputeHash(buffer: "SQLSTORE-AE-IV"u8.ToArray());
            Key = new byte[32];
        }

        Register(keyName: keyName, encrypt: (payload, kind) => EncryptPayload(plaintext: payload, encryptionType: kind, encryptionKey: encryptionKey, macKey: macKey, ivKey: ivKey), decrypt: (payload, kind) => DecryptPayload(ciphertext: payload, encryptionType: kind, encryptionKey: encryptionKey, macKey: macKey));
    }

    protected override void Dispose(bool disposing)
    {
        lock (derivationLock)
        {
            providers.Clear();
            base.Dispose(disposing: disposing);
        }
    }

    private static byte[] EncryptPayload(ReadOnlySpan<byte> plaintext, byte encryptionType, byte[] encryptionKey, byte[] macKey, byte[] ivKey)
    {
        Span<byte> iv = stackalloc byte[16];

        if (encryptionType == 1)
        {
            using var ivMac = new HMACSHA256(ivKey);

            ivMac.ComputeHash(buffer: plaintext.ToArray())
            .AsSpan(start: 0, length: 16)
            .CopyTo(destination: iv);
        }
        else
        {
            RandomNumberGenerator.Fill(data: iv);
        }

        using var aes = Aes.Create();
        aes.Key = encryptionKey;
        aes.IV = iv.ToArray();
        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;
        using var transform = aes.CreateEncryptor();
        var encrypted = transform.TransformFinalBlock(inputBuffer: plaintext.ToArray(), inputOffset: 0, inputCount: plaintext.Length);
        var output = new byte[1 + iv.Length + encrypted.Length + 32];
        output[0] = 1;
        iv.CopyTo(destination: output.AsSpan(start: 1));
        encrypted.CopyTo(array: output, index: 17);
        using var mac = new HMACSHA256(macKey);

        mac.ComputeHash(buffer: output, offset: 0, count: output.Length - 32)
            .CopyTo(array: output, index: output.Length - 32);

        return output;
    }

    private static byte[] DecryptPayload(ReadOnlySpan<byte> ciphertext, byte encryptionType, byte[] encryptionKey, byte[] macKey)
    {
        if (ciphertext.Length < 65 || ciphertext[0] != 1 || (ciphertext.Length - 49) % 16 != 0)
        {
            throw new System.IO.InvalidDataException(message: "Encrypted column payload is malformed.");
        }

        using var mac = new HMACSHA256(macKey);
        var expected = mac.ComputeHash(buffer: ciphertext[..^32].ToArray());

        if (!CryptographicOperations.FixedTimeEquals(left: expected, right: ciphertext[^32..]))
        {
            throw new System.IO.InvalidDataException("Encrypted column authentication failed.");
        }

        using var aes = Aes.Create();
        aes.Key = encryptionKey;

        aes.IV = ciphertext.Slice(start: 1, length: 16)
            .ToArray();

        aes.Mode = CipherMode.CBC;
        aes.Padding = PaddingMode.PKCS7;

        try
        {
            using var transform = aes.CreateDecryptor();

            return transform.TransformFinalBlock(inputBuffer: ciphertext.Slice(start: 17, length: ciphertext.Length - 49)
            .ToArray(), inputOffset: 0, inputCount: ciphertext.Length - 49);
        }
        catch (CryptographicException exception)
        {
            throw new System.IO.InvalidDataException("Encrypted column decryption failed.", exception);
        }
    }
}