// ---------------------------------------------------------------
// Copyright (c) Callum Marshall
// ---------------------------------------------------------------
using System;
using sql_storage_engine.Models.Catalog;

namespace sql_storage_engine.Services.Orchestrations.CatalogCodecs;
internal sealed partial class CatalogColumnAdmissionOrchestrationService
{
    private static void ValidateCatalogCodecContext(CatalogCodecContext catalogCodecContext) =>
        ArgumentNullException.ThrowIfNull(argument: catalogCodecContext);

    private static void ValidateCatalogIdentity(CatalogIdentity catalogIdentity)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogIdentity);
        var seed = catalogIdentity.Seed;
        var increment = catalogIdentity.Increment;
        var isNotForReplication = catalogIdentity.IsNotForReplication;

        if (increment.IsZero)
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(increment));
        }
    }

    private static void ValidateCatalogColumnEncryption(CatalogColumnEncryption catalogColumnEncryption)
    {
        ArgumentNullException.ThrowIfNull(argument: catalogColumnEncryption);
        var keyName = catalogColumnEncryption.KeyName;
        var encryptionType = catalogColumnEncryption.EncryptionType;
        var algorithm = catalogColumnEncryption.Algorithm;
        ValidateIdentifier(value: keyName, parameterName: nameof(keyName));

        if (!Enum.IsDefined(value: encryptionType))
        {
            throw new ArgumentOutOfRangeException(paramName: nameof(encryptionType));
        }

        if (!StringComparer.Ordinal.Equals(x: algorithm, y: "AEAD_AES_256_CBC_HMAC_SHA_256"))
        {
            throw new ArgumentException(message: "Unsupported Always Encrypted algorithm.", paramName: nameof(algorithm));
        }
    }

    private static void ValidateIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value: value))
        {
            throw new ArgumentException(message: "SQL identifier cannot be empty.", paramName: parameterName);
        }

        if (value.Length > 128)
        {
            throw new ArgumentOutOfRangeException(paramName: parameterName, message: "SQL identifiers cannot exceed 128 characters.");
        }
    }
}