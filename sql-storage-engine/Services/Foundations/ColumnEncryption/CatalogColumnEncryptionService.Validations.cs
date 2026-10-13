// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Foundations.ColumnEncryption;
internal sealed partial class CatalogColumnEncryptionService
{
    private static void ValidateKeyName(string keyName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(argument: keyName);
    }

    private static void ValidateProvider(string keyName, bool hasProvider, Func<byte[], byte, byte[]> encrypt, Func<byte[], byte, byte[]> decrypt)
    {
        ValidateKeyName(keyName: keyName);

        if (!hasProvider)
        {
            throw new ArgumentNullException(paramName: "provider");
        }

        ArgumentNullException.ThrowIfNull(argument: encrypt);
        ArgumentNullException.ThrowIfNull(argument: decrypt);
    }

    private static void ValidateKey(string keyName, byte[] key)
    {
        ValidateKeyName(keyName: keyName);
        ArgumentNullException.ThrowIfNull(argument: key);

        if (key.Length != 32)
        {
            throw new ArgumentException(message: "Column encryption keys must contain 256 bits.", paramName: nameof(key));
        }
    }

    private static void ValidatePayload(CatalogColumnEncryption catalogColumnEncryption, byte[] payload)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogColumnEncryption);
        ValidateKeyName(keyName: catalogColumnEncryption.KeyName);
        ArgumentNullException.ThrowIfNull(argument: payload);

        if (!Enum.IsDefined(value: catalogColumnEncryption.EncryptionType))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(catalogColumnEncryption));
        }
    }
}