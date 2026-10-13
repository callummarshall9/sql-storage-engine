// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Dependencies.ColumnEncryption;

namespace sql_storage_engine.Brokers.ColumnEncryption;
internal sealed class ColumnEncryptionBroker : IColumnEncryptionBroker
{
    private static readonly ColumnEncryptionRegistryDependency registry = new();
    public void Register(string keyName, Func<byte[], byte, byte[]> encrypt, Func<byte[], byte, byte[]> decrypt) =>
        registry.Register(keyName: keyName, encrypt: encrypt, decrypt: decrypt);

    public void RegisterKey(string keyName, byte[] key) =>
        registry.RegisterKey(keyName: keyName, key: key);

    public bool Unregister(string keyName) =>
        registry.Unregister(keyName: keyName);

    public byte[] Encrypt(string keyName, byte[] payload, byte encryptionType) =>
        registry.Encrypt(keyName: keyName, payload: payload, encryptionType: encryptionType);

    public byte[] Decrypt(string keyName, byte[] payload, byte encryptionType) =>
        registry.Decrypt(keyName: keyName, payload: payload, encryptionType: encryptionType);
}