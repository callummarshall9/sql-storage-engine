// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;

namespace sql_storage_engine.Brokers.ColumnEncryption;
internal interface IColumnEncryptionBroker
{
    void Register(string keyName, Func<byte[], byte, byte[]> encrypt, Func<byte[], byte, byte[]> decrypt);

    void RegisterKey(string keyName, byte[] key);

    bool Unregister(string keyName);

    byte[] Encrypt(string keyName, byte[] payload, byte encryptionType);

    byte[] Decrypt(string keyName, byte[] payload, byte encryptionType);
}