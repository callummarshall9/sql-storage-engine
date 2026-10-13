// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.ColumnEncryption;
internal interface ICatalogColumnEncryptionService
{
    void RegisterCatalogColumnEncryptionProvider(string keyName, bool hasProvider, Func<byte[], byte, byte[]> encrypt, Func<byte[], byte, byte[]> decrypt);

    void RegisterCatalogColumnEncryptionKey(string keyName, byte[] key);

    bool RemoveCatalogColumnEncryptionProvider(string keyName);

    byte[] EncryptCatalogColumnEncryptionPayload(CatalogColumnEncryption catalogColumnEncryption, byte[] payload);

    byte[] DecryptCatalogColumnEncryptionPayload(CatalogColumnEncryption catalogColumnEncryption, byte[] payload);
}