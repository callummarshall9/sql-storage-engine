// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Catalog;
using sql_storage_engine.Models.Catalog;
using sql_storage_engine.Rows;
using sql_storage_engine.Services.Foundations.ColumnEncryption;

namespace sql_storage_engine.Exposures.Rows;
public static class SqlColumnEncryptionExposure
{
    private static readonly ICatalogColumnEncryptionService service = new CatalogColumnEncryptionService();
    public static void Register(string keyName, ISqlColumnEncryptionProvider provider) =>
        service.RegisterCatalogColumnEncryptionProvider(keyName: keyName, hasProvider: provider != null, encrypt: (payload, kind) => provider!.Encrypt(plaintext: payload, encryptionType: (CatalogEncryptionType)kind), decrypt: (payload, kind) => provider!.Decrypt(ciphertext: payload, encryptionType: (CatalogEncryptionType)kind));

    public static void RegisterKey(string keyName, ReadOnlySpan<byte> key) =>
        service.RegisterCatalogColumnEncryptionKey(keyName: keyName, key: key.ToArray());

    public static bool Unregister(string keyName) =>
        service.RemoveCatalogColumnEncryptionProvider(keyName: keyName);

    internal static byte[] Encrypt(CatalogColumnEncryption metadata, ReadOnlySpan<byte> plaintext) =>
        service.EncryptCatalogColumnEncryptionPayload(catalogColumnEncryption: metadata, payload: plaintext.ToArray());

    internal static byte[] Decrypt(CatalogColumnEncryption metadata, ReadOnlySpan<byte> ciphertext) =>
        service.DecryptCatalogColumnEncryptionPayload(catalogColumnEncryption: metadata, payload: ciphertext.ToArray());
}