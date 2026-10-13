// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Catalog;

namespace sql_storage_engine.Rows;
public interface ISqlColumnEncryptionProvider
{
    byte[] Encrypt(ReadOnlySpan<byte> plaintext, CatalogEncryptionType encryptionType);

    byte[] Decrypt(ReadOnlySpan<byte> ciphertext, CatalogEncryptionType encryptionType);
}