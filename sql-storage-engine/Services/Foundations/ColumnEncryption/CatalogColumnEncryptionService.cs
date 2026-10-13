// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Brokers.ColumnEncryption;

namespace sql_storage_engine.Services.Foundations.ColumnEncryption;
internal sealed partial class CatalogColumnEncryptionService : ICatalogColumnEncryptionService
{
    private readonly IColumnEncryptionBroker broker = new ColumnEncryptionBroker();
    public void RegisterCatalogColumnEncryptionProvider(string keyName, bool hasProvider, Func<byte[], byte, byte[]> encrypt, Func<byte[], byte, byte[]> decrypt) =>
        TryCatch(operation: () =>
    {
        ValidateProvider(keyName: keyName, hasProvider: hasProvider, encrypt: encrypt, decrypt: decrypt);
        broker.Register(keyName: keyName, encrypt: encrypt, decrypt: decrypt);
    });

    public void RegisterCatalogColumnEncryptionKey(string keyName, byte[] key) =>
        TryCatch(operation: () =>
    {
        ValidateKey(keyName: keyName, key: key);
        broker.RegisterKey(keyName: keyName, key: key);
    });

    public bool RemoveCatalogColumnEncryptionProvider(string keyName) =>
        TryCatch(operation: () =>
    {
        ValidateKeyName(keyName: keyName);
        return broker.Unregister(keyName: keyName);
    });

    public byte[] EncryptCatalogColumnEncryptionPayload(CatalogColumnEncryption catalogColumnEncryption, byte[] payload) =>
        TryCatch(operation: () =>
    {
        ValidatePayload(catalogColumnEncryption: catalogColumnEncryption, payload: payload);
        return broker.Encrypt(keyName: catalogColumnEncryption.KeyName, payload: payload, encryptionType: (byte)catalogColumnEncryption.EncryptionType);
    });

    public byte[] DecryptCatalogColumnEncryptionPayload(CatalogColumnEncryption catalogColumnEncryption, byte[] payload) =>
        TryCatch(operation: () =>
    {
        ValidatePayload(catalogColumnEncryption: catalogColumnEncryption, payload: payload);
        return broker.Decrypt(keyName: catalogColumnEncryption.KeyName, payload: payload, encryptionType: (byte)catalogColumnEncryption.EncryptionType);
    });
}